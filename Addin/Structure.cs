// Are the files an assembly is built from the newest ones in EasyPDM?
//
// While an assembly is being worked on, someone else may upload a newer file of one of its
// components: a new revision, or the same revision overwritten. The document panel shows the
// state of every component (and of the document itself), and Upload refuses to send work built
// on an older file than the one in EasyPDM. The same check as in the FreeCAD add-on.
//
// Which file a local copy is, the server tells (POST /api/items/cad-versions, EasyPDM 0.9):
//   - by the version stamp the Upload macro writes into the custom property
//     EasyPDM_ContentVersion before saving and sending the file. It travels with the file through
//     Download and stays the same when SOLIDWORKS saves the file again unchanged, which it
//     does to components whenever the assembly is saved. A hash alone would call every one of
//     them changed;
//   - for files without a stamp (uploaded by an older macro), by the SHA-256 of the file on disk.
// The revision letter in the file name ("C0012(Bracket).B.SLDPRT") settles what neither can: a
// copy of an older revision is outdated even when the server has never seen its exact bytes.
//
// Collect() reads the documents through the SOLIDWORKS API and must run on its thread; Check()
// only hashes files and asks the server, so it may run in the background.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace EasyPDM.SolidWorksAddin
{
    internal enum CadState { Current, Outdated, Unknown, NotInPdm, NoFile, NotLoaded }

    internal sealed class ServerTooOldException : Exception
    {
    }

    internal sealed class Row
    {
        public string Label;
        public string Path;
        public int Depth;
        public int Quantity = 1;
        public bool Loaded = true;
        public string ItemId;
        public string Stamp;
        public Dictionary<string, object> Server;
        public CadState State = CadState.NotInPdm;

        public string LockedBy => Api.Bool(Server, "ownerLocked") ? Api.Str(Server, "ownerDisplayName") : null;
        public string LocalRevision => Structure.RevisionInName(Path);
    }

    internal static class Structure
    {
        public const string ItemIdProperty = "EasyPDM_ItemId";
        public const string StampProperty = "EasyPDM_ContentVersion";

        private static readonly Regex RevisionSuffix =
            new Regex(@"\.([A-Za-z]{1,3})\.sld(prt|asm)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Property(ModelDoc2 model, string name)
        {
            try
            {
                var manager = model.Extension.CustomPropertyManager[""];
                string value, resolved;
                manager.Get4(name, false, out value, out resolved);
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static string RevisionInName(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var m = RevisionSuffix.Match(System.IO.Path.GetFileName(path));
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
        }

        private static int RevisionNumber(string letters)
        {
            var n = 0;
            foreach (var ch in letters.ToUpperInvariant()) n = n * 26 + (ch - 'A' + 1);
            return n;
        }

        /// The document and every component it is built from, depth-first as in its tree, with
        /// identical sibling components counted once with their quantity.
        public static List<Row> Collect(ModelDoc2 top)
        {
            var rows = new List<Row>();
            var path = top.GetPathName() ?? "";
            rows.Add(FromModel(top, path, 0));
            if (top.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                var children = ((AssemblyDoc)top).GetComponents(true) as object[];
                Walk(children, 1, rows, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path });
            }
            return rows;
        }

        private static void Walk(object[] components, int depth, List<Row> rows, HashSet<string> ancestors)
        {
            if (components == null) return;
            var groups = new List<KeyValuePair<string, List<Component2>>>();
            foreach (var o in components)
            {
                var component = o as Component2;
                if (component == null) continue;
                try
                {
                    // Suppressed components are not part of the assembly; virtual ones live
                    // inside the assembly file and are never items of their own.
                    if (component.IsSuppressed() || component.IsVirtual) continue;
                }
                catch { continue; }
                var path = component.GetPathName() ?? "";
                var group = groups.FirstOrDefault(g => string.Equals(g.Key, path, StringComparison.OrdinalIgnoreCase));
                if (group.Value == null)
                    groups.Add(new KeyValuePair<string, List<Component2>>(path, new List<Component2> { component }));
                else
                    group.Value.Add(component);
            }
            foreach (var group in groups)
            {
                var component = group.Value[0];
                var model = component.GetModelDoc2() as ModelDoc2;
                var row = model != null
                    ? FromModel(model, group.Key, depth)
                    : new Row { Label = Label(group.Key), Path = group.Key, Depth = depth, Loaded = false, State = CadState.NotLoaded };
                row.Quantity = group.Value.Count;
                rows.Add(row);
                if (ancestors.Contains(group.Key)) continue;
                var next = new HashSet<string>(ancestors, StringComparer.OrdinalIgnoreCase) { group.Key };
                Walk(component.GetChildren() as object[], depth + 1, rows, next);
            }
        }

        private static string Label(string path) =>
            string.IsNullOrEmpty(path) ? "?" : System.IO.Path.GetFileNameWithoutExtension(path);

        private static Row FromModel(ModelDoc2 model, string path, int depth) => new Row
        {
            Label = string.IsNullOrEmpty(path) ? model.GetTitle() : Label(path),
            Path = path,
            Depth = depth,
            ItemId = Property(model, ItemIdProperty),
            Stamp = Property(model, StampProperty),
        };

        /// Asks the server about every row with an item and sets its state. Throws
        /// ServerTooOldException on servers before 0.9.
        public static void Check(List<Row> rows)
        {
            var request = new List<object>();
            var asked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (!row.Loaded || row.ItemId == null || !Guid.TryParse(row.ItemId, out _) || !asked.Add(row.ItemId))
                    continue;
                string hash = null;
                if (row.Stamp == null && File.Exists(row.Path))
                {
                    try { hash = FileHash(row.Path); }
                    catch (IOException) { /* open exclusively elsewhere: unknown */ }
                }
                request.Add(new Dictionary<string, object> { ["itemId"] = row.ItemId, ["contentVersion"] = row.Stamp, ["fileHash"] = hash });
            }

            var answers = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            if (request.Count > 0)
            {
                var result = Api.Call("POST", "/items/cad-versions", new Dictionary<string, object> { ["components"] = request });
                if (result.Status == 404 || result.Status == 405) throw new ServerTooOldException();
                if (!result.Ok) throw new InvalidOperationException(Api.Error(result));
                foreach (var answer in result.Body as object[] ?? new object[0])
                {
                    var id = Api.Str(answer, "itemId");
                    if (id != null) answers[id] = Api.Obj(answer);
                }
            }

            foreach (var row in rows)
            {
                if (!row.Loaded) continue;
                if (row.ItemId == null || !answers.TryGetValue(row.ItemId, out var server))
                {
                    row.State = CadState.NotInPdm;
                    continue;
                }
                row.Server = server;
                row.State = Classify(row, server);
            }
        }

        private static CadState Classify(Row row, Dictionary<string, object> server)
        {
            if (!Api.Bool(server, "found")) return CadState.NotInPdm;
            var local = Api.Str(server, "local");
            if (local == "none") return CadState.NoFile;
            if (local == "older") return CadState.Outdated;
            var mine = row.LocalRevision;
            var current = Api.Int(Api.Obj(server, "current"), "revisionNumber");
            if (mine != null && current.HasValue && RevisionNumber(mine) < current.Value) return CadState.Outdated;
            return local == "current" ? CadState.Current : CadState.Unknown;
        }

        /// The rows Upload refuses to send over, each document once.
        public static List<Row> Outdated(IEnumerable<Row> rows)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return rows.Where(r => r.State == CadState.Outdated && seen.Add(r.Path ?? r.Label)).ToList();
        }

        public static string When(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "?";
            return DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
                ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : iso;
        }

        private static string FileHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
