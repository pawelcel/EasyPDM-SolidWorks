// EasyPDM add-in for SOLIDWORKS: an EasyPDM tab in the CommandManager (parts, assemblies and
// drawings), a Tools > EasyPDM menu and a toolbar with Upload, Download, Lock, Log in/out and
// Panel, and the EasyPDM document panel in the Task Pane (DocumentPanel).
//
// Upload, Download, Log in and Log out run the EasyPDM macros themselves -- EasyPDMUpload.swp and
// EasyPDMDownload.swp, placed next to this DLL -- through ISldWorks.RunMacro2, the same way
// Tools > Macro > Run does. None of the upload or download logic is copied here, so the add-in
// and the live-tested macros cannot drift apart. What the macros do not do, the add-in does
// itself against the server with the macros' login (Server.cs): the document panel, locking,
// and refusing an Upload built on components that have a newer file in EasyPDM (Structure.cs),
// as the FreeCAD add-on does.
//
// Lock and Log in/out are one button each, as in FreeCAD: SOLIDWORKS cannot rename a command
// once created, so the button shows its state by being pressed (signed in; locked by you) or
// greyed out (released item, someone else's lock), and the panel's buttons carry the words.
//
// Registration (install.cmd, as administrator): regasm /codebase registers the COM class, and the
// ComRegisterFunction below adds the keys SOLIDWORKS reads its add-in list from.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using static EasyPDM.SolidWorksAddin.Texts;

namespace EasyPDM.SolidWorksAddin
{
    [ComVisible(true)]
    [Guid("91ca2960-0c65-4524-8727-c0fc38207edf")]
    [ProgId("EasyPDM.SolidWorksAddin")]
    public class EasyPdmAddin : ISwAddin
    {
        private const int GroupId = 0x45504D; // "EPM"
        private const string Title = "EasyPDM";

        private ISldWorks _sw;
        private ICommandManager _commands;
        private TaskpaneView _taskpane;
        private DocumentPanel _panel;

        // The commands: name, tooltip, callback and enable methods (called by SOLIDWORKS by name,
        // so these methods must stay public), icon letter and colour.
        internal static readonly (string Text, string Tip, string Callback, string Enable, string Letter, Color Colour)[] Commands =
        {
            ("Upload", "Send the active document to EasyPDM", nameof(OnUpload), nameof(OnDocumentEnable), "U", Color.FromArgb(29, 78, 216)),
            ("Download", "Download an item from EasyPDM with its components", nameof(OnDownload), "", "D", Color.FromArgb(21, 128, 61)),
            ("Lock", "Lock the active document's item for editing, or release your lock", nameof(OnLock), nameof(OnLockEnable), "L", Color.FromArgb(180, 83, 9)),
            ("Log in/out", "Log in to EasyPDM (approved in the browser), or log out", nameof(OnAccount), nameof(OnAccountEnable), "A", Color.FromArgb(71, 85, 105)),
            ("Panel", "Show the EasyPDM document panel", nameof(OnPanel), "", "P", Color.FromArgb(14, 116, 144)),
        };

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            _sw = (ISldWorks)ThisSW;
            _sw.SetAddinCallbackInfo2(0, this, Cookie);
            Texts.Use(Language());
            _commands = _sw.GetCommandManager(Cookie);
            Icons.Paths icons = null;
            try
            {
                icons = Icons.Write();
                CreateCommands(icons);
            }
            catch (Exception e)
            {
                Tell(T("Could not create the EasyPDM menu: {0}", e.Message), swMessageBoxIcon_e.swMbWarning);
            }
            try
            {
                CreatePanel(icons);
            }
            catch (Exception e)
            {
                Tell("EasyPDM: " + T("EasyPDM document panel") + ": " + e.Message, swMessageBoxIcon_e.swMbWarning);
            }
            return true;
        }

        public bool DisconnectFromSW()
        {
            try { _taskpane?.DeleteView(); } catch { /* SOLIDWORKS is closing */ }
            _panel?.Dispose();
            _panel = null;
            if (_taskpane != null) Marshal.ReleaseComObject(_taskpane);
            _taskpane = null;
            try { _commands?.RemoveCommandGroup2(GroupId, true); } catch { /* SOLIDWORKS is closing */ }
            if (_commands != null) Marshal.ReleaseComObject(_commands);
            _commands = null;
            _sw = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }

