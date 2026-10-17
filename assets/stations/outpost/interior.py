"""
Outpost, furnished: the game shell (build.py) plus the whole fitted ring interior, both decks, from the blueprints
(tools/blueprints: outpost_design -> blocking.json -> sketch_interior.py in PBR mode). One build: the shell's window
openings come from docs/blueprints/openings.json and the interior's frames meet the same glass.

Run inside Blender:
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/outpost/interior.py").read())
then e.g.  view("cabin", "A", x, y, yaw=.., pitch=.., lens=..)  /  orbit_clip(...)  /  export_interior()
Renders go to assets/stations/renders/outpost/interior/. The Earth below is at 1:4000 (same apparent size as from
400 km), centred straight down -Z, so a window looking out horizontally sees the horizon ~20 deg below eye level.
"""
import bpy, bmesh, json, math, os
from mathutils import Vector, Matrix

SF = r"C:/Users/diamo/Desktop/starforge"
REBUILD = globals().get("REBUILD", True)    # False: only define the helpers (the Blender MCP namespace resets every call)
if REBUILD: exec(open(SF + "/assets/stations/outpost/build.py").read())   # the shell (also re-exports outpost.glb)
exec(open(SF + "/tools/blueprints/sketch_interior.py").read())       # interior generators (developed plan -> ring)
PBR[0] = True
OPEN_ = json.load(open(SF + "/docs/blueprints/openings.json"))
DATA = json.load(open(os.path.join(ROOT, "blocking.json")))
OUTR = SF + "/assets/stations/renders/outpost/interior"
EARTH_SCALE = 1 / 4000.0
EARTH_DIR = Vector((-0.15, 0.345, -0.927)).normalized()   # station attitude: its axis is tilted ~40 deg off the local vertical
EARTH_ROT = (math.radians(90), 0.0, math.radians(-105))     # pole sideways; the Atlantic / West Africa below at frame 1
BAYr = math.radians(3.75)
OCT_C, OCT_F, LIP_IN, DG_ = 0.3, 0.24, 0.08, 0.45                  # as build.py's windows


def _coll(name):
    c = bpy.data.collections.get(name)
    if c:
        for o in list(c.objects): bpy.data.objects.remove(o, do_unlink=True)
    else:
        c = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c)
    c.hide_render = c.hide_viewport = False
    return c


def _oct8(A, B, K):
    return [(-(K - B), -B), (K - B, -B), (A, -(K - A)), (A, K - A), (K - B, B), (-(K - B), B), (-A, K - A), (-A, -(K - A))]


def window_frames(acc):
    """Inside face of every window: an angled octagonal lip on the wall and a reveal back to the shell's glass."""
    for side, r_wall, r_skin in (("outer", 44.0, 44.6), ("inner", 36.0, 35.4)):
        for w in OPEN_[side]:
            am = math.radians(w["a"]); d = Vector((math.cos(am), math.sin(am), 0)); t = Vector((-d.y, d.x, 0))
            n = d if side == "outer" else -d
            if side == "inner": t = -t
            cw, cs = d * (r_wall * math.cos(BAYr / 2)), d * (r_skin * math.cos(BAYr / 2))
            A, B, zc = w["w"] / 2, (w["z1"] - w["z0"]) / 2, (w["z0"] + w["z1"]) / 2
            K = A + B - OCT_C
            inner, outer = _oct8(A, B, K), _oct8(A + OCT_F, B + OCT_F, K + OCT_F * math.sqrt(2))
            P3 = lambda base, u, v, dd: tuple(base + t * u + Vector((0, 0, zc + v)) + n * dd)
            verts = [P3(cw, u, v, -0.01) for u, v in outer] + [P3(cw, u, v, -LIP_IN) for u, v in inner] + [P3(cs, u, v, -DG_ + 0.005) for u, v in inner]
            faces = [(i, (i + 1) % 8, 8 + (i + 1) % 8, 8 + i) for i in range(8)] + [(8 + i, 8 + (i + 1) % 8, 16 + (i + 1) % 8, 16 + i) for i in range(8)]
            acc.add("frame", verts, faces)
            # a warm light strip under the sill, inside
            p0 = Vector(P3(cw, -A * 0.8, -B - OCT_F - 0.05, -0.03)); p1 = Vector(P3(cw, A * 0.8, -B - OCT_F - 0.03, -0.05))
            acc.add("accent", [tuple(p0), tuple(Vector(P3(cw, A * 0.8, -B - OCT_F - 0.05, -0.03))), tuple(p1), tuple(Vector(P3(cw, -A * 0.8, -B - OCT_F - 0.03, -0.05)))], [(0, 1, 2, 3)])


