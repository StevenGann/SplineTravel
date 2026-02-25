namespace SplineTravel.Core.Geometry;

/// <summary>
/// 3D vector (value type). Ported from VB6 typVector3D / clsVector3D.
/// </summary>
public readonly struct Vector3
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Vector3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public readonly double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public static double Distance(Vector3 a, Vector3 b) =>
        Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y) + (b.Z - a.Z) * (b.Z - a.Z));

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3 operator *(double s, Vector3 v) => v * s;

    public readonly Vector3 Normalized()
    {
        var l = Length;
        if (l <= 1e-100) return new Vector3(1, 0, 0);
        return this * (1.0 / l);
    }

    public static double Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3 LinearCombination(double c0, Vector3 v0, double c1, Vector3 v1) =>
        new(c0 * v0.X + c1 * v1.X, c0 * v0.Y + c1 * v1.Y, c0 * v0.Z + c1 * v1.Z);

    public static Vector3 LinearCombination4(double c0, Vector3 v0, double c1, Vector3 v1, double c2, Vector3 v2, double c3, Vector3 v3) =>
        new(
            c0 * v0.X + c1 * v1.X + c2 * v2.X + c3 * v3.X,
            c0 * v0.Y + c1 * v1.Y + c2 * v2.Y + c3 * v3.Y,
            c0 * v0.Z + c1 * v1.Z + c2 * v2.Z + c3 * v3.Z);
}
