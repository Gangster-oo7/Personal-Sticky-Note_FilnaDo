using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class Paper
    {
        public readonly string Id, Label;
        public readonly Color Light, Dark, Accent;

        public Paper(string id, string label, int light, int dark, int accent)
        {
            Id = id;
            Label = label;
            Light = NoteStyle.ColorFrom(light);
            Dark = NoteStyle.ColorFrom(dark);
            Accent = NoteStyle.ColorFrom(accent);
        }
    }

    internal static class NoteStyle
    {
        public static readonly Icon AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        public static readonly Paper[] Papers =
        {
            new Paper("yellow", "黄色", 0xfff6cc, 0x38301f, 0xf5d76e),
            new Paper("green", "绿色", 0xe4f7da, 0x263725, 0x9cdd85),
            new Paper("pink", "粉色", 0xffe5ef, 0x402930, 0xf4a5c6),
            new Paper("purple", "紫色", 0xf1e7ff, 0x342942, 0xcbb2f1),
            new Paper("blue", "蓝色", 0xe3f4ff, 0x233543, 0x9ad1f6),
            new Paper("gray", "灰色", 0xf0f0f0, 0x343434, 0xc9c9c9),
            new Paper("charcoal", "炭灰", 0xe5e7ea, 0x26272b, 0xaaaeb8)
        };

        public static Color ColorFrom(int rgb)
        {
            return Color.FromArgb(unchecked((int)0xff000000) | rgb);
        }

        public static Paper GetPaper(string id)
        {
            return Papers.FirstOrDefault(p => p.Id == id) ?? Papers[0];
        }

        public static Color Text(bool dark) { return dark ? Color.WhiteSmoke : ColorFrom(0x26231e); }
        public static Color Surface(bool dark) { return ColorFrom(dark ? 0x232323 : 0xfbfaf7); }

        public static Button Button(string text, int width, string description = null)
        {
            var button = new Button
            {
                Text = text, Width = width, Height = 30, FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false, Margin = new Padding(0, 0, 4, 0),
                AccessibleName = description ?? text, Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        public static void Restore(Form form, WindowPosition position)
        {
            if (position.Width <= 0 || position.Height <= 0) return;
            var bounds = new Rectangle(position.X, position.Y, position.Width, position.Height);
            var area = Screen.FromRectangle(bounds).WorkingArea;
            bounds.Width = Math.Min(area.Width, Math.Max(bounds.Width, form.MinimumSize.Width));
            bounds.Height = Math.Min(area.Height, Math.Max(bounds.Height, form.MinimumSize.Height));
            bounds.X = Math.Max(area.Left, Math.Min(bounds.X, area.Right - bounds.Width));
            bounds.Y = Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - bounds.Height));
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = bounds;
        }

        public static void Remember(Form form, WindowPosition position, NoteSession session)
        {
            if (!form.Visible || form.WindowState != FormWindowState.Normal) return;
            position.X = form.Left;
            position.Y = form.Top;
            position.Width = form.Width;
            position.Height = form.Height;
            session.Changed();
        }
    }

    internal sealed class ListWindow : Form
    {
        private readonly NoteSession session;
        private readonly ListBox notes = new ListBox();
        private readonly TextBox search = new TextBox();
        private readonly Label footer = new Label();
        private readonly Label empty = new Label();
        private readonly ComboBox groups = new ComboBox();
        private readonly Button create = NoteStyle.Button("＋ 新建", 82);
        private readonly Button settings = NoteStyle.Button("设置", 54);
        private readonly Button manage = NoteStyle.Button("管理分组", 88);
        private readonly FlowLayoutPanel noteActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false
        };
        private string saveError = "";
        private Guid? SelectedGroup
        {
            get
            {
                var choice = groups.SelectedItem as GroupChoice;
                return choice == null ? null : choice.Id;
            }
        }

        public ListWindow(NoteSession session)
        {
            this.session = session;
            Text = Program.Name;
            Icon = NoteStyle.AppIcon;
            Font = new Font("Microsoft YaHei UI", 10);
            Size = new Size(402, 570);
            MinimumSize = new Size(330, 330);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6,
                ColumnCount = 1, Padding = new Padding(16, 12, 16, 8) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var heading = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var title = new Label { Text = "个人便签", AutoSize = true, Location = new Point(0, 3),
                Font = new Font(Font, FontStyle.Bold) };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 145, WrapContents = false };
            actions.Controls.Add(create);
            actions.Controls.Add(settings);
            heading.Controls.Add(title);
            heading.Controls.Add(actions);

            search.Dock = DockStyle.Top;
            search.AccessibleName = "搜索便签";
            search.TextChanged += (sender, args) => RefreshNotes();
            var filter = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 5) };
            groups.AccessibleName = "按分组筛选便签";
            groups.DropDownStyle = ComboBoxStyle.DropDownList;
            groups.Dock = DockStyle.Fill;
            groups.SelectionChangeCommitted += (sender, args) => RefreshNotes();
            manage.Dock = DockStyle.Right;
            manage.Click += (sender, args) => OpenGroupMenu();
            filter.Controls.Add(groups);
            filter.Controls.Add(manage);
            notes.Dock = DockStyle.Fill;
            notes.Margin = Padding.Empty;
            notes.AccessibleName = "便签列表";
            notes.BorderStyle = BorderStyle.None;
            notes.DrawMode = DrawMode.OwnerDrawFixed;
            notes.IntegralHeight = false;
            notes.ItemHeight = 68;
            notes.DrawItem += DrawNote;
            notes.MouseDoubleClick += (sender, args) =>
            {
                int index = notes.IndexFromPoint(args.Location);
                if (index != ListBox.NoMatches) session.Open(((NoteRow)notes.Items[index]).Note);
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("编辑便签", null, (sender, args) => OpenSelected());
            var moveMenu = new ToolStripMenuItem("移到分组");
            menu.Items.Add(moveMenu);
            menu.Items.Add("删除便签", null, (sender, args) => DeleteSelected());
            menu.Opening += (sender, args) =>
            {
                var row = notes.SelectedItem as NoteRow;
                args.Cancel = row == null;
                if (row != null) AddMoveItems(moveMenu.DropDownItems, row.Note);
            };
            notes.ContextMenuStrip = menu;
            notes.MouseDown += (sender, args) =>
            {
                if (args.Button == MouseButtons.Right)
                {
                    int index = notes.IndexFromPoint(args.Location);
                    notes.SelectedIndex = index == ListBox.NoMatches ? -1 : index;
                }
            };

            empty.Dock = DockStyle.Fill;
            empty.TextAlign = ContentAlignment.MiddleCenter;
            var cards = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            cards.Controls.Add(notes);
            cards.Controls.Add(empty);
            footer.Dock = DockStyle.Fill;
            footer.TextAlign = ContentAlignment.MiddleLeft;
            var edit = NoteStyle.Button("编辑", 58);
            var move = NoteStyle.Button("移到分组", 86);
            var delete = NoteStyle.Button("删除", 58);
            noteActions.Controls.AddRange(new Control[] { edit, move, delete });
            edit.Click += (sender, args) => OpenSelected();
            delete.Click += (sender, args) => DeleteSelected();
            move.Click += (sender, args) =>
            {
                var row = notes.SelectedItem as NoteRow;
                if (row == null) return;
                var picker = new ContextMenuStrip();
                AddMoveItems(picker.Items, row.Note);
                ShowMenu(picker, move);
            };
            notes.SelectedIndexChanged += (sender, args) => noteActions.Enabled = notes.SelectedItem != null;
            noteActions.Enabled = false;
            layout.Controls.Add(heading, 0, 0);
            layout.Controls.Add(search, 0, 1);
            layout.Controls.Add(filter, 0, 2);
            layout.Controls.Add(cards, 0, 3);
            layout.Controls.Add(noteActions, 0, 4);
            layout.Controls.Add(footer, 0, 5);
            Controls.Add(layout);

            create.Click += (sender, args) => NewNote();
            settings.Click += (sender, args) => OpenSettings();
            NoteStyle.Restore(this, session.Book.ListPosition);
            LocationChanged += (sender, args) => RememberPosition();
            SizeChanged += (sender, args) => RememberPosition();
            Shown += (sender, args) =>
            {
                SendMessage(search.Handle, 0x1501, IntPtr.Zero, "搜索便签（Ctrl+F）");
                notes.ItemHeight = (int)Math.Round(68 * CreateDpiScale());
            };
            ApplyTheme();
            RefreshGroups(null);
        }

        private float CreateDpiScale()
        {
            using (var graphics = CreateGraphics()) return graphics.DpiY / 96f;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr parameter, string text);

        public void ApplyTheme()
        {
            BackColor = notes.BackColor = NoteStyle.Surface(session.Dark);
            ForeColor = notes.ForeColor = search.ForeColor = groups.ForeColor = NoteStyle.Text(session.Dark);
            search.BackColor = groups.BackColor = NoteStyle.ColorFrom(session.Dark ? 0x333333 : 0xffffff);
            create.BackColor = NoteStyle.ColorFrom(0xf5d76e);
            create.ForeColor = NoteStyle.Text(false);
            settings.BackColor = manage.BackColor = NoteStyle.ColorFrom(session.Dark ? 0x3b3b3b : 0xeeeae0);
            foreach (Control button in noteActions.Controls) button.BackColor = settings.BackColor;
            notes.Invalidate();
        }

        public void RefreshNotes()
        {
            var selected = notes.SelectedItem as NoteRow;
            var query = search.Text.Trim();
            var groupId = SelectedGroup;
            var groupNames = session.Book.Groups.ToDictionary(group => group.Id, group => group.Name);
            var matches = session.Book.Notes
                .Where(note => (!groupId.HasValue || note.GroupId == groupId.Value) &&
                    CultureInfo.CurrentCulture.CompareInfo.IndexOf(note.PlainText, query,
                        CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth) >= 0)
                .OrderByDescending(note => note.Updated).Select(note =>
                {
                    string name;
                    return new NoteRow(note, groupNames.TryGetValue(note.GroupId, out name) ? name : "未分组");
                }).ToArray();
            bool same = matches.SequenceEqual(notes.Items.Cast<NoteRow>());
            if (!same)
            {
                int first = notes.TopIndex;
                notes.BeginUpdate();
                notes.Items.Clear();
                notes.Items.AddRange(matches);
                if (selected != null)
                    notes.SelectedIndex = Array.FindIndex(matches, row => row.Note.Id == selected.Note.Id);
                if (matches.Length != 0) notes.TopIndex = Math.Max(0, Math.Min(first, matches.Length - 1));
                notes.EndUpdate();
            }
            empty.Text = query.Length == 0 ? "这里还没有便签\n点击「新建」，记下一个想法" : "没有匹配的便签";
            empty.Visible = matches.Length == 0;
            notes.Visible = matches.Length != 0;
            footer.Text = saveError.Length != 0 ? saveError : matches.Length + " 条便签";
        }

        public void RefreshGroups(Guid? selected)
        {
            groups.BeginUpdate();
            groups.Items.Clear();
            groups.Items.Add(new GroupChoice(null, "全部便签"));
            groups.Items.Add(new GroupChoice(Guid.Empty, "未分组"));
            foreach (var group in session.Book.Groups.OrderBy(group => group.Name))
                groups.Items.Add(new GroupChoice(group.Id, group.Name));
            var index = groups.Items.Cast<GroupChoice>().ToList().FindIndex(choice => choice.Id == selected);
            groups.SelectedIndex = Math.Max(0, index);
            groups.EndUpdate();
            RefreshNotes();
        }

        private void NewNote()
        {
            search.Clear();
            session.NewNote(SelectedGroup.GetValueOrDefault());
        }

        private void AddMoveItems(ToolStripItemCollection items, Note note)
        {
            while (items.Count != 0)
            {
                var previous = items[0];
                items.RemoveAt(0);
                previous.Dispose();
            }
            foreach (var choice in groups.Items.Cast<GroupChoice>().Where(choice => choice.Id.HasValue))
            {
                var item = new ToolStripMenuItem(choice.Name) { Checked = note.GroupId == choice.Id.Value };
                item.Click += (sender, args) => session.Move(note, choice.Id.Value);
                items.Add(item);
            }
        }

        private void OpenGroupMenu()
        {
            var group = session.Book.Groups.FirstOrDefault(item => item.Id == SelectedGroup);
            var menu = new ContextMenuStrip();
            menu.Items.Add("新建分组", null, (sender, args) => EditGroup(null));
            menu.Items.Add("重命名当前分组", null, (sender, args) => EditGroup(group)).Enabled = group != null;
            menu.Items.Add("删除当前分组", null, (sender, args) =>
            {
                if (MessageBox.Show(this, "删除「" + group.Name + "」？里面的便签会移到「未分组」。",
                    Program.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    session.RemoveGroup(group);
            }).Enabled = group != null;
            ShowMenu(menu, manage);
        }

        private void EditGroup(NoteGroup group)
        {
            using (var dialog = new Form { Text = group == null ? "新建分组" : "重命名分组",
                Font = Font, ClientSize = new Size(338, 133), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi,
                BackColor = BackColor, ForeColor = ForeColor })
            {
                var label = new Label { Text = "分组名称（例如：临时、长期提醒）", AutoSize = true,
                    Location = new Point(14, 14) };
                var input = new TextBox { Text = group == null ? "" : group.Name, MaxLength = 32,
                    Width = 308, Location = new Point(14, 43), AccessibleName = "分组名称",
                    BackColor = search.BackColor, ForeColor = search.ForeColor };
                var ok = new Button { Text = "保存", Location = new Point(161, 89), Size = new Size(76, 30) };
                var cancel = new Button { Text = "取消", Location = new Point(246, 89), Size = new Size(76, 30),
                    DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] { label, input, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                dialog.ActiveControl = input;
                input.SelectAll();
                string name = "";
                ok.Click += (sender, args) =>
                {
                    name = input.Text.Trim();
                    if (name.Length == 0 || name.Any(ch => char.IsControl(ch) || ch == '\uffff' || ch == '\ufffe'))
                    {
                        MessageBox.Show(dialog, "请输入有效的分组名称。", Program.Name);
                        return;
                    }
                    if (name == "全部便签" || name == "未分组" || session.Book.Groups.Any(item =>
                        item != group && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show(dialog, "这个分组名称已存在，请换一个。", Program.Name);
                        return;
                    }
                    dialog.DialogResult = DialogResult.OK;
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (group == null)
                {
                    group = new NoteGroup();
                    session.Book.Groups.Add(group);
                }
                group.Name = name;
                session.Changed();
                RefreshGroups(group.Id);
            }
        }

        public void SetSaveError(string message)
        {
            saveError = message;
            RefreshNotes();
        }

        private void DrawNote(object sender, DrawItemEventArgs args)
        {
            if (args.Index < 0) return;
            var row = (NoteRow)notes.Items[args.Index];
            var paper = NoteStyle.GetPaper(row.Note.Color);
            var rect = new Rectangle(args.Bounds.X, args.Bounds.Y, args.Bounds.Width, args.Bounds.Height - 7);
            bool selected = (args.State & DrawItemState.Selected) != 0;
            using (var background = new SolidBrush(selected ? paper.Accent : session.Dark ? paper.Dark : paper.Light))
            using (var accent = new SolidBrush(paper.Accent))
            using (var bold = new Font(Font, FontStyle.Bold))
            {
                args.Graphics.FillRectangle(background, rect);
                args.Graphics.FillRectangle(accent, rect.Left, rect.Top, 4, rect.Height);
                var ink = NoteStyle.Text(session.Dark && !selected);
                var text = new Rectangle(rect.Left + 12, rect.Top, rect.Width - 22, rect.Height / 2);
                const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(args.Graphics, row.Preview, bold, text, ink, flags);
                text.Offset(0, text.Height);
                TextRenderer.DrawText(args.Graphics, row.Detail, Font, text, ink, flags);
            }
            if (selected) args.DrawFocusRectangle();
        }

        private void OpenSettings()
        {
            var menu = new ContextMenuStrip();
            var theme = new ToolStripMenuItem("外观");
            foreach (var pair in new[] { "system|跟随系统", "light|浅色", "dark|深色" })
            {
                string[] parts = pair.Split('|');
                var item = new ToolStripMenuItem(parts[1]) { Checked = session.Book.Theme == parts[0] };
                item.Click += (sender, args) =>
                {
                    session.Book.Theme = parts[0];
                    session.ApplySettings();
                };
                theme.DropDownItems.Add(item);
            }
            menu.Items.Add(theme);
            menu.Items.Add(new ToolStripMenuItem("删除前确认") { Checked = session.Book.ConfirmDelete });
            menu.Items[1].Click += (sender, args) =>
            {
                session.Book.ConfirmDelete = !session.Book.ConfirmDelete;
                session.Changed();
            };
            menu.Items.Add(new ToolStripSeparator());
            bool startup = DesktopIntegration.StartsWithWindows;
            var startupItem = new ToolStripMenuItem("登录 Windows 后自动启动") { Checked = startup };
            startupItem.Click += (sender, args) =>
            {
                if (!startup && MessageBox.Show(this,
                    "允许个人便签在你登录 Windows 后自动启动，并恢复上次打开的便签吗？",
                    Program.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
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
            ShowMenu(menu, settings);
        }

        private void ShowMenu(ContextMenuStrip menu, Control anchor)
        {
            menu.Closed += (sender, args) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(anchor, new Point(0, anchor.Height));
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

        private void OpenSelected()
        {
            var row = notes.SelectedItem as NoteRow;
            if (row != null) session.Open(row.Note);
        }

        private void DeleteSelected()
        {
            var row = notes.SelectedItem as NoteRow;
            if (row != null) session.Delete(row.Note, this);
        }

        private void RememberPosition() { NoteStyle.Remember(this, session.Book.ListPosition, session); }
        protected override void OnFormClosing(FormClosingEventArgs args) { session.CloseList(args); base.OnFormClosing(args); }

        public void FocusSearch()
        {
            search.Focus();
            search.SelectAll();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            switch (key)
            {
                case Keys.Control | Keys.N: NewNote(); return true;
                case Keys.Control | Keys.F: FocusSearch(); return true;
                case Keys.Control | Keys.W: Close(); return true;
                case Keys.Escape: if (search.Focused) { search.Clear(); return true; } break;
                case Keys.Enter: if (notes.Focused || search.Focused) { OpenSelected(); return true; } break;
                case Keys.Delete: if (notes.Focused) { DeleteSelected(); return true; } break;
                case Keys.Control | Keys.Tab: session.Cycle(this, false); return true;
                case Keys.Control | Keys.Shift | Keys.Tab: session.Cycle(this, true); return true;
            }
            return base.ProcessCmdKey(ref message, key);
        }

        private sealed class GroupChoice
        {
            public readonly Guid? Id;
            public readonly string Name;
            public GroupChoice(Guid? id, string name) { Id = id; Name = name; }
            public override string ToString() { return Name; }
        }

        private sealed class NoteRow : IEquatable<NoteRow>
        {
            public readonly Note Note;
            public readonly string Preview, Detail, Color;

            public NoteRow(Note note, string group)
            {
                Note = note;
                Color = note.Color;
                Preview = string.Join(" ", note.PlainText.Split((char[])null,
                    StringSplitOptions.RemoveEmptyEntries).Take(50));
                if (Preview.Length == 0) Preview = note.Rtf.Contains(@"\pict") ? "图片便签" : "空白便签";
                Detail = (note.Pinned ? "已置顶  ·  " : "") + group + "  ·  " +
                    note.Updated.ToLocalTime().ToString("MM/dd  HH:mm");
            }

            public bool Equals(NoteRow other)
            {
                return other != null && Note.Id == other.Note.Id && Preview == other.Preview &&
                    Detail == other.Detail && Color == other.Color;
            }

            public override string ToString() { return Preview + " " + Detail; }
        }
    }
}
