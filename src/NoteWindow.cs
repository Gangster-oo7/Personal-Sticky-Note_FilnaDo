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
    internal sealed class NoteWindow : Form
    {
        private readonly NoteSession session;
        private readonly RichEditor editor = new RichEditor();
        private readonly Panel header = new Panel();
        private readonly Panel page = new Panel();
        private readonly FlowLayoutPanel toolbar = new FlowLayoutPanel();
        private readonly Button add = NoteStyle.Button("＋", 34, "新建便签");
        private readonly Button pin = NoteStyle.Button("置顶", 64);
        private readonly Button more = NoteStyle.Button("⋯", 35, "便签选项");
        private readonly Button[] formats;
        private readonly ToolTip tips = new ToolTip();
        private bool contentChanged;
        public Note Note { get; private set; }

        public NoteWindow(NoteSession session, Note note)
        {
            this.session = session;
            Note = note;
            Text = Program.Name + " · 便签";
            Icon = NoteStyle.AppIcon;
            Font = new Font("Microsoft YaHei UI", 10);
            Size = new Size(350, 320);
            MinimumSize = new Size(310, 205);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            MaximizeBox = false;
            TopMost = note.Pinned;

            header.Dock = DockStyle.Top;
            header.Height = 40;
            header.Padding = new Padding(6, 5, 6, 4);
            add.Dock = DockStyle.Left;
            more.Dock = DockStyle.Right;
            pin.Dock = DockStyle.Right;
            header.Controls.Add(add);
            header.Controls.Add(pin);
            header.Controls.Add(more);
            add.Click += (sender, args) => session.NewNote();
            pin.Click += (sender, args) => TogglePin();
            more.Click += (sender, args) => OpenMenu();
            tips.SetToolTip(add, "新建便签（Ctrl+N）");
            tips.SetToolTip(more, "颜色、手写、便签列表和删除");

            editor.BorderStyle = BorderStyle.None;
            editor.Dock = DockStyle.Fill;
            editor.AccessibleName = "便签正文";
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
            toolbar.Controls.AddRange(formats);
            toolbar.Controls.Add(image);
            Controls.Add(page);
            Controls.Add(toolbar);
            Controls.Add(header);

            ApplyTheme();
            editor.ClearUndo();
            editor.TextChanged += (sender, args) => Edited();
            editor.SelectionChanged += (sender, args) => RefreshTools();
            InstallEditorMenu();
            NoteStyle.Restore(this, note.Position);
            Shown += (sender, args) => { editor.Focus(); RefreshTools(); };
            LocationChanged += (sender, args) => RememberPosition();
            SizeChanged += (sender, args) => RememberPosition();
        }

        private Button Tool(string label, string description, Action action, int width = 31)
        {
            var button = NoteStyle.Button(label, width, description);
            tips.SetToolTip(button, description);
            button.Click += (sender, args) => { action(); editor.Focus(); };
            return button;
        }

        private void TogglePin()
        {
            TopMost = Note.Pinned = !Note.Pinned;
            RefreshPin();
            session.Changed();
        }

        private void RefreshPin()
        {
            pin.Text = Note.Pinned ? "已置顶" : "置顶";
            pin.AccessibleName = Note.Pinned ? "取消窗口置顶" : "将便签窗口置顶";
            pin.BackColor = Note.Pinned ? NoteStyle.Text(false) : header.BackColor;
            pin.ForeColor = Note.Pinned ? Color.White : NoteStyle.Text(false);
            tips.SetToolTip(pin, pin.AccessibleName + "（Ctrl+Shift+P）");
        }

        public void ApplyTheme()
        {
            var paper = NoteStyle.GetPaper(Note.Color);
            var ink = NoteStyle.Text(session.Dark);
            header.BackColor = paper.Accent;
            add.BackColor = more.BackColor = paper.Accent;
            add.ForeColor = more.ForeColor = NoteStyle.Text(false);
            page.BackColor = editor.BackColor = toolbar.BackColor = session.Dark ? paper.Dark : paper.Light;
            if (editor.ForeColor != ink)
            {
                editor.ForeColor = ink;
                editor.ApplyInk(ink);
            }
            foreach (Control button in toolbar.Controls) button.ForeColor = ink;
            RefreshPin();
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
                formats[i].ForeColor = NoteStyle.Text(session.Dark && !active);
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
            var colors = new ToolStripMenuItem("便签颜色");
            foreach (var paper in NoteStyle.Papers)
            {
                var item = new ToolStripMenuItem(paper.Label) { Checked = Note.Color == paper.Id };
                item.BackColor = paper.Light;
                item.Click += (sender, args) =>
                {
                    Note.Color = paper.Id;
                    Note.Updated = DateTime.UtcNow;
                    ApplyTheme();
                    session.Changed();
                };
                colors.DropDownItems.Add(item);
            }
            menu.Items.Add(colors);
            menu.Items.Add("手写或涂画", null, (sender, args) => Sketch());
            menu.Items.Add("便签列表", null, (sender, args) => session.ShowList());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除便签", null, (sender, args) => session.Delete(Note, this));
            menu.Closed += (sender, args) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(more, new Point(0, more.Height));
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
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            switch (key)
            {
                case Keys.Control | Keys.N: session.NewNote(); return true;
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

        public void ApplyInk(Color ink)
        {
            var format = new CharacterFormat
            {
                Size = FormatSize, Mask = 0x40000000, TextColor = ColorTranslator.ToWin32(ink)
            };
            SendMessage(Handle, SetCharacterFormat, new IntPtr(4), ref format);
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
