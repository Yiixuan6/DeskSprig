using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

class BuildBrand
{
    [STAThread] static void Main(string[] args)
    {
        string root = args[0]; Directory.CreateDirectory(root);
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (int size in sizes)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
                var rear = Geometry.Parse("M 44,77 Q 43,63 57,56 L 120,22 Q 132,16 135,31 L 153,157 Q 155,170 142,176 L 65,205 Q 48,212 47,194 Z");
                var front = Geometry.Parse("M 103,115 Q 102,101 116,95 L 207,61 Q 222,56 222,73 L 222,199 Q 222,214 208,218 L 121,241 Q 105,246 104,229 Z");
                Brush back = new LinearGradientBrush(Color.FromRgb(150,177,142), Color.FromRgb(70,123,96), new Point(0,0),new Point(1,1));
                Brush face = new LinearGradientBrush(Color.FromRgb(139,176,137), Color.FromRgb(65,113,88), new Point(0,0),new Point(1,1));
                dc.DrawGeometry(back, null, rear); dc.DrawGeometry(face, new Pen(new SolidColorBrush(Color.FromArgb(80,255,255,255)), size <= 24 ? 2 : 1), front);
                if (size >= 32) dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(85,255,255,255)), 1.5), Geometry.Parse("M 107,210 L 107,118 Q 106,105 120,100 L 203,69"));
                dc.Pop();
            }
            var bitmap = new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream()) { encoder.Save(stream); images.Add(stream.ToArray()); }
            if (size == 256) File.WriteAllBytes(Path.Combine(root,"app.png"), images[images.Count-1]);
            if (size == 16 || size == 32) File.WriteAllBytes(Path.Combine(root,"app-"+size+".png"), images[images.Count-1]);
        }
        using (var writer = new BinaryWriter(File.Create(Path.Combine(root,"app.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i=0;i<sizes.Length;i++) { writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length; }
            foreach (var image in images) writer.Write(image);
        }
    }
}
