using System;
using System.Drawing;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class SettingsView : UserControl
    {
        private readonly Panel body = new Panel { Dock = DockStyle.Fill };
        private readonly FlowLayoutPanel general = new FlowLayoutPanel { Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0) };
        private readonly ShortcutEditor shortcuts;
        private readonly SoftButton generalTab = (SoftButton)NoteStyle.Button(L.T("常规"), 66);
        private readonly SoftButton shortcutTab = (SoftButton)NoteStyle.Button(L.T("快捷键"), 76);
        private readonly Button back = NoteStyle.Button("‹ " + L.T("返回列表"), 92);
        private readonly Label version = new Label { AutoSize = true };
        private readonly Label updateStatus = new Label { AutoSize = true, Visible = false,
            Padding = new Padding(8, 5, 8, 5), Margin = new Padding(0, 2, 0, 3), MaximumSize = new Size(260, 0) };

        public SettingsView(NoteSession session, ListWindow owner)
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(14, 8, 14, 5);
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
            bar.Controls.AddRange(new Control[] { back, generalTab, shortcutTab });
            back.Click += (sender, args) => owner.ShowNotebook();
            generalTab.Click += (sender, args) => OpenTab(false);
            shortcutTab.Click += (sender, args) => OpenTab(true);
            var appearance = new FlowLayoutPanel { Width = 290, Height = 32, WrapContents = false };
            appearance.Controls.Add(new Label { Text = L.T("外观"), AutoSize = true, MinimumSize = new Size(61, 25),
                TextAlign = ContentAlignment.MiddleLeft });
            var theme = new ComboBox { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = L.T("外观") };
            string[] values = { "system", "light", "dark" };
            theme.Items.AddRange(new[] { L.T("跟随系统"), L.T("浅色"), L.T("深色") });
            theme.SelectedIndex = Math.Max(0, Array.IndexOf(values, session.Book.Theme));
            theme.SelectionChangeCommitted += (sender, args) =>
            {
                session.Book.Theme = values[theme.SelectedIndex];
                session.ApplySettings();
            };
            appearance.Controls.Add(theme);
            general.Controls.Add(appearance);
            AddButton(L.T("默认配色…"), session.ConfigurePalette);
            var confirm = new CheckBox { Text = L.T("删除前确认"), AutoSize = true, Checked = session.Book.ConfirmDelete,
                Margin = new Padding(3, 8, 3, 4) };
            confirm.CheckedChanged += (sender, args) => { session.Book.ConfirmDelete = confirm.Checked; session.Changed(); };
            general.Controls.Add(confirm);
            var startup = new CheckBox { Text = L.T("登录 Windows 后自动启动"), AutoSize = true, AutoCheck = false,
                Checked = DesktopIntegration.StartsWithWindows, Margin = new Padding(3, 4, 3, 8) };
            startup.Click += (sender, args) =>
            {
                bool enabled = !DesktopIntegration.StartsWithWindows;
                if (!enabled || owner.Confirm(L.T("允许个人便签在登录 Windows 后自动显示列表和已置顶的便笺吗？")))
                    owner.TryDesktopAction(() => DesktopIntegration.SetStartup(enabled));
                startup.Checked = DesktopIntegration.StartsWithWindows;
            };
            general.Controls.Add(startup);
            AddButton(L.T("创建桌面快捷方式"), () => owner.TryDesktopAction(() =>
            {
                DesktopIntegration.CreateDesktopShortcut();
                session.Notify(L.T("已创建桌面快捷方式。"));
            }));
            AddButton(L.T("检查更新…"), session.CheckForUpdates);
            general.Controls.Add(updateStatus);
            version.Text = L.T("版本 {0}", AppUpdates.DisplayVersion);
            version.Margin = new Padding(3, 10, 3, 3);
            general.Controls.Add(version);
            shortcuts = new ShortcutEditor(session);
            body.Controls.Add(general);
            body.Controls.Add(shortcuts);
            Controls.Add(body);
            Controls.Add(bar);
            OpenTab(false);
        }

        private void AddButton(string text, Action action)
        {
            var button = NoteStyle.Button(text, 190);
            button.Margin = new Padding(0, 2, 0, 3);
            button.Click += (sender, args) => action();
            general.Controls.Add(button);
        }

        private void OpenTab(bool editShortcuts)
        {
            general.Visible = !editShortcuts;
            shortcuts.Visible = editShortcuts;
            generalTab.Selected = !editShortcuts;
            shortcutTab.Selected = editShortcuts;
            generalTab.Invalidate();
            shortcutTab.Invalidate();
        }

        public void ShowGeneral() { OpenTab(false); }

        public void SetUpdateStatus(string message, bool scroll)
        {
            updateStatus.Text = message;
            updateStatus.Visible = message.Length != 0;
            if (scroll && updateStatus.Visible) general.ScrollControlIntoView(updateStatus);
        }

        public void ApplyTheme(Color paper, Color ink)
        {
            BackColor = paper;
            ForeColor = ink;
            Color field = InterfaceStyle.Blend(paper, ink, 0.07f);
            back.BackColor = generalTab.BackColor = shortcutTab.BackColor = field;
            foreach (Control control in general.Controls)
                if (control is Button) control.BackColor = field;
            version.ForeColor = InterfaceStyle.Blend(ink, paper, 0.27f);
            updateStatus.BackColor = InterfaceStyle.Blend(paper, InterfaceStyle.Accent(paper), 0.15f);
            updateStatus.ForeColor = NoteStyle.Contrast(updateStatus.BackColor);
            shortcuts.ApplyTheme(paper, ink);
        }
    }
}
