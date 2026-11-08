using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class NoteWindow : CaptionWindow
    {
        private readonly NoteSession session;
        private readonly RichEditor editor = new RichEditor();
        private readonly Panel header = new Panel();
        private readonly Panel page = new Panel();
        private readonly FlowLayoutPanel toolbar = new FlowLayoutPanel();
        private readonly Button add = NoteStyle.Button("＋", 34, "新建便笺");
        private readonly Button title = NoteStyle.Button("命名便笺", 108);
        private readonly Button more = NoteStyle.Button("⋯", 35, "便笺选项");
        private readonly Button snap = NoteStyle.Button("磁吸", 59);
        private readonly FlowLayoutPanel appearance = new FlowLayoutPanel();
        private readonly Button font = NoteStyle.Button("字体", 60);
        private readonly Button ink = NoteStyle.Button("文字颜色", 82);
        private readonly Button[] formats;
        private readonly ToolTip tips = new ToolTip();
        private bool contentChanged;
        private bool styling;
        private bool paperOnly;
        private readonly Timer hoverTimer = new Timer { Interval = 90 };
        private ContextMenuStrip openMenu;
        public Note Note { get; private set; }

        public NoteWindow(NoteSession session, Note note)
        {
            this.session = session;
            Note = note;
            Text = Program.Name + "·便笺";
            Icon = NoteStyle.AppIcon;
            Font = new Font("Microsoft YaHei UI", 10);
            Size = new Size(378, 352);
            MinimumSize = new Size(326, 238);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            TopMost = note.Pinned;

            header.Dock = DockStyle.Top;
            header.Height = 40;
            header.Padding = new Padding(6, 5, 6, 4);
            add.Dock = DockStyle.Left;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            more.Dock = DockStyle.Right;
            snap.Dock = DockStyle.Right;
            header.Controls.Add(title);
            header.Controls.Add(add);
            header.Controls.Add(snap);
            header.Controls.Add(more);
            add.Click += (sender, args) => session.NewNote(Note.GroupId);
            title.Click += (sender, args) => Rename();
            more.Click += (sender, args) => OpenMenu();
            snap.Click += (sender, args) => { Note.Snap = !Note.Snap; RefreshSnap(); session.Changed(); };
            tips.SetToolTip(add, "新建便笺（Ctrl+N）");
            tips.SetToolTip(more, "背景颜色、手写、便笺列表和删除");
            RefreshTitle();

            editor.BorderStyle = BorderStyle.None;
            editor.Dock = DockStyle.Fill;
            editor.AccessibleName = "便笺正文";
            editor.ScrollBars = RichTextBoxScrollBars.Vertical;
            editor.DetectUrls = true;
            editor.HideSelection = false;
            editor.Font = new Font(Font.FontFamily, 11);
            if (!string.IsNullOrEmpty(note.Rtf)) editor.Rtf = note.Rtf;
            else editor.Text = note.PlainText;
            editor.LinkClicked += (sender, args) =>
            {
                Uri url;
                if (Uri.TryCreate(args.LinkText, UriKind.Absolute, out url) &&
                    (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
                {
                    try { System.Diagnostics.Process.Start(url.AbsoluteUri); }
                    catch (System.ComponentModel.Win32Exception error) { ShowError("无法打开链接", error); }
                }
            };
            page.Padding = new Padding(13, 12, 8, 5);
            page.Dock = DockStyle.Fill;
            page.Controls.Add(editor);

            toolbar.Dock = DockStyle.Bottom;
            toolbar.Height = 39;
            toolbar.Padding = new Padding(7, 4, 0, 4);
            toolbar.WrapContents = false;
            formats = new[]
            {
                Tool("B", "加粗（Ctrl+B）", () => Format(1)),
                Tool("I", "斜体（Ctrl+I）", () => Format(2)),
                Tool("U", "下划线（Ctrl+U）", () => Format(4)),
                Tool("S", "删除线（Ctrl+T）", () => Format(8)),
                Tool("•", "项目符号（Ctrl+Shift+L）", ToggleBullet)
            };
            formats[0].Font = new Font(Font, FontStyle.Bold);
            formats[1].Font = new Font(Font, FontStyle.Italic);
            formats[2].Font = new Font(Font, FontStyle.Underline);
            formats[3].Font = new Font(Font, FontStyle.Strikeout);
            var image = Tool("图片", "插入图片", PickImage, 53);
            var todo = Tool("☐ 待办", "添加待办（Ctrl+Shift+J）", () => editor.AddTodo(), 65);
            toolbar.Controls.AddRange(formats);
            toolbar.Controls.Add(todo);
            toolbar.Controls.Add(image);
            appearance.Dock = DockStyle.Bottom;
            appearance.Height = 35;
            appearance.Padding = new Padding(7, 0, 0, 3);
            appearance.WrapContents = false;
            appearance.Controls.Add(font);
            appearance.Controls.Add(ink);
            font.Click += (sender, args) => ChooseFont();
            ink.Click += (sender, args) => OpenInkMenu();
            Controls.Add(page);
            Controls.Add(appearance);
            Controls.Add(toolbar);
            Controls.Add(header);
            InstallCaption();

            ApplyTheme();
            editor.ClearUndo();
            editor.TextChanged += (sender, args) => { if (!styling) Edited(); };
            editor.SelectionChanged += (sender, args) => { if (!styling) RefreshTools(); };
            InstallEditorMenu();
            NoteStyle.Restore(this, note.Position);
            hoverTimer.Tick += (sender, args) => RefreshHover();
            Shown += (sender, args) => { editor.Focus(); RefreshTools(); RefreshHover(); hoverTimer.Start(); };
            LocationChanged += (sender, args) => RememberPosition();
            SizeChanged += (sender, args) => { RememberPosition(); if (paperOnly) UpdatePaperRegion(); };
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        private void RefreshHover()
        {
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            var target = WindowFromPoint(Cursor.Position);
            bool hovered = target == Handle || (target != IntPtr.Zero && GetAncestor(target, 2) == Handle);
            SetPaperOnly(!hovered && !InSizeMove && Enabled &&
                (openMenu == null || !openMenu.Visible) && !editor.ContextMenuStrip.Visible &&
                WindowState == FormWindowState.Normal);
        }

        private void SetPaperOnly(bool compact)
        {
            if (paperOnly == compact) return;
            paperOnly = compact;
            SuspendLayout();
            ShowCaption(!compact);
            header.Visible = toolbar.Visible = appearance.Visible = !compact;
            BackColor = compact ? editor.BackColor : header.BackColor;
            Padding = compact ? Padding.Empty : new Padding(5);
            UpdatePaperRegion();
            ResumeLayout();
        }

        private void UpdatePaperRegion()
        {
            if (!paperOnly) { Region = null; return; }
            int w = Width, h = Height;
            using (var graphics = CreateGraphics())
            using (var path = new GraphicsPath())
            {
                float r = Math.Min(Math.Min(w, h) / 2f, 22 * graphics.DpiX / 96f);
                float control = r * 0.82f;
                path.StartFigure();
                path.AddBezier(0, r, 0, r - control, r - control, 0, r, 0);
                path.AddLine(r, 0, w - r, 0);
                path.AddBezier(w - r, 0, w - r + control, 0, w, r - control, w, r);
                path.AddLine(w, r, w, h - r);
                path.AddBezier(w, h - r, w, h - r + control, w - r + control, h, w - r, h);
                path.AddLine(w - r, h, r, h);
                path.AddBezier(r, h, r - control, h, 0, h - r + control, 0, h - r);
                path.CloseFigure();
                Region = new Region(path);
            }
        }

        private void RefreshSnap()
        {
            snap.Text = Note.Snap ? "磁吸 ✓" : "磁吸";
            snap.BackColor = Note.Snap ? NoteStyle.Contrast(header.BackColor) : header.BackColor;
            snap.ForeColor = NoteStyle.Contrast(snap.BackColor);
            snap.AccessibleName = Note.Snap ? "关闭磁吸" : "开启磁吸";
            tips.SetToolTip(snap, snap.AccessibleName);
        }

        protected override void OnMoveCompleted()
        {
            if (Note.Snap && WindowState == FormWindowState.Normal)
            {
                using (var graphics = CreateGraphics())
                {
                    float scale = graphics.DpiX / 96f;
                    Location = NotePlacement.Snap(Bounds, Screen.FromControl(this).WorkingArea,
                        session.OtherNoteBounds(this), (int)Math.Round(18 * scale), (int)Math.Round(8 * scale));
                }
            }
            RefreshHover();
        }

        private Button Tool(string label, string description, Action action, int width = 31)
        {
            var button = NoteStyle.Button(label, width, description);
            tips.SetToolTip(button, description);
            button.Click += (sender, args) => { action(); editor.Focus(); };
            return button;
        }

        protected override void PinChanged(bool pinned)
        {
            Note.Pinned = pinned;
            session.Changed();
        }

        private void RefreshTitle()
        {
            title.Text = Note.Title.Length == 0 ? "命名便笺" : Note.Title.Replace("&", "&&");
            tips.SetToolTip(title, Note.Title.Length == 0 ? "命名便笺" : Note.Title + " · 点击重命名");
        }

        private void Rename()
        {
            var value = TextPrompt.Ask(this, "便笺名称", "名称（留空时显示正文预览）", Note.Title, 64, null);
            if (value == null || value == Note.Title) return;
            Note.Title = value;
            Note.Updated = DateTime.UtcNow;
            RefreshTitle();
            session.Changed();
        }

        public void ApplyTheme()
        {
            var paper = NoteStyle.GetPaper(Note.Color);
            var background = Note.CustomBackground == 0
                ? (session.Dark ? paper.Dark : paper.Light) : Color.FromArgb(Note.CustomBackground);
            var surface = Note.CustomBackground == 0 ? paper.Accent : background;
            var foreground = Note.CustomBackground == 0 ? NoteStyle.Text(session.Dark) : NoteStyle.Contrast(background);
            header.BackColor = add.BackColor = title.BackColor = more.BackColor = surface;
            BackColor = paperOnly ? background : surface;
            add.ForeColor = title.ForeColor = more.ForeColor = NoteStyle.Contrast(surface);
            page.BackColor = editor.BackColor = toolbar.BackColor = appearance.BackColor = background;
            if (Note.DefaultInk != foreground.ToArgb())
            {
                styling = true;
                try { editor.ReplaceDefaultInk(Note.DefaultInk, foreground); }
                finally { styling = false; }
                Note.DefaultInk = foreground.ToArgb();
                contentChanged = true;
                session.Changed();
            }
            foreach (Control button in toolbar.Controls.Cast<Control>().Concat(appearance.Controls.Cast<Control>()))
            {
                button.ForeColor = foreground;
                button.BackColor = background;
            }
            StyleCaption(surface, NoteStyle.Contrast(surface));
            RefreshSnap();
            RefreshTools();
        }

        private void RefreshTools()
        {
            var selected = editor.GetFormat();
            for (int i = 0; i < formats.Length; i++)
            {
                uint mask = (uint)(1 << i);
                bool active = i == 4 ? editor.SelectionBullet : (selected.Mask & selected.Effects & mask) != 0;
                formats[i].BackColor = active ? NoteStyle.GetPaper(Note.Color).Accent : toolbar.BackColor;
                formats[i].ForeColor = NoteStyle.Contrast(formats[i].BackColor);
            }
        }

        private void Format(uint mask)
        {
            var current = editor.GetFormat();
            editor.SetStyle(mask, (current.Mask & current.Effects & mask) == 0);
            if (editor.SelectionLength != 0) Edited();
            RefreshTools();
        }

        private void ToggleBullet()
        {
            editor.SelectionBullet = !editor.SelectionBullet;
            Edited();
            RefreshTools();
        }

        private void OpenMenu()
        {
            var menu = new ContextMenuStrip();
            var colors = new ToolStripMenuItem("背景颜色");
            foreach (var paper in NoteStyle.Papers)
            {
                var item = new ToolStripMenuItem(paper.Label) { Checked = Note.CustomBackground == 0 && Note.Color == paper.Id };
                item.BackColor = paper.Light;
                item.Click += (sender, args) =>
                {
                    Note.Color = paper.Id;
                    SetBackground(0);
                };
                colors.DropDownItems.Add(item);
            }
            colors.DropDownItems.Add(new ToolStripSeparator());
            colors.DropDownItems.Add("自定义背景…", null, (sender, args) =>
                PickColor(editor.BackColor, selected => SetBackground(selected.ToArgb())));
            menu.Items.Add(colors);
            menu.Items.Add("手写或涂画", null, (sender, args) => Sketch());
            menu.Items.Add("便笺列表", null, (sender, args) => session.ShowList());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除便笺", null, (sender, args) => session.Delete(Note, this));
            ShowMenu(menu, more);
        }

        private void SetBackground(int background)
        {
            Note.CustomBackground = background;
            Note.Updated = DateTime.UtcNow;
            ApplyTheme();
            session.Changed();
        }

        private void PickColor(Color initial, Action<Color> apply)
        {
            using (var picker = new ColorDialog { FullOpen = true, Color = initial })
                if (picker.ShowDialog(this) == DialogResult.OK) apply(picker.Color);
        }

        private void OpenInkMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("默认文字颜色", null, (sender, args) => ApplyTextColor(Color.FromArgb(Note.DefaultInk)));
            string[] names = { "黑色", "白色", "红色", "橙色", "绿色", "蓝色", "紫色" };
            int[] colors = { 0x000000, 0xffffff, 0xca3038, 0xbe591a, 0x297347, 0x225fc2, 0x7841ac };
            for (int i = 0; i < names.Length; i++)
            {
                Color color = NoteStyle.ColorFrom(colors[i]);
                var item = new ToolStripMenuItem(names[i]);
                if (colors[i] != 0xffffff) item.ForeColor = color;
                item.Click += (sender, args) => ApplyTextColor(color);
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("自定义文字颜色…", null, (sender, args) =>
                PickColor(editor.SelectionColor.IsEmpty ? Color.FromArgb(Note.DefaultInk) : editor.SelectionColor, ApplyTextColor));
            ShowMenu(menu, ink);
        }

        private void ShowMenu(ContextMenuStrip menu, Control anchor)
        {
            openMenu = menu;
            menu.Closed += (sender, args) =>
            {
                openMenu = null;
                BeginInvoke(new Action(menu.Dispose));
            };
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void ApplyTextColor(Color color)
        {
            editor.Focus();
            editor.SelectionColor = color;
            if (editor.SelectionLength != 0) Edited();
        }

        private void ChooseFont()
        {
            using (var dialog = new FontPicker(this, session.Fonts, editor.GetFormat().FaceName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                editor.Focus();
                editor.SetFace(dialog.Family);
                if (editor.SelectionLength != 0) Edited();
            }
        }

        private void InstallEditorMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("撤销", null, (sender, args) => UndoEdit(false));
            menu.Items.Add("重做", null, (sender, args) => UndoEdit(true));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("剪切", null, (sender, args) => editor.Cut());
            menu.Items.Add("复制", null, (sender, args) => editor.Copy());
            menu.Items.Add("粘贴", null, (sender, args) => editor.Paste());
            menu.Items.Add("删除所选内容", null, (sender, args) => editor.SelectedText = "");
            menu.Items.Add("全选", null, (sender, args) => editor.SelectAll());
            menu.Opening += (sender, args) =>
            {
                menu.Items[0].Enabled = editor.CanUndo;
                menu.Items[1].Enabled = editor.CanRedo;
                menu.Items[3].Enabled = menu.Items[4].Enabled = menu.Items[6].Enabled = editor.SelectionLength != 0;
            };
            editor.ContextMenuStrip = menu;
        }

        private void PickImage()
        {
            using (var picker = new OpenFileDialog { Title = "插入图片",
                Filter = "图片|*.png;*.jpg;*.jpeg;*.gif;*.bmp|所有文件|*.*" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (var image = Image.FromFile(picker.FileName))
                        InsertImage(image);
                }
                catch (Exception error) { ShowError("无法插入图片", error); }
            }
        }

        private void Sketch()
        {
            using (var sketch = new SketchWindow())
            {
                if (sketch.ShowDialog(this) == DialogResult.OK)
                    InsertImage(sketch.Image);
            }
            editor.Focus();
        }

        private void InsertImage(Image image)
        {
            editor.InsertImage(image);
            Edited();
        }

        private void Edited()
        {
            contentChanged = true;
            Note.Updated = DateTime.UtcNow;
            session.Changed();
        }

        private void UndoEdit(bool redo)
        {
            if (redo && editor.CanRedo) editor.Redo();
            else if (!redo && editor.CanUndo) editor.Undo();
            else return;
            Edited();
            RefreshTools();
        }

        public void CaptureContent()
        {
            if (!contentChanged) return;
            Note.Rtf = editor.Rtf;
            Note.PlainText = string.Concat(editor.Text.Where(ch => !char.IsControl(ch) ||
                ch == '\t' || ch == '\n' || ch == '\r')).Replace("\ufffc", "");
            contentChanged = false;
        }

        private void ShowError(string message, Exception error)
        {
            MessageBox.Show(this, message + "。\n\n" + error.Message, Program.Name,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void RememberPosition() { NoteStyle.Remember(this, Note.Position, session); }
        protected override void OnFormClosing(FormClosingEventArgs args) { session.CloseNote(this, args); base.OnFormClosing(args); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hoverTimer.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            switch (key)
            {
                case Keys.Control | Keys.N: session.NewNote(Note.GroupId); return true;
                case Keys.Control | Keys.H: session.ShowList(); return true;
                case Keys.Control | Keys.F: session.SearchNotes(); return true;
                case Keys.Control | Keys.W: Close(); return true;
                case Keys.Control | Keys.D: session.Delete(Note, this); return true;
                case Keys.Control | Keys.B: Format(1); return true;
                case Keys.Control | Keys.I: Format(2); return true;
                case Keys.Control | Keys.U: Format(4); return true;
                case Keys.Control | Keys.T: Format(8); return true;
                case Keys.Control | Keys.Y: UndoEdit(true); return true;
                case Keys.Control | Keys.Z: UndoEdit(false); return true;
                case Keys.Control | Keys.Shift | Keys.L: ToggleBullet(); return true;
                case Keys.Control | Keys.Shift | Keys.J: editor.AddTodo(); return true;
                case Keys.Control | Keys.Shift | Keys.K: editor.ToggleCurrentTodo(); return true;
                case Keys.Control | Keys.Shift | Keys.P: TogglePin(); return true;
                case Keys.Control | Keys.Tab: session.Cycle(this, false); return true;
                case Keys.Control | Keys.Shift | Keys.Tab: session.Cycle(this, true); return true;
            }
            return base.ProcessCmdKey(ref message, key);
        }
    }

    internal sealed class RichEditor : RichTextBox
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct CharacterFormat
        {
            public uint Size, Mask, Effects;
            public int Height, Offset, TextColor;
            public byte Charset, Pitch;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FaceName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr flags,
            ref CharacterFormat format);

        private const int GetCharacterFormat = 0x0400 + 58;
        private const int SetCharacterFormat = 0x0400 + 68;
        private static readonly uint FormatSize = (uint)Marshal.SizeOf(typeof(CharacterFormat));

        public CharacterFormat GetFormat()
        {
            var format = new CharacterFormat { Size = FormatSize };
            SendMessage(Handle, GetCharacterFormat, new IntPtr(1), ref format);
            return format;
        }

        public void SetStyle(uint mask, bool enabled)
        {
            var format = new CharacterFormat { Size = FormatSize, Mask = mask, Effects = enabled ? mask : 0 };
            SendMessage(Handle, SetCharacterFormat, new IntPtr(1), ref format);
        }

        public void SetFace(string family)
        {
            var format = new CharacterFormat { Size = FormatSize, Mask = 0x28000000, Charset = 1, FaceName = family };
            SendMessage(Handle, SetCharacterFormat, new IntPtr(1), ref format);
        }

        public void ReplaceDefaultInk(int oldInk, Color newInk)
        {
            int start = SelectionStart, length = SelectionLength;
            int newValue = ColorTranslator.ToWin32(newInk);
            int oldValue = oldInk == 0 ? -1 : ColorTranslator.ToWin32(Color.FromArgb(oldInk));
            if (TextLength == 0)
            {
                SelectionColor = newInk;
                return;
            }
            ReplaceInkRange(0, TextLength, oldValue, newValue, newInk);
            Select(start, length);
        }

        private void ReplaceInkRange(int start, int length, int oldValue, int newValue, Color ink)
        {
            const uint color = 0x40000000;
            Select(start, length);
            var format = GetFormat();
            if ((format.Mask & color) == 0 && length > 1)
            {
                int half = length / 2;
                ReplaceInkRange(start, half, oldValue, newValue, ink);
                ReplaceInkRange(start + half, length - half, oldValue, newValue, ink);
                return;
            }
            bool legacyDefault = oldValue == -1 && (format.TextColor == ColorTranslator.ToWin32(NoteStyle.Text(false)) ||
                format.TextColor == ColorTranslator.ToWin32(NoteStyle.Text(true)));
            if ((format.Effects & color) != 0 || legacyDefault || format.TextColor == oldValue)
                if (format.TextColor != newValue || (format.Effects & color) != 0) SelectionColor = ink;
        }

        private const char OpenBox = '☐';
        private const char CheckedBox = '☑';

        private static int LineStart(string text, int index)
        {
            return index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        }

        private static bool IsTodo(string text, int start)
        {
            return start + 1 < text.Length && (text[start] == OpenBox || text[start] == CheckedBox) &&
                text[start + 1] == ' ';
        }

        public void AddTodo()
        {
            string text = Text;
            int start = LineStart(text, SelectionStart);
            if (IsTodo(text, start)) return;
            int selection = SelectionStart, length = SelectionLength;
            Select(start, 0);
            if (SelectionBullet) SelectionBullet = false;
            SelectedText = OpenBox + " ";
            Select(selection + 2, length);
        }

        public void ToggleCurrentTodo() { ToggleTodo(LineStart(Text, SelectionStart)); }

        private void ToggleTodo(int index)
        {
            var text = Text;
            if (!IsTodo(text, index)) return;
            int start = SelectionStart, length = SelectionLength;
            Select(index, 1);
            SelectedText = (text[index] == OpenBox ? CheckedBox : OpenBox).ToString();
            Select(start, length);
        }

        protected override void OnMouseDown(MouseEventArgs args)
        {
            if (args.Button == MouseButtons.Left)
            {
                string text = Text;
                int start = LineStart(text, GetCharIndexFromPosition(args.Location));
                if (IsTodo(text, start))
                {
                    var box = GetPositionFromCharIndex(start);
                    var next = GetPositionFromCharIndex(start + 1);
                    if (args.X >= box.X && args.X < next.X && args.Y >= box.Y && args.Y < box.Y + Font.Height)
                    {
                        Focus();
                        ToggleTodo(start);
                        return;
                    }
                }
            }
            base.OnMouseDown(args);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            if (key == Keys.Enter && SelectionLength == 0)
            {
                string text = Text;
                int caret = SelectionStart, start = LineStart(text, caret);
                if (IsTodo(text, start) && caret >= start + 2)
                {
                    if (caret == start + 2 && (caret == text.Length || text[caret] == '\n'))
                    {
                        Select(start, 2);
                        SelectedText = "";
                    }
                    else SelectedText = "\n" + OpenBox + " ";
                    return true;
                }
            }
            return base.ProcessCmdKey(ref message, key);
        }

        public void InsertImage(Image image)
        {
            float scale = Math.Min(1f, Math.Max(160, ClientSize.Width - 26) / (float)image.Width);
            scale = Math.Min(scale, 900f / image.Height);
            int width = Math.Max(1, (int)Math.Round(scale * image.Width));
            int height = Math.Max(1, (int)Math.Round(scale * image.Height));
            using (var resized = new Bitmap(image, width, height))
            using (var stream = new MemoryStream())
            {
                resized.Save(stream, ImageFormat.Bmp);
                var rtf = new StringBuilder(@"{\rtf1\ansi{\pict\dibitmap0");
                rtf.AppendFormat(@"\picw{0}\pich{1}\picwgoal{2}\pichgoal{3} ",
                    width, height, width * 15, height * 15);
                // A BMP file starts with a 14-byte file header. RTF stores only the DIB.
                byte[] bitmap = stream.ToArray();
                rtf.Append(BitConverter.ToString(bitmap, 14).Replace("-", ""));
                rtf.Append("}}");
                SelectedRtf = rtf.ToString();
            }
        }
    }

    internal sealed class SketchWindow : Form
    {
        private readonly PictureBox canvas = new PictureBox();
        private Point? previous;
        private readonly Button insert = NoteStyle.Button("插入便签", 95);
        public Bitmap Image { get; private set; }

        public SketchWindow()
        {
            Text = "手写或涂画";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            Image = new Bitmap(440, 251);
            canvas.Image = Image;
            canvas.Dock = DockStyle.Fill;
            canvas.Cursor = Cursors.Cross;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 49, Padding = new Padding(8) };
            var clear = NoteStyle.Button("清空", 70);
            clear.Click += (sender, args) => Clear();
            insert.DialogResult = DialogResult.OK;
            var cancel = NoteStyle.Button("取消", 70);
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            actions.Controls.AddRange(new Control[] { insert, clear, cancel });
            Controls.Add(canvas);
            Controls.Add(actions);
            canvas.MouseDown += (sender, args) =>
            {
                if (args.Button != MouseButtons.Left) return;
                previous = args.Location;
                Stroke(args.Location);
            };
            canvas.MouseMove += (sender, args) => { if (previous.HasValue) Stroke(args.Location); };
            canvas.MouseUp += (sender, args) => previous = null;
            canvas.MouseCaptureChanged += (sender, args) => previous = null;
            Clear();
        }

        private void Clear()
        {
            using (var graphics = Graphics.FromImage(Image)) graphics.Clear(Color.White);
            canvas.Invalidate();
            insert.Enabled = false;
        }

        private void Stroke(Point next)
        {
            var start = previous.Value;
            using (var graphics = Graphics.FromImage(Image))
            using (var pen = new Pen(NoteStyle.Text(false), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (start == next) graphics.FillEllipse(Brushes.Black, next.X - 1, next.Y - 1, 3, 3);
                else graphics.DrawLine(pen, start, next);
            }
            previous = next;
            canvas.Invalidate();
            insert.Enabled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Image != null)
            {
                canvas.Image = null;
                Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
