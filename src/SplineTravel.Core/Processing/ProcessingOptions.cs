namespace SplineTravel.Core.Processing;

/// <summary>
/// All tunable options for spline/straight travel and seam concealment.
/// </summary>
public sealed class ProcessingOptions
{
    public bool UseSplineTravel { get; set; } = true;
    public bool UseStraightTravel { get; set; } = false;
    public bool SeamConcealment { get; set; } = true;

    public double RetractLength { get; set; } = 1.5;
    public double Acceleration { get; set; } = 800;
    public double CurveJerk { get; set; } = 2;
    public double SpeedLimit { get; set; } = 200;
    public double EAcceleration { get; set; } = 1000;
    public double EJerk { get; set; } = 8;
    public double ZJerk { get; set; } = 0;

    public double LoopTolerance { get; set; } = 0.3;
    public double SeamConcealRetractSpeed { get; set; } = 8;

    public double ZHop { get; set; } = 1;
    public double SpeedStraight { get; set; } = 200;
    public double RetractSpeedStraight { get; set; } = 300;
}
