using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The floating origin, and scaled space.
///
/// At true scale a world sits tens of thousands of units from its star, where a float can only place things to within
/// kilometres. So the rendered world is re-centred every frame on whatever the camera looks at: positions live in
/// double-precision system coordinates (the viewed star at zero) and become floats only after this origin is
/// subtracted. Nodes placed this way are TopLevel; the rest of a system hangs off the SystemView, which sits at its star.
///
/// A depth buffer can't hold a 40 m shuttle in front of you and a sun 150 million km behind it: past a ratio of about
/// ten million between the far and near planes nothing draws. So anything beyond a threshold is pulled toward the
/// camera and shrunk by the same factor ("scaled space"). Scaling about the eye changes nothing in the picture, only
/// depth; the squash is monotonic, so what is behind stays behind.
/// </summary>
public static class Frame
{
    /// <summary>System coordinates of the view's (0, 0, 0).</summary>
    public static D3 Origin;
    public static Vector3 View(D3 p) => new((float)(p.X - Origin.X), (float)(p.Y - Origin.Y), (float)(p.Z - Origin.Z));
    public static D3 Sys(Vector3 view) => new(view.X + Origin.X, view.Y + Origin.Y, view.Z + Origin.Z);
    /// <summary>Where the viewed star is, in view space (true, not squashed).</summary>
    public static Vector3 Star => View(D3.Zero);

    /// <summary>The camera's position in view space this frame.</summary>
    public static Vector3 Cam;
    static float _keep = 1e30f, _limit = 1e30f;
    /// <summary>Set by the camera after it moves: things nearer than ~12% of the far plane keep their place.</summary>
    public static void SetDepth(Vector3 cam, float far) { Cam = cam; _keep = far * 0.12f; _limit = far * 0.42f; }

    /// <summary>Where a point at `view` is drawn, and how much what is there must shrink to keep its size on screen.</summary>
    public static float Squash(Vector3 view, out Vector3 drawn)
    {
        var rel = view - Cam;
        float d = rel.Length();
        if (d <= _keep) { drawn = view; return 1f; }
        float f = _keep + (_limit - _keep) * (1f - _keep / d);
        float s = f / d;
        drawn = Cam + rel * s;
        return s;
    }

    /// <summary>Put a TopLevel node at a true view position, squashed if it is far. Returns the scale applied.</summary>
    public static float Place(Node3D n, Vector3 view, float scale = 1f)
    {
        float s = Squash(view, out var p);
        n.Position = p;
        n.Scale = Vector3.One * (scale * s);
        return s;
    }
}