        /// SOLIDWORKS' language: English when "Use English language menus" is ticked, otherwise
        /// the language of its menus; Windows' only when SOLIDWORKS cannot be asked.
        private string Language()
        {
            try
            {
                if (_sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swUseEnglishLanguage)) return "en";
                var name = (_sw.GetCurrentLanguage() ?? "").ToLowerInvariant();
                if (name == "polish") return "pl";
                if (name == "german") return "de";
                if (name.Length > 0) return "en";
            }
            catch { /* below */ }
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }

        internal ModelDoc2 ActiveModel
        {
            get
            {
                try { return _sw?.ActiveDoc as ModelDoc2; }
                catch { return null; }
            }
        }

        /// Which document a remembered state belongs to: its path, or its title while unsaved.
        internal static string DocKey(ModelDoc2 model)
        {
            if (model == null) return "";
            try
            {
                var path = model.GetPathName();
                return string.IsNullOrEmpty(path) ? "untitled:" + model.GetTitle() : path.ToLowerInvariant();
            }
            catch
            {
                return "";
            }
        }

        private void CreateCommands(Icons.Paths icons)
        {
            int errors = 0;
            // A group left from an earlier version with other commands is rebuilt, not reused.
            var ids = new int[Commands.Length];
            for (var i = 0; i < ids.Length; i++) ids[i] = i + 1;
            object registryIds;
            var changed = _commands.GetGroupDataFromRegistry(GroupId, out registryIds)
                && !IdsMatch(registryIds as int[], ids);

            var group = _commands.CreateCommandGroup2(GroupId, Title, Title, Title, -1, changed, ref errors);
            group.IconList = icons.Strips;
            group.MainIconList = icons.Main;

            var where = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);
            var indices = new int[Commands.Length];
            for (var i = 0; i < Commands.Length; i++)
            {
                var c = Commands[i];
                indices[i] = group.AddCommandItem2(T(c.Text), -1, T(c.Tip), T(c.Text), i, c.Callback, c.Enable, ids[i], where);
            }
            group.HasToolbar = true;
            group.HasMenu = true;
            group.Activate();

            // The command group alone gives the menu and a toolbar that is hidden by default; the
            // CommandManager tab is a separate object, one per document type. SOLIDWORKS keeps a
            // tab between sessions (the user may have rearranged it), so an existing one is kept
            // unless the commands changed.
            var commandIds = new int[indices.Length];
            var textBelow = new int[indices.Length];
            for (var i = 0; i < indices.Length; i++)
            {
                commandIds[i] = group.get_CommandID(indices[i]);
                textBelow[i] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
            }
            foreach (var docType in new[] { swDocumentTypes_e.swDocPART, swDocumentTypes_e.swDocASSEMBLY, swDocumentTypes_e.swDocDRAWING })
            {
                var tab = _commands.GetCommandTab((int)docType, Title);
                if (tab != null && changed)
                {
                    _commands.RemoveCommandTab(tab);
                    tab = null;
                }
                if (tab != null) continue;
                tab = _commands.AddCommandTab((int)docType, Title);
                tab.AddCommandTabBox().AddCommands(commandIds, textBelow);
            }
        }

        private void CreatePanel(Icons.Paths icons)
        {
            var tip = T("EasyPDM document panel");
            try
            {
                _taskpane = (TaskpaneView)_sw.CreateTaskpaneView3(icons?.Main, tip);
            }
            catch (Exception) when (icons != null)
            {
                // SOLIDWORKS before 2017 has only the single-bitmap version.
                _taskpane = (TaskpaneView)_sw.CreateTaskpaneView2(icons.Main[0], tip);
            }
            _panel = new DocumentPanel(this);
            _panel.CreateControl();
            _taskpane.DisplayWindowFromHandlex64(_panel.Handle.ToInt64());
        }

        private static bool IdsMatch(int[] a, int[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private void Tell(string message, swMessageBoxIcon_e icon = swMessageBoxIcon_e.swMbInformation) =>
            _sw?.SendMsgToUser2(message, (int)icon, (int)swMessageBoxBtn_e.swMbOk);

        private void RefreshPanel() => _panel?.Refresh(force: true);

        // --- Commands ------------------------------------------------------------------------

        // Enable methods: 0 greyed out, 1 enabled, 3 enabled and pressed. SOLIDWORKS calls them
        // often while idle, so they read only the registry and what the panel last learnt.
        public int OnDocumentEnable() => ActiveModel != null ? 1 : 0;

        public int OnAccountEnable() => Settings.SignedIn ? 3 : 1;

        public int OnLockEnable()
        {
            var model = ActiveModel;
            if (model == null || !Settings.SignedIn) return 0;
            if (_lockKnown.Key != DocKey(model)) return 1; // not worked out yet: a click asks
            if (_lockKnown.Action == "release") return 3;
            return _lockKnown.Action == "lock" ? 1 : 0;
        }

        public void OnUpload()
        {
            if (EnsureSignedIn() && !RefuseOutdated())
                RunMacro("EasyPDMUpload.swp", "EasyPDMUpload", "main");
            RefreshPanel();
        }

        public void OnDownload()
        {
            if (EnsureSignedIn())
                RunMacro("EasyPDMDownload.swp", "EasyPDMDownload", "main");
            RefreshPanel();
        }

        /// Signed in before Upload or Download starts, through the browser as the Log in button
        /// does. The macros would otherwise ask for a login and password themselves whenever the
        /// stored token is missing or refused; their own login stays only for servers before
        /// 0.9, which have no browser sign-in. False when the person cancelled the sign-in.
        private bool EnsureSignedIn()
        {
            var works = TokenWorks();
            Log.Line("Signed in: token " + (Settings.SignedIn ? "stored" : "none") + ", accepted: " + (works?.ToString() ?? "server not reached"));
            if (works != false) return true;
            var support = BrowserLoginSupported();
            Log.Line("Browser sign-in supported: " + (support?.ToString() ?? "server not reached"));
            if (support == false) return true; // a server before 0.9: the macro asks for a password
            RunMacro("EasyPDMUpload.swp", "EasyPDMUpload", "LoginInBrowser");
            works = TokenWorks();
            Log.Line("After the browser sign-in, accepted: " + (works?.ToString() ?? "server not reached"));
            return works != false;
        }

        /// True when the stored token is accepted, false when there is none or the server refuses
        /// it, null when the server cannot be asked (the macro then reports that itself).
        private static bool? TokenWorks()
        {
            if (!Settings.SignedIn) return false;
            try
            {
                var me = Api.Call("GET", "/auth/me");
                return me.Status == 401 ? false : (bool?)true;
            }
            catch
            {
                return null;
            }
        }

        /// Whether the server signs CAD programs in through the browser (EasyPDM 0.9): its token
        /// endpoint answers an unknown request with 410 Gone, where an older server has no such
        /// route. Null when the server cannot be reached -- then the browser sign-in, which asks
        /// for the server's address first, is the way to set it.
        private static bool? BrowserLoginSupported()
        {
            try
            {
                return Api.Call("POST", "/auth/device/token", new System.Collections.Generic.Dictionary<string, object> { ["deviceCode"] = "" }).Status == 410;
            }
            catch
            {
                return null;
            }
        }

        public void OnAccount()
        {
            if (Settings.SignedIn)
                RunMacro("EasyPDMUpload.swp", "EasyPDMUpload", "Logout");
            else
                RunMacro("EasyPDMUpload.swp", "EasyPDMUpload", "LoginInBrowser");
            RefreshPanel();
        }

        public void OnLock()
        {
            ToggleLock();
            RefreshPanel();
        }

        public void OnPanel()
        {
            try { _taskpane?.ShowView(); } catch { /* no panel: CreatePanel already said why */ }
            RefreshPanel();
        }

        /// Upload refuses work built on files older than those in EasyPDM: the newer ones have
        /// to be downloaded first. A server without the check (before 0.9), or one that cannot
        /// be asked, does not stop the upload -- the macro then reports what is wrong itself.
        private bool RefuseOutdated()
        {
            var model = ActiveModel;
            if (model == null || !Settings.SignedIn) return false;
            System.Collections.Generic.List<Row> rows;
            try
            {
                rows = Structure.Collect(model);
                Structure.Check(rows);
            }
            catch (Exception)
            {
                return false;
            }
            var stale = Structure.Outdated(rows);
            if (stale.Count == 0) return false;
            var lines = stale.Select(row =>
            {
                var current = Api.Obj(row.Server, "current");
                var here = Api.Str(Api.Obj(row.Server, "localVersion"), "revisionLabel") ?? row.LocalRevision ?? "?";
                return "• " + T("{0}: here {1}, in EasyPDM {2} (uploaded by {3} on {4})", row.Label, here,
                    Api.Str(current, "revisionLabel") ?? "?", Api.Str(current, "uploadedBy") ?? "?", Structure.When(Api.Str(current, "uploadedAt")));
            });
            Tell(T("Upload refused: this work is built on files older than those in EasyPDM. Download the newest first (Download), then upload again:\n\n{0}",
                string.Join("\n", lines)), swMessageBoxIcon_e.swMbStop);
            return true;
        }

        // --- One button for the lock ----------------------------------------------------------

        internal struct Choice
        {
            public string Action; // "lock", "release" or null (nothing to do)
            public string Why;    // why not, when Action is null
        }

        /// Release when the signed-in user holds the lock, lock when nobody does; nothing for a
        /// released or cancelled item (it cannot be changed) or while someone else holds it.
        internal static Choice LockChoice(System.Collections.Generic.Dictionary<string, object> item)
        {
            if (item == null) return new Choice();
            var status = Api.Str(item, "status");
            if (status == "wydany" || status == "anulowana")
                return new Choice { Why = T("Released or cancelled items cannot be changed.") };
            if (!Api.Bool(item, "ownerLocked")) return new Choice { Action = "lock" };
            var owner = Api.Str(item, "ownerId");
            if (owner != null && owner == Api.MyId()) return new Choice { Action = "release" };
            return new Choice { Why = T("Locked by {0}", Api.Str(item, "ownerDisplayName") ?? "?") };
        }

        // What the lock button can do for the document it was last worked out for.
        private static (string Key, string Action) _lockKnown = ("", null);

        internal static void RememberLock(string docKey, System.Collections.Generic.Dictionary<string, object> item) =>
            _lockKnown = (docKey, item == null ? null : LockChoice(item).Action);

        internal void ToggleLock()
        {
            var model = ActiveModel;
            if (model == null) { Tell(T("No document open.")); return; }
            if (!Settings.SignedIn) { Tell(T("Not signed in")); return; }
            var itemId = Structure.Property(model, Structure.ItemIdProperty);
            if (itemId == null) { Tell(T("This document is not in EasyPDM yet (upload it first).")); return; }
            try
            {
                var got = Api.Call("GET", "/items/" + itemId);
                if (!got.Ok) { Tell(Api.Error(got), swMessageBoxIcon_e.swMbStop); return; }
                var item = Api.Obj(got.Body);
                var choice = LockChoice(item);
                RememberLock(DocKey(model), item);
                if (choice.Action == null) { Tell(choice.Why ?? "", swMessageBoxIcon_e.swMbWarning); return; }
                var done = Api.Call("POST", "/items/" + itemId + "/" + choice.Action);
                if (!done.Ok) { Tell(Api.Error(done), swMessageBoxIcon_e.swMbStop); return; }
                RememberLock(DocKey(model), Api.Obj(Api.Call("GET", "/items/" + itemId).Body));
                Tell(T(choice.Action == "lock" ? "Locked for you: {0}" : "Lock released: {0}", Api.ItemLabel(item)));
            }
            catch (Exception e)
            {
                Tell(e.Message, swMessageBoxIcon_e.swMbStop);
            }
        }

        private void RunMacro(string file, string module, string procedure)
        {
            var path = Path.Combine(Path.GetDirectoryName(typeof(EasyPdmAddin).Assembly.Location) ?? "", file);
            if (!File.Exists(path))
            {
                Tell("EasyPDM: " + T("{0} is missing next to the add-in ({1}). Put the compiled EasyPDM macros there.", file, path),
                    swMessageBoxIcon_e.swMbStop);
                return;
            }
            // The module is looked up rather than assumed: importing the .bas names it
            // EasyPDMUpload / EasyPDMDownload, but a macro built another way (the code pasted
            // into the module SOLIDWORKS creates, "EasyPDMUpload1") has the same procedures
            // under another module name, and RunMacro2 then fails with error 22 (invalid index).
            var methods = MacroMethods(path);
            var found = FindModule(methods, module, procedure);
            if (found == null && methods.Length > 0)
            {
                Tell("EasyPDM: " + T("The macro {0} has no procedure {1}. It has: {2}", file, procedure, string.Join(", ", methods)),
                    swMessageBoxIcon_e.swMbStop);
                return;
            }
            int error;
            Log.Line("Run " + file + " " + (found ?? module) + "." + procedure);
            var ok = _sw.RunMacro2(path, found ?? module, procedure, (int)swRunMacroOption_e.swRunMacroUnloadAfterRun, out error);
            if (!ok)
                Tell("EasyPDM: " + T("The macro {0} ({1}) did not run, error {2}.", file, (found ?? module) + "." + procedure, error)
                    + (methods.Length > 0 ? "\n" + string.Join(", ", methods) : ""), swMessageBoxIcon_e.swMbStop);
        }

        /// The macro's procedures without arguments, as "Module.Procedure"; empty when SOLIDWORKS
        /// cannot list them.
        private string[] MacroMethods(string path)
        {
            try
            {
                var list = _sw.GetMacroMethods(path, 1 /* swMethodsWithoutArguments */) as object[];
                return list == null ? new string[0] : list.Select(o => Convert.ToString(o)).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        /// The module holding `procedure`: the expected one when it has it, otherwise the first
        /// that does; null when none does.
        internal static string FindModule(string[] methods, string expected, string procedure)
        {
            string first = null;
            foreach (var m in methods)
            {
                var dot = m.LastIndexOf('.');
                if (dot <= 0 || !string.Equals(m.Substring(dot + 1), procedure, StringComparison.OrdinalIgnoreCase)) continue;
                var module = m.Substring(0, dot);
                if (string.Equals(module, expected, StringComparison.OrdinalIgnoreCase)) return module;
                if (first == null) first = module;
            }
            return first;
        }

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\{" + t.GUID + "}"))
            {
                key.SetValue(null, 1);
                key.SetValue("Title", Title);
                key.SetValue("Description", "Upload and download documents to and from an EasyPDM server");
            }
            // Load at start-up for the user who installs it; others tick it in Tools > Add-Ins.
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\SolidWorks\AddInsStartup\{" + t.GUID + "}"))
                key.SetValue(null, 1);
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\SolidWorks\Addins\{" + t.GUID + "}", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\SolidWorks\AddInsStartup\{" + t.GUID + "}", false);
        }
    }

    // Toolbar icons drawn at start-up into %LOCALAPPDATA%\EasyPDM\SolidWorks: one horizontal strip
    // per size SOLIDWORKS asks for (20 to 128 px), a coloured square with a letter per command,
    // and the "E" for the group and the Task Pane tab.
    internal static class Icons
    {
        private static readonly int[] Sizes = { 20, 32, 40, 64, 96, 128 };

        internal sealed class Paths
        {
            public string[] Strips;
            public string[] Main;
        }

        public static Paths Write()
        {
            var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "EasyPDM", "SolidWorks");
            Directory.CreateDirectory(dir);
            var paths = new Paths { Strips = new string[Sizes.Length], Main = new string[Sizes.Length] };
            var letters = EasyPdmAddin.Commands.Select(c => c.Letter).ToArray();
            var colours = EasyPdmAddin.Commands.Select(c => c.Colour).ToArray();
            for (var s = 0; s < Sizes.Length; s++)
            {
                paths.Strips[s] = Draw(Path.Combine(dir, "commands" + letters.Length + "_" + Sizes[s] + ".png"), Sizes[s], letters, colours);
                paths.Main[s] = Draw(Path.Combine(dir, "main_" + Sizes[s] + ".png"), Sizes[s], new[] { "E" }, new[] { Color.FromArgb(29, 78, 216) });
            }
            return paths;
        }

        private static string Draw(string path, int size, string[] letters, Color[] colours)
        {
            using (var bmp = new Bitmap(size * letters.Length, size, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Segoe UI", size * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var white = new SolidBrush(Color.White))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                for (var i = 0; i < letters.Length; i++)
                {
                    var pad = Math.Max(1, size / 10);
                    var rect = new Rectangle(i * size + pad, pad, size - 2 * pad, size - 2 * pad);
                    using (var fill = new SolidBrush(colours[i])) g.FillRectangle(fill, rect);
                    g.DrawString(letters[i], font, white, rect, format);
                }
                bmp.Save(path, ImageFormat.Png);
            }
            return path;
        }
    }
}
