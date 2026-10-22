using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopWidgets
{
    public sealed class CountdownRing : FrameworkElement
    {
        public double Fraction = 1;
        protected override void OnRender(DrawingContext dc)
        {
            double side = Math.Min(ActualWidth, ActualHeight), radius = Math.Max(0, side / 2 - 6);
            if (radius <= 0) return;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var background = TryFindResource("Line") as Brush ?? Brushes.Gray;
            var foreground = TryFindResource("Selected") as Brush ?? Brushes.Green;
            var track = new Pen(background, 7); track.StartLineCap = track.EndLineCap = PenLineCap.Round;
            dc.DrawEllipse(null, track, center, radius, radius);
            double progress = Math.Max(0, Math.Min(1, Fraction)); if (progress <= 0) return;
            var pen = new Pen(foreground, 7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (progress >= .99999) { dc.DrawEllipse(null, pen, center, radius, radius); return; }
            double angle = progress * 2 * Math.PI;
            var path = new StreamGeometry(); using (var ctx = path.Open()) { ctx.BeginFigure(new Point(center.X, center.Y - radius), false, false); ctx.ArcTo(new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius), new Size(radius, radius), 0, progress > .5, SweepDirection.Clockwise, true, false); }
            path.Freeze(); dc.DrawGeometry(null, pen, path);
        }
    }
    public sealed class Sparkline : FrameworkElement
    {
        readonly Queue<double> samples = new Queue<double>();
        public int Count { get { return samples.Count; } }
        public void Add(double value) { samples.Enqueue(Math.Max(0, value)); while (samples.Count > 30) samples.Dequeue(); InvalidateVisual(); }
        public void Clear() { samples.Clear(); InvalidateVisual(); }
        protected override void OnRender(DrawingContext dc)
        {
            if (samples.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;
            double[] values = samples.ToArray(); double maximum = 1; foreach (double v in values) maximum = Math.Max(maximum, v);
            var shape = new StreamGeometry(); using (var ctx = shape.Open()) { for (int i = 0; i < values.Length; i++) { var point = new Point(i * ActualWidth / Math.Max(1, values.Length - 1), ActualHeight - 2 - values[i] / maximum * Math.Max(0, ActualHeight - 4)); if (i == 0) ctx.BeginFigure(point, false, false); else ctx.LineTo(point, true, false); } }
            shape.Freeze(); dc.DrawGeometry(null, new Pen(TryFindResource("Selected") as Brush ?? Brushes.Green, 1.7), shape);
        }
    }
}
