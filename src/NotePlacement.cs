using System;
using System.Collections.Generic;
using System.Drawing;

namespace PersonalStickyNote
{
    internal static class NotePlacement
    {
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
