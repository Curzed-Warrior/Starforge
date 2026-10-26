using System;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// Placeholder cubes for the war economy until the real models exist: factories orange, shipyard slips blue (a grey
/// hull inside each busy slip), barracks green, armies on the ground olive. One face of every cube carries the owner's
/// empire colour. They sit in a shell 2.2 to 2.8 seat radii out. Fleets and the armies they carry are drawn with real
/// hulls (WorksFleets).
/// </summary>
public partial class Works
{
    const int WarPropMax = 160;
    MultiMeshInstance3D _warProps;

    const string CubeShader = @"shader_type spatial;
varying vec3 v_empire; varying float v_face;
void vertex() { v_empire = INSTANCE_CUSTOM.rgb; v_face = step(0.9, NORMAL.y); }
void fragment() { ALBEDO = mix(COLOR.rgb, v_empire, v_face); EMISSION = ALBEDO * 0.12; ROUGHNESS = 0.7; }";

    void BuildWarProps()
    {
        var mat = new ShaderMaterial { Shader = new Shader { Code = CubeShader } };
        var box = new BoxMesh { Size = Vector3.One, Material = mat };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = box, InstanceCount = WarPropMax, VisibleInstanceCount = 0 };
        _warProps = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere };
        _near.AddChild(_warProps);
    }

    void UpdateWarProps(Colony c, double year)
    {
        var mm = _warProps.Multimesh;
        var seat = c.Seat;
        if (seat == null) { mm.VisibleInstanceCount = 0; return; }
        var sim = c.Sim; var civ = c.Civ;
        var p0 = PosOfD(seat, _sv.VisYear);
        var empire = civ.Color;
        int k = 0;
        // each kind gets its own stretch of the ring, so a glance tells them apart
        Transform3D Put(int slot, int of, float lane, float km, Color col, float spin = 0, Color? tint = null)
        {
            if (k >= WarPropMax) return default;
            float a = lane + slot * (0.9f / Math.Max(of, 6)) + _clock * 0.002f;
            float r = seat.Radius * (2.2f + 0.6f * ((slot * 0.618f) % 1f));
            var at = Drawn(p0 + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(slot * 1.7f) * seat.Radius * 0.05f, Mathf.Sin(a) * r), out float sq, out _);
            // true size is far below a pixel at system zoom: placeholders are drawn at least ~0.7% of the seat's radius per km
            float size = Math.Max(km * StarSystem.Km, seat.Radius * 0.007f * km);
            var t = new Transform3D(new Basis(Vector3.Up, spin).Scaled(Vector3.One * size * sq), at);
            mm.SetInstanceTransform(k, t);
            mm.SetInstanceColor(k, col);
            var e = tint ?? empire;
            mm.SetInstanceCustomData(k, new Color(e.R, e.G, e.B, 1));
            k++;
            return t;
        }

        int fac = Math.Min(c.Foundries, 12);
        for (int i = 0; i < fac; i++) Put(i, fac, 0f, 3f, WarData.Colour("factory"));

        int first = civ.Colonies.TakeWhile(x => x != c).Sum(sim.SlipsOf), mine = sim.SlipsOf(c);
        for (int i = 0; i < mine; i++)
        {
            var slip = Put(i, mine, 1.2f, 5f, WarData.Colour("shipyard"));
            // the hull being built sits inside its slip, 1 to 2 km as it grows
            if (first + i < civ.Yard.Count && civ.Yard[first + i].Progress > 0 && k < WarPropMax)
            {
                mm.SetInstanceTransform(k, new Transform3D(slip.Basis.Scaled(Vector3.One * (1f + (float)civ.Yard[first + i].Progress) / 5f), slip.Origin));
                mm.SetInstanceColor(k, WarData.Colour("hull")); mm.SetInstanceCustomData(k, new Color(empire.R, empire.G, empire.B, 1));
                k++;
            }
        }

        var here = c.Star.Id;
        var train = civ.Barracks.Where(t => t.At == here).ToList();
        for (int i = 0; i < train.Count; i++) Put(i, train.Count, 2.4f, 2f, WarData.Colour("barracks"));

        // special forces, purple: courses at the capital, and the player's own teams wherever they are (others' are secret)
        int courses = c.Home ? civ.Academy.Count : 0;
        var me = sim.Humanity;
        var teams = sim.Teams.Where(t => t.Civ == me.Id && t.To < 0 && t.At == here).ToList();
        for (int i = 0; i < courses + teams.Count; i++) Put(i, courses + teams.Count, 5.6f, 1.5f, WarData.Colour("specops"), 0, i < courses ? null : me.Color);

        // armies on the ground (those aboard a fleet are drawn as its transports, WorksFleets)
        var armies = sim.Armies.Where(a => a.At == here && a.Fleet < 0).ToList();
        for (int i = 0; i < armies.Count; i++) Put(i, armies.Count, 4.6f, 1.5f, WarData.Colour("transport"));

        mm.VisibleInstanceCount = k;
    }
}
