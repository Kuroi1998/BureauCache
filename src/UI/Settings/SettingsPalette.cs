using System;
using System.Drawing;

static class SettingsPalette
{
    public static Color OnFill(Color fill)
    {
        double luminance = 0.2126 * Linear(fill.R) + 0.7152 * Linear(fill.G) + 0.0722 * Linear(fill.B);
        double whiteContrast = 1.05 / (luminance + 0.05);
        double blackContrast = (luminance + 0.05) / 0.05;
        return whiteContrast >= blackContrast ? Color.White : Color.Black;
    }

    static double Linear(byte channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
