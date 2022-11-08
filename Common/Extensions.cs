namespace YouTubeDownloaderV2.Common;

using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class Extensions
{
    public static BitmapSource ReplaceColor(this BitmapSource source, UniColor fromColor, UniColor toColor)
    {
        if (source.Format != PixelFormats.Bgra32) return source;

        var bytesPerPixel = (source.Format.BitsPerPixel + 7) / 8;
        var stride = bytesPerPixel * source.PixelWidth;
        var buffer = new byte[stride * source.PixelHeight];

        source.CopyPixels(buffer, stride, 0);

        for (int y = 0; y < source.PixelHeight; y++)
        {
            for (int x = 0; x < source.PixelWidth; x++)
            {
                var i = stride * y + bytesPerPixel * x;

                //buffer[i] = B | buffer[i + 1] = G | buffer[i + 2] = R | buffer[i + 3] = A
                if (buffer[i] == fromColor.B && buffer[i + 1] == fromColor.G && buffer[i + 2] == fromColor.R)
                {
                    buffer[i] = toColor.B;
                    buffer[i + 1] = toColor.G;
                    buffer[i + 2] = toColor.R;
                }
            }
        }

        return BitmapSource.Create(
            source.PixelWidth, source.PixelHeight,
            source.DpiX, source.DpiY,
            source.Format, null, buffer, stride);
    }

    public static BitmapSource ReplaceColor(this BitmapSource source, UniColor replacementColor)
    {
        if (source.Format != PixelFormats.Bgra32) return source;

        var bytesPerPixel = (source.Format.BitsPerPixel + 7) / 8;
        var stride = bytesPerPixel * source.PixelWidth;
        var buffer = new byte[stride * source.PixelHeight];

        source.CopyPixels(buffer, stride, 0);

        for (int y = 0; y < source.PixelHeight; y++)
        {
            for (int x = 0; x < source.PixelWidth; x++)
            {
                var i = stride * y + bytesPerPixel * x;

                if (buffer[i + 3] > 0)
                {
                    buffer[i] = replacementColor.B;
                    buffer[i + 1] = replacementColor.G;
                    buffer[i + 2] = replacementColor.R;
                }
            }
        }

        return BitmapSource.Create(
            source.PixelWidth, source.PixelHeight,
            source.DpiX, source.DpiY,
            source.Format, null, buffer, stride);
    }

    public static float GetBrightness(this UniColor c)
    {
        var r = c.R / 255f;
        var g = c.G / 255f;
        var b = c.B / 255f;

        var min = r;
        var max = r;

        if (g > max) max = g;
        if (b > max) max = b;

        if (g < min) min = g;
        if (b < min) min = b;

        return (max + min) / 2;
    }

    public static UniColor AdjustBrightness(this UniColor c, float percentage)
        => new((byte)Math.Clamp(c.R * percentage, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(c.G * percentage, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(c.B * percentage, byte.MinValue, byte.MaxValue), c.A);

    public static bool IsEmpty(this TextBox txtBox) => string.IsNullOrWhiteSpace(txtBox.Text);
}