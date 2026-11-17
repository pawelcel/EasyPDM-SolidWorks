// The EasyPDM document panel, a tab of the SOLIDWORKS Task Pane: the active document's item
// (record name, name, revision, status, who holds the lock), buttons for the commands that act
// on it, and for an assembly the tree of its components, each marked whether it is the newest
// file in EasyPDM (Structure). The same panel as in the FreeCAD add-on.
//
// It asks the server only when the active document changes (checked every 1.5 s while the panel
// is visible), on Refresh, and after a command, never otherwise. Documents are read through the
// SOLIDWORKS API on its thread; the server is asked in the background, so a slow server does not
// freeze SOLIDWORKS, and an answer that arrives after the user moved on is dropped.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using static EasyPDM.SolidWorksAddin.Texts;

namespace EasyPDM.SolidWorksAddin
{
    internal sealed class DocumentPanel : UserControl
    {
        private readonly EasyPdmAddin _addin;
        private readonly Label _statusLine = new Label();
        private readonly Dictionary<string, Label> _fields = new Dictionary<string, Label>();
        private readonly Label _message = new Label();
        private readonly Button _refresh, _open, _lock, _upload, _account;
        private readonly Label _summary = new Label();
        private readonly TreeView _tree = new TreeView();
        private readonly ToolTip _tips = new ToolTip();
        private readonly Timer _timer = new Timer { Interval = 1500 };
        private string _shownFor;
        private int _generation;
        private Dictionary<string, object> _item;

        private static readonly Color Warning = Color.FromArgb(211, 84, 0);

        public DocumentPanel(EasyPdmAddin addin)
        {
            _addin = addin;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Window;
            Padding = new Padding(6);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Style(_statusLine);
            _statusLine.Font = new Font(Font, FontStyle.Italic);
            layout.Controls.Add(_statusLine);

            var form = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var key in new[] { "Record", "Name", "Revision", "Status", "Locked by" })
            {
                var name = new Label { Text = T(key) + ":", AutoSize = true, Margin = new Padding(0, 2, 6, 2) };
                var value = new Label { Text = "–", AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
                value.Font = new Font(Font, FontStyle.Bold);
                _fields[key] = value;
                form.Controls.Add(name);
                form.Controls.Add(value);
            }
            layout.Controls.Add(form);

            Style(_message);
            layout.Controls.Add(_message);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
            _refresh = AddButton(buttons, T("Refresh"), (s, e) => Refresh(force: true));
            _open = AddButton(buttons, T("Open in browser"), (s, e) => OpenInBrowser());
            _lock = AddButton(buttons, T("Lock for editing"), (s, e) => { _addin.ToggleLock(); Refresh(force: true); });
            _upload = AddButton(buttons, "Upload", (s, e) => _addin.OnUpload());
            _account = AddButton(buttons, T("Log in"), (s, e) => _addin.OnAccount());
            layout.Controls.Add(buttons);

            Style(_summary);
            layout.Controls.Add(_summary);

            _tree.Dock = DockStyle.Fill;
            _tree.ShowNodeToolTips = true;
            _tree.HideSelection = false;
            _tree.Visible = false;
            layout.Controls.Add(_tree);
            layout.RowStyles.Clear();
            for (var i = 0; i < layout.Controls.Count - 1; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Controls.Add(layout);
            _timer.Tick += (s, e) => FollowActiveDocument();
            _timer.Start();
            Show(null, "");
        }

        private static void Style(Label label)
        {
            label.AutoSize = true;
            label.Dock = DockStyle.Top;
            label.MaximumSize = new Size(10000, 0);
            label.Margin = new Padding(0, 3, 0, 3);
        }

        private Button AddButton(FlowLayoutPanel parent, string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 2, 4, 2) };
            b.Click += (s, e) =>
            {
                try { click(s, e); }
                catch (Exception ex) { _message.Text = ex.Message; }
            };
            parent.Controls.Add(b);
            return b;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Labels wrap at the panel's width.
            var width = Math.Max(50, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            foreach (var label in new[] { _statusLine, _message, _summary }) label.MaximumSize = new Size(width, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Dispose(); _tips.Dispose(); }
            base.Dispose(disposing);
        }

        private void FollowActiveDocument()
        {
            if (!Visible || !IsHandleCreated) return;
            if (EasyPdmAddin.DocKey(_addin.ActiveModel) + "|" + Settings.Token != _shownFor) Refresh(force: true);
        }

