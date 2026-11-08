using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class NoteWindow : CaptionWindow
    {
        private const int ToolbarIconSize = 24;
        private readonly NoteSession session;
        private readonly RichEditor editor = new RichEditor();
        private readonly Panel page = new Panel();
        private readonly ToolStrip toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden,
            CanOverflow = true, Padding = new Padding(6, 3, 6, 3), TabStop = true,
            ImageScalingSize = new Size(ToolbarIconSize, ToolbarIconSize),
            Renderer = new EditorStripRenderer(), AccessibleName = L.T("便笺编辑工具") };
        private readonly System.Collections.Generic.Dictionary<ToolStripButton, Glyph> icons =
            new System.Collections.Generic.Dictionary<ToolStripButton, Glyph>();
        private readonly ToolStripButton[] formats;
        private readonly ToolStripButton add, more, snap, ink, todo;
        private Color colorIconInk;
        private Color colorIconUnderline;
        private bool contentChanged;
        private bool styling;
        public Note Note { get; private set; }

        public NoteWindow(NoteSession session, Note note) : base(showIcon: false)
        {
            SuspendLayout();
            this.session = session;
            Note = note;
            Icon = NoteStyle.AppIcon;
            Font = new Font("Microsoft YaHei UI", 10);
            Size = new Size(528, 356);
            MinimumSize = new Size(326, 238);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            TopMost = note.Pinned;
            PinShortcut = session.Shortcuts.Label(ShortcutAction.Pin);

            editor.BorderStyle = BorderStyle.None;
            editor.Dock = DockStyle.Fill;
            editor.ScrollBars = RichTextBoxScrollBars.Vertical;
            editor.DetectUrls = true;
            editor.HideSelection = false;
            if (!string.IsNullOrEmpty(note.Rtf)) editor.Rtf = note.Rtf;
            else editor.Text = note.PlainText;
            bool normalized = editor.NormalizeBodyFont();
            if (editor.NormalizeTodoSpacing() || normalized)
            {
                contentChanged = true;
                session.Changed();
            }
            editor.LinkClicked += (sender, args) =>
            {
                Uri url;
                if (Uri.TryCreate(args.LinkText, UriKind.Absolute, out url) &&
                    (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
                {
                    try { System.Diagnostics.Process.Start(url.AbsoluteUri); }
                    catch (System.ComponentModel.Win32Exception error) { ShowError(L.T("无法打开链接"), error); }
                }
            };
            page.Padding = new Padding(17, 14, 12, 9);
            page.Dock = DockStyle.Fill;
            page.Controls.Add(editor);

            toolbar.Font = new Font(Font.FontFamily, 12);
            add = Tool("新建便笺", () => session.NewNote(Note.GroupId), Glyph.Add, true);
            ink = Tool("文字颜色", OpenInkMenu, essential: true);
            ink.DisplayStyle = ToolStripItemDisplayStyle.Image;
            ink.ImageScaling = ToolStripItemImageScaling.None;
            formats = new[]
            {
                Tool("B", () => Format(1)),
                Tool("I", () => Format(2)),
                Tool("U", () => Format(4)),
                Tool("S", () => Format(8)),
                Tool("≡", ToggleBullet)
            };
            formats[0].Font = new Font(toolbar.Font, FontStyle.Bold);
            formats[1].Font = new Font(toolbar.Font, FontStyle.Italic);
            formats[2].Font = new Font(toolbar.Font, FontStyle.Underline);
            formats[3].Font = new Font(toolbar.Font, FontStyle.Strikeout);
            todo = Tool("待办列表", editor.ToggleTodoList, Glyph.Todo, true);
            var image = Tool("插入图片", PickImage, Glyph.Image);
            snap = Tool("磁吸", () => { Note.Snap = !Note.Snap; RefreshSnap(); session.Changed(); }, Glyph.Magnet, true);
            more = Tool("更多操作", OpenMenu, Glyph.More, true);
            toolbar.Items.AddRange(new ToolStripItem[] { add,
                formats[0], formats[1], formats[2], formats[3], ink, formats[4], todo, image, snap, more });
            Controls.Add(page);
            Controls.Add(toolbar);
            InstallCaption();

            ApplyLanguage();
            ApplyTheme();
            editor.ClearUndo();
            editor.TextChanged += (sender, args) => { if (!styling) Edited(); };
            editor.SelectionChanged += (sender, args) => { if (!styling) RefreshTools(); };
            editor.CompositionEnded += (sender, args) => RefreshTools();
            Load += (sender, args) =>
            {
                NoteStyle.PlaceOnScreen(this, note.Position, center: false);
                using (var graphics = CreateGraphics())
                {
                    int size = (int)Math.Round(ToolbarIconSize * graphics.DpiX / 96f);
                    toolbar.ImageScalingSize = new Size(size, size);
                }
                RefreshToolbarIcons(toolbar.BackColor, ink.ForeColor);
                RefreshTools();
            };
            Shown += (sender, args) => { editor.Focus(); RefreshTools(); };
            LocationChanged += (sender, args) => RememberPosition();
            SizeChanged += (sender, args) => RememberPosition();
            ResumeLayout(false);
        }

        public void ApplyLanguage()
        {
            Text = L.T("个人便签·便笺");
            toolbar.AccessibleName = L.T("便笺编辑工具");
            editor.AccessibleName = L.T("便笺正文");
            foreach (ToolStripButton button in toolbar.Items)
                button.Text = button.AccessibleName = button.ToolTipText = L.T((string)button.Tag);
            ApplyCaptionLanguage();
            RefreshShortcutLabels();
            RefreshSnap();
            InstallEditorMenu();
        }

        private void RefreshSnap()
        {
            snap.Checked = Note.Snap;
            snap.AccessibleName = Note.Snap ? L.T("关闭磁吸") : L.T("开启磁吸");
            snap.ToolTipText = snap.AccessibleName;
        }

        protected override void OnMoveCompleted()
        {
            if (Note.Snap) SnapToDesktop(session.OtherWindowBounds(this));
        }

        private ToolStripButton Tool(string label, Action action, Glyph? icon = null, bool essential = false)
        {
            var button = new ToolStripButton { Tag = label,
                DisplayStyle = icon.HasValue ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.Text,
                ImageScaling = ToolStripItemImageScaling.None,
                Overflow = essential ? ToolStripItemOverflow.Never : ToolStripItemOverflow.AsNeeded,
                Padding = new Padding(1, 4, 1, 4) };
            if (icon.HasValue) icons.Add(button, icon.Value);
            button.Click += (sender, args) => action();
            return button;
        }

        private void Describe(ToolStripButton item, string label, ShortcutAction action)
        {
            item.AccessibleName = label;
            item.ToolTipText = L.T("{0}（{1}）", label, session.Shortcuts.Label(action));
        }

        public void RefreshShortcutLabels()
        {
            PinShortcut = session.Shortcuts.Label(ShortcutAction.Pin);
            RefreshPin();
            Describe(add, L.T("新建便笺"), ShortcutAction.New);
            Describe(todo, L.T("切换待办列表"), ShortcutAction.Todo);
            var actions = new[] { ShortcutAction.Bold, ShortcutAction.Italic, ShortcutAction.Underline,
                ShortcutAction.Strike, ShortcutAction.Bullet };
            for (int i = 0; i < formats.Length; i++)
                Describe(formats[i], ShortcutMap.Commands.First(command => command.Action == actions[i]).Label, actions[i]);
        }

        protected override void PinChanged(bool pinned)
        {
            Note.Pinned = pinned;
            session.Changed();
        }

        public void ApplyTheme()
        {
            var paper = NoteStyle.GetPaper(Note.Color);
            var background = Note.CustomBackground == 0
                ? (session.Dark ? paper.Dark : paper.Light) : Color.FromArgb(Note.CustomBackground);
            var foreground = Note.CustomBackground == 0 ? NoteStyle.Text(session.Dark) : NoteStyle.Contrast(background);
            var tint = Note.CustomBackground == 0 ? paper.Accent : foreground;
            var surface = InterfaceStyle.Blend(background, tint, 0.12f);
            BackColor = surface;
            page.BackColor = editor.BackColor = toolbar.BackColor = background;
            toolbar.OverflowButton.DropDown.BackColor = background;
            toolbar.OverflowButton.DropDown.ForeColor = foreground;
            if (Note.DefaultInk != foreground.ToArgb())
            {
                styling = true;
                try { editor.ReplaceDefaultInk(Note.DefaultInk, foreground); }
                finally { styling = false; }
                Note.DefaultInk = foreground.ToArgb();
                contentChanged = true;
                session.Changed();
            }
            foreach (ToolStripItem item in toolbar.Items) item.ForeColor = foreground;
            RefreshToolbarIcons(background, foreground);
            StyleCaption(surface, NoteStyle.Contrast(surface));
            RefreshSnap();
            RefreshTools();
        }

        private void RefreshToolbarIcons(Color background, Color foreground)
        {
            foreach (var pair in icons)
            {
                var previous = pair.Key.Image;
                pair.Key.Image = InterfaceStyle.Icon(pair.Value,
                    pair.Value == Glyph.Add ? InterfaceStyle.Accent(background) : foreground,
                    toolbar.ImageScalingSize.Height);
                if (previous != null) previous.Dispose();
            }
        }

        private void RefreshTools()
        {
            if (editor.IsImeComposing) return;
            var selected = editor.GetFormat();
            const uint characterColor = 0x40000000; // CFM_COLOR / CFE_AUTOCOLOR
            RefreshTextColor((selected.Mask & characterColor) != 0 && (selected.Effects & characterColor) == 0
                ? ColorTranslator.FromWin32(selected.TextColor) : Color.FromArgb(Note.DefaultInk));
            for (int i = 0; i < formats.Length; i++)
            {
                uint mask = (uint)(1 << i);
                bool active = i == 4 ? editor.SelectionBullet : (selected.Mask & selected.Effects & mask) != 0;
                formats[i].Checked = active;
            }
            todo.Checked = editor.CurrentLineIsTodo;
        }

        private void RefreshTextColor(Color underline)
        {
            var foreground = ink.ForeColor;
            int height = toolbar.ImageScalingSize.Height;
            var previous = ink.Image;
            if (previous != null && previous.Height == height && foreground == colorIconInk && underline == colorIconUnderline)
                return;
            ink.Image = InterfaceStyle.TextColorIcon(foreground, underline, height);
            colorIconInk = foreground;
            colorIconUnderline = underline;
            if (previous != null) previous.Dispose();
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
            menu.Items.Add(Note.Title.Length == 0 ? L.T("命名便笺…") : L.T("重命名便笺…"), null,
                (sender, args) => session.Rename(Note, this));
            menu.Items.Add(L.T("手写或涂画"), null, (sender, args) => Sketch());
            menu.Items.Add(L.T("便笺列表"), null, (sender, args) => session.ShowList());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("删除便笺"), null, (sender, args) => session.Delete(Note, this));
            ShowMenu(menu, more);
        }

        private ToolStripMenuItem BackgroundMenu()
        {
            var colors = new ToolStripMenuItem(L.T("便笺背景"));
            foreach (var paper in new[] { NoteStyle.Neutral }.Concat(NoteStyle.Papers))
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
            colors.DropDownItems.Add(L.T("自定义背景…"), null, (sender, args) =>
                PickColor(editor.BackColor, selected => SetBackground(selected.ToArgb())));
            return colors;
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
            menu.Items.Add(L.T("默认文字颜色"), null, (sender, args) => ApplyTextColor(Color.FromArgb(Note.DefaultInk)));
            string[] names = { L.T("黑色"), L.T("白色"), L.T("红色"), L.T("橙色"), L.T("绿色"), L.T("蓝色"), L.T("紫色") };
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
            menu.Items.Add(L.T("自定义文字颜色…"), null, (sender, args) =>
                PickColor(editor.SelectionColor.IsEmpty ? Color.FromArgb(Note.DefaultInk) : editor.SelectionColor, ApplyTextColor));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(BackgroundMenu());
            ShowMenu(menu, ink);
        }

        private void ShowMenu(ContextMenuStrip menu, ToolStripItem anchor)
        {
            var parent = anchor.GetCurrentParent();
            if (parent == null) { menu.Dispose(); return; }
            // Anchor overflow commands to the persistent toolbar, since its overflow menu closes on selection.
            if (parent != toolbar) PresentMenu(menu, toolbar, new Point(toolbar.Width - 30, toolbar.Height));
            else PresentMenu(menu, parent, new Point(anchor.Bounds.Left, anchor.Bounds.Bottom));
        }

        private void ApplyTextColor(Color color)
        {
            editor.Focus();
            editor.SelectionColor = color;
            RefreshTextColor(color);
            if (editor.SelectionLength != 0) Edited();
        }

        private void InstallEditorMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(L.T("撤销"), null, (sender, args) => UndoEdit(false));
            menu.Items.Add(L.T("重做"), null, (sender, args) => UndoEdit(true));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("剪切"), null, (sender, args) => editor.Cut());
            menu.Items.Add(L.T("复制"), null, (sender, args) => editor.Copy());
            menu.Items.Add(L.T("粘贴"), null, (sender, args) => editor.PasteWithMarkdown(false));
            menu.Items.Add(L.T("删除所选内容"), null, (sender, args) => editor.SelectedText = "");
            menu.Items.Add(L.T("全选"), null, (sender, args) => editor.SelectAll());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("粘贴 Markdown 待办"), null, (sender, args) => editor.PasteWithMarkdown(true));
            menu.Opening += (sender, args) =>
            {
                menu.Items[0].Enabled = editor.CanUndo;
                menu.Items[1].Enabled = editor.CanRedo;
                menu.Items[3].Enabled = menu.Items[4].Enabled = menu.Items[6].Enabled = editor.SelectionLength != 0;
            };
            var previous = editor.ContextMenuStrip;
            editor.ContextMenuStrip = menu;
            if (previous != null) previous.Dispose();
        }

        private void PickImage()
        {
            using (var picker = new OpenFileDialog { Title = L.T("插入图片"),
                Filter = L.T("图片") + "|*.png;*.jpg;*.jpeg;*.gif;*.bmp|" + L.T("所有文件") + "|*.*" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (var image = Image.FromFile(picker.FileName))
                        InsertImage(image);
                }
                catch (Exception error) { ShowError(L.T("无法插入图片"), error); }
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
            if (editor.IsImeComposing) return;
            todo.Checked = editor.CurrentLineIsTodo;
        }

        private void UndoEdit(bool redo)
        {
            if (redo && editor.CanRedo) editor.Redo();
            else if (!redo && editor.CanUndo) editor.Undo();
            else return;
            Edited();
            RefreshTools();
        }

        public bool IsImeComposing { get { return editor.IsImeComposing; } }

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
                foreach (var button in icons.Keys)
                    if (button.Image != null) button.Image.Dispose();
                if (ink != null && ink.Image != null) ink.Image.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            if (editor.IsImeComposing) return base.ProcessCmdKey(ref message, key);
            switch (session.Shortcuts.Find(key, true))
            {
                case ShortcutAction.New: session.NewNote(Note.GroupId); return true;
                case ShortcutAction.OpenList: session.ShowList(); return true;
                case ShortcutAction.Search: session.SearchNotes(); return true;
                case ShortcutAction.Close: Close(); return true;
                case ShortcutAction.Delete: session.Delete(Note, this); return true;
                case ShortcutAction.Bold: Format(1); return true;
                case ShortcutAction.Italic: Format(2); return true;
                case ShortcutAction.Underline: Format(4); return true;
                case ShortcutAction.Strike: Format(8); return true;
                case ShortcutAction.Redo: UndoEdit(true); return true;
                case ShortcutAction.Undo: UndoEdit(false); return true;
                case ShortcutAction.Bullet: ToggleBullet(); return true;
                case ShortcutAction.Todo: editor.ToggleTodoList(); return true;
                case ShortcutAction.ToggleTodo: editor.ToggleCurrentTodo(); return true;
                case ShortcutAction.Pin: TogglePin(); return true;
                case ShortcutAction.Next: session.Cycle(this, false); return true;
                case ShortcutAction.Previous: session.Cycle(this, true); return true;
            }
            if (session.Shortcuts.IsReplacedDefault(key, true)) return true;
            return base.ProcessCmdKey(ref message, key);
        }
    }

    internal sealed class RichEditor : RichTextBox
    {
        private const string BodyFace = "Microsoft YaHei UI";
        private const int BodyHeight = 220; // 11 pt in twips.
        private const uint BodyMask = 0xA8000000; // CFM_FACE | CFM_SIZE | CFM_CHARSET
        private const int GetLanguageOptions = 0x0400 + 121;
        private const int SetLanguageOptions = 0x0400 + 120;
        private const int AutoKeyboard = 0x0001;
        private const int AutoFont = 0x0002;
        private const int NotifyDuringComposition = 0x0008;
        private const int AutoFontSize = 0x0010;
        private const int DualFont = 0x0080;
        private bool imeComposing;
        public bool IsImeComposing { get { return imeComposing; } }
        public event EventHandler CompositionEnded;

        public RichEditor()
        {
            Font = new Font(BodyFace, BodyHeight / 20f);
        }

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
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr flags, IntPtr value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr flags, ref TextRange range);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr flags, ref CharacterRange range);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr flags, ref Point point);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, ref Point point, IntPtr index);
        [DllImport("user32.dll")]
        private static extern bool GetUpdateRect(IntPtr window, IntPtr rect, bool erase);
        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr window, out PaintState state);
        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr window, ref PaintState state);

        [StructLayout(LayoutKind.Sequential)]
        private struct TextRange
        {
            public int Start, End;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CharacterRange
        {
            public int Start, End;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaintState
        {
            private IntPtr dc;
            private int erase, left, top, right, bottom, restore, update;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] private byte[] reserved;
        }

        private const int GetCharacterFormat = 0x0400 + 58;
        private const int SetCharacterFormat = 0x0400 + 68;
        private static readonly uint FormatSize = (uint)Marshal.SizeOf(typeof(CharacterFormat));

        public CharacterFormat GetFormat()
        {
            var format = new CharacterFormat { Size = FormatSize };
            SendMessage(Handle, GetCharacterFormat, new IntPtr(1), ref format);
            return format;
        }

        protected override void OnHandleCreated(EventArgs args)
        {
            base.OnHandleCreated(args);
            var options = SendMessage(Handle, GetLanguageOptions, IntPtr.Zero, IntPtr.Zero).ToInt32();
            options &= ~(AutoKeyboard | AutoFont | AutoFontSize | DualFont | NotifyDuringComposition);
            SendMessage(Handle, SetLanguageOptions, IntPtr.Zero, new IntPtr(options));
            SetBodyFont(0);
        }

        public void SetStyle(uint mask, bool enabled)
        {
            var format = new CharacterFormat { Size = FormatSize, Mask = mask, Effects = enabled ? mask : 0 };
            SendMessage(Handle, SetCharacterFormat, new IntPtr(1), ref format);
        }

        private void SetBodyFont(int scope)
        {
            var format = new CharacterFormat { Size = FormatSize, Mask = BodyMask, Height = BodyHeight,
                FaceName = BodyFace, Charset = 1 };
            // Setting the default takes zero. Selection changes also keep the current keyboard layout.
            SendMessage(Handle, SetCharacterFormat, new IntPtr(scope == 0 ? 0 : scope | 0x20), ref format);
        }

        public bool NormalizeBodyFont()
        {
            SetBodyFont(0);
            if (TextLength == 0) return false;
            return NormalizeFontRange(0, TextLength);
        }

        private bool NormalizeFontRange(int start, int length)
        {
            if (length <= 0) return false;
            int caret = SelectionStart, selection = SelectionLength;
            Select(start, length);
            var format = GetFormat();
            const uint faceAndSize = 0xA0000000;
            bool change = (format.Mask & faceAndSize) != faceAndSize || format.Height != BodyHeight ||
                !string.Equals(format.FaceName, BodyFace, StringComparison.OrdinalIgnoreCase);
            if (change) SetBodyFont(1);
            Select(caret, selection);
            return change;
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
        private const char TaskSpacer = '\u2002';
        private const int StyledMarkerLength = 3;
        private static readonly Regex MarkdownMarker = new Regex(@"^ {0,3}[-+*][ \t]+\[([ xX])\](?:[ \t]+|$)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private string paragraphs;
        private int pressedMarker = -1;
        private int hoveredMarker = -1;
        private BufferedGraphicsContext checklistBuffer;
        private bool paintChecklist;

        private string Paragraphs { get { return paragraphs ?? (paragraphs = Text); } }

        public bool CurrentLineIsTodo
        {
            get { return IsTodo(Paragraphs, LineStart(Paragraphs, SelectionStart)); }
        }

        private static int LineStart(string text, int index)
        {
            int end = Math.Min(index, text.Length);
            return end <= 0 ? 0 : text.LastIndexOf('\n', end - 1) + 1;
        }

        private static bool IsTodo(string text, int start)
        {
            return start + 1 < text.Length && (text[start] == OpenBox || text[start] == CheckedBox) &&
                text[start + 1] == ' ';
        }

        private static int MarkerLength(string text, int start)
        {
            return start + 2 < text.Length && text[start + 2] == TaskSpacer ? StyledMarkerLength : 2;
        }

        private static string TaskPrefix(char marker) { return marker + " " + TaskSpacer; }

        public bool NormalizeTodoSpacing()
        {
            string text = Paragraphs;
            var missing = new System.Collections.Generic.List<int>();
            for (int start = 0; start < text.Length;)
            {
                if (IsTodo(text, start) && MarkerLength(text, start) == 2) missing.Add(start + 2);
                int end = text.IndexOf('\n', start);
                if (end < 0) break;
                start = end + 1;
            }
            if (missing.Count == 0) return false;
            int selectionStart = SelectionStart, selectionEnd = selectionStart + SelectionLength;
            for (int i = missing.Count - 1; i >= 0; i--)
            {
                int position = missing[i];
                Select(position, 0);
                SelectedText = TaskSpacer.ToString();
                if (selectionStart >= position) selectionStart++;
                if (selectionEnd >= position) selectionEnd++;
            }
            Select(selectionStart, selectionEnd - selectionStart);
            return true;
        }

        public void ToggleTodoList()
        {
            string text = Paragraphs;
            int selectionStart = SelectionStart, selectionEnd = selectionStart + SelectionLength;
            int last = LineStart(text, Math.Max(selectionStart, selectionEnd - 1));
            var lines = new System.Collections.Generic.List<int> { LineStart(text, selectionStart) };
            while (lines[lines.Count - 1] < last)
                lines.Add(text.IndexOf('\n', lines[lines.Count - 1]) + 1);
            bool remove = lines.All(start => IsTodo(text, start));
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                int start = lines[i];
                if (!remove && IsTodo(text, start)) continue;
                int length = remove ? MarkerLength(text, start) : StyledMarkerLength;
                Select(start, remove ? length : 0);
                if (!remove && SelectionBullet) SelectionBullet = false;
                SelectedText = remove ? "" : TaskPrefix(OpenBox);
                selectionStart = MoveSelection(selectionStart, start, length, remove);
                selectionEnd = MoveSelection(selectionEnd, start, length, remove);
            }
            Select(selectionStart, selectionEnd - selectionStart);
        }

        private static int MoveSelection(int offset, int start, int length, bool remove)
        {
            if (offset < start) return offset;
            return remove ? Math.Max(start, offset - length) : offset + length;
        }

        public void ToggleCurrentTodo() { ToggleTodo(LineStart(Paragraphs, SelectionStart)); }

        private void ToggleTodo(int index)
        {
            var text = Paragraphs;
            if (!IsTodo(text, index)) return;
            int start = SelectionStart, length = SelectionLength;
            Select(index, 1);
            SelectedText = (text[index] == OpenBox ? CheckedBox : OpenBox).ToString();
            Select(start, length);
        }

        internal static string ConvertMarkdownTasks(string input, bool includeFirstLine)
        {
            string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
            return MarkdownMarker.Replace(normalized, marker => marker.Index == 0 && !includeFirstLine
                ? marker.Value : TaskPrefix(marker.Groups[1].Value == " " ? OpenBox : CheckedBox));
        }

        public void PasteWithMarkdown(bool forceText)
        {
            try
            {
                if (Clipboard.ContainsText() && (forceText || !Clipboard.ContainsData(DataFormats.Rtf)))
                {
                    string text = Clipboard.GetText();
                    bool atStart = SelectionStart == LineStart(Paragraphs, SelectionStart);
                    string converted = ConvertMarkdownTasks(text, atStart);
                    if (forceText || converted != text.Replace("\r\n", "\n").Replace('\r', '\n'))
                    {
                        SelectedText = converted;
                        return;
                    }
                }
            }
            catch (ExternalException) { }
            if (forceText) return;
            int start = SelectionStart;
            Paste();
            NormalizeFontRange(start, SelectionStart - start);
            NormalizeTodoSpacing();
        }

        protected override void OnKeyPress(KeyPressEventArgs args)
        {
            if (!IsImeComposing && args.KeyChar == ' ' && SelectionLength == 0)
            {
                int caret = SelectionStart, start = LineStart(Paragraphs, caret);
                if (caret - start <= 14)
                {
                    string prefix = Paragraphs.Substring(start, caret - start);
                    var marker = MarkdownMarker.Match(prefix);
                    if (marker.Success && marker.Length == prefix.Length && prefix.EndsWith("]", StringComparison.Ordinal))
                    {
                        Select(start, caret - start);
                        SelectedText = ConvertMarkdownTasks(prefix, true);
                        args.Handled = true;
                        return;
                    }
                }
            }
            base.OnKeyPress(args);
        }

        protected override void OnTextChanged(EventArgs args)
        {
            paragraphs = null;
            hoveredMarker = -1;
            Invalidate();
            base.OnTextChanged(args);
        }

        protected override void OnMouseMove(MouseEventArgs args)
        {
            base.OnMouseMove(args);
            if (IsImeComposing) return;
            int marker = TodoAt(args.Location);
            if (hoveredMarker == marker) return;
            hoveredMarker = marker;
            Cursor = marker < 0 ? Cursors.IBeam : Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs args)
        {
            base.OnMouseLeave(args);
            hoveredMarker = -1;
            Cursor = Cursors.IBeam;
            Invalidate();
        }

        private int TodoAt(Point point)
        {
            int start = LineStart(Paragraphs, GetCharIndexFromPosition(point));
            if (!IsTodo(Paragraphs, start)) return -1;
            var marker = MarkerArea(Paragraphs, start);
            marker.Width = Math.Min(marker.Width, Font.Height + 4);
            return marker.Contains(point) ? start : -1;
        }

        private Rectangle MarkerArea(string text, int start)
        {
            Point origin = CharacterPosition(start);
            int content = start + MarkerLength(text, start);
            Point next = CharacterPosition(content);
            int width = next.Y == origin.Y && next.X > origin.X
                ? next.X - origin.X : CharacterPosition(content - 1).X - origin.X + Font.Height / 2;
            if (width <= 1) return Rectangle.Empty;
            int following = GetFirstCharIndexFromLine(GetLineFromCharIndex(start) + 1);
            int height = following >= 0 ? CharacterPosition(following).Y - origin.Y : 0;
            if (height <= 0) height = Math.Max(Font.Height, (int)Math.Ceiling(width * 1.4));
            return new Rectangle(origin.X, origin.Y, width, height);
        }

        // The WinForms text and position accessors can finish an active IME composition.
        // These Rich Edit messages read the live text and layout without committing it.
        private string CompositionText()
        {
            int length = TextLength;
            var range = new TextRange { End = length, Buffer = Marshal.AllocHGlobal((length + 1) * 2) };
            try
            {
                int copied = SendMessage(Handle, 0x044b, IntPtr.Zero, ref range).ToInt32(); // EM_GETTEXTRANGE
                return Marshal.PtrToStringUni(range.Buffer, copied).Replace('\r', '\n');
            }
            finally { Marshal.FreeHGlobal(range.Buffer); }
        }

        private Point CharacterPosition(int index)
        {
            var point = Point.Empty;
            SendMessage(Handle, 0x00d6, ref point, new IntPtr(index)); // EM_POSFROMCHAR
            return point;
        }

        private int CharacterAt(Point point)
        {
            return SendMessage(Handle, 0x00d7, IntPtr.Zero, ref point).ToInt32(); // EM_CHARFROMPOS
        }

        protected override void WndProc(ref Message message)
        {
            int id = message.Msg;
            if (id == 0x0014 && paintChecklist) // WM_ERASEBKGND, handled in the buffer
            {
                message.Result = new IntPtr(1);
                return;
            }
            if (id == 0x000f && ClientSize.Width > 0 && ClientSize.Height > 0 && // WM_PAINT
                GetUpdateRect(message.HWnd, IntPtr.Zero, false))
            {
                if (!imeComposing)
                {
                    string text = Paragraphs;
                    paintChecklist = text.IndexOf(OpenBox) >= 0 || text.IndexOf(CheckedBox) >= 0;
                }
                if (paintChecklist)
                {
                    PaintBuffered(message.HWnd);
                    message.Result = IntPtr.Zero;
                    return;
                }
            }
            if (id == 0x010d) imeComposing = true; // WM_IME_STARTCOMPOSITION
            if (!IsImeComposing && (id == 0x0201 || id == 0x0202 || id == 0x0203)) // WM_LBUTTONDOWN, UP, DBLCLK
            {
                long coordinates = message.LParam.ToInt64();
                int marker = TodoAt(new Point((short)coordinates, (short)(coordinates >> 16)));
                if (id == 0x0201 && marker >= 0)
                {
                    Focus();
                    pressedMarker = marker;
                    Capture = true;
                    return;
                }
                if (id == 0x0202 && pressedMarker >= 0)
                {
                    int pressed = pressedMarker;
                    pressedMarker = -1;
                    Capture = false;
                    if (pressed == marker) ToggleTodo(marker);
                    return;
                }
                if (id == 0x0203 && marker >= 0) return;
            }
            if (id == 0x0215) pressedMarker = -1; // WM_CAPTURECHANGED
            base.WndProc(ref message);
            if (imeComposing && (id == 0x010e || id == 0x0008)) // WM_IME_ENDCOMPOSITION / WM_KILLFOCUS
            {
                imeComposing = false;
                Invalidate();
                var ended = CompositionEnded;
                if (ended != null) ended(this, EventArgs.Empty);
            }
            if (id == 0x0318 && message.WParam != IntPtr.Zero) // WM_PRINTCLIENT
                using (var canvas = Graphics.FromHdc(message.WParam)) PaintTodoBoxes(canvas);
        }

        private void PaintBuffered(IntPtr window)
        {
            PaintState state;
            var dc = BeginPaint(window, out state);
            try
            {
                if (dc == IntPtr.Zero) return;
                if (checklistBuffer == null) checklistBuffer = new BufferedGraphicsContext();
                var capacity = checklistBuffer.MaximumBuffer;
                if (capacity.Width <= ClientSize.Width || capacity.Height <= ClientSize.Height)
                    checklistBuffer.MaximumBuffer = new Size(ClientSize.Width + 1, ClientSize.Height + 1);
                using (var target = Graphics.FromHdc(dc))
                using (var buffer = checklistBuffer.Allocate(target, ClientRectangle))
                {
                    var canvas = buffer.Graphics;
                    canvas.Clear(BackColor);
                    var destination = canvas.GetHdc();
                    try
                    {
                        var print = Message.Create(window, 0x0318, destination, new IntPtr(4)); // WM_PRINTCLIENT, PRF_CLIENT
                        base.WndProc(ref print);
                    }
                    finally { canvas.ReleaseHdc(destination); }
                    PaintTodoBoxes(canvas);
                    buffer.Render();
                }
            }
            finally { EndPaint(window, ref state); }
        }

        private void PaintTodoBoxes(Graphics canvas)
        {
            string text = IsImeComposing ? CompositionText() : Paragraphs;
            if (text.Length == 0 || ClientSize.Height <= 0) return;
            int first = LineStart(text, Math.Max(0, CharacterAt(Point.Empty)));
            int last = CharacterAt(new Point(0, ClientSize.Height - 1));
            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            Color accent = InterfaceStyle.Accent(BackColor);
            var selection = new CharacterRange();
            SendMessage(Handle, 0x0434, IntPtr.Zero, ref selection); // EM_EXGETSEL
            int preferred = Math.Max(11, (int)Math.Round(14 * canvas.DpiX / 96f));
            for (int start = first; start <= last;)
            {
                if (IsTodo(text, start))
                {
                    Rectangle area = MarkerArea(text, start);
                    if (area.Width >= 9 && area.IntersectsWith(ClientRectangle))
                    {
                        bool selected = start >= selection.Start && start < selection.End;
                        bool done = text[start] == CheckedBox;
                        Color background = selected ? SystemColors.Highlight : BackColor;
                        int size = Math.Min(preferred, Math.Min(area.Width - 2, area.Height - 2));
                        var box = new Rectangle(area.Left + 1,
                            area.Top + (area.Height - size) / 2, size, size);
                        using (var clear = new SolidBrush(background)) canvas.FillRectangle(clear, area);
                        using (var path = InterfaceStyle.Rounded(box, 3))
                        using (var fill = new SolidBrush(done ? accent : InterfaceStyle.Blend(background, accent,
                            start == hoveredMarker ? 0.20f : 0.025f)))
                        using (var border = new Pen(done || start == hoveredMarker ? accent :
                            InterfaceStyle.Blend(background, NoteStyle.Contrast(background), 0.43f), 1.15f))
                        {
                            canvas.FillPath(fill, path);
                            canvas.DrawPath(border, path);
                        }
                        if (done)
                        {
                            using (var mark = new Pen(NoteStyle.Contrast(accent), 1.45f)
                                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                                canvas.DrawLines(mark, new[] { new PointF(box.X + size * 0.23f, box.Y + size * 0.51f),
                                    new PointF(box.X + size * 0.44f, box.Y + size * 0.71f),
                                    new PointF(box.X + size * 0.78f, box.Y + size * 0.30f) });
                        }
                    }
                }
                int lineEnd = text.IndexOf('\n', start);
                if (lineEnd < 0) break;
                start = lineEnd + 1;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && checklistBuffer != null) checklistBuffer.Dispose();
            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            if (IsImeComposing) return base.ProcessCmdKey(ref message, key);
            if (key == (Keys.Control | Keys.V) || key == (Keys.Shift | Keys.Insert))
            {
                PasteWithMarkdown(false);
                return true;
            }
            if ((key == Keys.Enter || key == Keys.Back || key == Keys.Delete || key == Keys.Home) && SelectionLength == 0)
            {
                string text = Paragraphs;
                int caret = SelectionStart, start = LineStart(text, caret);
                if (IsTodo(text, start))
                {
                    int length = MarkerLength(text, start);
                    int content = start + length;
                    int end = text.IndexOf('\n', content);
                    if (end < 0) end = text.Length;
                    if (key == Keys.Home && GetLineFromCharIndex(start) == GetLineFromCharIndex(caret))
                    { Select(content, 0); return true; }
                    if ((key == Keys.Back && caret == content) || (key == Keys.Delete && caret == start) ||
                        (key == Keys.Enter && string.IsNullOrWhiteSpace(text.Substring(content, end - content))))
                    {
                        Select(start, length);
                        SelectedText = "";
                        return true;
                    }
                    if (key == Keys.Enter)
                    {
                        if (caret < content) Select(content, 0);
                        SelectedText = "\n" + TaskPrefix(OpenBox);
                        return true;
                    }
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
        private readonly Button insert = NoteStyle.Button(L.T("插入便签"), 95);
        public Bitmap Image { get; private set; }

        public SketchWindow()
        {
            Text = L.T("手写或涂画");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            Image = new Bitmap(440, 251);
            canvas.Image = Image;
            canvas.Dock = DockStyle.Fill;
            canvas.Cursor = Cursors.Cross;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 49, Padding = new Padding(8) };
            var clear = NoteStyle.Button(L.T("清空"), 70);
            clear.Click += (sender, args) => Clear();
            insert.DialogResult = DialogResult.OK;
            var cancel = NoteStyle.Button(L.T("取消"), 70);
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
