using SplineTravel.Core.GCode;
using SplineTravel.Core.Precision;

namespace SplineTravel.Core.Geometry;

/// <summary>
/// Fits a cubic Bezier between two points with given entry/exit speeds and generates jerk-limited segments.
/// Ported from VB6 clsTravelGenerator.
/// </summary>
public sealed class TravelSplineGenerator
{
    public Vector3 P1 { get; set; }
    public Vector3 P2 { get; set; }
    public Vector3 InSpeed { get; set; }
    public Vector3 OutSpeed { get; set; }

    public double SpeedLimit { get; set; } = 200;
    public double ZJerk { get; set; } = 0;
    public double CurveJerk { get; set; } = 2;
    public double Retract { get; set; } = 1.5;
    public double RetractAcceleration { get; set; } = 1000;
    public double RetractJerk { get; set; } = 8;
    public double Acceleration { get; set; } = 800;
    public bool BRetract { get; set; } = true;
    public bool BUnretract { get; set; } = true;

    private const int IterCount = 30;
    private const double Eps = 1e-100;

    public Bezier3 FitBezier(out double moveTimeResult)
    {
        double spd = Math.Max(InSpeed.Length, OutSpeed.Length);
        if (spd < CurveJerk)
            throw new InvalidOperationException("Too slow to fit spline");

        var inSpeedHop = new Vector3(InSpeed.X, InSpeed.Y, InSpeed.Z + ZJerk);
        var outSpeedHop = new Vector3(OutSpeed.X, OutSpeed.Y, OutSpeed.Z - ZJerk);

        double stopDist = Acceleration * Math.Pow(spd / Acceleration, 2) / 2;
        var bz = new Bezier3();
        bz[0] = P1;
        bz[3] = P2;

        double time = stopDist / spd / 10;
        double logFactor = 0.25;
        double timeOfSolved = time;

        for (int i = 0; i < IterCount; i++)
        {
            bz[1] = P1 + inSpeedHop * (time / 3);
            bz[2] = P2 + outSpeedHop * (-time / 3);

            var acc1 = bz.GetDeriv2(0) * (1.0 / (time * time));
            var acc2 = bz.GetDeriv2(1) * (1.0 / (time * time));
            double maxAcc = Math.Max(acc1.Length, acc2.Length);

            if (maxAcc <= Acceleration)
            {
                timeOfSolved = time;
                time *= Math.Exp(-logFactor);
                logFactor /= 2;
            }
            else
                time *= Math.Exp(logFactor);
        }

        moveTimeResult = timeOfSolved;
        return bz;
    }

    public List<GeometricMove> GenerateMoveTrainForBezier(Bezier3 bz, double timeOfMove)
    {
        if (CurveJerk <= 1e-100)
            throw new InvalidOperationException("CurveJerk is zero");

        var rtr = new RetractProfile
        {
            RetractLen = Retract,
            RetractA = RetractAcceleration,
            MoveTime = timeOfMove,
            BRetract = BRetract,
            BUnretract = BUnretract
        };
        bz.DerivJerk = CurveJerk * timeOfMove;
        rtr.DerivJerk = RetractJerk * timeOfMove;

        var moves = new List<GeometricMove>();
        double prevT = 0;
        var prevPos = bz.GetValue(0);

        while (true)
        {
            double curT = 1;
            bz.ShrinkInterval(prevT, ref curT);
            rtr.ShrinkInterval(prevT, ref curT);
            double timestep = timeOfMove * (curT - prevT);

            var curPos = bz.GetValue(curT);
            var move = new GeometricMove
            {
                P1 = prevPos,
                P2 = curPos,
                Time = timestep,
                Extrusion = -(rtr.GetValue(curT) - rtr.GetValue(prevT))
            };
            if (move.Speed > SpeedLimit)
                move.Speed = SpeedLimit;
            moves.Add(move);

            prevT = curT;
            prevPos = curPos;
            if (curT >= 1 - PrecisionSettings.RelConfusion)
                break;
        }

        return moves;
    }
}
