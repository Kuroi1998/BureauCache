// BureauCache — WeatherIcons
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

// =====================================================================
//  Icônes météo dessinées (soleil, lune, nuages, pluie, neige, orage…)
// =====================================================================
static class WeatherIcons
{
    static Color SunColor { get { return Color.FromArgb(250, 190, 40); } }
    static Color MoonColor { get { return Settings.Dark ? Color.FromArgb(215, 220, 240) : Color.FromArgb(120, 130, 170); } }
    static Color CloudColor { get { return Settings.Dark ? Color.FromArgb(222, 228, 238) : Color.FromArgb(168, 178, 194); } }
    static Color DarkCloud { get { return Settings.Dark ? Color.FromArgb(150, 158, 175) : Color.FromArgb(110, 120, 138); } }
    static Color RainColor { get { return Color.FromArgb(70, 150, 240); } }
    static Color SnowColor { get { return Settings.Dark ? Color.FromArgb(235, 245, 255) : Color.FromArgb(120, 170, 220); } }

    public static void Draw(Graphics g, int code, bool day, RectangleF r)
    {
        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try { DrawSky(g, Weather.SkyOf(code), day, r); }
        finally { g.SmoothingMode = old; }
    }

    static void DrawSky(Graphics g, Sky sky, bool day, RectangleF r)
    {
        float s = Math.Min(r.Width, r.Height);
        RectangleF b = new RectangleF(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s);
        RectangleF cloud = new RectangleF(b.X + s * 0.12f, b.Y + s * 0.2f, s * 0.76f, s * 0.46f);
        switch (sky)
        {
            case Sky.Clear:
                Light(g, day, b.X + s / 2, b.Y + s / 2, s * 0.24f);
                break;
            case Sky.MainlyClear:
                Light(g, day, b.X + s * 0.45f, b.Y + s * 0.42f, s * 0.22f);
                Cloud(g, new RectangleF(b.X + s * 0.42f, b.Y + s * 0.55f, s * 0.5f, s * 0.3f), CloudColor);
                break;
            case Sky.PartlyCloudy:
                Light(g, day, b.X + s * 0.36f, b.Y + s * 0.34f, s * 0.2f);
                Cloud(g, new RectangleF(b.X + s * 0.2f, b.Y + s * 0.4f, s * 0.7f, s * 0.42f), CloudColor);
                break;
            case Sky.Overcast:
                Cloud(g, new RectangleF(b.X + s * 0.3f, b.Y + s * 0.2f, s * 0.6f, s * 0.36f), DarkCloud);
                Cloud(g, new RectangleF(b.X + s * 0.1f, b.Y + s * 0.36f, s * 0.72f, s * 0.44f), CloudColor);
                break;
            case Sky.Fog:
                Cloud(g, new RectangleF(b.X + s * 0.15f, b.Y + s * 0.12f, s * 0.7f, s * 0.4f), CloudColor);
                using (Pen p = new Pen(DarkCloud, s * 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (int i = 0; i < 3; i++)
                    {
                        float y = b.Y + s * (0.62f + i * 0.12f), indent = i == 1 ? s * 0.1f : 0;
                        g.DrawLine(p, b.X + s * 0.14f + indent, y, b.X + s * 0.86f - (i == 1 ? 0 : s * 0.1f), y);
                    }
                break;
            case Sky.Drizzle:
                Cloud(g, cloud, CloudColor);
                using (SolidBrush br = new SolidBrush(RainColor))
                    for (int i = 0; i < 3; i++) Dot(g, br, b.X + s * (0.32f + i * 0.18f), b.Y + s * (0.76f + (i % 2) * 0.08f), s * 0.045f);
                break;
            case Sky.Rain:
            case Sky.Showers:
                if (sky == Sky.Showers) Light(g, day, b.X + s * 0.3f, b.Y + s * 0.26f, s * 0.17f);
                Cloud(g, cloud, CloudColor);
                Drops(g, b, s);
                break;
            case Sky.FreezingRain:
                Cloud(g, cloud, CloudColor);
                Drops(g, b, s);
                Flake(g, b.X + s * 0.78f, b.Y + s * 0.84f, s * 0.07f);
                break;
            case Sky.Snow:
            case Sky.SnowShowers:
                if (sky == Sky.SnowShowers) Light(g, day, b.X + s * 0.3f, b.Y + s * 0.26f, s * 0.17f);
                Cloud(g, cloud, CloudColor);
                for (int i = 0; i < 3; i++) Flake(g, b.X + s * (0.3f + i * 0.2f), b.Y + s * (0.78f + (i % 2) * 0.08f), s * 0.07f);
                break;
            default:
                Cloud(g, cloud, DarkCloud);
                PointF[] bolt = {
                    new PointF(b.X + s * 0.52f, b.Y + s * 0.58f), new PointF(b.X + s * 0.38f, b.Y + s * 0.8f),
                    new PointF(b.X + s * 0.5f, b.Y + s * 0.8f), new PointF(b.X + s * 0.42f, b.Y + s * 0.98f),
                    new PointF(b.X + s * 0.66f, b.Y + s * 0.72f), new PointF(b.X + s * 0.54f, b.Y + s * 0.72f),
                    new PointF(b.X + s * 0.62f, b.Y + s * 0.58f) };
                using (SolidBrush br = new SolidBrush(SunColor)) g.FillPolygon(br, bolt);
                break;
        }
    }

    // Soleil le jour, lune la nuit
    static void Light(Graphics g, bool day, float cx, float cy, float rad)
    {
        if (day)
        {
            using (Pen p = new Pen(SunColor, rad * 0.28f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(p, cx + (float)Math.Cos(a) * rad * 1.35f, cy + (float)Math.Sin(a) * rad * 1.35f,
                                  cx + (float)Math.Cos(a) * rad * 1.75f, cy + (float)Math.Sin(a) * rad * 1.75f);
                }
            using (SolidBrush b = new SolidBrush(SunColor)) g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
            return;
        }
        using (GraphicsPath moon = new GraphicsPath())
        using (GraphicsPath bite = new GraphicsPath())
        using (Region region = new Region())
        using (SolidBrush b = new SolidBrush(MoonColor))
        {
            moon.AddEllipse(cx - rad * 1.2f, cy - rad * 1.2f, rad * 2.4f, rad * 2.4f);
            bite.AddEllipse(cx - rad * 0.5f, cy - rad * 1.6f, rad * 2.4f, rad * 2.4f);
            region.MakeEmpty();
            region.Union(moon);
            region.Exclude(bite);
            g.FillRegion(b, region);
        }
    }

    // Nuage : base arrondie et deux bosses
    static void Cloud(Graphics g, RectangleF r, Color c)
    {
        using (GraphicsPath p = new GraphicsPath { FillMode = FillMode.Winding })
        using (SolidBrush b = new SolidBrush(c))
        {
            float h = r.Height;
            p.AddEllipse(r.X, r.Y + h * 0.42f, r.Width * 0.42f, h * 0.58f);
            p.AddEllipse(r.X + r.Width * 0.18f, r.Y + h * 0.12f, r.Width * 0.48f, h * 0.8f);
            p.AddEllipse(r.X + r.Width * 0.48f, r.Y, r.Width * 0.46f, h * 0.9f);
            p.AddEllipse(r.Right - r.Width * 0.36f, r.Y + h * 0.4f, r.Width * 0.36f, h * 0.6f);
            p.AddRectangle(new RectangleF(r.X + r.Width * 0.2f, r.Y + h * 0.6f, r.Width * 0.62f, h * 0.4f));
            g.FillPath(b, p);
        }
    }

    static void Drops(Graphics g, RectangleF b, float s)
    {
        using (Pen p = new Pen(RainColor, s * 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            for (int i = 0; i < 3; i++)
            {
                float x = b.X + s * (0.34f + i * 0.17f), y = b.Y + s * 0.72f;
                g.DrawLine(p, x, y, x - s * 0.06f, y + s * 0.16f);
            }
    }

    static void Dot(Graphics g, Brush b, float cx, float cy, float rad)
    {
        g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
    }

    static void Flake(Graphics g, float cx, float cy, float rad)
    {
        using (Pen p = new Pen(SnowColor, Math.Max(1.2f, rad * 0.35f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            for (int i = 0; i < 3; i++)
            {
                double a = i * Math.PI / 3 + Math.PI / 2;
                float dx = (float)Math.Cos(a) * rad, dy = (float)Math.Sin(a) * rad;
                g.DrawLine(p, cx - dx, cy - dy, cx + dx, cy + dy);
            }
    }
}
