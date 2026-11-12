using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace AssetGen;

internal static class Program
{
    private const int Width = 1920;
    private const int Height = 1080;

    private static string _output = string.Empty;

    private static void Main(string[] args)
    {
        _output = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XDLauncher", "Assets"));

        Directory.CreateDirectory(_output);
        Directory.CreateDirectory(Path.Combine(_output, "backgrounds"));

        Console.WriteLine("Generando fondos...");
        DrawForest(Path.Combine(_output, "backgrounds", "bg-bosque.png"));
        DrawAurora(Path.Combine(_output, "backgrounds", "bg-aurora.png"));
        DrawSunrise(Path.Combine(_output, "backgrounds", "bg-amanecer.png"));
        DrawCave(Path.Combine(_output, "backgrounds", "bg-cueva.png"));

        Console.WriteLine("Generando logotipo, icono y avatar...");
        using (var logo = DrawLogo(512))
            logo.Save(Path.Combine(_output, "logo.png"), ImageFormat.Png);

        using (var avatar = DrawAvatar(256))
            avatar.Save(Path.Combine(_output, "avatar-default.png"), ImageFormat.Png);

        WriteIco(Path.Combine(_output, "app.ico"), new[] { 16, 24, 32, 48, 64, 128, 256 });

        Console.WriteLine("Recursos gráficos listos en " + _output);
    }

    // ------------------------------------------------------------------ backgrounds

    private static void DrawForest(string path)
    {
        using var bmp = NewCanvas(
            Color.FromArgb(6, 24, 22),
            Color.FromArgb(9, 42, 34),
            Color.FromArgb(38, 92, 67),
            Color.FromArgb(10, 20, 15));
        using var g = Graphics.FromImage(bmp);
        Prepare(g);

        DrawStars(g, 190, new Rectangle(0, 0, Width, 760), Color.FromArgb(110, 190, 255, 210), 7);
        DrawMoon(g, new PointF(1510, 205), 96, Color.FromArgb(210, 202, 255, 218));
        DrawClouds(g, 90, 180, 620, Color.FromArgb(30, 150, 220, 190), 4, 9);
        DrawMountains(g, Color.FromArgb(17, 48, 44), Color.FromArgb(24, 66, 55), 690, 0.9);
        DrawMountains(g, Color.FromArgb(12, 36, 33), Color.FromArgb(18, 48, 40), 790, 1.25);
        DrawHills(g, Color.FromArgb(10, 29, 24), 880);

        // Layered blocky forest.
        var random = new Random(2107);
        for (var layer = 0; layer < 3; layer++)
        {
            var baseY = 810 + layer * 90;
            var count = 22 - layer * 5;
            for (var i = 0; i < count; i++)
            {
                var x = random.Next(-80, Width + 80);
                var scale = 0.55f + layer * 0.35f + (float)random.NextDouble() * 0.5f;
                DrawBlockTree(g, x, baseY + random.Next(-30, 50), scale, layer, random);
            }
        }

        // Ground.
        using (var ground = new LinearGradientBrush(new Rectangle(0, 890, Width, 200),
                   Color.FromArgb(250, 6, 20, 15), Color.FromArgb(255, 3, 10, 8), 90f))
            g.FillRectangle(ground, 0, 890, Width, 200);

        DrawGrassBand(g, 870, Color.FromArgb(255, 18, 62, 35), Color.FromArgb(255, 9, 33, 21), new Random(77));

        // Fireflies and foreground grass.
        DrawParticles(g, 150, new Rectangle(0, 430, Width, 600), Color.FromArgb(180, 130, 255, 150), 2.5f, 7);
        DrawForegroundGrass(g, new Random(2035));

        ApplyVignette(g);
        ApplyGrain(g, 8000, 26);
        bmp.Save(path, ImageFormat.Png);
    }

