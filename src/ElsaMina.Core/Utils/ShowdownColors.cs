using System.Drawing;
using System.Globalization;

namespace ElsaMina.Core.Utils;

public static class ShowdownColors
{
    private static readonly Dictionary<string, Color> HEX_COLOR_CACHE = new();

    public static Color ToColor(this string text)
    {
        text = text.ToLowerAlphaNum();

        if (HEX_COLOR_CACHE.TryGetValue(text, out var cachedColor))
        {
            return cachedColor;
        }

        var hash = text.ToMd5Digest();

        double h = int.Parse(hash.Substring(4, 4), NumberStyles.HexNumber) % 360;
        double s = int.Parse(hash.Substring(0, 4), NumberStyles.HexNumber) % 50 + 40;
        double l = int.Parse(hash.Substring(8, 4), NumberStyles.HexNumber) % 20 + 30;

        var (r, g, b) = HslToRgb(h, s, l);

        var lum = r * r * r * 0.2126 + g * g * g * 0.7152 + b * b * b * 0.0722;
        var hlMod = (lum - 0.2) * -150;
        if (hlMod > 18)
        {
            hlMod = (hlMod - 18) * 2.5;
        }
        else if (hlMod < 0)
        {
            hlMod = hlMod / 3;
        }
        else
        {
            hlMod = 0;
        }

        var hDist = Math.Min(Math.Abs(180 - h), Math.Abs(240 - h));
        if (hDist < 15)
        {
            hlMod += (15 - hDist) / 3;
        }

        l += hlMod;

        (r, g, b) = HslToRgb(h, s, l);

        var color = Color.FromArgb((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
        HEX_COLOR_CACHE[text] = color;
        return color;
    }

    public static string ToHexString(this Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static string ToHslString(this Color color)
    {
        var hue = color.GetHue();
        var saturation = color.GetSaturation() * 100;
        var brightness = color.GetBrightness() * 100;
        return $"HSL({Math.Round(hue)}, {Math.Round(saturation)}%, {Math.Round(brightness)}%)";
    }

    public static string ToRgbString(this Color color)
    {
        return $"RGB({color.R}, {color.G}, {color.B})";
    }

    private static (double, double, double) HslToRgb(double h, double s, double l)
    {
        var c = (100 - Math.Abs(2 * l - 100)) * s / 100 / 100;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l / 100 - c / 2;

        var (r, g, b) = (int)(h / 60) switch
        {
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            5 => (c, 0.0, x),
            _ => (c, x, 0.0)
        };

        return (r + m, g + m, b + m);
    }

    public static Color FromHsl(double h, double s, double l)
    {
        var (r, g, b) = HslToRgb(h, s, l);
        return Color.FromArgb((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
    }
}