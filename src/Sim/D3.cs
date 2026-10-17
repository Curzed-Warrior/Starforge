using System;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// A double-precision point. System coordinates reach hundreds of thousands of units at true scale, where a float
/// can only place things to within kilometres; positions are kept in doubles and turned into floats only after the
/// view's origin has been subtracted (see View.Frame).
/// </summary>
public readonly struct D3
{
    public readonly double X, Y, Z;
    public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
    public static readonly D3 Zero = new(0, 0, 0);
    public static implicit operator D3(Vector3 v) => new(v.X, v.Y, v.Z);
    public static D3 operator +(D3 a, D3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static D3 operator -(D3 a, D3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static D3 operator +(D3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static D3 operator -(D3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static D3 operator *(D3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static D3 operator -(D3 a) => new(-a.X, -a.Y, -a.Z);
    public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);
    public D3 Lerp(D3 b, double t) => new(X + (b.X - X) * t, Y + (b.Y - Y) * t, Z + (b.Z - Z) * t);
    /// <summary>Lossy: fine for directions and for anything near the star; use View.Frame for positions near the camera.</summary>
    public Vector3 F => new((float)X, (float)Y, (float)Z);
    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}
