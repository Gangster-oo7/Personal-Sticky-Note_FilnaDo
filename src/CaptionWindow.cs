using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal abstract class CaptionWindow : Form
    {
        private readonly Panel caption = new Panel { Dock = DockStyle.Top, Height = 37 };
        private readonly Label captionText = new Label { Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0),
            AutoEllipsis = true };
        private readonly PictureBox icon = new PictureBox { Dock = DockStyle.Left, Width = 30,
            SizeMode = PictureBoxSizeMode.CenterImage, AccessibleName = Program.Name };
        private readonly SoftButton pin = (SoftButton)NoteStyle.Button("", 35, "置顶");
        private readonly SoftButton minimize = (SoftButton)NoteStyle.Button("", 34, "最小化");
        private readonly SoftButton maximize = (SoftButton)NoteStyle.Button("", 34, "最大化");
        private readonly SoftButton close = (SoftButton)NoteStyle.Button("", 34, "关闭");
        private readonly ToolTip tips = new ToolTip();
        private Color captionInk;
        private Rectangle normalBounds;
        private bool moved;
        private ContextMenuStrip activeMenu;
        protected string PinShortcut { get; set; }
        protected bool InSizeMove { get; private set; }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr handle);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.Style &= ~0x00c00000; // WS_CAPTION
                parameters.ExStyle &= ~0x00000100; // WS_EX_WINDOWEDGE
                return parameters;
            }
        }

        protected CaptionWindow()
        {
            Padding = new Padding(5);
            pin.Symbol = Glyph.Pin;
            minimize.Symbol = Glyph.Minimize;
            close.Symbol = Glyph.Close;
            caption.Controls.Add(captionText);
            using (var small = new Icon(NoteStyle.AppIcon, new Size(16, 16))) icon.Image = small.ToBitmap();
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

        protected void InstallCaption()
        {
            Controls.Add(caption);
            SizeChanged += (sender, args) => UpdateCorners();
            UpdateCorners();
        }

        private void UpdateCorners()
        {
            if (WindowState != FormWindowState.Normal || Width < 20 || Height < 20) { Region = null; return; }
            using (var path = InterfaceStyle.Rounded(new Rectangle(Point.Empty, Size), Math.Max(10, Padding.Left * 2)))
                Region = new Region(path);
        }
        protected virtual void OnMoveCompleted() { }

        protected void PresentMenu(ContextMenuStrip menu, Control anchor, Point point)
        {
            if (activeMenu != null) activeMenu.Close();
            activeMenu = menu;
            menu.Closed += MenuClosed;
            menu.Show(anchor, point);
        }

        private void MenuClosed(object sender, ToolStripDropDownClosedEventArgs args)
        {
            var menu = (ContextMenuStrip)sender;
            menu.Closed -= MenuClosed;
            if (activeMenu == menu) activeMenu = null;
            if (IsHandleCreated && !IsDisposed && !Disposing) BeginInvoke(new Action(menu.Dispose));
            else menu.Dispose();
        }

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
            caption.BackColor = captionText.BackColor = icon.BackColor = surface;
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

        protected void RefreshPin()
        {
            pin.AccessibleName = TopMost ? "取消置顶" : "窗口置顶";
            pin.Selected = TopMost;
            pin.BackColor = TopMost ? InterfaceStyle.Blend(caption.BackColor, NoteStyle.ColorFrom(0x399c78), 0.19f)
                : caption.BackColor;
            pin.ForeColor = captionInk;
            pin.Invalidate();
            tips.SetToolTip(pin, pin.AccessibleName + "（" + PinShortcut + "）");
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
            maximize.Symbol = maximized ? Glyph.Restore : Glyph.Maximize;
            maximize.Invalidate();
            maximize.AccessibleName = maximized ? "还原窗口" : "最大化";
            tips.SetToolTip(maximize, maximize.AccessibleName);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0231) { InSizeMove = true; moved = false; } // WM_ENTERSIZEMOVE
            if (message.Msg == 0x0216 && InSizeMove) moved = true; // WM_MOVING
            bool restore = message.Msg == 0x0112 && // WM_SYSCOMMAND
                (message.WParam.ToInt64() & 0xfff0) == 0xf120 && WindowState != FormWindowState.Normal;
            if (message.Msg == 0x0112 && WindowState == FormWindowState.Normal)
            {
                long command = message.WParam.ToInt64() & 0xfff0;
                if (command == 0xf020 || command == 0xf030) normalBounds = Bounds; // SC_MINIMIZE / SC_MAXIMIZE
            }
            if (message.Msg == 0x0083) // WM_NCCALCSIZE, both forms start with the client rectangle.
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
            if (message.Msg == 0x0085) // WM_NCPAINT
            {
                message.Result = IntPtr.Zero;
                return;
            }
            if (message.Msg == 0x0086) message.LParam = new IntPtr(-1); // WM_NCACTIVATE without native repaint
            base.WndProc(ref message);
            if (message.Msg == 0x0232) // WM_EXITSIZEMOVE
            {
                InSizeMove = false;
                if (moved) OnMoveCompleted();
            }
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
                if (activeMenu != null)
                {
                    activeMenu.Closed -= MenuClosed;
                    activeMenu.Dispose();
                    activeMenu = null;
                }
                tips.Dispose();
                if (icon.Image != null) icon.Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
