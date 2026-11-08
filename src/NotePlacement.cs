using System;
using System.Collections.Generic;
using System.Drawing;

namespace PersonalStickyNote
{
    internal static class NotePlacement
    {
        public static Rectangle InitialBounds(Rectangle desktop, Size preferred, Size minimum)
        {
            var size = new Size(Math.Min(preferred.Width, desktop.Width * 42 / 100),
                Math.Min(preferred.Height, desktop.Height * 70 / 100));
            var bounds = Fit(new Rectangle(desktop.Location, size), desktop, minimum);
            bounds.Offset((desktop.Width - bounds.Width) / 2, (desktop.Height - bounds.Height) / 2);
            return bounds;
        }

        public static Rectangle Fit(Rectangle bounds, Rectangle desktop, Size minimum)
        {
            bounds.Width = Math.Min(desktop.Width, Math.Max(bounds.Width, minimum.Width));
            bounds.Height = Math.Min(desktop.Height, Math.Max(bounds.Height, minimum.Height));
            bounds.X = Math.Max(desktop.Left, Math.Min(bounds.X, desktop.Right - bounds.Width));
            bounds.Y = Math.Max(desktop.Top, Math.Min(bounds.Y, desktop.Bottom - bounds.Height));
            return bounds;
        }

        public static Point Snap(Rectangle note, Rectangle desktop, IEnumerable<Rectangle> neighbors, int range, int gap)
        {
            int x = note.Left, y = note.Top;
            int left = Math.Abs(note.Left - desktop.Left), right = Math.Abs(note.Right - desktop.Right);
            int top = Math.Abs(note.Top - desktop.Top), bottom = Math.Abs(note.Bottom - desktop.Bottom);
            bool edgeX = Math.Min(left, right) <= range;
            bool edgeY = Math.Min(top, bottom) <= range;
            if (edgeX) x = left <= right ? desktop.Left : desktop.Right - note.Width;
            if (edgeY) y = top <= bottom ? desktop.Top : desktop.Bottom - note.Height;
            var best = new Point(x, y);
            if (edgeX && edgeY) return best;

            // A nearby note aligns both axes. A lone screen edge only aligns one.
            int distance = edgeX || edgeY ? range * range + 1 : int.MaxValue;
            foreach (var other in neighbors)
            {
                if (!desktop.IntersectsWith(other)) continue;
                Test(new Point(other.Right + gap, other.Top), note, desktop, range, ref best, ref distance);
                Test(new Point(other.Left - gap - note.Width, other.Top), note, desktop, range, ref best, ref distance);
                Test(new Point(other.Left, other.Bottom + gap), note, desktop, range, ref best, ref distance);
                Test(new Point(other.Left, other.Top - gap - note.Height), note, desktop, range, ref best, ref distance);
            }
            return best;
        }

        private static void Test(Point target, Rectangle note, Rectangle desktop, int range, ref Point best, ref int distance)
        {
            int x = Math.Abs(target.X - note.Left), y = Math.Abs(target.Y - note.Top);
            if (x > range || y > range || !desktop.Contains(new Rectangle(target, note.Size))) return;
            int score = x * x + y * y;
            if (score >= distance) return;
            best = target;
            distance = score;
        }
    }
}
