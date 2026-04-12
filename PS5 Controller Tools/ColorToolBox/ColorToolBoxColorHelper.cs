using System;
using System.Windows.Media;

namespace PS5_Controller_Tools.ColorToolBox
{
    public static class ColorToolBoxColorHelper
    {
        public static Color FromHSV(double hue, double saturation, double value)
        {
            hue = NormalizeHue(hue);
            saturation = Math.Clamp(saturation, 0.0, 1.0);
            value = Math.Clamp(value, 0.0, 1.0);

            int hi = Convert.ToInt32(Math.Floor(hue / 60.0)) % 6;
            double f = (hue / 60.0) - Math.Floor(hue / 60.0);

            double scaledValue = value * 255.0;
            byte v = (byte)Math.Round(scaledValue);
            byte p = (byte)Math.Round(scaledValue * (1.0 - saturation));
            byte q = (byte)Math.Round(scaledValue * (1.0 - (f * saturation)));
            byte t = (byte)Math.Round(scaledValue * (1.0 - ((1.0 - f) * saturation)));

            return hi switch
            {
                0 => Color.FromRgb(v, t, p),
                1 => Color.FromRgb(q, v, p),
                2 => Color.FromRgb(p, v, t),
                3 => Color.FromRgb(p, q, v),
                4 => Color.FromRgb(t, p, v),
                _ => Color.FromRgb(v, p, q),
            };
        }

        public static void ToHSV(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            hue = 0.0;
            if (delta > 0.0)
            {
                if (Math.Abs(max - r) < double.Epsilon)
                    hue = 60.0 * (((g - b) / delta) % 6.0);
                else if (Math.Abs(max - g) < double.Epsilon)
                    hue = 60.0 * (((b - r) / delta) + 2.0);
                else
                    hue = 60.0 * (((r - g) / delta) + 4.0);
            }

            if (hue < 0.0)
                hue += 360.0;

            saturation = max <= 0.0 ? 0.0 : delta / max;
            value = max;
        }

        public static Color GetPaletteColor(double xRatio, double yRatio)
        {
            xRatio = Math.Clamp(xRatio, 0.0, 1.0);
            yRatio = Math.Clamp(yRatio, 0.0, 1.0);

            Color pureHueColor = FromHSV(xRatio * 360.0, 1.0, 1.0);

            if (yRatio <= 0.5)
            {
                double tintRatio = yRatio / 0.5;
                return Lerp(Colors.White, pureHueColor, tintRatio);
            }

            double shadeRatio = (yRatio - 0.5) / 0.5;
            return Lerp(pureHueColor, Colors.Black, shadeRatio);
        }

        public static Color ApplyIntensityAroundMidpoint(Color baseColor, double sliderValue)
        {
            sliderValue = Math.Clamp(sliderValue, 0.0, 100.0);

            if (Math.Abs(sliderValue - 50.0) < 0.0001)
                return baseColor;

            if (sliderValue < 50.0)
            {
                double ratioToBase = sliderValue / 50.0;
                return Lerp(Colors.Black, baseColor, ratioToBase);
            }

            double ratioToWhite = (sliderValue - 50.0) / 50.0;
            return Lerp(baseColor, Colors.White, ratioToWhite);
        }

        public static double EstimatePaletteVerticalRatio(Color color)
        {
            double brightness = Math.Max(color.R, Math.Max(color.G, color.B)) / 255.0;
            double darkness = 1.0 - brightness;

            if (brightness >= 0.999)
                return 0.0;

            if (darkness >= 0.999)
                return 1.0;

            ToHSV(color, out _, out double saturation, out double value);

            if (saturation < 0.01)
            {
                return 0.5 + ((1.0 - value) * 0.5);
            }

            if (value >= 0.999)
            {
                double tintRatio = 1.0 - saturation;
                return tintRatio * 0.5;
            }

            return 0.5 + ((1.0 - value) * 0.5);
        }

        public static Color Lerp(Color from, Color to, double ratio)
        {
            ratio = Math.Clamp(ratio, 0.0, 1.0);

            return Color.FromRgb(
                (byte)Math.Round(from.R + ((to.R - from.R) * ratio)),
                (byte)Math.Round(from.G + ((to.G - from.G) * ratio)),
                (byte)Math.Round(from.B + ((to.B - from.B) * ratio)));
        }

        private static double NormalizeHue(double hue)
        {
            hue %= 360.0;
            if (hue < 0.0)
                hue += 360.0;

            return hue;
        }
    }
}
