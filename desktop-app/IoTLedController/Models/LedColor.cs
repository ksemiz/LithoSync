namespace IoTLedController.Models;

// =============================================================================
//  LedColor.cs  —  LED renk veri modeli
// =============================================================================

public struct LedColor
{
    public byte R;
    public byte G;
    public byte B;

    public LedColor(byte r, byte g, byte b) { R = r; G = g; B = b; }

    // System.Drawing.Color dönüşümü
    public System.Drawing.Color ToDrawingColor()
        => System.Drawing.Color.FromArgb(R, G, B);

    // Windows.Media.Color dönüşümü (WPF)
    public System.Windows.Media.Color ToMediaColor()
        => System.Windows.Media.Color.FromRgb(R, G, B);

    public static LedColor FromDrawingColor(System.Drawing.Color c)
        => new(c.R, c.G, c.B);

    public static LedColor FromMediaColor(System.Windows.Media.Color c)
        => new(c.R, c.G, c.B);

    public static readonly LedColor Black = new(0, 0, 0);
    public static readonly LedColor White = new(255, 255, 255);
    public static readonly LedColor Red   = new(255, 0, 0);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
