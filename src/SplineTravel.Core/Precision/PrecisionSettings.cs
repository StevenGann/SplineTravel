namespace SplineTravel.Core.Precision;

/// <summary>
/// Decimal places and equality thresholds for G-code output.
/// Ported from VB6 mdlPrecision.
/// </summary>
public sealed class PrecisionSettings
{
    public const double RelConfusion = 1e-12;

    public int PosDecimals { get; set; } = 3;
    public int ExtrDecimals { get; set; } = 3;
    public int SpeedDecimals { get; set; } = -1;

    public double PosConfusion => Math.Pow(10, -PosDecimals - 1);
    public double ExtrConfusion => Math.Pow(10, -ExtrDecimals - 1);
    public double SpeedConfusion => SpeedDecimals < 0 ? 0 : Math.Pow(10, -SpeedDecimals - 1);

    public static double Round(double value, int decimals)
    {
        if (decimals < 0) decimals = 0;
        return Math.Round(value, decimals);
    }
}
