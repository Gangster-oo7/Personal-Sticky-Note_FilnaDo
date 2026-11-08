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
            menu.Items.Add("设置", null, (sender, args) => list.OpenSettings());
            menu.Items.Add("检查更新…", null, (sender, args) => session.CheckForUpdates());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (sender, args) => session.Quit());

            icon = new NotifyIcon { Icon = NoteStyle.AppIcon, Text = Program.Name + " " + AppUpdates.DisplayVersion,
                ContextMenuStrip = menu, Visible = true };
            icon.MouseDoubleClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left) session.ShowList();
            };
        }

        private void AddAction(string label, ShortcutAction shortcut, ShortcutMap shortcuts, Action action)
        {
            var item = new ToolStripMenuItem(label) { ShortcutKeyDisplayString = shortcuts.Label(shortcut) };
            item.Click += (sender, args) => action();
            menu.Opening += (sender, args) => item.ShortcutKeyDisplayString = shortcuts.Label(shortcut);
            menu.Items.Add(item);
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