def build_interior():
    coll = _coll("OutpostInterior")
    acc = Acc()
    L = R_MID * math.radians(30)
    for deck in ("A", "B"):
        FFL[0] = DATA[deck]["ffl"]
        blocking(acc, DATA, deck, -0.01, 12 * L + 0.01)
        design(acc, DATA, deck, -0.01, 12 * L + 0.01)
        cabins(acc, DATA, deck, -0.01, 12 * L + 0.01)
        rooms_fitout(acc, DATA, deck, -0.01, 12 * L + 0.01)
    FFL[0] = 0.0
    window_frames(acc)
    acc.build(coll)
    for o in coll.objects: o.name = "Int_" + o.name.split("_", 1)[1].split(".")[0]
    return acc


# ------------------------------------------------------------------ Earth from low orbit
def _tex(name, data=False):
    img = bpy.data.images.load(SF + "/textures/" + name, check_existing=True)
    if data: img.colorspace_settings.name = "Non-Color"
    return img


def sun(rot=(math.radians(55), 0, math.radians(35)), strength=4.5):
    """The sun: same direction as render.py's key light (which it replaces), so exterior and interior shots agree."""
    o = bpy.data.objects.get("IN_Sun")
    if not o:
        o = bpy.data.objects.new("IN_Sun", bpy.data.lights.new("IN_Sun", "SUN")); bpy.context.scene.collection.objects.link(o)
    o.data.energy = strength; o.data.color = (1.0, 0.97, 0.92); o.data.angle = math.radians(0.53)
    o.rotation_euler = rot
    for n in ("Sun",):
        if bpy.data.objects.get(n): bpy.data.objects[n].hide_render = True
    bpy.context.view_layer.update()
    return (o.matrix_world.to_3x3() @ Vector((0, 0, 1))).normalized()        # toward the sun


