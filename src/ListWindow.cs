using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            ShowPlusMinus = false, ShowRootLines = false, DrawMode = TreeViewDrawMode.OwnerDrawAll,
            AccessibleName = "便笺和分组" };
        private readonly TextBox search = new TextBox { Dock = DockStyle.Top, AccessibleName = "搜索便笺" };
        private readonly Panel searchFrame = new Panel { Dock = DockStyle.Fill,
            Padding = new Padding(9, 6, 9, 0), Margin = new Padding(0, 0, 0, 8) };
        private readonly Label footer = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label heading = new Label { Text = "便笺", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft };
        private readonly Button create = NoteStyle.Button("＋  新建便笺", 124);
        private readonly Button settings = NoteStyle.Button("设置", 62);
        private readonly TableLayoutPanel layout;
        private SettingsView settingsView;
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

            layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1,
                Padding = new Padding(14, 12, 14, 5) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var top = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            heading.Font = new Font(Font.FontFamily, 15, FontStyle.Bold);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, WrapContents = false, Width = 195,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 4, 0, 0) };
            actions.Controls.AddRange(new Control[] { settings, create });
            top.Controls.Add(heading);
            top.Controls.Add(actions);
            create.Click += (sender, args) => NewNote(SelectedGroup);
            settings.Click += (sender, args) => OpenSettings();
            layout.Controls.Add(top, 0, 0);
            search.BorderStyle = BorderStyle.None;
            searchFrame.Controls.Add(search);
            searchFrame.Paint += (sender, args) =>
            {
                args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var fill = new SolidBrush(search.BackColor))
                using (var path = InterfaceStyle.Rounded(searchFrame.ClientRectangle, 8))
                    args.Graphics.FillPath(fill, path);
            };
            layout.Controls.Add(searchFrame, 0, 1);
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
            notes.DrawNode += DrawRow;
            notes.MouseUp += (sender, args) =>
            {
                var node = TargetAt(args.Location);
                if (args.Button == MouseButtons.Left && node != null && node.Parent == null)
                    node.Toggle();
                if (args.Button != MouseButtons.Right) return;
                notes.SelectedNode = node;
                OpenNodeMenu(node, args.Location);
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
                RefreshShortcutLabels();
                using (var graphics = CreateGraphics())
                    notes.ItemHeight = (int)Math.Round(34 * graphics.DpiY / 96f);
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
            BackColor = notes.BackColor = NoteStyle.Background(session.Book.ListPaper,
                session.Book.ListCustomColor, session.Dark);
            ForeColor = notes.ForeColor = NoteStyle.Contrast(BackColor);
            searchFrame.BackColor = BackColor;
            search.BackColor = InterfaceStyle.Blend(BackColor, ForeColor, 0.047f);
            search.ForeColor = NoteStyle.Contrast(search.BackColor);
            create.BackColor = session.Book.ListCustomColor == 0
                ? InterfaceStyle.Blend(BackColor, NoteStyle.GetPaper(session.Book.ListPaper).Accent, 0.58f)
                : InterfaceStyle.Blend(BackColor, ForeColor, 0.22f);
            create.ForeColor = NoteStyle.Contrast(create.BackColor);
            settings.BackColor = InterfaceStyle.Blend(BackColor, ForeColor, 0.07f);
            settings.ForeColor = NoteStyle.Contrast(settings.BackColor);
            footer.ForeColor = InterfaceStyle.Blend(ForeColor, BackColor, 0.30f);
            StyleCaption(InterfaceStyle.Blend(BackColor, ForeColor, 0.027f), ForeColor);
            if (settingsView != null) settingsView.ApplyTheme(BackColor, ForeColor);
            notes.Invalidate();
        }

        private void DrawRow(object sender, DrawTreeNodeEventArgs args)
        {
            var canvas = args.Graphics;
            var row = new Rectangle(0, args.Bounds.Top, notes.ClientSize.Width, args.Bounds.Height);
            bool group = args.Node.Tag is NoteGroup;
            bool selected = args.Node == notes.SelectedNode;
            Color ink = notes.ForeColor;
            Color paper = notes.BackColor;
            Color fill = selected || args.Node == dropGroup ? InterfaceStyle.Blend(paper, InterfaceStyle.Accent(paper), 0.16f)
                : group ? InterfaceStyle.Blend(paper, ink, 0.035f) : paper;
            using (var backdrop = new SolidBrush(paper)) canvas.FillRectangle(backdrop, row);
            using (var brush = new SolidBrush(fill))
            using (var path = InterfaceStyle.Rounded(Rectangle.Inflate(row, -2, -2), 7))
            {
                canvas.SmoothingMode = SmoothingMode.AntiAlias;
                canvas.FillPath(brush, path);
            }
            float scale = canvas.DpiX / 96f;
            float x = (group ? 15 : 34) * scale, y = row.Top + row.Height / 2f;
            if (group)
            {
                using (var pen = new Pen(InterfaceStyle.Blend(ink, fill, 0.25f), 1.5f * scale)
                    { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    float r = 4 * scale;
                    var points = args.Node.IsExpanded
                        ? new[] { new PointF(x - r, y - r / 2), new PointF(x, y + r / 2), new PointF(x + r, y - r / 2) }
                        : new[] { new PointF(x - r / 2, y - r), new PointF(x + r / 2, y), new PointF(x - r / 2, y + r) };
                    canvas.DrawLines(pen, points);
                }
            }
            else
            {
                var note = (Note)args.Node.Tag;
                var color = note.CustomBackground == 0 ? NoteStyle.GetPaper(note.Color).Accent : Color.FromArgb(note.CustomBackground);
                using (var dot = new SolidBrush(InterfaceStyle.Blend(color, ink, 0.22f)))
                    canvas.FillEllipse(dot, x - 3 * scale, y - 3 * scale, 6 * scale, 6 * scale);
            }
            int start = (int)Math.Round((group ? 29 : 48) * scale);
            int countWidth = group ? (int)Math.Round(40 * scale) : 8;
            var text = new Rectangle(start, row.Top, Math.Max(0, row.Width - start - countWidth), row.Height);
            string label = group ? ((NoteGroup)args.Node.Tag).Name : args.Node.Text;
            TextRenderer.DrawText(canvas, label, notes.Font, text, ink,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (group)
            {
                var badge = new Rectangle(row.Right - countWidth + 2, row.Top + row.Height / 5,
                    countWidth - 12, row.Height * 3 / 5);
                using (var shape = InterfaceStyle.Rounded(badge, 8))
                using (var brush = new SolidBrush(InterfaceStyle.Blend(fill, ink, 0.06f)))
                    canvas.FillPath(brush, shape);
                TextRenderer.DrawText(canvas, args.Node.Nodes.Count.ToString(), notes.Font, badge,
                    InterfaceStyle.Blend(ink, fill, 0.24f), TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
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
                foreach (var note in matched[group.Id])
                    signature.Add(note.Id + "|" + note.Color + "|" + note.CustomBackground + "|" + Preview(note));
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
            menu.Items.Add("新建分组…", null, (sender, args) => EditGroup(null));
            if (node == null)
            {
                menu.Items.Add("新建便笺", null, (sender, args) => NewNote(Guid.Empty));
                ShowMenu(menu, notes, point);
                return;
            }
            menu.Items.Add(new ToolStripSeparator());
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
                notes.Invalidate();
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
            dropGroup = null;
            notes.Invalidate();
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

        public void OpenSettings()
        {
            session.ShowList(openNotebook: false);
            if (settingsView == null)
            {
                settingsView = new SettingsView(session, this);
                Controls.Add(settingsView);
                Controls.SetChildIndex(settingsView, Controls.GetChildIndex(layout));
                settingsView.ApplyTheme(BackColor, ForeColor);
            }
            layout.Hide();
            settingsView.Show();
        }

        public void ShowNotebook()
        {
            if (settingsView != null)
            {
                Controls.Remove(settingsView);
                settingsView.Dispose();
                settingsView = null;
            }
            layout.Show();
        }

        public bool Confirm(string message)
        {
            return MessageBox.Show(this, message, Program.Name, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void ShowMenu(ContextMenuStrip menu, Control anchor, Point point)
        {
            PresentMenu(menu, anchor, point);
        }

        public void TryDesktopAction(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                MessageBox.Show(this, "无法更新快捷方式。\n\n" + error.Message, Program.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void SetSaveError(string message) { saveError = message; RefreshNotes(); }
        public void FocusSearch() { ShowNotebook(); search.Focus(); search.SelectAll(); }
        public void RefreshShortcutLabels()
        {
            PinShortcut = session.Shortcuts.Label(ShortcutAction.Pin);
            RefreshPin();
            if (search.IsHandleCreated)
                SendMessage(search.Handle, 0x1501, IntPtr.Zero,
                    "搜索名称和正文（" + session.Shortcuts.Label(ShortcutAction.Search) + "）");
        }
        private void RememberPosition() { NoteStyle.Remember(this, session.Book.ListPosition, session); }
        protected override void PinChanged(bool pinned) { session.Book.ListPinned = pinned; session.Changed(); }
        protected override void OnFormClosing(FormClosingEventArgs args) { session.CloseList(args); base.OnFormClosing(args); }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            var selected = notes.SelectedNode;
            var note = selected == null ? null : selected.Tag as Note;
            switch (session.Shortcuts.Find(key, false))
            {
                case ShortcutAction.New: ShowNotebook(); NewNote(SelectedGroup); return true;
                case ShortcutAction.OpenList: ShowNotebook(); return true;
                case ShortcutAction.Search: FocusSearch(); return true;
                case ShortcutAction.Close: Close(); return true;
                case ShortcutAction.Delete: if (settingsView == null && note != null) session.Delete(note, this); return true;
                case ShortcutAction.Pin: TogglePin(); return true;
                case ShortcutAction.Next: session.Cycle(this, false); return true;
                case ShortcutAction.Previous: session.Cycle(this, true); return true;
            }
            if (key == Keys.Escape && settingsView != null) { ShowNotebook(); return true; }
            if (key == Keys.Escape && search.Focused) { search.Clear(); return true; }
            if (key == Keys.Enter && settingsView == null && note != null && (notes.Focused || search.Focused))
            { session.Open(note); return true; }
            if (key == Keys.Delete && notes.Focused && note != null) { session.Delete(note, this); return true; }
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
