using System;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class TrayHost : IDisposable
    {
        private readonly NotifyIcon icon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();

        public TrayHost(NoteSession session, ListWindow list)
        {
            AddAction("打开便笺列表", ShortcutAction.OpenList, session.Shortcuts, () => session.ShowList());
            AddAction("新建便笺", ShortcutAction.New, session.Shortcuts, () => session.NewNote());
            AddAction("搜索便笺", ShortcutAction.Search, session.Shortcuts, session.SearchNotes);
            menu.Items.Add(new ToolStripSeparator());
            AddItem("设置", () => list.OpenSettings());
            AddItem("检查更新…", session.CheckForUpdates);
            menu.Items.Add(new ToolStripSeparator());
            AddItem("退出", () => session.Quit());

            icon = new NotifyIcon { Icon = NoteStyle.AppIcon, Text = Program.Name + " " + AppUpdates.DisplayVersion,
                ContextMenuStrip = menu, Visible = true };
            icon.MouseDoubleClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left) session.ShowList();
            };
        }

        private void AddAction(string label, ShortcutAction shortcut, ShortcutMap shortcuts, Action action)
        {
            var item = AddItem(label, action);
            item.ShortcutKeyDisplayString = shortcuts.Label(shortcut);
            menu.Opening += (sender, args) => item.ShortcutKeyDisplayString = shortcuts.Label(shortcut);
        }

        private ToolStripMenuItem AddItem(string label, Action action)
        {
            var item = new ToolStripMenuItem(L.T(label)) { Tag = label };
            item.Click += (sender, args) => action();
            menu.Items.Add(item);
            return item;
        }

        public void ApplyLanguage()
        {
            icon.Text = Program.Name + " " + AppUpdates.DisplayVersion;
            foreach (ToolStripItem item in menu.Items)
                if (item.Tag is string) item.Text = L.T((string)item.Tag);
        }

        public void Notify(string message)
        {
            icon.ShowBalloonTip(3500, Program.Name, message, ToolTipIcon.Info);
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
        }
    }
}
