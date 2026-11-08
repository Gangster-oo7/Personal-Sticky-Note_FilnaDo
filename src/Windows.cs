using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class Paper
    {
        public readonly string Id, Label;
        public readonly Color Light, Dark, Accent;

        public Paper(string id, string label, int light, int dark, int accent)
        {
            Id = id;
            Label = label;
            Light = NoteStyle.ColorFrom(light);
            Dark = NoteStyle.ColorFrom(dark);
            Accent = NoteStyle.ColorFrom(accent);
        }
    }

    internal static class NoteStyle
    {
        public static readonly Icon AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        public static readonly Paper[] Papers =
        {
            new Paper("yellow", "黄色", 0xfff6cc, 0x38301f, 0xf5d76e),
            new Paper("green", "绿色", 0xe4f7da, 0x263725, 0x9cdd85),
            new Paper("pink", "粉色", 0xffe5ef, 0x402930, 0xf4a5c6),
            new Paper("purple", "紫色", 0xf1e7ff, 0x342942, 0xcbb2f1),
            new Paper("blue", "蓝色", 0xe3f4ff, 0x233543, 0x9ad1f6),
            new Paper("gray", "灰色", 0xf0f0f0, 0x343434, 0xc9c9c9),
            new Paper("charcoal", "炭灰", 0xe5e7ea, 0x26272b, 0xaaaeb8)
        };

        public static Color ColorFrom(int rgb)
        {
            return Color.FromArgb(unchecked((int)0xff000000) | rgb);
        }

        public static Paper GetPaper(string id)
        {
            return Papers.FirstOrDefault(p => p.Id == id) ?? Papers[0];
        }

        public static Color Text(bool dark) { return dark ? Color.WhiteSmoke : ColorFrom(0x26231e); }
        public static Color Surface(bool dark) { return ColorFrom(dark ? 0x232323 : 0xfbfaf7); }
        public static Color Contrast(Color background)
        {
            double luminance = 0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) +
                0.0722 * Linear(background.B);
            return Text(luminance < 0.23);
        }

        private static double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        public static Button Button(string text, int width, string description = null)
        {
            var button = new Button
            {
                Text = text, Width = width, Height = 30, FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false, Margin = new Padding(0, 0, 4, 0),
                AccessibleName = description ?? text, Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        public static void Restore(Form form, WindowPosition position)
        {
            if (position.Width <= 0 || position.Height <= 0) return;
            var bounds = new Rectangle(position.X, position.Y, position.Width, position.Height);
            var area = Screen.FromRectangle(bounds).WorkingArea;
            bounds.Width = Math.Min(area.Width, Math.Max(bounds.Width, form.MinimumSize.Width));
            bounds.Height = Math.Min(area.Height, Math.Max(bounds.Height, form.MinimumSize.Height));
            bounds.X = Math.Max(area.Left, Math.Min(bounds.X, area.Right - bounds.Width));
            bounds.Y = Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - bounds.Height));
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = bounds;
        }

        public static void Remember(Form form, WindowPosition position, NoteSession session)
        {
            if (!form.Visible || form.WindowState != FormWindowState.Normal) return;
            position.X = form.Left;
            position.Y = form.Top;
            position.Width = form.Width;
            position.Height = form.Height;
            session.Changed();
        }
    }

}