        public void Refresh(bool force)
        {
            var model = _addin.ActiveModel;
            _shownFor = EasyPdmAddin.DocKey(model) + "|" + Settings.Token;
            var generation = ++_generation;
            ShowAccount();
            if (model == null)
            {
                Show(null, T("No document open."));
                ShowStructure(null, null);
                return;
            }
            if (!Settings.SignedIn)
            {
                Show(null, T("Not signed in"));
                ShowStructure(null, null);
                return;
            }

            List<Row> rows;
            try
            {
                rows = Structure.Collect(model);
            }
            catch (Exception e)
            {
                Show(null, e.Message);
                return;
            }
            var itemId = rows[0].ItemId;
            var key = EasyPdmAddin.DocKey(model);
            _message.Text = "…";

            Task.Run(() =>
            {
                Dictionary<string, object> item = null;
                string message = "";
                string structureError = null;
                var tooOld = false;
                try
                {
                    if (itemId == null)
                        message = T("This document is not in EasyPDM yet (upload it first).");
                    else
                    {
                        var result = Api.Call("GET", "/items/" + itemId);
                        if (result.Ok) item = Api.Obj(result.Body);
                        else message = Api.Error(result);
                    }
                    if (item != null) Api.MyId();
                }
                catch (Exception e) { message = e.Message; }
                // Also for a document not in EasyPDM yet: its components may well be.
                try { Structure.Check(rows); }
                catch (ServerTooOldException) { tooOld = true; }
                catch (Exception e) { structureError = e.Message; }

                return new Action(() =>
                {
                    if (generation != _generation || IsDisposed) return;
                    EasyPdmAddin.RememberLock(key, item);
                    Show(item, message);
                    if (tooOld) ShowStructure(null, null);
                    else ShowStructure(rows, structureError);
                });
            }).ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion && IsHandleCreated && !IsDisposed)
                    BeginInvoke(t.Result);
            });
        }

        private void ShowAccount()
        {
            if (Settings.SignedIn)
            {
                var name = Settings.DisplayName;
                _statusLine.Text = name.Length > 0 ? T("Signed in as {0} on {1}", name, Settings.ServerUrl) : T("Signed in on {0}", Settings.ServerUrl);
                _account.Text = T("Log out");
            }
            else
            {
                _statusLine.Text = T("Not signed in");
                _account.Text = T("Log in");
            }
        }

        private void Show(Dictionary<string, object> item, string message)
        {
            _item = item;
            if (item != null)
            {
                _fields["Record"].Text = Api.ItemLabel(item);
                _fields["Name"].Text = Api.Str(item, "name") ?? Api.Str(item, "fileName") ?? "";
                _fields["Revision"].Text = Api.Str(item, "revisionLabel") ?? Api.Str(item, "revisionNumber") ?? "";
                _fields["Status"].Text = Status(Api.Str(item, "status"));
                _fields["Locked by"].Text = Api.Bool(item, "ownerLocked") ? Api.Str(item, "ownerDisplayName") ?? "?" : T("nobody");
            }
            else
            {
                foreach (var value in _fields.Values) value.Text = "–";
            }
            _message.Text = message;
            _open.Enabled = item != null;
            var choice = EasyPdmAddin.LockChoice(item);
            _lock.Text = T(choice.Action == "release" || (choice.Action == null && Api.Bool(item, "ownerLocked")) ? "Release lock" : "Lock for editing");
            _lock.Enabled = choice.Action != null;
            _tips.SetToolTip(_lock, choice.Why ?? "");
            _upload.Enabled = _addin.ActiveModel != null;
        }

        private static string StateText(CadState state)
        {
            switch (state)
            {
                case CadState.Current: return T("✓ newest");
                case CadState.Outdated: return T("⚠ newer file in EasyPDM");
                case CadState.Unknown: return T("• changed here, not uploaded");
                case CadState.NoFile: return T("– no file in EasyPDM");
                case CadState.NotLoaded: return T("– not loaded (lightweight)");
                default: return T("– not in EasyPDM yet");
            }
        }

        /// The document and its components with their state; hidden for a document without
        /// components that is up to date, shown with just a line when it is not.
        private void ShowStructure(List<Row> rows, string error)
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            _summary.Text = "";
            _summary.ForeColor = SystemColors.ControlText;
            _tree.Visible = false;
            try
            {
                if (error != null)
                {
                    _summary.Text = T("Could not check the components: {0}", error);
                    return;
                }
                if (rows == null) return;
                var stale = Structure.Outdated(rows);
                if (stale.Count > 0)
                {
                    _summary.Text = T("Older than in EasyPDM: {0}. Download the newest before uploading.", stale.Count);
                    _summary.ForeColor = Warning;
                }
                else if (rows.Count > 1)
                    _summary.Text = T("All components are the newest files in EasyPDM.");
                if (rows.Count < 2) return;

                var parents = new List<TreeNode>();
                foreach (var row in rows)
                {
                    var current = Api.Obj(row.Server, "current");
                    var local = Api.Obj(row.Server, "localVersion");
                    var here = Api.Str(local, "revisionLabel") ?? row.LocalRevision;
                    var there = Api.Str(current, "revisionLabel");
                    var text = row.Quantity == 1 ? row.Label : row.Label + " ×" + row.Quantity;
                    var revisions = new List<string>();
                    if (!string.IsNullOrEmpty(here)) revisions.Add(T("here {0}", here));
                    if (!string.IsNullOrEmpty(there)) revisions.Add(T("EasyPDM {0}", there));
                    if (revisions.Count > 0) text += "  (" + string.Join(", ", revisions) + ")";
                    text += "   " + StateText(row.State);
                    if (row.LockedBy != null) text += "  🔒 " + row.LockedBy;
                    var node = new TreeNode(text);
                    if (row.State == CadState.Outdated) node.ForeColor = Warning;
                    if (current != null)
                        node.ToolTipText = T("In EasyPDM: {0}, uploaded by {1} on {2}",
                            Api.Str(current, "fileName") ?? "", Api.Str(current, "uploadedBy") ?? "?", Structure.When(Api.Str(current, "uploadedAt")));
                    if (parents.Count > row.Depth) parents.RemoveRange(row.Depth, parents.Count - row.Depth);
                    if (parents.Count > 0) parents[parents.Count - 1].Nodes.Add(node);
                    else _tree.Nodes.Add(node);
                    parents.Add(node);
                }
                _tree.ExpandAll();
                _tree.Visible = true;
                if (_tree.Nodes.Count > 0) _tree.Nodes[0].EnsureVisible();
            }
            finally
            {
                _tree.EndUpdate();
            }
        }

        private void OpenInBrowser()
        {
            // /?item=<id> opens the item itself (EasyPDM servers from 0.9).
            var id = Api.Str(_item, "id");
            if (id != null) Process.Start(Settings.ServerUrl + "/?item=" + id);
        }
    }
}
