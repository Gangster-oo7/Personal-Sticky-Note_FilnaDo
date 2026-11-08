using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal abstract class CaptionWindow : Form
    {
        private readonly Panel caption = new Panel { Dock = DockStyle.Top, Height = 33 };
        private readonly Label captionText = new Label { Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0),
            AutoEllipsis = true };
        private readonly PictureBox icon = new PictureBox { Dock = DockStyle.Left, Width = 27,
            SizeMode = PictureBoxSizeMode.CenterImage, AccessibleName = Program.Name };
        private readonly Button pin = NoteStyle.Button("置顶", 64);
        private readonly Button minimize = NoteStyle.Button("−", 33, "最小化");
        private readonly Button maximize = NoteStyle.Button("□", 33, "最大化");
        private readonly Button close = NoteStyle.Button("×", 33, "关闭");
        private readonly ToolTip tips = new ToolTip();
        private Color captionInk;
        private Rectangle normalBounds;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr handle);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

        protected CaptionWindow()
        {
            Padding = new Padding(5);
            caption.Controls.Add(captionText);
            using (var small = new Icon(NoteStyle.AppIcon, new Size(17, 17))) icon.Image = small.ToBitmap();
            caption.Controls.Add(icon);
            foreach (var button in new[] { pin, minimize, maximize, close })
            {
                button.Dock = DockStyle.Right;
                caption.Controls.Add(button);
            }
            pin.Click += (sender, args) => TogglePin();
            minimize.Click += (sender, args) =>
            {
                if (WindowState == FormWindowState.Normal) normalBounds = Bounds;
                WindowState = FormWindowState.Minimized;
            };
            maximize.Click += (sender, args) => ToggleMaximize();
            close.Click += (sender, args) => Close();
            tips.SetToolTip(minimize, "最小化");
            tips.SetToolTip(close, "关闭");
            captionText.MouseDown += StartCaptionDrag;
            icon.MouseDown += StartCaptionDrag;
            TextChanged += (sender, args) => captionText.Text = Text;
            SizeChanged += (sender, args) => RefreshMaximize();
            RefreshMaximize();
        }

        protected void InstallCaption() { Controls.Add(caption); }

        private void StartCaptionDrag(object sender, MouseEventArgs args)
        {
            if (args.Button != MouseButtons.Left) return;
            if (args.Clicks == 2) { ToggleMaximize(); return; }
            var screen = ((Control)sender).PointToScreen(args.Location);
            int point = (screen.X & 0xffff) | (screen.Y << 16);
            ReleaseCapture();
            SendMessage(Handle, 0x00a1, new IntPtr(2), new IntPtr(point)); // WM_NCLBUTTONDOWN, HTCAPTION
        }

        protected void StyleCaption(Color surface, Color ink)
        {
            caption.BackColor = surface;
            captionText.ForeColor = captionInk = ink;
            foreach (var button in new[] { minimize, maximize, close })
            {
                button.BackColor = surface;
                button.ForeColor = ink;
            }
            close.FlatAppearance.MouseOverBackColor = NoteStyle.ColorFrom(0xc42b36);
            RefreshPin();
        }

        protected void TogglePin()
        {
            TopMost = !TopMost;
            RefreshPin();
            PinChanged(TopMost);
        }

        protected abstract void PinChanged(bool pinned);

        private void RefreshPin()
        {
            pin.Text = TopMost ? "已置顶" : "置顶";
            pin.AccessibleName = TopMost ? "取消置顶" : "窗口置顶";
            pin.BackColor = TopMost ? NoteStyle.ColorFrom(0x3e3420) : caption.BackColor;
            pin.ForeColor = TopMost ? Color.White : captionInk;
            tips.SetToolTip(pin, pin.AccessibleName + "（Ctrl+Shift+P）");
        }

        private void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized) RestoreNormal();
            else
            {
                if (WindowState == FormWindowState.Normal) normalBounds = Bounds;
                WindowState = FormWindowState.Maximized;
            }
        }

        public void RestoreNormal()
        {
            WindowState = FormWindowState.Normal;
            if (!normalBounds.IsEmpty) Bounds = normalBounds;
        }

        private void RefreshMaximize()
        {
            bool maximized = WindowState == FormWindowState.Maximized;
            maximize.Text = maximized ? "❐" : "□";
            maximize.AccessibleName = maximized ? "还原窗口" : "最大化";
            tips.SetToolTip(maximize, maximize.AccessibleName);
        }

        protected override void WndProc(ref Message message)
        {
            bool restore = message.Msg == 0x0112 && // WM_SYSCOMMAND
                (message.WParam.ToInt64() & 0xfff0) == 0xf120 && WindowState != FormWindowState.Normal;
            if (message.Msg == 0x0112 && WindowState == FormWindowState.Normal)
            {
                long command = message.WParam.ToInt64() & 0xfff0;
                if (command == 0xf020 || command == 0xf030) normalBounds = Bounds; // SC_MINIMIZE / SC_MAXIMIZE
            }
            if (message.Msg == 0x0083 && message.WParam != IntPtr.Zero) // WM_NCCALCSIZE
            {
                if (IsZoomed(message.HWnd))
                {
                    var area = Screen.FromHandle(message.HWnd).WorkingArea;
                    Marshal.WriteInt32(message.LParam, 0, area.Left);
                    Marshal.WriteInt32(message.LParam, 4, area.Top);
                    Marshal.WriteInt32(message.LParam, 8, area.Right);
                    Marshal.WriteInt32(message.LParam, 12, area.Bottom);
                }
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
            if (restore && WindowState == FormWindowState.Normal && !normalBounds.IsEmpty)
                Bounds = normalBounds;
            if (message.Msg != 0x0084) return; // WM_NCHITTEST
            if (WindowState != FormWindowState.Normal)
            {
                message.Result = new IntPtr(1);
                return;
            }
            long coords = message.LParam.ToInt64();
            var point = PointToClient(new Point((short)coords, (short)(coords >> 16)));
            bool left = point.X < Padding.Left, right = point.X >= ClientSize.Width - Padding.Right;
            bool top = point.Y < Padding.Top, bottom = point.Y >= ClientSize.Height - Padding.Bottom;
            if (top) message.Result = new IntPtr(left ? 13 : right ? 14 : 12);
            else if (bottom) message.Result = new IntPtr(left ? 16 : right ? 17 : 15);
            else if (left || right) message.Result = new IntPtr(left ? 10 : 11);
            else message.Result = new IntPtr(1); // HTCLIENT
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tips.Dispose();
                if (icon.Image != null) icon.Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
