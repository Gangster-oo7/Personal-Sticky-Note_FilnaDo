using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal enum Glyph { Pin, Magnet, Add, More, Todo, Image, Undo, Redo, Minimize, Maximize, Restore, Close }

    internal static class InterfaceStyle
    {
        public static Color Accent(Color surface)
        {
            return NoteStyle.Contrast(surface).ToArgb() == Color.WhiteSmoke.ToArgb()
                ? NoteStyle.ColorFrom(0x76d6ad) : NoteStyle.ColorFrom(0x258263);
        }

        public static Color Blend(Color a, Color b, float proportion)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * proportion),
                (int)(a.G + (b.G - a.G) * proportion), (int)(a.B + (b.B - a.B) * proportion));
        }

        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int size = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2);
            if (size < 2) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.Left, bounds.Top, size, size, 180, 90);
            path.AddArc(bounds.Right - size, bounds.Top, size, size, 270, 90);
            path.AddArc(bounds.Right - size, bounds.Bottom - size, size, size, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - size, size, size, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Bitmap Icon(Glyph glyph, Color ink)
        {
            var image = new Bitmap(18, 18);
            using (var graphics = Graphics.FromImage(image)) Draw(graphics, glyph, ink, new Rectangle(0, 0, 18, 18));
            return image;
        }

        public static void Draw(Graphics graphics, Glyph glyph, Color ink, Rectangle bounds)
        {
            var state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            graphics.ScaleTransform(bounds.Width / 18f, bounds.Height / 18f);
            using (var pen = new Pen(ink, 1.45f) { StartCap = LineCap.Round, EndCap = LineCap.Round,
                LineJoin = LineJoin.Round })
            {
                switch (glyph)
                {
                    case Glyph.Pin:
                        graphics.RotateTransform(34);
                        graphics.DrawLines(pen, new[] { new Point(-4,-6), new Point(4,-6), new Point(3,-4),
                            new Point(3,0), new Point(5,3), new Point(-5,3), new Point(-3,0), new Point(-3,-4), new Point(-4,-6) });
                        graphics.DrawLine(pen, 0, 3, 0, 8);
                        break;
                    case Glyph.Magnet:
                        graphics.DrawArc(pen, -6, -5, 12, 12, 0, 180);
                        graphics.DrawArc(pen, -3, -2, 6, 6, 0, 180);
                        graphics.DrawLines(pen, new[] { new Point(-6,1), new Point(-6,-6), new Point(-3,-6), new Point(-3,1) });
                        graphics.DrawLines(pen, new[] { new Point(3,1), new Point(3,-6), new Point(6,-6), new Point(6,1) });
                        graphics.DrawLine(pen, -6, -2, -3, -2); graphics.DrawLine(pen, 3, -2, 6, -2);
                        break;
                    case Glyph.Add:
                        graphics.DrawEllipse(pen, -7, -7, 14, 14);
                        graphics.DrawLine(pen, -3, 0, 3, 0); graphics.DrawLine(pen, 0, -3, 0, 3);
                        break;
                    case Glyph.More:
                        using (var fill = new SolidBrush(ink))
                            for (int x = -5; x <= 5; x += 5) graphics.FillEllipse(fill, x - 1, -1, 2, 2);
                        break;
                    case Glyph.Todo:
                        graphics.DrawRectangle(pen, -6, -6, 12, 12);
                        graphics.DrawLines(pen, new[] { new Point(-3,0), new Point(-1,2), new Point(3,-3) });
                        break;
                    case Glyph.Image:
                        graphics.DrawRectangle(pen, -7, -5, 14, 11);
                        graphics.DrawEllipse(pen, 1, -3, 2, 2);
                        graphics.DrawLines(pen, new[] { new Point(-6,4), new Point(-2,0), new Point(1,3), new Point(3,1), new Point(6,4) });
                        break;
                    case Glyph.Redo:
                        graphics.ScaleTransform(-1, 1);
                        goto case Glyph.Undo;
                    case Glyph.Undo:
                        graphics.DrawBezier(pen, -6, -2, 4, -5, 10, 5, 0, 5);
                        graphics.DrawLines(pen, new[] { new Point(-2,-6), new Point(-6,-2), new Point(-2,2) });
                        break;
                    case Glyph.Minimize: graphics.DrawLine(pen, -5, 0, 5, 0); break;
                    case Glyph.Maximize: graphics.DrawRectangle(pen, -5, -5, 10, 10); break;
                    case Glyph.Restore:
                        graphics.DrawRectangle(pen, -6, -3, 9, 9);
                        graphics.DrawLines(pen, new[] { new Point(-3,-5), new Point(-3,-6), new Point(6,-6), new Point(6,3), new Point(5,3) });
                        break;
                    case Glyph.Close:
                        graphics.DrawLine(pen, -5, -5, 5, 5); graphics.DrawLine(pen, -5, 5, 5, -5);
                        break;
                }
            }
            graphics.Restore(state);
        }
    }

    internal sealed class SoftButton : Button
    {
        public Glyph? Symbol;
        public bool Selected;
        private bool hovered;
        private bool pressed;

        protected override void OnMouseEnter(EventArgs args) { base.OnMouseEnter(args); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs args) { base.OnMouseLeave(args); hovered = pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs args) { base.OnMouseDown(args); pressed = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs args) { base.OnMouseUp(args); pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs args)
        {
            var canvas = args.Graphics;
            canvas.Clear(Parent == null ? BackColor : Parent.BackColor);
            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = BackColor;
            if (hovered && !FlatAppearance.MouseOverBackColor.IsEmpty) fill = FlatAppearance.MouseOverBackColor;
            else if (hovered || pressed || Selected)
                fill = InterfaceStyle.Blend(fill, ForeColor, pressed || Selected ? 0.16f : 0.08f);
            var area = Rectangle.Inflate(ClientRectangle, -1, -1);
            using (var path = InterfaceStyle.Rounded(area, Math.Max(4, Height / 5)))
            using (var brush = new SolidBrush(fill))
            {
                canvas.FillPath(brush, path);
                if (Focused && ShowFocusCues)
                {
                    using (var outline = new Pen(InterfaceStyle.Blend(fill, ForeColor, 0.45f))) canvas.DrawPath(outline, path);
                }
            }
            var ink = Enabled ? NoteStyle.Contrast(fill) : InterfaceStyle.Blend(fill, ForeColor, 0.46f);
            if (Symbol.HasValue)
            {
                int size = (int)Math.Round(17 * canvas.DpiX / 96f);
                InterfaceStyle.Draw(canvas, Symbol.Value, ink,
                    new Rectangle((Width - size) / 2, (Height - size) / 2, size, size));
            }
            else TextRenderer.DrawText(canvas, Text, Font, area, ink, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class EditorStripRenderer : ToolStripProfessionalRenderer
    {
        public EditorStripRenderer() { RoundedEdges = false; }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs args)
        {
            var area = args.ToolStrip.ClientRectangle;
            using (var pen = new Pen(InterfaceStyle.Blend(args.BackColor, NoteStyle.Contrast(args.BackColor), 0.10f)))
                args.Graphics.DrawLine(pen, area.Left, area.Bottom - 1, area.Right, area.Bottom - 1);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs args)
        {
            var button = (ToolStripButton)args.Item;
            if (!button.Selected && !button.Checked && !button.Pressed) return;
            var background = args.ToolStrip.BackColor;
            using (var brush = new SolidBrush(InterfaceStyle.Blend(background, NoteStyle.Contrast(background),
                button.Checked || button.Pressed ? 0.16f : 0.08f)))
            using (var path = InterfaceStyle.Rounded(new Rectangle(1, 2, button.Width - 2, button.Height - 4), 5))
            {
                args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                args.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs args)
        {
            using (var brush = new SolidBrush(args.ToolStrip.BackColor))
                args.Graphics.FillRectangle(brush, args.AffectedBounds);
        }

        protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs args)
        {
            var bounds = new Rectangle(1, 1, args.Item.Width - 2, args.Item.Height - 2);
            var background = args.ToolStrip.BackColor;
            var ink = NoteStyle.Contrast(background);
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = InterfaceStyle.Rounded(bounds, 5))
            using (var fill = new SolidBrush(InterfaceStyle.Blend(background, ink, args.Item.Selected ? 0.13f : 0.055f)))
            using (var pen = new Pen(ink, 1.25f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                args.Graphics.FillPath(fill, path);
                float x = args.Item.Width / 2f, y = args.Item.Height / 2f;
                args.Graphics.DrawLines(pen, new[] { new PointF(x - 3, y - 1.5f), new PointF(x, y + 1.5f),
                    new PointF(x + 3, y - 1.5f) });
            }
        }
    }
}
