using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    public sealed class ShortcutBinding
    {
        public string Command;
        public int Key;
    }

    internal enum ShortcutAction
    {
        New, OpenList, Search, Close, Delete, Pin, Next, Previous,
        Bold, Italic, Underline, Strike, Bullet, Todo, ToggleTodo, Undo, Redo
    }

    internal sealed class ShortcutMap
    {
        internal sealed class Command
        {
            public readonly ShortcutAction Action;
            private readonly string label;
            public string Label { get { return L.T(label); } }
            public readonly Keys Default;
            public readonly bool EditorOnly;
            public Command(ShortcutAction action, string label, Keys key, bool editorOnly = false)
            {
                Action = action; this.label = label; Default = key; EditorOnly = editorOnly;
            }
        }

        private const Keys Ctrl = Keys.Control;
        private const Keys CtrlShift = Keys.Control | Keys.Shift;
        public static readonly Command[] Commands =
        {
            new Command(ShortcutAction.New, "新建便笺", Ctrl | Keys.N),
            new Command(ShortcutAction.OpenList, "打开便笺列表", Ctrl | Keys.H),
            new Command(ShortcutAction.Search, "搜索便笺", Ctrl | Keys.F),
            new Command(ShortcutAction.Close, "关闭当前窗口", Ctrl | Keys.W),
            new Command(ShortcutAction.Delete, "删除便笺", Ctrl | Keys.D),
            new Command(ShortcutAction.Pin, "切换窗口置顶", CtrlShift | Keys.P),
            new Command(ShortcutAction.Next, "切换到下一窗口", Ctrl | Keys.Tab),
            new Command(ShortcutAction.Previous, "切换到上一窗口", CtrlShift | Keys.Tab),
            new Command(ShortcutAction.Bold, "加粗", Ctrl | Keys.B, true),
            new Command(ShortcutAction.Italic, "斜体", Ctrl | Keys.I, true),
            new Command(ShortcutAction.Underline, "下划线", Ctrl | Keys.U, true),
            new Command(ShortcutAction.Strike, "删除线", Ctrl | Keys.T, true),
            new Command(ShortcutAction.Bullet, "项目符号", CtrlShift | Keys.L, true),
            new Command(ShortcutAction.Todo, "切换待办列表", CtrlShift | Keys.J, true),
            new Command(ShortcutAction.ToggleTodo, "切换待办状态", CtrlShift | Keys.K, true),
            new Command(ShortcutAction.Undo, "撤销", Ctrl | Keys.Z, true),
            new Command(ShortcutAction.Redo, "重做", Ctrl | Keys.Y, true)
        };

        private readonly Notebook book;
        private Dictionary<ShortcutAction, Keys> keys = Defaults();

        public ShortcutMap(Notebook book)
        {
            this.book = book;
            var candidate = Defaults();
            foreach (var item in book.Shortcuts)
            {
                ShortcutAction action;
                if (Enum.TryParse(item.Command, out action) && candidate.ContainsKey(action) &&
                    ValidateKey((Keys)item.Key) == null) candidate[action] = (Keys)item.Key;
            }
            if (candidate.Values.Distinct().Count() == candidate.Count) keys = candidate;
        }

        public Dictionary<ShortcutAction, Keys> Copy() { return new Dictionary<ShortcutAction, Keys>(keys); }
        public static Dictionary<ShortcutAction, Keys> Defaults()
        {
            return Commands.ToDictionary(command => command.Action, command => command.Default);
        }

        public void Save(Dictionary<ShortcutAction, Keys> value)
        {
            keys = new Dictionary<ShortcutAction, Keys>(value);
            book.Shortcuts = Commands.Where(item => keys[item.Action] != item.Default)
                .Select(item => new ShortcutBinding { Command = item.Action.ToString(), Key = (int)keys[item.Action] })
                .ToList();
        }

        public ShortcutAction? Find(Keys key, bool editor)
        {
            foreach (var command in Commands)
                if ((!command.EditorOnly || editor) && keys[command.Action] == key) return command.Action;
            return null;
        }

        public bool IsReplacedDefault(Keys key, bool editor)
        {
            return Commands.Any(command => (!command.EditorOnly || editor) &&
                command.Default == key && keys[command.Action] != key);
        }

        public string Label(ShortcutAction action) { return Format(keys[action]); }

        public static string Format(Keys key)
        {
            var parts = new List<string>();
            if ((key & Keys.Control) != 0) parts.Add("Ctrl");
            if ((key & Keys.Alt) != 0) parts.Add("Alt");
            if ((key & Keys.Shift) != 0) parts.Add("Shift");
            Keys code = key & Keys.KeyCode;
            if (code >= Keys.D0 && code <= Keys.D9) parts.Add(((int)code - (int)Keys.D0).ToString());
            else parts.Add(code.ToString());
            return string.Join("+", parts);
        }

        public static string ValidateKey(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            bool letter = code >= Keys.A && code <= Keys.Z;
            bool number = code >= Keys.D0 && code <= Keys.D9;
            bool function = code >= Keys.F1 && code <= Keys.F12;
            if ((key & (Keys.Control | Keys.Alt)) == 0 || !(letter || number || function || code == Keys.Tab))
                return L.T("请使用 Ctrl 或 Alt，加上字母、数字、Tab 或 F1–F12。");
            if (((key & Keys.Modifiers) == Keys.Control && new[] { Keys.A, Keys.C, Keys.X, Keys.V }.Contains(code)) ||
                ((key & Keys.Alt) != 0 && (code == Keys.F4 || code == Keys.Tab)))
                return L.T("这个组合键保留给 Windows 或基本编辑操作。");
            return null;
        }
    }

    internal sealed class ShortcutEditor : UserControl
    {
        private readonly NoteSession session;
        private Dictionary<ShortcutAction, Keys> working;
        private readonly Dictionary<ShortcutAction, KeyBox> boxes = new Dictionary<ShortcutAction, KeyBox>();
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 44, TextAlign = ContentAlignment.MiddleLeft };

        public ShortcutEditor(NoteSession session)
        {
            this.session = session;
            Dock = DockStyle.Fill;
            Padding = new Padding(14, 10, 14, 10);
            working = session.Shortcuts.Copy();
            var hint = new Label { Text = L.T("点按组合键框后输入新组合。快捷键在个人便签窗口中生效。"),
                Dock = DockStyle.Top, Height = 54, TextAlign = ContentAlignment.MiddleLeft };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
            foreach (var command in ShortcutMap.Commands)
            {
                var label = new Label { Text = command.Label, Dock = DockStyle.Fill, Height = 40,
                    TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(2, 1, 0, 4) };
                var box = new KeyBox { Text = ShortcutMap.Format(working[command.Action]), Dock = DockStyle.Fill,
                    ReadOnly = true, AccessibleName = L.T("{0}的快捷键", command.Label), Margin = new Padding(4, 1, 12, 5) };
                box.Record = key => Assign(command.Action, key);
                boxes.Add(command.Action, box);
                rows.Controls.Add(label);
                rows.Controls.Add(box);
            }
            scroll.Controls.Add(rows);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 39 };
            var save = NoteStyle.Button(L.T("保存快捷键"), 110);
            var reset = NoteStyle.Button(L.T("恢复默认"), 94);
            save.Click += (sender, args) =>
            {
                session.Shortcuts.Save(working);
                session.ShortcutsChanged();
                status.Text = L.T("快捷键已保存。");
            };
            reset.Click += (sender, args) =>
            {
                working = ShortcutMap.Defaults();
                foreach (var pair in boxes) pair.Value.Text = ShortcutMap.Format(working[pair.Key]);
                status.Text = L.T("已填入默认值，点击「保存快捷键」后生效。");
            };
            buttons.Controls.AddRange(new Control[] { save, reset });
            Controls.Add(scroll);
            Controls.Add(status);
            Controls.Add(buttons);
            Controls.Add(hint);
        }

        public void ApplyTheme(Color paper, Color ink)
        {
            BackColor = paper;
            ForeColor = ink;
            var field = InterfaceStyle.Blend(paper, ink, 0.07f);
            foreach (var box in boxes.Values) { box.BackColor = field; box.ForeColor = ink; }
            foreach (var panel in Controls.OfType<FlowLayoutPanel>())
                foreach (Control button in panel.Controls) button.BackColor = field;
        }

        private void Assign(ShortcutAction action, Keys key)
        {
            string error = ShortcutMap.ValidateKey(key);
            if (error != null) { status.Text = error; return; }
            var conflict = ShortcutMap.Commands.FirstOrDefault(command => command.Action != action &&
                working[command.Action] == key);
            if (conflict != null) { status.Text = L.T("此组合键已用于「{0}」。", conflict.Label); return; }
            working[action] = key;
            boxes[action].Text = ShortcutMap.Format(key);
            status.Text = L.T("点击「保存快捷键」使修改生效。");
        }

        private sealed class KeyBox : TextBox
        {
            public Action<Keys> Record;
            protected override bool ProcessCmdKey(ref Message message, Keys key)
            {
                Keys code = key & Keys.KeyCode;
                if (key == Keys.Tab || key == (Keys.Shift | Keys.Tab) || key == Keys.Escape ||
                    key == (Keys.Alt | Keys.F4)) return base.ProcessCmdKey(ref message, key);
                if (code != Keys.ControlKey && code != Keys.ShiftKey && code != Keys.Menu) Record(key);
                return true;
            }
        }
    }
}