def earth(to_sun):
    coll = _coll("EarthView")
    R, dist = 6371000 * EARTH_SCALE, 6771000 * EARTH_SCALE
    def ball(name, r, matl):
        bm = bmesh.new(); bm.loops.layers.uv.new("UVMap")
        bmesh.ops.create_uvsphere(bm, u_segments=256, v_segments=128, radius=r, calc_uvs=True)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        for p in me.polygons: p.use_smooth = True
        me.materials.append(matl)
        o = bpy.data.objects.new(name, me); coll.objects.link(o); o.location = EARTH_DIR * dist
        o.rotation_euler = EARTH_ROT; o.visible_shadow = False
        return o
    # surface: day map lit by the sun direction, a sunglint on the oceans, city lights on the night side. Shaded in the
    # material (emission) so the model station, only 100 m above it at 1:4000, can't cast a shadow on it.
    m = bpy.data.materials.get("IN_Earth") or bpy.data.materials.new("IN_Earth"); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    L = nt.links.new
    out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission")
    day = nt.nodes.new("ShaderNodeTexImage"); day.image = _tex("8k_earth_daymap.jpg")
    night = nt.nodes.new("ShaderNodeTexImage"); night.image = _tex("8k_earth_nightmap.jpg")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"; dot.inputs[1].default_value = to_sun
    lam = nt.nodes.new("ShaderNodeMapRange"); lam.inputs["From Min"].default_value = -0.05; lam.inputs["From Max"].default_value = 1.0
    nf = nt.nodes.new("ShaderNodeMapRange"); nf.inputs["From Min"].default_value = 0.05; nf.inputs["From Max"].default_value = -0.15
    dl = nt.nodes.new("ShaderNodeMix"); dl.data_type = "RGBA"; dl.blend_type = "MULTIPLY"; dl.inputs[0].default_value = 1.0
    nl = nt.nodes.new("ShaderNodeMix"); nl.data_type = "RGBA"; nl.blend_type = "MULTIPLY"; nl.inputs[0].default_value = 1.0
    nl2 = nt.nodes.new("ShaderNodeMix"); nl2.data_type = "RGBA"; nl2.blend_type = "MULTIPLY"; nl2.inputs[0].default_value = 1.0
    nl2.inputs[7].default_value = (0.35, 0.3, 0.2, 1)
    add = nt.nodes.new("ShaderNodeMix"); add.data_type = "RGBA"; add.blend_type = "ADD"; add.inputs[0].default_value = 1.0
    L(geo.outputs["Normal"], dot.inputs[0]); L(dot.outputs["Value"], lam.inputs["Value"]); L(dot.outputs["Value"], nf.inputs["Value"])
    L(day.outputs[0], dl.inputs[6]); L(lam.outputs["Result"], dl.inputs[7])
    L(night.outputs[0], nl.inputs[6]); L(nf.outputs["Result"], nl.inputs[7]); L(nl.outputs[2], nl2.inputs[6])
    L(dl.outputs[2], add.inputs[6]); L(nl2.outputs[2], add.inputs[7])
    L(add.outputs[2], em.inputs[0]); em.inputs[1].default_value = 1.6
    L(em.outputs[0], out.inputs[0])
    e = ball("Earth", R, m)
    # clouds: white, lit by the same sun direction, alpha from the cloud map
    mc = bpy.data.materials.get("IN_Clouds") or bpy.data.materials.new("IN_Clouds"); mc.use_nodes = True
    nt = mc.node_tree; nt.nodes.clear()
    L = nt.links.new
    out = nt.nodes.new("ShaderNodeOutputMaterial"); mix = nt.nodes.new("ShaderNodeMixShader")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent"); em = nt.nodes.new("ShaderNodeEmission")
    cl = nt.nodes.new("ShaderNodeTexImage"); cl.image = _tex("8k_earth_clouds.jpg", True)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"; dot.inputs[1].default_value = to_sun
    lam = nt.nodes.new("ShaderNodeMapRange"); lam.inputs["From Min"].default_value = -0.05; lam.inputs["From Max"].default_value = 1.0
    sc2 = nt.nodes.new("ShaderNodeMath"); sc2.operation = "MULTIPLY"; sc2.inputs[1].default_value = 1.7
    L(geo.outputs["Normal"], dot.inputs[0]); L(dot.outputs["Value"], lam.inputs["Value"]); L(lam.outputs["Result"], sc2.inputs[0])
    L(sc2.outputs[0], em.inputs[1]); em.inputs[0].default_value = (1, 1, 1, 1)
    L(cl.outputs[0], mix.inputs[0]); L(tr.outputs[0], mix.inputs[1]); L(em.outputs[0], mix.inputs[2]); L(mix.outputs[0], out.inputs[0])
    mc.surface_render_method = "BLENDED"
    c = ball("Clouds", R * 1.004, mc)
    # atmosphere: a blue rim, brightest at the limb on the day side
    ma = bpy.data.materials.get("IN_Atmo") or bpy.data.materials.new("IN_Atmo"); ma.use_nodes = True
    nt = ma.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); add = nt.nodes.new("ShaderNodeAddShader")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent"); em = nt.nodes.new("ShaderNodeEmission")
    em.inputs[0].default_value = (0.3, 0.55, 1.0, 1)
    lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs[0].default_value = 0.6
    pw = nt.nodes.new("ShaderNodeMath"); pw.operation = "POWER"; pw.inputs[1].default_value = 6.0
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"; dot.inputs[1].default_value = to_sun
    lit = nt.nodes.new("ShaderNodeMapRange"); lit.inputs["From Min"].default_value = -0.3; lit.inputs["From Max"].default_value = 0.3
    mm = nt.nodes.new("ShaderNodeMath"); mm.operation = "MULTIPLY"
    sc_ = nt.nodes.new("ShaderNodeMath"); sc_.operation = "MULTIPLY"; sc_.inputs[1].default_value = 1.2
    nt.links.new(lw.outputs["Facing"], pw.inputs[0]); nt.links.new(geo.outputs["Normal"], dot.inputs[0]); nt.links.new(dot.outputs["Value"], lit.inputs["Value"])
    nt.links.new(pw.outputs[0], mm.inputs[0]); nt.links.new(lit.outputs["Result"], mm.inputs[1]); nt.links.new(mm.outputs[0], sc_.inputs[0])
    nt.links.new(sc_.outputs[0], em.inputs[1]); nt.links.new(tr.outputs[0], add.inputs[0]); nt.links.new(em.outputs[0], add.inputs[1])
    nt.links.new(add.outputs[0], out.inputs[0])
    ma.surface_render_method = "BLENDED"
    a = ball("Atmosphere", R * 1.02, ma)
    return e, c


def world_stars():
    sc = bpy.context.scene
    w = sc.world or bpy.data.worlds.new("World"); sc.world = w
    w.use_nodes = True; nt = w.node_tree
    bg = nt.nodes.get("Background")
    if not nt.nodes.get("Stars"):
        vor = nt.nodes.new("ShaderNodeTexVoronoi"); vor.name = "Stars"; vor.inputs["Scale"].default_value = 420.0
        ramp = nt.nodes.new("ShaderNodeValToRGB"); ramp.name = "StarRamp"
        ramp.color_ramp.elements[0].color = (1.6, 1.6, 1.7, 1)
        ramp.color_ramp.elements[1].position = 0.045; ramp.color_ramp.elements[1].color = (0.003, 0.0035, 0.006, 1)
        coord = nt.nodes.new("ShaderNodeTexCoord")
        nt.links.new(coord.outputs["Generated"], vor.inputs["Vector"]); nt.links.new(vor.outputs["Distance"], ramp.inputs["Fac"])
        nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs[1].default_value = 1.0