    private static void DrawAurora(string path)
    {
        using var bmp = NewCanvas(
            Color.FromArgb(4, 10, 30),
            Color.FromArgb(9, 24, 54),
            Color.FromArgb(22, 72, 84),
            Color.FromArgb(4, 8, 20));
        using var g = Graphics.FromImage(bmp);
        Prepare(g);

        DrawStars(g, 320, new Rectangle(0, 0, Width, 760), Color.FromArgb(150, 210, 235, 255), 8);
        DrawMoon(g, new PointF(1560, 190), 74, Color.FromArgb(200, 220, 245, 255));

        // Aurora ribbons.
        DrawAuroraRibbon(g, 120, 400, 900, 330, Color.FromArgb(90, 58, 255, 176));
        DrawAuroraRibbon(g, 260, 310, 1120, 420, Color.FromArgb(65, 70, 150, 255));
        DrawAuroraRibbon(g, -120, 480, 1200, 300, Color.FromArgb(55, 180, 90, 255));
        DrawAuroraRibbon(g, 700, 240, 1250, 460, Color.FromArgb(45, 80, 255, 210));

        DrawMountains(g, Color.FromArgb(13, 30, 60), Color.FromArgb(20, 45, 82), 700, 0.85);
        DrawMountains(g, Color.FromArgb(8, 20, 42), Color.FromArgb(14, 33, 60), 800, 1.2);
        DrawSnowCaps(g, 700, 0.85);
        DrawHills(g, Color.FromArgb(6, 16, 34), 900);

        var random = new Random(420);
        for (var layer = 0; layer < 2; layer++)
        {
            for (var i = 0; i < 18; i++)
            {
                DrawBlockTree(g, random.Next(-100, Width + 100), 880 + layer * 80, 0.55f + layer * 0.3f + (float)random.NextDouble() * 0.4f,
                    1, random);
            }
        }

        using (var ground = new LinearGradientBrush(new Rectangle(0, 900, Width, 180),
                   Color.FromArgb(255, 4, 13, 25), Color.FromArgb(255, 2, 7, 14), 90f))
            g.FillRectangle(ground, 0, 900, Width, 180);

        DrawGrassBand(g, 888, Color.FromArgb(255, 20, 68, 78), Color.FromArgb(255, 7, 28, 32), random);
        DrawParticles(g, 120, new Rectangle(0, 300, Width, 700), Color.FromArgb(170, 150, 230, 255), 2.2f, 13);
        DrawForegroundGrass(g, new Random(99));
        ApplyVignette(g);
        ApplyGrain(g, 6500, 24);
        bmp.Save(path, ImageFormat.Png);
    }

    private static void DrawSunrise(string path)
    {
        using var bmp = NewCanvas(
            Color.FromArgb(21, 25, 58),
            Color.FromArgb(79, 46, 84),
            Color.FromArgb(212, 111, 96),
            Color.FromArgb(255, 190, 112));
        using var g = Graphics.FromImage(bmp);
        Prepare(g);

        DrawStars(g, 90, new Rectangle(0, 0, Width, 430), Color.FromArgb(90, 255, 240, 210), 6);

        // Sun and glow.
        using (var glow = new GraphicsPath())
        {
            glow.AddEllipse(1190, 330, 520, 520);
            using var brush = new PathGradientBrush(glow)
            {
                CenterColor = Color.FromArgb(140, 255, 196, 120),
                SurroundColors = new[] { Color.FromArgb(0, 255, 170, 100) }
            };
            g.FillPath(brush, glow);
        }
        using (var sun = new SolidBrush(Color.FromArgb(245, 255, 224, 168)))
            g.FillEllipse(sun, 1370, 510, 160, 160);

        DrawClouds(g, 141, 210, 640, Color.FromArgb(55, 255, 210, 180), 5, 17);
        DrawClouds(g, 130, 300, 520, Color.FromArgb(35, 255, 180, 160), 3, 4);

        DrawMountains(g, Color.FromArgb(70, 49, 76), Color.FromArgb(99, 62, 82), 640, 0.8);
        DrawMountains(g, Color.FromArgb(43, 38, 65), Color.FromArgb(65, 47, 70), 740, 1.15);
        DrawHills(g, Color.FromArgb(30, 31, 45), 850);

        var random = new Random(6021);
        for (var layer = 0; layer < 3; layer++)
        {
            for (var i = 0; i < 14 - layer * 3; i++)
            {
                DrawBlockTree(g, random.Next(-80, Width + 80), 870 + layer * 70,
                    0.6f + layer * 0.3f + (float)random.NextDouble() * 0.45f, 2, random);
            }
        }

        using (var water = new LinearGradientBrush(new Rectangle(0, 870, Width, 240),
                   Color.FromArgb(180, 100, 62, 74), Color.FromArgb(255, 12, 20, 30), 90f))
            g.FillRectangle(water, 0, 870, Width, 240);

        // Water reflections.
        for (var i = 0; i < 110; i++)
        {
            var x = random.Next(1050, 1750);
            var y = random.Next(880, 1060);
            var w = random.Next(30, 190);
            using var pen = new Pen(Color.FromArgb(random.Next(15, 55), 255, 194, 140), random.Next(1, 3));
            g.DrawLine(pen, x, y, x + w, y);
        }

        DrawGrassBand(g, 846, Color.FromArgb(255, 42, 54, 35), Color.FromArgb(255, 22, 29, 20), random);
        DrawParticles(g, 70, new Rectangle(400, 300, 1100, 500), Color.FromArgb(140, 255, 217, 155), 2f, 21);
        DrawForegroundGrass(g, new Random(4));
        ApplyVignette(g);
        ApplyGrain(g, 7000, 22);
        bmp.Save(path, ImageFormat.Png);
    }

