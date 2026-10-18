using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Just enough of Godot's API for src/Sim and src/Tests to run outside the engine. The maths follows Godot's
// (single-precision vectors and colours); I/O maps res:// to the repository and user:// to a scratch folder.
namespace Godot;

public struct Vector3 : IEquatable<Vector3>
{
    public float X, Y, Z;
    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
    public static readonly Vector3 Zero = new(0, 0, 0), One = new(1, 1, 1), Up = new(0, 1, 0), Down = new(0, -1, 0),
        Right = new(1, 0, 0), Left = new(-1, 0, 0), Forward = new(0, 0, -1), Back = new(0, 0, 1);
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3 operator *(Vector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator *(float s, Vector3 a) => a * s;
    public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vector3 operator /(Vector3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);
    public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
    public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);
    public bool Equals(Vector3 o) => X == o.X && Y == o.Y && Z == o.Z;
    public override bool Equals(object o) => o is Vector3 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";
    public float LengthSquared() => X * X + Y * Y + Z * Z;
    public float Length() => MathF.Sqrt(LengthSquared());
    public Vector3 Normalized() { float l = LengthSquared(); if (l == 0) return Zero; l = MathF.Sqrt(l); return new(X / l, Y / l, Z / l); }
    public float Dot(Vector3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vector3 Cross(Vector3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public float DistanceTo(Vector3 b) => (b - this).Length();
    public float DistanceSquaredTo(Vector3 b) => (b - this).LengthSquared();
    public Vector3 Lerp(Vector3 to, float w) => new(X + (to.X - X) * w, Y + (to.Y - Y) * w, Z + (to.Z - Z) * w);
    public Vector3 Abs() => new(MathF.Abs(X), MathF.Abs(Y), MathF.Abs(Z));
    public Vector3 Floor() => new(MathF.Floor(X), MathF.Floor(Y), MathF.Floor(Z));
    public Vector3 Round() => new(MathF.Round(X), MathF.Round(Y), MathF.Round(Z));
    public Vector3 DirectionTo(Vector3 to) => (to - this).Normalized();
    public Vector3 Clamp(Vector3 lo, Vector3 hi) => new(Math.Clamp(X, lo.X, hi.X), Math.Clamp(Y, lo.Y, hi.Y), Math.Clamp(Z, lo.Z, hi.Z));
    /// <summary>Rotation about a unit axis (Rodrigues), as Godot's Basis(axis, angle) * v.</summary>
    public Vector3 Rotated(Vector3 axis, float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return this * c + axis.Cross(this) * s + axis * (axis.Dot(this) * (1 - c));
    }
}

public struct Vector2 : IEquatable<Vector2>
{
    public float X, Y;
    public Vector2(float x, float y) { X = x; Y = y; }
    public static readonly Vector2 Zero = new(0, 0), One = new(1, 1);
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, float s) => new(a.X * s, a.Y * s);
    public static Vector2 operator /(Vector2 a, float s) => new(a.X / s, a.Y / s);
    public bool Equals(Vector2 o) => X == o.X && Y == o.Y;
    public override bool Equals(object o) => o is Vector2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public float Length() => MathF.Sqrt(X * X + Y * Y);
    public float DistanceTo(Vector2 b) => (b - this).Length();
}

public struct Color
{
    public float R, G, B, A;
    public Color(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
    public Color(Color c, float a) { R = c.R; G = c.G; B = c.B; A = a; }
    public static readonly Color White = new(1, 1, 1), Black = new(0, 0, 0);
    public float H
    {
        get
        {
            float max = MathF.Max(R, MathF.Max(G, B)), min = MathF.Min(R, MathF.Min(G, B)), d = max - min;
            if (d == 0) return 0;
            float h = R == max ? (G - B) / d : G == max ? 2 + (B - R) / d : 4 + (R - G) / d;
            h /= 6f; if (h < 0) h += 1f;
            return h;
        }
    }
    public float S { get { float max = MathF.Max(R, MathF.Max(G, B)), min = MathF.Min(R, MathF.Min(G, B)); return max == 0 ? 0 : (max - min) / max; } }
    public float V => MathF.Max(R, MathF.Max(G, B));
    public static Color FromHsv(float h, float s, float v, float a = 1f)
    {
        h = (h % 1f) * 6f; if (h < 0) h += 6f;
        int i = (int)MathF.Floor(h); float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        return (i % 6) switch { 0 => new(v, t, p, a), 1 => new(q, v, p, a), 2 => new(p, v, t, a), 3 => new(p, q, v, a), 4 => new(t, p, v, a), _ => new(v, p, q, a) };
    }
    public static Color FromHtml(string s)
    {
        s = s.TrimStart('#');
        float C(int i) => int.Parse(s.Substring(i, 2), NumberStyles.HexNumber) / 255f;
        return s.Length >= 8 ? new(C(0), C(2), C(4), C(6)) : new(C(0), C(2), C(4));
    }
    public Color Lerp(Color to, float w) => new(R + (to.R - R) * w, G + (to.G - G) * w, B + (to.B - B) * w, A + (to.A - A) * w);
    public Color Lightened(float a) => new(R + (1 - R) * a, G + (1 - G) * a, B + (1 - B) * a, A);
    public Color Darkened(float a) => new(R * (1 - a), G * (1 - a), B * (1 - a), A);
    public static Color operator *(Color c, float s) => new(c.R * s, c.G * s, c.B * s, c.A * s);
    public override string ToString() => $"({R}, {G}, {B}, {A})";
}

public static class Mathf
{
    public const float Pi = MathF.PI, Tau = MathF.PI * 2, E = MathF.E, Inf = float.PositiveInfinity;
    public static float Abs(float x) => MathF.Abs(x); public static double Abs(double x) => Math.Abs(x); public static int Abs(int x) => Math.Abs(x);
    public static float Max(float a, float b) => MathF.Max(a, b); public static double Max(double a, double b) => Math.Max(a, b); public static int Max(int a, int b) => Math.Max(a, b);
    public static float Min(float a, float b) => MathF.Min(a, b); public static double Min(double a, double b) => Math.Min(a, b); public static int Min(int a, int b) => Math.Min(a, b);
    public static float Clamp(float x, float lo, float hi) => x < lo ? lo : x > hi ? hi : x;
    public static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;
    public static int Clamp(int x, int lo, int hi) => x < lo ? lo : x > hi ? hi : x;
    public static float Sqrt(float x) => MathF.Sqrt(x); public static double Sqrt(double x) => Math.Sqrt(x);
    public static float Pow(float x, float y) => MathF.Pow(x, y); public static double Pow(double x, double y) => Math.Pow(x, y);
    public static float Log(float x) => MathF.Log(x); public static double Log(double x) => Math.Log(x);
    public static float Exp(float x) => MathF.Exp(x); public static double Exp(double x) => Math.Exp(x);
    public static float Sin(float x) => MathF.Sin(x); public static double Sin(double x) => Math.Sin(x);
    public static float Cos(float x) => MathF.Cos(x); public static double Cos(double x) => Math.Cos(x);
    public static float Tan(float x) => MathF.Tan(x); public static double Tan(double x) => Math.Tan(x);
    public static float Atan(float x) => MathF.Atan(x); public static double Atan(double x) => Math.Atan(x);
    public static float Atan2(float y, float x) => MathF.Atan2(y, x); public static double Atan2(double y, double x) => Math.Atan2(y, x);
    public static float Floor(float x) => MathF.Floor(x); public static double Floor(double x) => Math.Floor(x);
    public static float Ceil(float x) => MathF.Ceiling(x); public static double Ceil(double x) => Math.Ceiling(x);
    public static float Round(float x) => MathF.Round(x); public static double Round(double x) => Math.Round(x);
    public static float Sign(float x) => MathF.Sign(x); public static double Sign(double x) => Math.Sign(x);
    public static float DegToRad(float d) => d * (Pi / 180f); public static double DegToRad(double d) => d * (Math.PI / 180.0);
    public static float RadToDeg(float r) => r * (180f / Pi); public static double RadToDeg(double r) => r * (180.0 / Math.PI);
    public static float Lerp(float a, float b, float w) => a + (b - a) * w; public static double Lerp(double a, double b, double w) => a + (b - a) * w;
    public static float InverseLerp(float a, float b, float v) => (v - a) / (b - a); public static double InverseLerp(double a, double b, double v) => (v - a) / (b - a);
    public static float SmoothStep(float a, float b, float x) { if (a == b) return 0; float t = Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3 - 2 * t); }
    public static double SmoothStep(double a, double b, double x) { if (a == b) return 0; double t = Clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3 - 2 * t); }
    public static float PosMod(float a, float b) { float r = a % b; if ((r < 0 && b > 0) || (r > 0 && b < 0)) r += b; return r; }
    public static double PosMod(double a, double b) { double r = a % b; if ((r < 0 && b > 0) || (r > 0 && b < 0)) r += b; return r; }
    public static int PosMod(int a, int b) { int r = a % b; if ((r < 0 && b > 0) || (r > 0 && b < 0)) r += b; return r; }
    public static float Wrap(float v, float lo, float hi) { float r = hi - lo; return r == 0 ? lo : lo + PosMod(v - lo, r); }
    public static double Wrap(double v, double lo, double hi) { double r = hi - lo; return r == 0 ? lo : lo + PosMod(v - lo, r); }
    public static int Wrap(int v, int lo, int hi) { int r = hi - lo; return r == 0 ? lo : lo + PosMod(v - lo, r); }
    public static bool IsEqualApprox(float a, float b) => a == b || MathF.Abs(a - b) < 1e-6f * MathF.Max(1, MathF.Abs(a));
    public static bool IsZeroApprox(float a) => MathF.Abs(a) < 1e-6f;
}