# ------------------------------------------------------------------ lights and rendering
def lamps_near(acc, at, radius=16.0, w_per_m2=70.0):
    for o in [o for o in bpy.data.objects if o.name.startswith("IN_lamp")]: bpy.data.objects.remove(o, do_unlink=True)
    n = 0
    for (x0, y0, x1, y1, h0, h1, ffl) in acc.lights:
        Lx, Wy = abs(x1 - x0), abs(y1 - y0)
        if Lx * Wy < 0.04: continue
        segs = max(1, int(Lx / 4.0 + 0.999))
        for k in range(segs):
            xa = min(x0, x1) + Lx * k / segs; xb = xa + Lx / segs; xm = (xa + xb) / 2; ym = (y0 + y1) / 2
            FFL[0] = ffl
            c = Vector(P(xm, ym, min(h0, h1) - 0.03))
            if (c - at).length > radius: continue
            ld = bpy.data.lights.new(f"IN_lamp{n}", "AREA"); ld.shape = "RECTANGLE"
            ld.size, ld.size_y = (xb - xa) * (R_MID + ym) / R_MID, max(Wy, 0.1)
            ld.energy = max(15.0, ld.size * ld.size_y * w_per_m2); ld.color = (1.0, 0.9, 0.78)
            o = bpy.data.objects.new(f"IN_lamp{n}", ld); bpy.context.scene.collection.objects.link(o)
            th = TH0 + xm / R_MID
            o.matrix_world = Matrix.Translation(c) @ Matrix.Rotation(th + math.pi / 2, 4, "Z")
            n += 1
    FFL[0] = 0.0
    return n


def settings(samples=64, res=(1920, 1080), exposure=0.0):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res; sc.render.resolution_percentage = 100
    sc.render.use_freestyle = False
    sc.eevee.taa_render_samples = samples
    for k, v in (("use_raytracing", True), ("use_shadows", True)):
        try: setattr(sc.eevee, k, v)
        except Exception: pass
    try: sc.eevee.ray_tracing_options.resolution_scale = "1"
    except Exception: pass
    sc.view_settings.view_transform = "AgX"; sc.view_settings.exposure = exposure
    try: sc.view_settings.look = "AgX - Medium High Contrast"
    except Exception: pass
    world_stars()


def visible(shell_rooms):
    for c in bpy.context.scene.collection.children:
        c.hide_render = c.name not in ("Outpost", "OutpostInterior", "EarthView")
    bpy.data.objects["Ring_Rooms"].hide_render = not shell_rooms
    bpy.data.collections["OutpostInterior"].hide_render = shell_rooms


def view(name, deck, x, y, yaw=0.0, pitch=0.0, lens=18.0, eye=1.65, exposure=0.0, samples=64, res=(1920, 1080), animate=False):
    FFL[0] = DATA[deck]["ffl"]
    cam = camera(R_MID, x, y, eye=eye, yaw=yaw, pitch=pitch, lens=lens)
    cam.data.clip_end = 8000.0
    FFL[0] = 0.0
    lamps_near(ACC, cam.matrix_world.translation)
    g = bpy.data.materials["WinGlass"]; g.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.14
    gb = g.node_tree.nodes["Principled BSDF"]; gb.inputs["IOR"].default_value = 1.2; gb.inputs["Specular IOR Level"].default_value = 0.25
    for cls, st in (("light", 4.0), ("glass", 1.2)):
        m_ = bpy.data.materials.get("IN_" + cls)
        if m_: m_.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = st
    visible(False)
    settings(samples, res, exposure)
    os.makedirs(OUTR, exist_ok=True)
    sc = bpy.context.scene
    if animate:
        # PNG frames (Blender 5.x moved video output); encode afterwards with ffmpeg
        sc.render.image_settings.file_format = "PNG"
        os.makedirs(os.path.join(OUTR, name), exist_ok=True)
        sc.render.filepath = os.path.join(OUTR, name, "f_####")
        bpy.ops.render.render(animation=True)
        return sc.render.filepath
    sc.render.image_settings.file_format = "PNG"
    sc.render.filepath = os.path.join(OUTR, name + ".png")
    bpy.ops.render.render(write_still=True)
    return sc.render.filepath


