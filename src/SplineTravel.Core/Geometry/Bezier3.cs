using SplineTravel.Core.Precision;

namespace SplineTravel.Core.Geometry;

/// <summary>
/// Cubic Bezier curve (4 poles). Ported from VB6 clsBezier.
/// B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t)t^2 P2 + t^3 P3, t in [0,1].
/// </summary>
public sealed class Bezier3
{
    private readonly Vector3[] _poles = new Vector3[4];
    private double _tOfLowA = 0.5;
    private double _maxA = 1;
    private double _derivJerk = 1e-7;
    private bool _invalidated = true;

    public Vector3 this[int index]
    {
        get => _poles[index];
        set { _poles[index] = value; _invalidated = true; }
    }

    public double DerivJerk
    {
        get => _derivJerk;
        set { _derivJerk = value; _invalidated = true; }
    }

    public Vector3 GetValue(double t)
    {
        double s = 1 - t;
        return Vector3.LinearCombination4(
            s * s * s, _poles[0],
            3 * s * s * t, _poles[1],
            3 * s * t * t, _poles[2],
            t * t * t, _poles[3]);
    }

    public Vector3 GetDeriv(double t)
    {
        double s = 1 - t;
        return Vector3.LinearCombination4(
            -3 * s * s, _poles[0],
            -6 * s * t + 3 * s * s, _poles[1],
            -3 * t * t + 6 * t * s, _poles[2],
            3 * t * t, _poles[3]);
    }

    public Vector3 GetDeriv2(double t)
    {
        double s = 1 - t;
        return Vector3.LinearCombination4(
            6 * s, _poles[0],
            6 * t - 6 * s - 6 * s, _poles[1],
            -6 * t - 6 * t + 6 * s, _poles[2],
            6 * t, _poles[3]);
    }

    public double GetNextBreakpoint(double prevT) => 1;

    public bool ShrinkInterval(double prevT, ref double curT)
    {
        if (_invalidated) Recompute();
        double tBreakpoint = GetNextBreakpoint(prevT);
        double tstep = _maxA <= 1e-100 ? 1 : _derivJerk / _maxA;
        double logFactor = 0.25;
        double validTstep = tstep;

        for (int i = 0; i < 7; i++)
        {
            tstep *= Math.Exp(logFactor);
            double jerk = Vector3.Distance(GetDeriv(prevT + tstep), GetDeriv(prevT));
            if (prevT < _tOfLowA + PrecisionSettings.RelConfusion && prevT + tstep > _tOfLowA)
            {
                double jerk2 = Vector3.Distance(GetDeriv(_tOfLowA), GetDeriv(prevT));
                if (jerk2 > jerk) jerk = jerk2;
            }
            if (jerk < _derivJerk)
                validTstep = tstep;
            else
            {
                tstep = validTstep;
                logFactor /= 2;
            }
        }

        if (prevT + tstep > tBreakpoint)
            tstep = tBreakpoint - prevT;
        double tstepToEnd = tBreakpoint - prevT;
        if (tstep < tstepToEnd - PrecisionSettings.RelConfusion && tstepToEnd - tstep < tstepToEnd * 0.25)
            tstep = tstepToEnd * 0.3;

        if (curT > prevT + tstep + PrecisionSettings.RelConfusion)
        {
            curT = prevT + tstep;
            return true;
        }
        return false;
    }

    public void Recompute()
    {
        var a1 = GetDeriv2(0);
        var a2 = GetDeriv2(1);
        var d = a2 - a1;
        if (d.Length <= 1e-100)
            _tOfLowA = 1000;
        else
        {
            var dn = d.Normalized();
            double tA = -Vector3.Dot(a1, dn);
            _tOfLowA = tA / d.Length;
        }
        _maxA = Math.Max(a1.Length, a2.Length);
        _invalidated = false;
    }
}
