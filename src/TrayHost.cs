using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class TrayHost : IDisposable
    {
        private readonly NotifyIcon icon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();

        public TrayHost(NoteSession session, ListWindow list)
        {
            AddAction("打开便笺列表", "Ctrl+H", () => session.ShowList());
            AddAction("新建便笺", "Ctrl+N", () => session.NewNote());
            AddAction("搜索便笺", "Ctrl+F", session.SearchNotes);
            menu.Items.Add("快捷键…", null, (sender, args) => ShowShortcuts(session, list));
            var settings = new ToolStripMenuItem("设置");
            list.FillSettings(settings.DropDownItems);
            settings.DropDownOpening += (sender, args) =>
            {
                while (settings.DropDownItems.Count > 0)
                {
                    var item = settings.DropDownItems[0];
                    settings.DropDownItems.RemoveAt(0);
                    item.Dispose();
                }
                list.FillSettings(settings.DropDownItems);
            };
            menu.Items.Add(settings);
            menu.Items.Add("检查更新…", null, (sender, args) => session.CheckForUpdates());
            menu.Items.Add("赞赏作者…", null, (sender, args) => ShowReward(session, list));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出个人便签", null, (sender, args) => session.Quit());

            icon = new NotifyIcon { Icon = NoteStyle.AppIcon, Text = Program.Name + " " + AppUpdates.DisplayVersion,
                ContextMenuStrip = menu, Visible = true };
            icon.MouseDoubleClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left) session.ShowList();
            };
        }

        private void AddAction(string label, string shortcut, Action action)
        {
            var item = new ToolStripMenuItem(label) { ShortcutKeyDisplayString = shortcut };
            item.Click += (sender, args) => action();
            menu.Items.Add(item);
        }

        public void Notify(string message)
        {
            icon.ShowBalloonTip(3500, Program.Name, message, ToolTipIcon.Info);
        }

        private static void ShowShortcuts(NoteSession session, Form owner)
        {
            session.ShowList();
            MessageBox.Show(owner,
                "Ctrl+N　新建便笺\nCtrl+F　搜索\nCtrl+H　打开列表\nCtrl+W　关闭当前窗口\nCtrl+D　删除便笺\n" +
                "Ctrl+Tab / Ctrl+Shift+Tab　切换窗口\nCtrl+Shift+P　置顶\n\n" +
                "Ctrl+B / I / U / T　加粗、斜体、下划线、删除线\nCtrl+Shift+L　项目符号\n" +
                "Ctrl+Shift+J　添加待办\nCtrl+Shift+K　切换待办状态",
                "个人便签快捷键", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ShowReward(NoteSession session, Form owner)
        {
            session.ShowList();
            try
            {
                using (var picture = ReadReward())
                {
                    if (picture == null)
                    {
                        MessageBox.Show(owner, "作者暂未提供赞赏二维码。", "赞赏作者");
                        return;
                    }
                    using (var dialog = new Form { Text = "赞赏作者", ClientSize = new Size(345, 375),
                        StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                        ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false, Font = owner.Font,
                        BackColor = Color.White, AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96) })
                    {
                        var code = new PictureBox { Dock = DockStyle.Fill, Image = picture,
                            SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "作者收款二维码" };
                        var label = new Label { Text = "谢谢支持 · 扫码赞赏作者", Dock = DockStyle.Bottom,
                            Height = 40, TextAlign = ContentAlignment.MiddleCenter };
                        dialog.Padding = new Padding(14);
                        dialog.Controls.Add(code);
                        dialog.Controls.Add(label);
                        dialog.ShowDialog(owner);
                    }
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(owner, "无法打开作者二维码。\n\n" + error.Message, "赞赏作者");
            }
        }

        private static Bitmap ReadReward()
        {
            string sidecar = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RewardQR.png");
            using (var stream = File.Exists(sidecar) ? File.OpenRead(sidecar) :
                Assembly.GetExecutingAssembly().GetManifestResourceStream("PersonalStickyNote.Reward"))
            {
                if (stream == null) return null;
                using (var image = Image.FromStream(stream)) return new Bitmap(image);
            }
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
        }
    }
}
