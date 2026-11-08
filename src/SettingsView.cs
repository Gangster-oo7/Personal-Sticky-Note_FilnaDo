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
        private readonly SoftButton generalTab = (SoftButton)NoteStyle.Button("常规", 66);
        private readonly SoftButton shortcutTab = (SoftButton)NoteStyle.Button("快捷键", 76);
        private readonly Button back = NoteStyle.Button("‹  返回列表", 108);
        private readonly Label version = new Label { AutoSize = true };

        public SettingsView(NoteSession session, ListWindow owner)
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(14, 8, 14, 5);
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
            bar.Controls.AddRange(new Control[] { back, generalTab, shortcutTab });
            back.Click += (sender, args) => owner.ShowNotebook();
            generalTab.Click += (sender, args) => OpenTab(false);
            shortcutTab.Click += (sender, args) => OpenTab(true);
            var appearance = new FlowLayoutPanel { Width = 250, Height = 32, WrapContents = false };
            appearance.Controls.Add(new Label { Text = "外观", Width = 61, Height = 25,
                TextAlign = ContentAlignment.MiddleLeft });
            var theme = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "外观" };
            string[] values = { "system", "light", "dark" };
            theme.Items.AddRange(new[] { "跟随系统", "浅色", "深色" });
            theme.SelectedIndex = Math.Max(0, Array.IndexOf(values, session.Book.Theme));
            theme.SelectionChangeCommitted += (sender, args) =>
            {
                session.Book.Theme = values[theme.SelectedIndex];
                session.ApplySettings();
            };
            appearance.Controls.Add(theme);
            general.Controls.Add(appearance);
            AddButton("默认配色…", session.ConfigurePalette);
            var confirm = new CheckBox { Text = "删除前确认", AutoSize = true, Checked = session.Book.ConfirmDelete,
                Margin = new Padding(3, 8, 3, 4) };
            confirm.CheckedChanged += (sender, args) => { session.Book.ConfirmDelete = confirm.Checked; session.Changed(); };
            general.Controls.Add(confirm);
            var startup = new CheckBox { Text = "登录 Windows 后自动启动", AutoSize = true, AutoCheck = false,
                Checked = DesktopIntegration.StartsWithWindows, Margin = new Padding(3, 4, 3, 8) };
            startup.Click += (sender, args) =>
            {
                bool enabled = !DesktopIntegration.StartsWithWindows;
                if (!enabled || owner.Confirm("允许个人便签在登录 Windows 后自动显示列表和已置顶的便笺吗？"))
                    owner.TryDesktopAction(() => DesktopIntegration.SetStartup(enabled));
                startup.Checked = DesktopIntegration.StartsWithWindows;
            };
            general.Controls.Add(startup);
            AddButton("创建桌面快捷方式", () => owner.TryDesktopAction(() =>
            {
                DesktopIntegration.CreateDesktopShortcut();
                session.Notify("已创建桌面快捷方式。");
            }));
            AddButton("检查更新…", session.CheckForUpdates);
            AddButton("GitHub 更新访问…", () => session.Updates.ConfigureAccess(owner));
            version.Text = "版本 " + AppUpdates.DisplayVersion;
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

        public void ApplyTheme(Color paper, Color ink)
        {
            BackColor = paper;
            ForeColor = ink;
            Color field = InterfaceStyle.Blend(paper, ink, 0.07f);
            back.BackColor = generalTab.BackColor = shortcutTab.BackColor = field;
            foreach (Control control in general.Controls)
                if (control is Button) control.BackColor = field;
            version.ForeColor = InterfaceStyle.Blend(ink, paper, 0.27f);
            shortcuts.ApplyTheme(paper, ink);
        }
    }
}