    private static void DrawCave(string path)
    {
        using var bmp = NewCanvas(
            Color.FromArgb(4, 7, 10),
            Color.FromArgb(9, 18, 24),
            Color.FromArgb(16, 40, 46),
            Color.FromArgb(2, 5, 7));
        using var g = Graphics.FromImage(bmp);
        Prepare(g);

        // Distant cavern opening.
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(520, 120, 900, 720);
            using var brush = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(105, 48, 170, 152),
                SurroundColors = new[] { Color.FromArgb(0, 20, 80, 76) }
            };
            g.FillPath(brush, glowPath);
        }

        // Cavern silhouette.
        var random = new Random(1337);
        var leftWall = new List<PointF> { new(0, 0), new(430, 0) };
        for (var y = 0; y <= Height; y += 55)
            leftWall.Add(new PointF(250 + random.Next(-45, 90), y));
        leftWall.Add(new PointF(0, Height));
        using (var wall = new SolidBrush(Color.FromArgb(255, 3, 8, 10)))
            g.FillPolygon(wall, leftWall.ToArray());

        var rightWall = new List<PointF> { new(Width, 0), new(1520, 0) };
        for (var y = 0; y <= Height; y += 60)
            rightWall.Add(new PointF(1630 + random.Next(-100, 50), y));
        rightWall.Add(new PointF(Width, Height));
        using (var wall = new SolidBrush(Color.FromArgb(255, 3, 8, 10)))
            g.FillPolygon(wall, rightWall.ToArray());

        // Ceiling and floor.
        using (var ceiling = new SolidBrush(Color.FromArgb(255, 3, 8, 10)))
            g.FillPolygon(ceiling, new[]
            {
                new PointF(0, 0), new PointF(Width, 0), new PointF(Width, 130),
                new PointF(1500, 85), new PointF(1150, 150), new PointF(760, 80), new PointF(300, 145), new PointF(0, 100)
            });
        using (var floor = new SolidBrush(Color.FromArgb(255, 2, 6, 8)))
            g.FillPolygon(floor, new[]
            {
                new PointF(0, Height), new PointF(Width, Height), new PointF(Width, 910),
                new PointF(1630, 880), new PointF(1300, 940), new PointF(950, 880), new PointF(590, 930),
                new PointF(300, 890), new PointF(0, 925)
            });

        // Stalactites / stalagmites.
        for (var i = 0; i < 17; i++)
        {
            var x = random.Next(420, 1500);
            var length = random.Next(50, 210);
            using var brush = new SolidBrush(Color.FromArgb(255, 5, 12, 15));
            if (i % 2 == 0)
                g.FillPolygon(brush, new[]
                {
                    new PointF(x, 0), new PointF(x + random.Next(40, 100), 0),
                    new PointF(x + random.Next(15, 50), length)
                });
            else
                g.FillPolygon(brush, new[]
                {
                    new PointF(x, Height), new PointF(x + random.Next(40, 100), Height),
                    new PointF(x + random.Next(15, 50), Height - length)
                });
        }

        // Glowing crystals.
        DrawCrystalCluster(g, 330, 840, 1.6f, Color.FromArgb(220, 56, 238, 184), random);
        DrawCrystalCluster(g, 1490, 760, 1.15f, Color.FromArgb(190, 68, 190, 255), random);
        DrawCrystalCluster(g, 860, 1010, 0.85f, Color.FromArgb(160, 120, 255, 190), random);
        DrawCrystalCluster(g, 1180, 150, 0.75f, Color.FromArgb(130, 80, 220, 255), random);

        // Water pool reflection.
        using (var pool = new LinearGradientBrush(new Rectangle(450, 960, 1050, 120),
                   Color.FromArgb(90, 34, 140, 130), Color.FromArgb(0, 0, 0, 0), 90f))
            g.FillRectangle(pool, 450, 950, 1050, 130);

        DrawParticles(g, 180, new Rectangle(250, 150, 1450, 800), Color.FromArgb(170, 90, 255, 200), 2.8f, 31);
        DrawParticles(g, 60, new Rectangle(300, 400, 1300, 500), Color.FromArgb(120, 150, 220, 255), 1.6f, 8);
        ApplyVignette(g, 0.86f);
        ApplyGrain(g, 9000, 20);
        bmp.Save(path, ImageFormat.Png);
    }

    // ------------------------------------------------------------------ scene helpers

    private static Bitmap NewCanvas(Color top, Color upper, Color lower, Color bottom)
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        using var brush = new LinearGradientBrush(new Rectangle(0, 0, Width, Height),
            Color.Black, Color.Black, LinearGradientMode.Vertical);
        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { top, upper, lower, bottom },
            Positions = new[] { 0f, 0.46f, 0.78f, 1f }
        };
        g.FillRectangle(brush, 0, 0, Width, Height);
        return bmp;
    }

    private static void Prepare(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
    }

    private static void DrawStars(Graphics g, int count, Rectangle area, Color color, int seed)
    {
        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            var x = random.Next(area.Left, area.Right);
            var y = random.Next(area.Top, area.Bottom);
            var size = random.Next(1, 4);
            var alpha = random.Next(25, color.A + 1);
            using var brush = new SolidBrush(Color.FromArgb(alpha, color));
            g.FillRectangle(brush, x, y, size, size);
            if (size >= 3 && random.Next(4) == 0)
            {
                using var pen = new Pen(Color.FromArgb(alpha / 2, color), 1);
                g.DrawLine(pen, x - 4, y + 1, x + size + 4, y + 1);
                g.DrawLine(pen, x + 1, y - 4, x + 1, y + size + 4);
            }
        }
    }

    private static void DrawMoon(Graphics g, PointF center, float radius, Color color)
    {
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(center.X - radius * 2.2f, center.Y - radius * 2.2f, radius * 4.4f, radius * 4.4f);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(70, color),
                SurroundColors = new[] { Color.FromArgb(0, color) }
            };
            g.FillPath(glow, glowPath);
        }
        using (var brush = new SolidBrush(Color.FromArgb(230, color)))
            g.FillEllipse(brush, center.X - radius / 2, center.Y - radius / 2, radius, radius);

        // Blocky craters.
        using var crater = new SolidBrush(Color.FromArgb(32, 80, 110, 110));
        g.FillRectangle(crater, center.X - radius * 0.18f, center.Y - radius * 0.12f, radius * 0.18f, radius * 0.18f);
        g.FillRectangle(crater, center.X + radius * 0.08f, center.Y + radius * 0.02f, radius * 0.12f, radius * 0.12f);
        g.FillRectangle(crater, center.X - radius * 0.02f, center.Y + radius * 0.24f, radius * 0.1f, radius * 0.1f);
    }

    private static void DrawClouds(Graphics g, int count, int yMin, int yMax, Color color, int seed, int blocks)
    {
        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            var x = random.Next(-200, Width + 50);
            var y = random.Next(yMin, yMax);
            var scale = random.Next(16, 30);
            var alpha = random.Next(Math.Max(8, color.A / 3), color.A + 1);
            using var brush = new SolidBrush(Color.FromArgb(alpha, color));
            for (var b = 0; b < blocks; b++)
            {
                var bx = x + random.Next(-5, 7) * scale;
                var by = y + random.Next(-1, 3) * scale;
                g.FillRectangle(brush, bx, by, scale * random.Next(2, 6), scale * random.Next(1, 2));
            }
        }
    }

    private static void DrawMountains(Graphics g, Color top, Color bottom, int horizon, double roughness)
    {
        var random = new Random((int)(horizon * 13 + roughness * 100));
        var points = new List<PointF> { new(0, Height) };
        var x = -100f;
        var y = horizon - 120f;
        while (x < Width + 150)
        {
            var step = random.Next(90, 230);
            var peak = y - random.Next(40, 260) * (float)roughness;
            points.Add(new PointF(x, y));
            points.Add(new PointF(x + step * 0.45f, peak));
            points.Add(new PointF(x + step, y - random.Next(-30, 40)));
            x += step;
            y = horizon - random.Next(20, 160);
        }
        points.Add(new PointF(Width + 150, Height));

        using var path = new GraphicsPath();
        path.AddPolygon(points.ToArray());
        using var brush = new LinearGradientBrush(new Rectangle(0, horizon - 480, Width, 700),
            Color.FromArgb(240, top), Color.FromArgb(255, bottom), 90f);
        g.FillPath(brush, path);

        // Blocky facets.
        using var facet = new Pen(Color.FromArgb(38, 200, 255, 220), 2);
        for (var i = 3; i < points.Count - 4; i += 3)
        {
            var p = points[i];
            g.DrawLine(facet, p.X, p.Y, p.X + random.Next(20, 80), p.Y + random.Next(40, 150));
        }
    }

    private static void DrawSnowCaps(Graphics g, int horizon, double roughness)
    {
        var random = new Random(881);
        for (var i = 0; i < 18; i++)
        {
            var x = random.Next(0, Width);
            var y = horizon - random.Next(120, 320);
            using var brush = new SolidBrush(Color.FromArgb(35, 220, 240, 255));
            g.FillPolygon(brush, new[]
            {
                new PointF(x, y), new PointF(x + random.Next(30, 80), y - random.Next(10, 45)),
                new PointF(x + random.Next(50, 100), y + random.Next(20, 50))
            });
        }
    }

    private static void DrawHills(Graphics g, Color color, int top)
    {
        var random = new Random(top);
        using var path = new GraphicsPath();
        var points = new List<PointF> { new(0, Height) };
        for (var x = 0f; x <= Width + 100; x += 120)
            points.Add(new PointF(x, top + random.Next(-45, 55)));
        points.Add(new PointF(Width, Height));
        path.AddPolygon(points.ToArray());
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void DrawGrassBand(Graphics g, int top, Color grass, Color soil, Random random)
    {
        using var soilBrush = new SolidBrush(soil);
        using var grassBrush = new SolidBrush(grass);
        var points = new List<PointF> { new(0, top) };
        for (var x = 0; x <= Width; x += 48)
            points.Add(new PointF(x, top + random.Next(-10, 12)));
        points.Add(new PointF(Width, top + 44));
        for (var x = Width; x >= 0; x -= 48)
            points.Add(new PointF(x, top + 38 + random.Next(-12, 16)));
        points.Add(new PointF(0, top));
        g.FillPolygon(soilBrush, points.ToArray());

        for (var x = -20; x < Width + 20; x += 34)
        {
            var h = random.Next(10, 34);
            g.FillRectangle(grassBrush, x, top + random.Next(-8, 8), 34, h);
        }
    }

    private static void DrawForegroundGrass(Graphics g, Random random)
    {
        using var brush = new SolidBrush(Color.FromArgb(255, 2, 8, 5));
        for (var i = 0; i < 200; i++)
        {
            var x = random.Next(-30, Width + 30);
            var h = random.Next(8, 60);
            var w = random.Next(3, 10);
            g.FillRectangle(brush, x, Height - h, w, h);
        }
        using var bottom = new LinearGradientBrush(new Rectangle(0, Height - 120, Width, 120),
            Color.FromArgb(0, 0, 0, 0), Color.FromArgb(210, 1, 5, 3), 90f);
        g.FillRectangle(bottom, 0, Height - 120, Width, 120);
    }

    private static void DrawBlockTree(Graphics g, float x, float baseY, float scale, int layer, Random random)
    {
        var trunkWidth = 14 * scale;
        var trunkHeight = 120 * scale;
        using var trunk = new SolidBrush(Color.FromArgb(layer == 2 ? 255 : 220,
            12 + layer * 4, 25 + layer * 3, 15 + layer * 2));
        g.FillRectangle(trunk, x, baseY - trunkHeight, trunkWidth, trunkHeight);

        var leafColor = Color.FromArgb(layer == 2 ? 190 : 235,
            12 + random.Next(0, 16), 52 + random.Next(0, 35), 30 + random.Next(0, 20));
        using var leaf = new SolidBrush(leafColor);
        var size = 62 * scale;
        var crownY = baseY - trunkHeight - size * 0.3f;
        g.FillRectangle(leaf, x - size, crownY, size * 3.2f, size);
        g.FillRectangle(leaf, x - size * 1.6f, crownY - size * 0.8f, size * 4.4f, size);
        g.FillRectangle(leaf, x - size * 0.8f, crownY - size * 1.8f, size * 2.8f, size);

        if (random.Next(3) == 0)
        {
            using var highlight = new SolidBrush(Color.FromArgb(24, 160, 255, 140));
            g.FillRectangle(highlight, x - size * 1.2f, crownY - size * 0.3f, size, size * 0.6f);
        }
    }

    private static void DrawAuroraRibbon(Graphics g, float x1, float y1, float x2, float y2, Color color)
    {
        using var path = new GraphicsPath();
        using var pen = new Pen(Color.FromArgb(color.A, color), 120)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var softPen = new Pen(Color.FromArgb(color.A / 3, color), 260)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        path.AddBezier(x1, y1, x1 + 420, y1 - 260, x2 - 420, y2 + 220, x2, y2);
        g.DrawPath(softPen, path);
        g.DrawPath(pen, path);

        using var bright = new Pen(Color.FromArgb(color.A / 2, 220, 255, 240), 24)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawPath(bright, path);
    }

    private static void DrawCrystalCluster(Graphics g, float x, float y, float scale, Color color, Random random)
    {
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(x - 150 * scale, y - 160 * scale, 300 * scale, 300 * scale);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(80, color),
                SurroundColors = new[] { Color.FromArgb(0, color) }
            };
            g.FillPath(glow, glowPath);
        }

        for (var i = 0; i < 5; i++)
        {
            var offset = (i - 2) * 22 * scale;
            var height = random.Next(40, 100) * scale;
            var width = random.Next(12, 26) * scale;
            var cx = x + offset;
            var tip = new PointF(cx, y - height);
            var left = new PointF(cx - width / 2, y);
            var right = new PointF(cx + width / 2, y);
            using var brush = new LinearGradientBrush(
                new RectangleF(cx - width, y - height, width * 2, height),
                Color.FromArgb(220, 200, 255, 240), Color.FromArgb(200, color), 90f);
            g.FillPolygon(brush, new[] { tip, right, left });
            using var edge = new Pen(Color.FromArgb(120, 230, 255, 250), 1);
            g.DrawLine(edge, left, tip);
            g.DrawLine(edge, tip, right);
        }
    }

    private static void DrawParticles(Graphics g, int count, Rectangle area, Color color, float maxSize, int seed)
    {
        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            var x = random.Next(area.Left, area.Right);
            var y = random.Next(area.Top, area.Bottom);
            var size = 1 + (float)random.NextDouble() * maxSize;
            var alpha = random.Next(20, color.A + 1);
            using var glow = new SolidBrush(Color.FromArgb(alpha / 4, color));
            g.FillEllipse(glow, x - size * 2, y - size * 2, size * 5, size * 5);
            using var core = new SolidBrush(Color.FromArgb(alpha, color));
            g.FillEllipse(core, x, y, size, size);
        }
    }

    private static void ApplyVignette(Graphics g, float strength = 1f)
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new Rectangle(0, 0, Width, Height));
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(Width * 0.48f, Height * 0.46f),
            CenterColor = Color.FromArgb(0, 0, 0, 0),
            SurroundColors = new[] { Color.FromArgb((int)(175 * strength), 0, 0, 0) }
        };
        g.FillRectangle(brush, 0, 0, Width, Height);

        using var top = new LinearGradientBrush(new Rectangle(0, 0, Width, 220),
            Color.FromArgb(110, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90f);
        g.FillRectangle(top, 0, 0, Width, 220);
    }

    private static void ApplyGrain(Graphics g, int count, int maxAlpha)
    {
        var random = new Random(771);
        using var brush = new SolidBrush(Color.FromArgb(maxAlpha, 255, 255, 255));
        for (var i = 0; i < count; i++)
        {
            var x = random.Next(0, Width);
            var y = random.Next(0, Height);
            g.FillRectangle(brush, x, y, 1, 1);
        }
    }

    // ------------------------------------------------------------------ branding

    private static Bitmap DrawLogo(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        Prepare(g);
        g.Clear(Color.Transparent);

        var rect = new RectangleF(size * 0.03f, size * 0.03f, size * 0.94f, size * 0.94f);
        using (var path = RoundedRect(rect, size * 0.22f))
        {
            using var background = new LinearGradientBrush(rect,
                Color.FromArgb(255, 9, 22, 14), Color.FromArgb(255, 18, 47, 29), 55f);
            g.FillPath(background, path);
            using var border = new Pen(Color.FromArgb(255, 82, 224, 118), Math.Max(2, size * 0.018f));
            g.DrawPath(border, path);
            using var inner = new Pen(Color.FromArgb(64, 180, 255, 198), Math.Max(1, size * 0.006f));
            var innerRect = new RectangleF(rect.X + size * 0.045f, rect.Y + size * 0.045f,
                rect.Width - size * 0.09f, rect.Height - size * 0.09f);
            using var innerPath = RoundedRect(innerRect, size * 0.18f);
            g.DrawPath(inner, innerPath);
        }

        // Isometric grass block watermark.
        var cx = size * 0.5f;
        var topY = size * 0.28f;
        var left = new PointF(size * 0.17f, size * 0.48f);
        var right = new PointF(size * 0.83f, size * 0.48f);
        var middle = new PointF(cx, size * 0.68f);
        var bottom = new PointF(cx, size * 0.86f);

        using (var topFace = new SolidBrush(Color.FromArgb(70, 58, 206, 105)))
            g.FillPolygon(topFace, new[] { new PointF(cx, topY), right, middle, left });
        using (var leftFace = new SolidBrush(Color.FromArgb(52, 98, 66, 40)))
            g.FillPolygon(leftFace, new[] { left, middle, bottom, new PointF(size * 0.17f, size * 0.66f) });
        using (var rightFace = new SolidBrush(Color.FromArgb(40, 52, 34, 21)))
            g.FillPolygon(rightFace, new[] { middle, right, new PointF(size * 0.83f, size * 0.66f), bottom });

        // XD monogram.
        using var font = new Font("Segoe UI Black", size * 0.34f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var textRect = new RectangleF(0, size * 0.22f, size, size * 0.55f);
        using (var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
        {
            var shadowRect = textRect;
            shadowRect.Offset(size * 0.012f, size * 0.018f);
            g.DrawString("XD", font, shadow, shadowRect, format);
        }
        using (var textBrush = new LinearGradientBrush(textRect,
                   Color.FromArgb(255, 244, 255, 246), Color.FromArgb(255, 118, 239, 146), 60f))
            g.DrawString("XD", font, textBrush, textRect, format);

        // Pixel accents.
        using (var accent = new SolidBrush(Color.FromArgb(230, 91, 232, 124)))
        {
            var unit = Math.Max(2f, size * 0.018f);
            g.FillRectangle(accent, size * 0.12f, size * 0.12f, unit, unit);
            g.FillRectangle(accent, size * 0.88f - unit, size * 0.12f, unit, unit);
            g.FillRectangle(accent, size * 0.12f, size * 0.88f - unit, unit, unit);
            g.FillRectangle(accent, size * 0.88f - unit, size * 0.88f - unit, unit, unit);
        }

        return bmp;
    }

    private static Bitmap DrawAvatar(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        Prepare(g);
        g.Clear(Color.FromArgb(255, 18, 34, 24));

        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, size, size),
                   Color.FromArgb(255, 42, 104, 62), Color.FromArgb(255, 15, 28, 20), 55f))
            g.FillRectangle(bg, 0, 0, size, size);

        // Pixel character, inspired by a generic blocky avatar (original artwork).
        var unit = size / 16f;
        using (var hood = new SolidBrush(Color.FromArgb(255, 33, 132, 74)))
            g.FillRectangle(hood, unit * 4, unit * 2, unit * 8, unit * 11);
        using (var hoodDark = new SolidBrush(Color.FromArgb(255, 22, 94, 52)))
        {
            g.FillRectangle(hoodDark, unit * 4, unit * 2, unit * 8, unit * 2);
            g.FillRectangle(hoodDark, unit * 3, unit * 4, unit * 1.5f, unit * 7);
            g.FillRectangle(hoodDark, unit * 11.5f, unit * 4, unit * 1.5f, unit * 7);
        }
        using (var face = new SolidBrush(Color.FromArgb(255, 226, 174, 122)))
            g.FillRectangle(face, unit * 5, unit * 5, unit * 6, unit * 6);
        using (var hair = new SolidBrush(Color.FromArgb(255, 72, 43, 24)))
        {
            g.FillRectangle(hair, unit * 5, unit * 5, unit * 6, unit * 1);
            g.FillRectangle(hair, unit * 5, unit * 5, unit * 1, unit * 3);
        }
        using (var eyeWhite = new SolidBrush(Color.FromArgb(255, 248, 250, 246)))
        {
            g.FillRectangle(eyeWhite, unit * 6, unit * 7, unit * 1.2f, unit * 1.2f);
            g.FillRectangle(eyeWhite, unit * 8.8f, unit * 7, unit * 1.2f, unit * 1.2f);
        }
        using (var eye = new SolidBrush(Color.FromArgb(255, 24, 46, 30)))
        {
            g.FillRectangle(eye, unit * 6.4f, unit * 7.3f, unit * 0.6f, unit * 0.7f);
            g.FillRectangle(eye, unit * 9.2f, unit * 7.3f, unit * 0.6f, unit * 0.7f);
        }
        using (var mouth = new SolidBrush(Color.FromArgb(255, 126, 69, 48)))
            g.FillRectangle(mouth, unit * 7, unit * 9.5f, unit * 2, unit * 0.55f);
        using (var body = new SolidBrush(Color.FromArgb(255, 20, 91, 54)))
            g.FillRectangle(body, unit * 4.5f, unit * 11, unit * 7, unit * 5);
        using (var shirt = new SolidBrush(Color.FromArgb(255, 84, 208, 118)))
            g.FillRectangle(shirt, unit * 6, unit * 11, unit * 4, unit * 1);

        using var border = new Pen(Color.FromArgb(90, 120, 255, 157), Math.Max(1, size * 0.02f));
        g.DrawRectangle(border, 0, 0, size - 1, size - 1);
        return bmp;
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void WriteIco(string path, int[] sizes)
    {
        var pngs = new List<(int Size, byte[] Data)>();
        foreach (var size in sizes)
        {
            using var bmp = DrawLogo(size);
            using var stream = new MemoryStream();
            bmp.Save(stream, ImageFormat.Png);
            pngs.Add((size, stream.ToArray()));
        }

        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)pngs.Count);

        var offset = 6 + pngs.Count * 16;
        foreach (var (size, data) in pngs)
        {
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }

        foreach (var (_, data) in pngs)
            writer.Write(data);
    }
}
