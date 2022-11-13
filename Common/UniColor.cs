namespace YouTubeDownloaderV2.Common;

using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;
using Color = System.Drawing.Color;
using WindowsColor = System.Windows.Media.Color;

[Serializable]
public readonly struct UniColor : IEquatable<UniColor>
{
    public static UniColor Empty { get; } = new(byte.MinValue, byte.MinValue, byte.MinValue, byte.MinValue, true);

    public static UniColor Black { get; } = new(byte.MinValue, byte.MinValue, byte.MinValue, byte.MaxValue, false);
    public static UniColor White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, false);

    [JsonRequired]
    public bool IsEmpty { get; init; }

    [JsonRequired]
    public byte R { get; init; }

    [JsonRequired]
    public byte G { get; init; }

    [JsonRequired]
    public byte B { get; init; }

    [JsonRequired]
    public byte A { get; init; }

    [JsonConstructor]
    private UniColor(byte r, byte g, byte b, byte a, bool isEmpty)
    {
        R = r;
        G = g;
        B = b;
        A = a;
        IsEmpty = isEmpty;
    }

    public UniColor() : this(byte.MinValue, byte.MinValue, byte.MinValue, byte.MinValue, true) { }

    public UniColor(byte red, byte green, byte blue) : this(red, green, blue, byte.MaxValue, false) { }

    public UniColor(byte red, byte green, byte blue, byte alpha) : this(red, green, blue, alpha, false) { }

    public static UniColor FromRgb(byte red, byte green, byte blue) => new(red, green, blue);

    public static UniColor FromRgba(byte red, byte green, byte blue, byte alpha) => new(red, green, blue, alpha);

    public static implicit operator Color(UniColor uc) => Color.FromArgb(uc.A, uc.R, uc.G, uc.B);

    public static implicit operator UniColor(Color c) => new(c.R, c.G, c.B, c.A);

    public static implicit operator WindowsColor(UniColor uc) => WindowsColor.FromArgb(uc.A, uc.R, uc.G, uc.B);

    public static implicit operator UniColor(WindowsColor wc) => new(wc.R, wc.G, wc.B, wc.A);

    public static explicit operator SolidColorBrush(UniColor uc) => new(uc);

    public static explicit operator UniColor(SolidColorBrush scb) => new(scb.Color.R, scb.Color.G, scb.Color.B, scb.Color.A);

    public static bool operator ==(UniColor left, UniColor right)
    {
        return left.R == right.R
            && left.G == right.G
            && left.B == right.B
            && left.A == right.A
            && left.IsEmpty == left.IsEmpty;
    }

    public static bool operator !=(UniColor left, UniColor right) => !(left == right);

    public bool Equals(UniColor other) => this == other;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is UniColor other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(R.GetHashCode(), G.GetHashCode(), B.GetHashCode(), A.GetHashCode());
}