public static class GD
{
    public static void Print(params object[] what) => Console.WriteLine(string.Concat(what));
    public static void PrintErr(params object[] what) => Console.Error.WriteLine(string.Concat(what));
    public static void PushWarning(params object[] what) => Console.Error.WriteLine("WARNING: " + string.Concat(what));
    public static void PushError(params object[] what) => Console.Error.WriteLine("ERROR: " + string.Concat(what));
    static readonly Random _r = new();
    public static uint Randi() => (uint)_r.NextInt64(0, uint.MaxValue);
    public static float Randf() => _r.NextSingle();
}

public static class ProjectSettings
{
    public static string GlobalizePath(string p) => FileAccess.Globalize(p);
}

public static class OS
{
    /// <summary>Set by the headless runner: the arguments after "--", as Godot would pass them.</summary>
    public static string[] UserArgs = Array.Empty<string>();
    public static string[] GetCmdlineUserArgs() => UserArgs;
    public static bool HasEnvironment(string v) => Environment.GetEnvironmentVariable(v) != null;
    public static string GetEnvironment(string v) => Environment.GetEnvironmentVariable(v) ?? "";
}

public sealed class FileAccess : IDisposable
{
    public enum ModeFlags { Read = 1, Write = 2, ReadWrite = 3, WriteRead = 7 }
    /// <summary>The repository root (res://) and the scratch folder (user://); set by the headless runner.</summary>
    public static string ResRoot = ".", UserRoot = Path.Combine(Path.GetTempPath(), "starforge-headless", Environment.ProcessId.ToString());
    public static string Globalize(string p)
    {
        if (p.StartsWith("res://")) return Path.Combine(ResRoot, p[6..]);
        if (p.StartsWith("user://")) { Directory.CreateDirectory(UserRoot); return Path.Combine(UserRoot, p[7..]); }
        return p;
    }
    readonly string _path; readonly ModeFlags _mode; readonly System.Text.StringBuilder _out = new();
    FileAccess(string path, ModeFlags mode) { _path = path; _mode = mode; }
    public static FileAccess Open(string path, ModeFlags mode)
    {
        var p = Globalize(path);
        if (mode == ModeFlags.Read && !File.Exists(p)) return null;
        return new FileAccess(p, mode);
    }
    public static bool FileExists(string path) => File.Exists(Globalize(path));
    public static string GetFileAsString(string path) { var p = Globalize(path); return File.Exists(p) ? File.ReadAllText(p) : ""; }
    public static ulong GetModifiedTime(string path) => (ulong)new DateTimeOffset(File.GetLastWriteTimeUtc(Globalize(path))).ToUnixTimeSeconds();
    public string GetAsText() => File.ReadAllText(_path);
    public void StoreString(string s) => _out.Append(s);
    public void StoreLine(string s) => _out.Append(s).Append('\n');
    public void Close() => Dispose();
    bool _closed;
    public void Dispose() { if (_closed) return; _closed = true; if (_mode != ModeFlags.Read) File.WriteAllText(_path, _out.ToString()); }
}