def orbit(frames=144, degrees=2.0):
    """Earth turns under the station (true rate is ~4 deg/min; this clip runs it about 30x)."""
    sc = bpy.context.scene; sc.frame_start, sc.frame_end = 1, frames; sc.render.fps = 24
    for nm in ("Earth", "Clouds"):
        o = bpy.data.objects[nm]; o.animation_data_clear()
        x0, y0, z0 = EARTH_ROT
        o.rotation_euler = (x0, y0, z0); o.keyframe_insert("rotation_euler", frame=1)
        o.rotation_euler = (x0, y0, z0 + math.radians(degrees) * (1.0 if nm == "Earth" else 1.08)); o.keyframe_insert("rotation_euler", frame=frames)
        act = o.animation_data.action
        fcs = getattr(act, "fcurves", None)
        if fcs is None:          # Blender 4.4+: layered actions keep their curves in channel bags
            fcs = [fc for lay in act.layers for st in lay.strips for cb in st.channelbags for fc in cb.fcurves]
        for fc in fcs:
            for kp in fc.keyframe_points: kp.interpolation = "LINEAR"


COLLIDE = ("floor", "wall", "hull", "eqpm", "furn", "frame")       # Int_<cls>: what you can stand on / bump into


def godot_extras():
    """interior_lights.json beside the .glb: ceiling-light points (Godot coords: x, z_blender, -y_blender) for the walker,
    plus where the Earth and the sun are, so the game's view out of the windows matches the renders."""
    pts = []
    for (x0, y0, x1, y1, h0, h1, ffl) in ACC.lights:
        Lx, Wy = abs(x1 - x0), abs(y1 - y0)
        if Lx * Wy < 0.04: continue
        n = max(1, int(Lx / 3.0 + 0.5))
        for k in range(n):
            xm = min(x0, x1) + Lx * (k + 0.5) / n; ym = (y0 + y1) / 2
            th = TH0 + xm / R_MID; r = R_MID + ym; z = ffl + min(h0, h1) - 0.08
            pts.append([round(r * math.cos(th), 2), round(z, 2), round(-r * math.sin(th), 2)])
    s = bpy.data.objects["IN_Sun"].matrix_world.to_3x3() @ Vector((0, 0, -1))       # direction the light travels
    e = EARTH_DIR * 6771000 * EARTH_SCALE
    sth = TH0 + (3.5 * R_MID * math.radians(30) - 6.5) / R_MID      # spawn: open floor of the C04 concourse, facing along the ring
    out = {"lights": pts, "sun_dir": [s.x, s.z, -s.y], "earth_pos": [e.x, e.z, -e.y], "earth_r": 6371000 * EARTH_SCALE,
           "spawn": [round(40 * math.cos(sth), 3), DATA["A"]["ffl"] + 1.0, round(-40 * math.sin(sth), 3)], "spawn_yaw": sth}
    json.dump(out, open(SF + "/assets/stations/outpost/interior_lights.json", "w"))
    return len(pts)


def export_interior():
    for cn in ("Outpost", "OutpostInterior"):        # hidden objects can't be selected, so they'd silently drop out
        bpy.data.collections[cn].hide_viewport = False
    # Godot makes a static collision body for nodes named *-col (the system view strips them again)
    renamed = []
    for o in list(bpy.data.collections["OutpostInterior"].objects) + [bpy.data.objects["Ring_Windows"]]:
        if o.name == "Ring_Windows" or o.name[4:] in COLLIDE:
            old = o.name; o.name = old + "-col"; renamed.append((o, old))
    godot_extras()
    for o in bpy.context.scene.objects: o.select_set(False)
    bpy.data.objects["Outpost"].select_set(True)
    for o in bpy.data.collections["Outpost"].objects:
        if o.name != "Ring_Rooms": o.select_set(True)
    for o in bpy.data.collections["OutpostInterior"].objects: o.select_set(True)
    path = SF + "/assets/stations/outpost/outpost_interior.glb"
    try:
        bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True, export_lights=True)
    finally:
        for o, old in renamed: o.name = old
    return path


LIGHTS_JSON = SF + "/docs/blueprints/sketch/interior_lights.json"
class _Lights: pass
if REBUILD:
    ACC = build_interior()
    json.dump(ACC.lights, open(LIGHTS_JSON, "w"))
    TO_SUN = sun()
    earth(TO_SUN)
else:
    ACC = _Lights(); ACC.lights = [tuple(l) for l in json.load(open(LIGHTS_JSON))]
    if globals().get("REEARTH"): TO_SUN = sun(); earth(TO_SUN)
result = {"lights": len(ACC.lights)}
