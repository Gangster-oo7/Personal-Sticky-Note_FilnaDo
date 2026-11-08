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
        private readonly Panel page = new Panel();
        private readonly ToolStrip toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden,
            CanOverflow = true, Padding = new Padding(6, 3, 6, 3), TabStop = true,
            Renderer = new EditorStripRenderer(), AccessibleName = "便笺编辑工具" };
        private readonly System.Collections.Generic.Dictionary<ToolStripButton, Glyph> icons =
            new System.Collections.Generic.Dictionary<ToolStripButton, Glyph>();
        private readonly ToolStripButton[] formats;
        private readonly ToolStripButton add, more, snap, font, fontSize, ink, undo, redo, todo;
        private bool contentChanged;
        private bool styling;
        public Note Note { get; private set; }

        public NoteWindow(NoteSession session, Note note)
        {
            this.session = session;
            Note = note;
            Text = Program.Name + "·便笺";
            Icon = NoteStyle.AppIcon;
            Font = new Font("Microsoft YaHei UI", 10);
            Size = new Size(528, 356);
            MinimumSize = new Size(326, 238);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            TopMost = note.Pinned;
            PinShortcut = session.Shortcuts.Label(ShortcutAction.Pin);

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
            page.Padding = new Padding(17, 14, 12, 9);
            page.Dock = DockStyle.Fill;
            page.Controls.Add(editor);

            toolbar.Font = Font;
            add = Tool("新建便笺", () => session.NewNote(Note.GroupId), Glyph.Add, true);
            undo = Tool("撤销", () => UndoEdit(false), Glyph.Undo);
            redo = Tool("重做", () => UndoEdit(true), Glyph.Redo);
            font = Tool("字体 ▾", ChooseFont, essential: true);
            fontSize = Tool("11 pt ▾", ChooseFontSize);
            fontSize.AccessibleName = fontSize.ToolTipText = "字号";
            ink = Tool("颜色 ▾", OpenInkMenu, essential: true);
            formats = new[]
            {
                Tool("B", () => Format(1)),
                Tool("I", () => Format(2)),
                Tool("U", () => Format(4)),
                Tool("S", () => Format(8)),
                Tool("≡", ToggleBullet)
            };
            formats[0].Font = new Font(Font, FontStyle.Bold);
            formats[1].Font = new Font(Font, FontStyle.Italic);
            formats[2].Font = new Font(Font, FontStyle.Underline);
            formats[3].Font = new Font(Font, FontStyle.Strikeout);
            todo = Tool("添加待办", editor.AddTodo, Glyph.Todo, true);
            var image = Tool("插入图片", PickImage, Glyph.Image);
            snap = Tool("磁吸", () => { Note.Snap = !Note.Snap; RefreshSnap(); session.Changed(); }, Glyph.Magnet, true);
            more = Tool("更多操作", OpenMenu, Glyph.More, true);
            toolbar.Items.AddRange(new ToolStripItem[] { add, undo, redo, new ToolStripSeparator(), font, fontSize,
                formats[0], formats[1], formats[2], formats[3], ink, formats[4], todo, image, snap, more });
            RefreshShortcutLabels();
            Controls.Add(page);
            Controls.Add(toolbar);
            InstallCaption();

            ApplyTheme();
            editor.ClearUndo();
            editor.TextChanged += (sender, args) => { if (!styling) Edited(); };
            editor.SelectionChanged += (sender, args) => { if (!styling) RefreshTools(); };
            InstallEditorMenu();
            NoteStyle.Restore(this, note.Position);
            Shown += (sender, args) => { editor.Focus(); RefreshTools(); };
            LocationChanged += (sender, args) => RememberPosition();
            SizeChanged += (sender, args) => RememberPosition();
        }

        private void RefreshSnap()
        {
            snap.Checked = Note.Snap;
            snap.AccessibleName = Note.Snap ? "关闭磁吸" : "开启磁吸";
            snap.ToolTipText = snap.AccessibleName;
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
        }

        private ToolStripButton Tool(string label, Action action, Glyph? icon = null, bool essential = false)
        {
            var button = new ToolStripButton(label) { AccessibleName = label, ToolTipText = label,
                DisplayStyle = icon.HasValue ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.Text,
                Overflow = essential ? ToolStripItemOverflow.Never : ToolStripItemOverflow.AsNeeded,
                Padding = new Padding(1, 4, 1, 4) };
            if (icon.HasValue) icons.Add(button, icon.Value);
            button.Click += (sender, args) => action();
            return button;
        }

        private void Describe(ToolStripButton item, string label, ShortcutAction action)
        {
            item.AccessibleName = label;
            item.ToolTipText = label + "（" + session.Shortcuts.Label(action) + "）";
        }

        public void RefreshShortcutLabels()
        {
            PinShortcut = session.Shortcuts.Label(ShortcutAction.Pin);
            RefreshPin();
            Describe(add, "新建便笺", ShortcutAction.New);
            Describe(undo, "撤销", ShortcutAction.Undo);
            Describe(redo, "重做", ShortcutAction.Redo);
            Describe(todo, "添加待办", ShortcutAction.Todo);
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

        private void Rename()
        {
            var value = TextPrompt.Ask(this, "便笺名称", "名称（留空时显示正文预览）", Note.Title, 64, null);
            if (value == null || value == Note.Title) return;
            Note.Title = value;
            Note.Updated = DateTime.UtcNow;
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
            foreach (var pair in icons)
            {
                var previous = pair.Key.Image;
                pair.Key.Image = InterfaceStyle.Icon(pair.Value, foreground);
                if (previous != null) previous.Dispose();
            }
            StyleCaption(surface, NoteStyle.Contrast(surface));
            RefreshSnap();
            RefreshTools();
        }

        private void RefreshTools()
        {
            var selected = editor.GetFormat();
            fontSize.Text = (selected.Mask & 0x80000000u) == 0
                ? "字号 ▾" : Math.Round(selected.Height / 20m).ToString("0") + " pt ▾";
            for (int i = 0; i < formats.Length; i++)
            {
                uint mask = (uint)(1 << i);
                bool active = i == 4 ? editor.SelectionBullet : (selected.Mask & selected.Effects & mask) != 0;
                formats[i].Checked = active;
            }
            undo.Enabled = editor.CanUndo;
            redo.Enabled = editor.CanRedo;
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
            menu.Items.Add(Note.Title.Length == 0 ? "命名便笺…" : "重命名便笺…", null, (sender, args) => Rename());
            menu.Items.Add("手写或涂画", null, (sender, args) => Sketch());
            menu.Items.Add("便笺列表", null, (sender, args) => session.ShowList());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除便笺", null, (sender, args) => session.Delete(Note, this));
            ShowMenu(menu, more);
        }

        private ToolStripMenuItem BackgroundMenu()
        {
            var colors = new ToolStripMenuItem("便笺背景");
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
            colors.DropDownItems.Add("自定义背景…", null, (sender, args) =>
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
            if (editor.SelectionLength != 0) Edited();
        }

        private void ChooseFont()
        {
            var menu = new ContextMenuStrip();
            var current = editor.GetFormat().FaceName;
            var names = session.Fonts.Families();
            var favorites = new[] { current, "微软雅黑", "Microsoft YaHei UI", "宋体", "SimSun", "仿宋", "FangSong",
                "楷体", "KaiTi", "幼圆", "YouYuan", "黑体", "SimHei", "等线", "DengXian", "Segoe UI" }
                .Where(name => names.Contains(name, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var ordered = favorites.Concat(names).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var panel = new Panel { Size = new Size(228, 244), Padding = new Padding(4) };
            var filter = new TextBox { Dock = DockStyle.Top, AccessibleName = "搜索字体" };
            var choices = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "字体列表" };
            panel.Controls.Add(choices);
            panel.Controls.Add(filter);
            choices.Items.AddRange(ordered);
            choices.SelectedIndex = Array.FindIndex(ordered, name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
            menu.Items.Add(new ToolStripControlHost(panel) { AutoSize = false, Size = panel.Size });
            filter.TextChanged += (sender, args) =>
            {
                choices.BeginUpdate();
                choices.Items.Clear();
                choices.Items.AddRange(ordered.Where(name => name.IndexOf(filter.Text.Trim(),
                    StringComparison.CurrentCultureIgnoreCase) >= 0).ToArray());
                if (choices.Items.Count > 0) choices.SelectedIndex = 0;
                choices.EndUpdate();
            };
            Action choose = () =>
            {
                var family = choices.SelectedItem as string;
                if (family == null) return;
                menu.Close();
                ApplyFont(family);
            };
            choices.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left && choices.IndexFromPoint(args.Location) >= 0) choose();
            };
            KeyEventHandler selectOnEnter = (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; choose(); }
                if (args.KeyCode == Keys.Down && sender == filter && choices.Items.Count > 0)
                { choices.Focus(); args.SuppressKeyPress = true; }
            };
            choices.KeyDown += selectOnEnter;
            filter.KeyDown += selectOnEnter;
            choices.PreviewKeyDown += (sender, args) => { if (args.KeyCode == Keys.Enter) args.IsInputKey = true; };
            filter.PreviewKeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter || args.KeyCode == Keys.Down) args.IsInputKey = true;
            };
            menu.Opened += (sender, args) => filter.Focus();
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("导入字体…", null, (sender, args) => ImportFont());
            ShowMenu(menu, font);
        }

        private void ChooseFontSize()
        {
            var menu = new ContextMenuStrip();
            int size = editor.GetFormat().Height;
            foreach (int points in new[] { 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32 })
            {
                var item = new ToolStripMenuItem(points + " pt") { Checked = size == points * 20 };
                item.Click += (sender, args) =>
                {
                    editor.Focus();
                    editor.SetPointSize(points);
                    if (editor.SelectionLength != 0) Edited();
                    RefreshTools();
                };
                menu.Items.Add(item);
            }
            ShowMenu(menu, fontSize);
        }

        private void ApplyFont(string family)
        {
            editor.Focus();
            editor.SetFace(family);
            if (editor.SelectionLength != 0) Edited();
        }

        private void ImportFont()
        {
            using (var picker = new OpenFileDialog { Title = "导入字体", Filter = "字体文件|*.ttf;*.otf;*.ttc" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var families = session.Fonts.Import(picker.FileName);
                    if (families.Length == 1) ApplyFont(families[0]);
                    else ChooseFont();
                }
                catch (Exception error) { ShowError("无法导入字体", error); }
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
            undo.Enabled = editor.CanUndo;
            redo.Enabled = editor.CanRedo;
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
                foreach (var button in icons.Keys)
                    if (button.Image != null) button.Image.Dispose();
            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
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
                case ShortcutAction.Todo: editor.AddTodo(); return true;
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

        public void SetPointSize(int points)
        {
            var format = new CharacterFormat { Size = FormatSize, Mask = 0x80000000, Height = points * 20 };
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
