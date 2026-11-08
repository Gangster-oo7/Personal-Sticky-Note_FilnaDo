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
        private readonly Button create = NoteStyle.Button("＋ 新建", 82);
        private readonly Button settings = NoteStyle.Button("设置", 54);
        private string saveError = "";

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

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5,
                ColumnCount = 1, Padding = new Padding(16, 12, 16, 8) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
            var caption = new Label { Text = "全部便签", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold) };
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
            menu.Items.Add("打开便签", null, (sender, args) => OpenSelected());
            menu.Items.Add("删除便签", null, (sender, args) => DeleteSelected());
            menu.Opening += (sender, args) => args.Cancel = notes.SelectedItem == null;
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
            layout.Controls.Add(heading, 0, 0);
            layout.Controls.Add(search, 0, 1);
            layout.Controls.Add(caption, 0, 2);
            layout.Controls.Add(cards, 0, 3);
            layout.Controls.Add(footer, 0, 4);
            Controls.Add(layout);

            create.Click += (sender, args) => session.NewNote();
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
            ForeColor = notes.ForeColor = search.ForeColor = NoteStyle.Text(session.Dark);
            search.BackColor = NoteStyle.ColorFrom(session.Dark ? 0x333333 : 0xffffff);
            create.BackColor = NoteStyle.ColorFrom(0xf5d76e);
            create.ForeColor = NoteStyle.Text(false);
            settings.BackColor = NoteStyle.ColorFrom(session.Dark ? 0x3b3b3b : 0xeeeae0);
            notes.Invalidate();
        }

        public void RefreshNotes()
        {
            var selected = notes.SelectedItem as NoteRow;
            var query = search.Text.Trim();
            var matches = session.Book.Notes.Where(note => CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                note.PlainText, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth) >= 0)
                .OrderByDescending(note => note.Updated).Select(note => new NoteRow(note)).ToArray();
            bool same = matches.SequenceEqual(notes.Items.Cast<NoteRow>());
            if (!same)
            {
                int first = notes.TopIndex;
                notes.BeginUpdate();
                notes.Items.Clear();
                notes.Items.AddRange(matches);
                if (selected != null)
                    notes.SelectedIndex = Array.FindIndex(matches, row => row.Note.Id == selected.Note.Id);
                if (matches.Length != 0) notes.TopIndex = Math.Min(first, matches.Length - 1);
                notes.EndUpdate();
            }
            empty.Text = query.Length == 0 ? "还没有便签\n点击「新建」，记下一个想法" : "没有匹配的便签";
            empty.Visible = matches.Length == 0;
            notes.Visible = matches.Length != 0;
            footer.Text = saveError.Length != 0 ? saveError : matches.Length + " 条便签";
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
            menu.Items.Add("退出个人便签", null, (sender, args) => session.Quit());
            menu.Closed += (sender, args) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(settings, new Point(0, settings.Height));
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
                case Keys.Control | Keys.N: session.NewNote(); return true;
                case Keys.Control | Keys.F: FocusSearch(); return true;
                case Keys.Control | Keys.W: Close(); return true;
                case Keys.Escape: if (search.Focused) { search.Clear(); return true; } break;
                case Keys.Enter: OpenSelected(); return true;
                case Keys.Delete: if (notes.Focused) { DeleteSelected(); return true; } break;
                case Keys.Control | Keys.Tab: session.Cycle(this, false); return true;
                case Keys.Control | Keys.Shift | Keys.Tab: session.Cycle(this, true); return true;
            }
            return base.ProcessCmdKey(ref message, key);
        }

        private sealed class NoteRow : IEquatable<NoteRow>
        {
            public readonly Note Note;
            public readonly string Preview, Detail, Color;

            public NoteRow(Note note)
            {
                Note = note;
                Color = note.Color;
                Preview = string.Join(" ", note.PlainText.Split((char[])null,
                    StringSplitOptions.RemoveEmptyEntries).Take(50));
                if (Preview.Length == 0) Preview = note.Rtf.Contains(@"\pict") ? "图片便签" : "空白便签";
                Detail = (note.Pinned ? "已置顶   ·   " : "") + note.Updated.ToLocalTime().ToString("MM/dd  HH:mm");
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
