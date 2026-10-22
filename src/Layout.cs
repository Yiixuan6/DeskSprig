using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DesktopWidgets
{
    public static class Layout
    {
        public const double SnapDistance = 6;
        public static Point Snap(Rect moving, IEnumerable<Rect> targets, bool bypass)
        { return Snap(moving,targets,bypass,null); }
        public static Point Snap(Rect moving, IEnumerable<Rect> targets, bool bypass,ISet<Rect> ignored)
        {
            if (bypass) return moving.TopLeft;
            return SnapCore(moving,targets.ToList(),false,ignored);
        }
        static Point SnapCore(Rect moving,List<Rect> others,bool bypass,ISet<Rect> ignored)
        {
            if (bypass) return moving.TopLeft;
            Point best = moving.TopLeft; double distance = Double.MaxValue;
            foreach (var r in others)
            {
                if(ignored!=null && ignored.Contains(r)) continue;
                foreach (double x in new[] { r.Left - moving.Width, r.Right })
                {
                    if (Math.Abs(x - moving.Left) > SnapDistance || moving.Bottom < r.Top - SnapDistance || moving.Top > r.Bottom + SnapDistance) continue;
                    double y = moving.Top;
                    if (Math.Abs(y - r.Top) <= SnapDistance) y = r.Top;
                    else if (Math.Abs(moving.Bottom - r.Bottom) <= SnapDistance) y = r.Bottom - moving.Height;
                    Consider(new Rect(x,y,moving.Width,moving.Height), moving, others, ref best, ref distance);
                }
                foreach (double y in new[] { r.Top - moving.Height, r.Bottom })
                {
                    if (Math.Abs(y - moving.Top) > SnapDistance || moving.Right < r.Left - SnapDistance || moving.Left > r.Right + SnapDistance) continue;
                    double x = moving.Left;
                    if (Math.Abs(x - r.Left) <= SnapDistance) x = r.Left;
                    else if (Math.Abs(moving.Right - r.Right) <= SnapDistance) x = r.Right - moving.Width;
                    Consider(new Rect(x,y,moving.Width,moving.Height), moving, others, ref best, ref distance);
                }
            }
            return best;
        }
        // Work-area edges are optional so component-only callers retain their behavior.
        public static Point Snap(Rect moving,IEnumerable<Rect> targets,bool bypass,ISet<Rect> ignored,Rect? work,int ignoredEdges)
        {
            var others=targets.ToList(); var point=SnapCore(moving,others,bypass,ignored);
            if(bypass || !work.HasValue) return point;
            Rect w=work.Value;
            if(w.IsEmpty || moving.Width>w.Width || moving.Height>w.Height) return point;
            double x=point.X,y=point.Y; bool edgeX=false,edgeY=false;
            if((ignoredEdges&1)==0 && Math.Abs(moving.Left-w.Left)<=SnapDistance) { x=w.Left; edgeX=true; }
            else if((ignoredEdges&2)==0 && Math.Abs(moving.Right-w.Right)<=SnapDistance) { x=w.Right-moving.Width; edgeX=true; }
            if((ignoredEdges&4)==0 && Math.Abs(moving.Top-w.Top)<=SnapDistance) { y=w.Top; edgeY=true; }
            else if((ignoredEdges&8)==0 && Math.Abs(moving.Bottom-w.Bottom)<=SnapDistance) { y=w.Bottom-moving.Height; edgeY=true; }
            if(!edgeX && !edgeY) return point;
            var candidate=new Rect(x,y,moving.Width,moving.Height);
            if(!Overlaps(candidate,others)) return candidate.TopLeft;
            // At an occupied corner, keep a usable single-axis snap if possible.
            if(edgeX && edgeY) {
                candidate=new Rect(x,point.Y,moving.Width,moving.Height); if(!Overlaps(candidate,others)) return candidate.TopLeft;
                candidate=new Rect(point.X,y,moving.Width,moving.Height); if(!Overlaps(candidate,others)) return candidate.TopLeft;
            }
            return point;
        }
        static bool Overlaps(Rect candidate,List<Rect> others)
        { return others.Any(r=>candidate.Left<r.Right && candidate.Right>r.Left && candidate.Top<r.Bottom && candidate.Bottom>r.Top); }
        static void Consider(Rect candidate, Rect original, List<Rect> others, ref Point best, ref double distance)
        {
            if (Overlaps(candidate,others)) return;
            double d = (candidate.Left-original.Left)*(candidate.Left-original.Left)+(candidate.Top-original.Top)*(candidate.Top-original.Top);
            if (d < distance) { best = candidate.TopLeft; distance = d; }
        }
        public static double MinimumHeight(string id)
        { switch (WidgetApp.KindOf(id)) { case "clock": return 150; case "countdown": return 280; case "status": return 240; case "shortcuts": return 170; default: return 220; } }
        public static double DefaultHeight(string id)
        { switch (WidgetApp.KindOf(id)) { case "clock": return 185; case "countdown": return 330; case "status": return 265; case "shortcuts": return 220; default: return 300; } }
        public static Point FreeNear(Rect work, WindowState anchor, IEnumerable<WindowState> existing, double width, double height)
        {
            var occupied = existing.Where(w => w.Visible).Select(w => new Rect(w.Left - 6, w.Top - 6, w.Width + 12, w.Height + 12)).ToList();
            double right = work.Right - width, bottom = work.Bottom - height;
            Point best = new Point(Math.Max(work.Left, Math.Min(anchor.Left + anchor.Width + 16, right)), Math.Max(work.Top, Math.Min(anchor.Top, bottom)));
            double distance = Double.MaxValue;
            for (double y = work.Top + 12; y <= bottom; y += 24)
                for (double x = work.Left + 12; x <= right; x += 24)
                {
                    var rect = new Rect(x, y, width, height); if (occupied.Any(r => r.IntersectsWith(rect))) continue;
                    double d = (x - anchor.Left) * (x - anchor.Left) + (y - anchor.Top) * (y - anchor.Top);
                    if (d < distance) { distance = d; best = new Point(x, y); }
                }
            return best;
        }
    }
    // A fresh drag of an already-adjacent card must move immediately. Re-arm that
    // target only after leaving its zone; all calculations take raw pointer motion.
    public sealed class SnapGesture
    {
        readonly HashSet<Rect> initialContacts;
        Rect? contactWork;
        int ignoredEdges;
        public SnapGesture(Rect start,IEnumerable<Rect> targets) : this(start,targets,null) { }
        public SnapGesture(Rect start,IEnumerable<Rect> targets,Rect? work)
        {
            initialContacts=new HashSet<Rect>(targets.Where(r=>Near(start,r,Layout.SnapDistance))); contactWork=work;
            if(work.HasValue) { var w=work.Value; if(Math.Abs(start.Left-w.Left)<=Layout.SnapDistance) ignoredEdges|=1; if(Math.Abs(start.Right-w.Right)<=Layout.SnapDistance) ignoredEdges|=2; if(Math.Abs(start.Top-w.Top)<=Layout.SnapDistance) ignoredEdges|=4; if(Math.Abs(start.Bottom-w.Bottom)<=Layout.SnapDistance) ignoredEdges|=8; }
        }
        static bool Near(Rect m,Rect r,double distance)
        {
            return ((Math.Abs(m.Right-r.Left)<=distance || Math.Abs(m.Left-r.Right)<=distance) && m.Bottom>=r.Top-distance && m.Top<=r.Bottom+distance)
                || ((Math.Abs(m.Bottom-r.Top)<=distance || Math.Abs(m.Top-r.Bottom)<=distance) && m.Right>=r.Left-distance && m.Left<=r.Right+distance);
        }
        public Point Apply(Rect raw,IEnumerable<Rect> targets,bool bypass)
        {
            return Apply(raw,targets,bypass,null);
        }
        public Point Apply(Rect raw,IEnumerable<Rect> targets,bool bypass,Rect? work)
        {
            initialContacts.RemoveWhere(r=>!Near(raw,r,Layout.SnapDistance+2));
            if(contactWork!=work) { contactWork=work; ignoredEdges=0; }
            if(work.HasValue) {
                var w=work.Value; double release=Layout.SnapDistance+2;
                if(Math.Abs(raw.Left-w.Left)>release) ignoredEdges&=~1;
                if(Math.Abs(raw.Right-w.Right)>release) ignoredEdges&=~2;
                if(Math.Abs(raw.Top-w.Top)>release) ignoredEdges&=~4;
                if(Math.Abs(raw.Bottom-w.Bottom)>release) ignoredEdges&=~8;
            }
            return Layout.Snap(raw,targets,bypass,initialContacts,work,ignoredEdges);
        }
    }
}
