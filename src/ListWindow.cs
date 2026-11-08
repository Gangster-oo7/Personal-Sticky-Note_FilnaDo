using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class ListWindow : CaptionWindow
    {
        private readonly NoteSession session;
        private readonly TreeView notes = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            HideSelection = false, FullRowSelect = true, ShowLines = false, AllowDrop = true,
            AccessibleName = "便笺和分组" };
        private readonly TextBox search = new TextBox { Dock = DockStyle.Top, AccessibleName = "搜索便笺" };
        private readonly Label footer = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Button create = NoteStyle.Button("＋ 新建便笺", 102);
        private readonly Button manage = NoteStyle.Button("新建分组", 80);
        private readonly Button settings = NoteStyle.Button("设置", 48);
        private string saveError = "";
        private string[] rendered = new string[0];
        private bool refreshing;
        private Note dragging;
        private TreeNode dropGroup;
        private int lastScroll;

        public ListWindow(NoteSession session)
        {
            this.session = session;
            Text = Program.Name;
            Icon = NoteStyle.AppIcon;
            Font = new Font("Microsoft YaHei UI", 10);
            Size = new Size(416, 570);
            MinimumSize = new Size(330, 320);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            TopMost = session.Book.ListPinned;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1,
                Padding = new Padding(12, 10, 12, 4) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            actions.Controls.AddRange(new Control[] { create, manage, settings });
            create.Click += (sender, args) => NewNote(SelectedGroup);
            manage.Click += (sender, args) => EditGroup(null);
            settings.Click += (sender, args) => OpenSettings();
            layout.Controls.Add(actions, 0, 0);
            layout.Controls.Add(search, 0, 1);
            layout.Controls.Add(notes, 0, 2);
            layout.Controls.Add(footer, 0, 3);
            Controls.Add(layout);
            InstallCaption();

            search.TextChanged += (sender, args) => RefreshNotes();
            notes.NodeMouseDoubleClick += (sender, args) =>
            {
                var note = args.Node.Tag as Note;
                if (args.Button == MouseButtons.Left && note != null) session.Open(note);
            };
            notes.NodeMouseClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left && args.Node.Parent == null &&
                    args.Clicks == 1 && args.Node.Bounds.Contains(args.Location))
                    args.Node.Toggle();
                if (args.Button != MouseButtons.Right) return;
                notes.SelectedNode = args.Node;
                OpenNodeMenu(args.Node, args.Location);
            };
            notes.AfterExpand += (sender, args) => RememberGroup(args.Node, false);
            notes.AfterCollapse += (sender, args) => RememberGroup(args.Node, true);
            notes.ItemDrag += (sender, args) =>
            {
                var node = (TreeNode)args.Item;
                var note = node.Tag as Note;
                if (args.Button != MouseButtons.Left || note == null) return;
                dragging = note;
                try { notes.DoDragDrop(new DataObject("PersonalStickyNote.Note", note.Id.ToString()), DragDropEffects.Move); }
                finally { dragging = null; ClearDropMark(); }
            };
            notes.DragEnter += DragOverNote;
            notes.DragOver += DragOverNote;
            notes.DragLeave += (sender, args) => ClearDropMark();
            notes.DragDrop += DropNote;
            NoteStyle.Restore(this, session.Book.ListPosition);
            LocationChanged += (sender, args) => RememberPosition();
            SizeChanged += (sender, args) => RememberPosition();
            Shown += (sender, args) =>
            {
                SendMessage(search.Handle, 0x1501, IntPtr.Zero, "搜索名称和正文（Ctrl+F）");
                using (var graphics = CreateGraphics())
                    notes.ItemHeight = (int)Math.Round(30 * graphics.DpiY / 96f);
            };
            ApplyTheme();
            RefreshNotes();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr parameter, string text);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr parameter, IntPtr item);

        private Guid SelectedGroup { get { return GroupId(notes.SelectedNode); } }

        private static Guid GroupId(TreeNode node)
        {
            if (node == null) return Guid.Empty;
            var note = node.Tag as Note;
            if (note != null) return note.GroupId;
            var group = node.Tag as NoteGroup;
            return group == null ? Guid.Empty : group.Id;
        }

        public void ApplyTheme()
        {
            BackColor = notes.BackColor = NoteStyle.Surface(session.Dark);
            ForeColor = notes.ForeColor = search.ForeColor = NoteStyle.Text(session.Dark);
            search.BackColor = NoteStyle.ColorFrom(session.Dark ? 0x333333 : 0xffffff);
            create.BackColor = NoteStyle.ColorFrom(0xf5d76e);
            create.ForeColor = NoteStyle.Text(false);
            settings.BackColor = manage.BackColor = NoteStyle.ColorFrom(session.Dark ? 0x3b3b3b : 0xeeeae0);
            StyleCaption(BackColor, ForeColor);
        }

        public void RefreshNotes()
        {
            var query = search.Text.Trim();
            var matched = session.OrderedNotes().Where(note => Matches(note.Title, query) ||
                Matches(note.PlainText, query)).ToLookup(note => note.GroupId);
            var groups = session.Book.Groups.Concat(new[] { new NoteGroup
            {
                Id = Guid.Empty, Name = "未分组", Collapsed = session.Book.UngroupedCollapsed
            }}).Where(group => query.Length == 0 || matched[group.Id].Any()).ToArray();
            var signature = new List<string> { query };
            foreach (var group in groups)
            {
                signature.Add(group.Id + "|" + group.Name);
                foreach (var note in matched[group.Id]) signature.Add(note.Id + "|" + Preview(note));
            }
            int count = matched.Sum(group => group.Count());
            footer.Text = saveError.Length != 0 ? saveError :
                (count == 0 && query.Length != 0 ? "没有匹配的便笺" : count + " 条便笺");
            if (signature.SequenceEqual(rendered)) return;
            rendered = signature.ToArray();
            string selectedKey = notes.SelectedNode == null ? "" : notes.SelectedNode.Name;
            string topKey = notes.TopNode == null ? "" : notes.TopNode.Name;
            refreshing = true;
            notes.BeginUpdate();
            try
            {
                notes.Nodes.Clear();
                foreach (var group in groups)
                {
                    var members = matched[group.Id].ToArray();
                    var root = new TreeNode(group.Name + "  (" + members.Length + ")")
                        { Name = "g" + group.Id, Tag = group };
                    notes.Nodes.Add(root);
                    foreach (var note in members)
                        root.Nodes.Add(new TreeNode(Preview(note)) { Name = "n" + note.Id, Tag = note });
                    if (query.Length != 0 || !group.Collapsed) root.Expand();
                }
                if (selectedKey.Length != 0)
                    notes.SelectedNode = notes.Nodes.Find(selectedKey, true)
                        .FirstOrDefault(node => node.Parent == null || node.Parent.IsExpanded);
                if (topKey.Length != 0)
                {
                    var top = notes.Nodes.Find(topKey, true)
                        .FirstOrDefault(node => node.Parent == null || node.Parent.IsExpanded);
                    if (top != null) notes.TopNode = top;
                }
            }
            finally { notes.EndUpdate(); refreshing = false; }
        }

        private static bool Matches(string text, string query)
        {
            return query.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(text, query,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth) >= 0;
        }

        private static string Preview(Note note)
        {
            if (!string.IsNullOrWhiteSpace(note.Title)) return note.Title;
            string text = string.Join(" ", note.PlainText.Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries).Take(50));
            if (text.Length > 72)
            {
                var preview = new StringInfo(text);
                if (preview.LengthInTextElements > 64) return preview.SubstringByTextElements(0, 64) + "…";
            }
            if (text.Length != 0) return text;
            return note.Rtf.Contains(@"\pict") ? "图片便笺" : "空白便笺";
        }

        private void RememberGroup(TreeNode node, bool collapsed)
        {
            if (refreshing || search.Text.Trim().Length != 0) return;
            var group = node.Tag as NoteGroup;
            if (group == null) return;
            if (group.Id == Guid.Empty) session.Book.UngroupedCollapsed = collapsed;
            else group.Collapsed = collapsed;
            session.Changed();
        }

        private void OpenNodeMenu(TreeNode node, Point point)
        {
            var menu = new ContextMenuStrip();
            var note = node.Tag as Note;
            if (note != null)
            {
                menu.Items.Add("打开／编辑便笺", null, (sender, args) => session.Open(note));
                var move = new ToolStripMenuItem("移到分组");
                move.DropDownItems.Add("未分组", null, (sender, args) => session.Move(note, Guid.Empty));
                foreach (var group in session.Book.Groups)
                    move.DropDownItems.Add(group.Name, null, (sender, args) => session.Move(note, group.Id));
                menu.Items.Add(move);
                menu.Items.Add("删除便笺", null, (sender, args) => session.Delete(note, this));
            }
            else
            {
                var group = (NoteGroup)node.Tag;
                menu.Items.Add("在此新建便笺", null, (sender, args) => NewNote(group.Id));
                if (group.Id != Guid.Empty)
                {
                    menu.Items.Add("重命名分组", null, (sender, args) => EditGroup(group));
                    menu.Items.Add("删除分组", null, (sender, args) =>
                    {
                        if (Confirm("删除「" + group.Name + "」？组内便笺会移到「未分组」。"))
                            session.RemoveGroup(group);
                    });
                }
            }
            ShowMenu(menu, notes, point);
        }

        private void NewNote(Guid groupId)
        {
            search.Clear();
            session.NewNote(groupId);
            var root = notes.Nodes.Find("g" + groupId, false).FirstOrDefault();
            if (root != null) root.Expand();
        }

        private void EditGroup(NoteGroup group)
        {
            string name = TextPrompt.Ask(this, group == null ? "新建分组" : "重命名分组",
                "分组名称", group == null ? "" : group.Name, 32, value =>
                {
                    if (value.Length == 0) return "请输入分组名称。";
                    if (value == "未分组" || session.Book.Groups.Any(item => item != group &&
                        string.Equals(item.Name, value, StringComparison.OrdinalIgnoreCase)))
                        return "这个分组名称已存在，请换一个。";
                    return null;
                });
            if (name == null) return;
            if (group == null)
            {
                group = new NoteGroup();
                session.Book.Groups.Add(group);
            }
            group.Name = name;
            session.Changed();
            RefreshNotes();
        }

        private TreeNode TargetAt(Point point)
        {
            if (!notes.ClientRectangle.Contains(point)) return null;
            for (var row = notes.TopNode; row != null; row = row.NextVisibleNode)
            {
                var bounds = row.Bounds;
                if (bounds.Top > point.Y || bounds.IsEmpty) return null;
                if (point.Y < bounds.Bottom) return row;
            }
            return null;
        }

        private void DragOverNote(object sender, DragEventArgs args)
        {
            var moving = dragging;
            var point = notes.PointToClient(new Point(args.X, args.Y));
            var target = TargetAt(point);
            ClearDropMark();
            if (moving == null || target == null || moving == target.Tag)
            {
                args.Effect = DragDropEffects.None;
                return;
            }
            args.Effect = DragDropEffects.Move;
            if (target.Tag is Note)
                SendMessage(notes.Handle, 0x111a, point.Y >= target.Bounds.Top + target.Bounds.Height / 2
                    ? new IntPtr(1) : IntPtr.Zero, target.Handle); // TVM_SETINSERTMARK
            else
            {
                dropGroup = target;
                target.BackColor = NoteStyle.ColorFrom(session.Dark ? 0x57482a : 0xffe49c);
            }
            if (Environment.TickCount - lastScroll < 110 || notes.TopNode == null) return;
            lastScroll = Environment.TickCount;
            if (point.Y < notes.ItemHeight && notes.TopNode.PrevVisibleNode != null)
                notes.TopNode = notes.TopNode.PrevVisibleNode;
            else if (point.Y >= notes.ClientSize.Height - notes.ItemHeight && notes.TopNode.NextVisibleNode != null)
                notes.TopNode = notes.TopNode.NextVisibleNode;
        }

        private void ClearDropMark()
        {
            SendMessage(notes.Handle, 0x111a, IntPtr.Zero, IntPtr.Zero);
            if (dropGroup == null) return;
            dropGroup.BackColor = Color.Empty;
            dropGroup = null;
        }

        private void DropNote(object sender, DragEventArgs args)
        {
            ClearDropMark();
            var point = notes.PointToClient(new Point(args.X, args.Y));
            var target = TargetAt(point);
            var moving = dragging;
            if (target == null || moving == null || target.Tag == moving) return;
            Guid group = GroupId(target);
            session.Move(moving, group, target.Tag as Note,
                point.Y >= target.Bounds.Top + target.Bounds.Height / 2);
            var root = notes.Nodes.Find("g" + group, false).FirstOrDefault();
            if (root != null) root.Expand();
            var moved = notes.Nodes.Find("n" + moving.Id, true).FirstOrDefault();
            if (moved != null) notes.SelectedNode = moved;
        }

        private void OpenSettings()
        {
            var menu = new ContextMenuStrip();
            var theme = new ToolStripMenuItem("外观");
            foreach (var pair in new[] { "system|跟随系统", "light|浅色", "dark|深色" })
            {
                string[] parts = pair.Split('|');
                var item = new ToolStripMenuItem(parts[1]) { Checked = session.Book.Theme == parts[0] };
                item.Click += (sender, args) => { session.Book.Theme = parts[0]; session.ApplySettings(); };
                theme.DropDownItems.Add(item);
            }
            menu.Items.Add(theme);
            var deletion = new ToolStripMenuItem("删除前确认") { Checked = session.Book.ConfirmDelete };
            deletion.Click += (sender, args) => { session.Book.ConfirmDelete = !session.Book.ConfirmDelete; session.Changed(); };
            menu.Items.Add(deletion);
            menu.Items.Add(new ToolStripSeparator());
            bool startup = DesktopIntegration.StartsWithWindows;
            var startupItem = new ToolStripMenuItem("登录 Windows 后自动启动") { Checked = startup };
            startupItem.Click += (sender, args) =>
            {
                if (startup || Confirm("允许个人便签在你登录 Windows 后自动启动，并恢复上次打开的便笺吗？"))
                    TryDesktopAction(() => DesktopIntegration.SetStartup(!startup));
            };
            menu.Items.Add(startupItem);
            menu.Items.Add("创建桌面快捷方式", null, (sender, args) => TryDesktopAction(() =>
            {
                DesktopIntegration.CreateDesktopShortcut();
                MessageBox.Show(this, "已创建桌面快捷方式「个人便签」。", Program.Name);
            }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出个人便签", null, (sender, args) => session.Quit());
            ShowMenu(menu, settings, new Point(0, settings.Height));
        }

        private bool Confirm(string message)
        {
            return MessageBox.Show(this, message, Program.Name, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void ShowMenu(ContextMenuStrip menu, Control anchor, Point point)
        {
            menu.Closed += (sender, args) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(anchor, point);
        }

        private void TryDesktopAction(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                MessageBox.Show(this, "无法更新快捷方式。\n\n" + error.Message, Program.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void SetSaveError(string message) { saveError = message; RefreshNotes(); }
        public void FocusSearch() { search.Focus(); search.SelectAll(); }
        private void RememberPosition() { NoteStyle.Remember(this, session.Book.ListPosition, session); }
        protected override void PinChanged(bool pinned) { session.Book.ListPinned = pinned; session.Changed(); }
        protected override void OnFormClosing(FormClosingEventArgs args) { session.CloseList(args); base.OnFormClosing(args); }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            var selected = notes.SelectedNode;
            var note = selected == null ? null : selected.Tag as Note;
            switch (key)
            {
                case Keys.Control | Keys.N: NewNote(SelectedGroup); return true;
                case Keys.Control | Keys.F: FocusSearch(); return true;
                case Keys.Control | Keys.W: Close(); return true;
                case Keys.Control | Keys.Shift | Keys.P: TogglePin(); return true;
                case Keys.Escape: if (search.Focused) { search.Clear(); return true; } break;
                case Keys.Enter: if (note != null) { session.Open(note); return true; } break;
                case Keys.Delete: if (notes.Focused && note != null) { session.Delete(note, this); return true; } break;
                case Keys.Control | Keys.Tab: session.Cycle(this, false); return true;
                case Keys.Control | Keys.Shift | Keys.Tab: session.Cycle(this, true); return true;
            }
            return base.ProcessCmdKey(ref message, key);
        }
    }

    internal static class TextPrompt
    {
        public static string Ask(Form owner, string title, string label, string value, int maxLength,
            Func<string, string> validate)
        {
            using (var dialog = new Form { Text = title, Font = owner.Font, ClientSize = new Size(338, 130),
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
                AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi })
            {
                var caption = new Label { Text = label, AutoSize = true, Location = new Point(14, 14) };
                var input = new TextBox { Text = value, MaxLength = maxLength, Width = 308,
                    Location = new Point(14, 43), AccessibleName = label };
                var ok = new Button { Text = "保存", Location = new Point(161, 86), Size = new Size(76, 30) };
                var cancel = new Button { Text = "取消", Location = new Point(246, 86), Size = new Size(76, 30),
                    DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] { caption, input, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                dialog.ActiveControl = input;
                input.SelectAll();
                string result = null;
                ok.Click += (sender, args) =>
                {
                    var candidate = input.Text.Trim();
                    string error = null;
                    try { System.Xml.XmlConvert.VerifyXmlChars(candidate); }
                    catch (System.Xml.XmlException) { error = "名称含有无法保存的字符。"; }
                    if (error == null && validate != null) error = validate(candidate);
                    if (error != null) { MessageBox.Show(dialog, error, title); return; }
                    result = candidate;
                    dialog.DialogResult = DialogResult.OK;
                };
                return dialog.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }
    }
}
