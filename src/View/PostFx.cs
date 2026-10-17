using Godot;

namespace Starforge.View;

/// <summary>Screen-space grade drawn under the HUD: lens ghosts, fringing, vignette, grain.</summary>
public partial class PostFx : CanvasLayer
{
    ShaderMaterial _mat;
    float _t;

    public override void _Ready()
    {
        Layer = 1;
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/post.gdshader") };
        var rect = new ColorRect { Material = _mat, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(rect);
    }

    public float Warp;
    public void Update(Camera3D cam, SystemView sv, float dt)
    {
        _mat.SetShaderParameter("warp", Warp);
        _t += dt;
        _mat.SetShaderParameter("time", _t % 100f);
        var star = sv.TrueStar;
        float vis = 0;
        var vp = cam.GetViewport().GetVisibleRect().Size;
        Vector2 uv = new(-10, -10);
        if (!cam.IsPositionBehind(star))
        {
            var sp = cam.UnprojectPosition(star);
            uv = sp / vp;
            float onScreen = Mathf.Clamp(1.5f - Mathf.Max(Mathf.Abs(uv.X - 0.5f), Mathf.Abs(uv.Y - 0.5f)) * 2f, 0, 1);
            // blocked by a world in front of it?
            var from = cam.GlobalPosition;
            var dir = (star - from);
            float len = dir.Length();
            dir /= len;
            bool blocked = false;
            foreach (var bv in sv.Bodies)
            {
                if (bv.B.Kind == Sim.BodyKind.Belt) continue;
                var oc = bv.TrueView - from;
                float t = oc.Dot(dir);
                if (t <= 0 || t >= len) continue;
                if ((oc - dir * t).LengthSquared() < bv.B.Radius * bv.B.Radius * 1.02f) { blocked = true; break; }
            }
            float diskPx = sv.Sys.StarRadius / Mathf.Max(len, 0.01f) * vp.Y;
            vis = blocked ? 0 : onScreen * Mathf.Clamp(1.4f - diskPx / 500f, 0, 1) * sv.ViewDim * Mathf.Clamp(3f - Mathf.Log(len / (0.7f * Sim.StarSystem.AU) + 1f) * 0.5f, 0, 1);
        }
        _mat.SetShaderParameter("sun_uv", uv);
        _mat.SetShaderParameter("sun_vis", vis);
        var c = sv.Sys.StarColor;
        _mat.SetShaderParameter("sun_col", new Vector3(c.R, c.G, c.B));
    }
}
