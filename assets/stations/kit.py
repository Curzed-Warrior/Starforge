"""
Starforge station kit: shared building blocks for every station design (run inside Blender).

    exec(open(r".../assets/stations/kit.py").read())
    col, root = setup("Outpost")
    ... build with box / cyl / revolve / greeble / truss / antennas / lamp ...
    export(col, root, "outpost")

Everything is in metres, Z up. Faces always point out of the solid they bound, so Godot can cull back faces.
Materials use the tiling texture sets from make_textures.py (one repeat = TILE metres), wired the way the
glTF exporter understands (albedo, ORM, normal map). Meshes named "...-col" get trimesh collision in Godot.
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

STATIONS = r"C:\Users\diamo\Desktop\starforge\assets\stations"
TEX = os.path.join(STATIONS, "textures")
TILE = 8.0

def Rz(a): return Matrix.Rotation(a, 4, "Z")
def Rx(a): return Matrix.Rotation(a, 4, "X")
def Ry(a): return Matrix.Rotation(a, 4, "Y")

# ------------------------------------------------------------------ scene
def setup(name):
    """Hide every other station collection, clear and return (collection, root empty) for this one."""
    for c in bpy.context.scene.collection.children:
        c.hide_render = c.hide_viewport = c.name != name and c.name not in ("Collection",)
    col = bpy.data.collections.get(name)
    if col:
        for o in list(col.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    col.hide_render = col.hide_viewport = False
    for o in list(bpy.data.objects):
        if o.name == "Cube":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0: bpy.data.meshes.remove(m)
    for l in list(bpy.data.lights):
        if l.users == 0: bpy.data.lights.remove(l)
    root = bpy.data.objects.new(name, None)
    col.objects.link(root)
    global _col, _root
    _col, _root = col, root
    return col, root

def show(name):
    """Show only this station's collection (for renders)."""
    for c in bpy.context.scene.collection.children:
        c.hide_render = c.hide_viewport = c.name != name

# ------------------------------------------------------------------ materials
def _image(name, data=False):
    path = os.path.join(TEX, name + ".png")
    img = bpy.data.images.get(name + ".png") or bpy.data.images.load(path, check_existing=True)
    if os.path.normcase(bpy.path.abspath(img.filepath)) != os.path.normcase(path):
        img.filepath = path; img.reload()
    img.colorspace_settings.name = "Non-Color" if data else "sRGB"
    return img

def mat(name, color=(0.5, 0.5, 0.5), metal=0.0, rough=0.5, emit=None, strength=0.0, alpha=1.0, tex=None, tint=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
            nt.nodes.remove(n)
    b = nt.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    b.inputs["Emission Strength"].default_value = 0.0
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = strength
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
        m.surface_render_method = "BLENDED"
    m.use_backface_culling = alpha >= 1
    if tex:
        alb = nt.nodes.new("ShaderNodeTexImage"); alb.image = _image(tex + "_albedo")
        if tint:
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            mix.inputs[7].default_value = (*tint, 1)
            nt.links.new(alb.outputs[0], mix.inputs[6]); nt.links.new(mix.outputs[2], b.inputs["Base Color"])
        else:
            nt.links.new(alb.outputs[0], b.inputs["Base Color"])
        orm = nt.nodes.new("ShaderNodeTexImage"); orm.image = _image(tex + "_orm", True)
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(orm.outputs[0], sep.inputs[0])
        nt.links.new(sep.outputs[1], b.inputs["Roughness"])
        nt.links.new(sep.outputs[2], b.inputs["Metallic"])
        nrm = nt.nodes.new("ShaderNodeTexImage"); nrm.image = _image(tex + "_normal", True)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(nrm.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    return m

M = {
    "Hull":       mat("Hull", tex="hull"),
    "HullDark":   mat("HullDark", tex="hulldark"),
    "Trim":       mat("Trim", tex="hull", tint=(0.75, 0.32, 0.16)),
    "TrimBlue":   mat("TrimBlue", tex="hull", tint=(0.35, 0.5, 0.75)),
    "Glass":      mat("Glass", (0.4, 0.55, 0.65), 0.0, 0.03, alpha=0.2),
    "Floor":      mat("Floor", tex="floor"),
    "Wall":       mat("Wall", tex="wall"),
    "Ceiling":    mat("Ceiling", tex="ceiling"),
    "LightWarm":  mat("LightWarm", (1, 0.9, 0.75), emit=(1, 0.86, 0.68), strength=12),
    "LightCool":  mat("LightCool", (0.8, 0.9, 1), emit=(0.7, 0.85, 1), strength=10),
    "Window":     mat("Window", (0.9, 0.75, 0.5), emit=(1, 0.75, 0.45), strength=5),
    "WindowCool": mat("WindowCool", (0.6, 0.8, 1), emit=(0.55, 0.8, 1), strength=6),
    "Glow":       mat("Glow", (1, 0.4, 0.08), emit=(1, 0.32, 0.04), strength=4),   # furnace light through openings
    "NavRed":     mat("NavRed", (1, 0.1, 0.05), emit=(1, 0.08, 0.03), strength=40),
    "NavGreen":   mat("NavGreen", (0.1, 1, 0.2), emit=(0.05, 1, 0.15), strength=40),
    "NavWhite":   mat("NavWhite", (1, 1, 1), emit=(1, 1, 1), strength=60),
    "Screen":     mat("Screen", (0.1, 0.4, 0.6), emit=(0.2, 0.65, 1), strength=6),
    "Solar":      mat("Solar", (0.04, 0.07, 0.18), 0.7, 0.18),
    "Radiator":   mat("Radiator", (0.16, 0.16, 0.18), 0.6, 0.45),     # no glow: the game brightens any emission four-fold
    "Plant":      mat("Plant", (0.12, 0.38, 0.08), 0.0, 0.7),
    "Soil":       mat("Soil", (0.16, 0.1, 0.06), 0.0, 0.9),
    "Grow":       mat("Grow", (1, 0.5, 0.9), emit=(1, 0.35, 0.85), strength=2.2),
    "Fabric":     mat("Fabric", (0.35, 0.42, 0.5), 0.0, 0.9),
}

# ------------------------------------------------------------------ mesh helpers
def uv_map(bm, mode):
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        if f.tag: continue
        n = f.normal
        if mode == "cyl" and abs(n.z) < 0.7:
            c = f.calc_center_median()
            a0 = math.atan2(c.y, c.x)
            for l in f.loops:
                p = l.vert.co
                da = (math.atan2(p.y, p.x) - a0 + math.pi) % (2 * math.pi) - math.pi
                l[uv].uv = (((a0 + da) * math.hypot(p.x, p.y)) / TILE, p.z / TILE)
        else:
            ax = max(range(3), key=lambda i: abs(n[i]))
            u, v = [(1, 2), (0, 2), (0, 1)][ax]
            for l in f.loops:
                l[uv].uv = (l.vert.co[u] / TILE, l.vert.co[v] / TILE)

def obj(name, bm, mats, smooth=False, uv="box", parent=None):
    """Turn a bmesh into an object. mats: names from M, in material-index order. Unused slots are dropped."""
    bm.normal_update()
    uv_map(bm, uv)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in mats:
        me.materials.append(M[m])
    for p in me.polygons: p.use_smooth = smooth
    old = bpy.data.objects.get(name)
    if old and old.type == "MESH": old.name = name + "~"        # names are global in the .blend: the build being exported gets the exact one the game looks for
    o = bpy.data.objects.new(name, me)
    _col.objects.link(o)
    o.parent = parent or _root
    return o

def since(bm, before):
    """The vertices added to bm since before = set(bm.verts) was taken (bmesh reuses freed slots, so order lies)."""
    return [v for v in bm.verts if v not in before]

def tag(faces, mi):
    for f in faces: f.material_index = mi

def box(bm, size, at, rot=Matrix.Identity(4), mi=0):
    vs = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    bmesh.ops.scale(bm, vec=Vector(size), verts=vs)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(at)) @ rot, verts=vs)
    tag({f for v in vs for f in v.link_faces}, mi)
    return vs

def cyl(bm, r1, r2, depth, at, rot=Matrix.Identity(4), seg=24, mi=0, caps=True):
    vs = bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth)["verts"]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(at)) @ rot, verts=vs)
    tag({f for v in vs for f in v.link_faces}, mi)
    return vs

def sphere(bm, r, at, mi=0, seg=16):
    vs = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2, radius=r)["verts"]
    bmesh.ops.translate(bm, vec=Vector(at), verts=vs)
    tag({f for v in vs for f in v.link_faces}, mi)
    return vs

def revolve(bm, profile, steps, mat_of=lambda i, s: 0, closed=True, toward=None, skip=lambda i, s: False, a0=0.0, a1=2 * math.pi, cap=None):
    """Revolve (r, z) points around Z (optionally only an arc a0..a1). toward(centre) -> the direction faces
    should point; faces are flipped to match. skip(edge, step) leaves holes. cap=material index closes the
    two ends of a partial arc of a closed profile."""
    n = len(profile)
    full = abs(a1 - a0 - 2 * math.pi) < 1e-6
    rings = []
    for s in range(steps + (0 if full else 1)):
        a = a0 + (a1 - a0) * s / steps
        rings.append([bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for r, z in profile])
    made = []
    for s in range(steps):
        A, B = rings[s], rings[(s + 1) % len(rings)]
        for i in range(n if closed else n - 1):
            if skip(i, s): continue
            j = (i + 1) % n
            f = bm.faces.new((A[i], A[j], B[j], B[i]))
            f.material_index = mat_of(i, s)
            made.append(f)
    if toward:
        bm.normal_update()
        flip = [f for f in made if f.normal.dot(toward(f.calc_center_median())) < 0]
        if flip: bmesh.ops.reverse_faces(bm, faces=flip)
    if cap is not None and closed and not full:
        for ring, a, sgn in ((rings[0], a0, -1), (rings[-1], a1, 1)):
            f = bm.faces.new(ring)
            f.material_index = cap
            f.normal_update()
            if f.normal.dot(Vector((-math.sin(a), math.cos(a), 0)) * sgn) < 0: f.normal_flip()
            made.append(f)
    return made

def tube_dir(rc, zc=0.0):
    """Direction from a torus tube's centreline (radius rc, height zc) out to a point."""
    def f(c):
        rr = math.hypot(c.x, c.y) or 1e-6
        return Vector((c.x / rr * (rr - rc), c.y / rr * (rr - rc), c.z - zc)).normalized()
    return f

def radial(c):
    v = Vector((c.x, c.y, 0)); return v.normalized() if v.length > 1e-6 else Vector((1, 0, 0))

def ring_bevel(rin, rout, z0, z1):
    """The chamfer solid_ring cuts off each corner of its section."""
    return 0.14 * min(rout - rin, z1 - z0)

def solid_ring(bm, rin, rout, z0, z1, steps, mats=(0, 0, 0, 0), a0=0.0, a1=2 * math.pi, bevel=None):
    """A closed ring with a chamfered (octagonal) section, faces outward. mats: (inner wall, bottom, outer wall, top);
    the chamfers take the outer wall's material. Greeble surfaces should keep ring_bevel() clear of each edge."""
    c = ring_bevel(rin, rout, z0, z1) if bevel is None else bevel
    if c <= 1e-4:
        prof = [(rin, z0), (rout, z0), (rout, z1), (rin, z1)]
        mo = {0: mats[1], 1: mats[2], 2: mats[3], 3: mats[0]}
    else:
        prof = [(rin, z0 + c), (rin + c, z0), (rout - c, z0), (rout, z0 + c), (rout, z1 - c), (rout - c, z1), (rin + c, z1), (rin, z1 - c)]
        mo = {0: mats[2], 1: mats[1], 2: mats[2], 3: mats[2], 4: mats[2], 5: mats[3], 6: mats[2], 7: mats[0]}
    full = abs(a1 - a0 - 2 * math.pi) < 1e-6
    revolve(bm, prof, steps, lambda i, s: mo[i], toward=tube_dir((rin + rout) / 2, (z0 + z1) / 2), a0=a0, a1=a1,
            cap=None if full else mats[2])
    return c

def tunnel(bm, size, wall, at, rot, mi_out=0, mi_in=1, mi_floor=None):
    """An open-ended square tube along local X: hull outside, lining inside (faces pointing in)."""
    L, w, h = size
    o = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    bmesh.ops.scale(bm, vec=Vector((L, w + 2 * wall, h + 2 * wall)), verts=o)
    i = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    bmesh.ops.scale(bm, vec=Vector((L, w, h)), verts=i)
    bm.normal_update()
    ends = [f for f in {f for v in o + i for f in v.link_faces} if abs(f.normal.x) > 0.9]
    bmesh.ops.delete(bm, geom=ends, context="FACES")
    inner = list({f for v in i if v.is_valid for f in v.link_faces})
    bmesh.ops.reverse_faces(bm, faces=inner)
    tag({f for v in o if v.is_valid for f in v.link_faces}, mi_out)
    tag(inner, mi_in)
    bm.normal_update()
    if mi_floor is not None:
        tag([f for f in inner if f.normal.z > 0.9], mi_floor)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(at)) @ rot, verts=[v for v in o + i if v.is_valid])

# ------------------------------------------------------------------ surfaces for greebling
# A surface is f(u, v) -> (point, normal, tangent), with u, v in [0, 1).
def cyl_surface(r, z0, z1, a0=0.0, a1=2 * math.pi, inward=False):
    def f(u, v):
        a = a0 + (a1 - a0) * u
        n = Vector((math.cos(a), math.sin(a), 0))
        return n * r + Vector((0, 0, z0 + (z1 - z0) * v)), -n if inward else n, Vector((-math.sin(a), math.cos(a), 0))
    f.area = abs(a1 - a0) * r * abs(z1 - z0)
    return f

def disc_surface(r0, r1, z, up=True, a0=0.0, a1=2 * math.pi):
    def f(u, v):
        a = a0 + (a1 - a0) * u
        r = math.sqrt(r0 * r0 + (r1 * r1 - r0 * r0) * v)    # uniform by area
        return Vector((math.cos(a) * r, math.sin(a) * r, z)), Vector((0, 0, 1 if up else -1)), Vector((-math.sin(a), math.cos(a), 0))
    f.area = abs(a1 - a0) / 2 * (r1 * r1 - r0 * r0)
    return f

def cone_surface(r0, z0, r1, z1, a0=0.0, a1=2 * math.pi):
    def f(u, v):
        a = a0 + (a1 - a0) * u
        r = r0 + (r1 - r0) * v; z = z0 + (z1 - z0) * v
        d = Vector((math.cos(a), math.sin(a), 0))
        slope = Vector((d.x * (r1 - r0), d.y * (r1 - r0), z1 - z0)).normalized()
        t = Vector((-math.sin(a), math.cos(a), 0))
        return d * r + Vector((0, 0, z)), t.cross(slope).normalized(), t
    f.area = abs(a1 - a0) * (r0 + r1) / 2 * math.hypot(r1 - r0, z1 - z0)
    return f

def plane_surface(origin, du, dv):
    """A flat parallelogram: origin + u*du + v*dv, normal du x dv."""
    origin, du, dv = Vector(origin), Vector(du), Vector(dv)
    n = du.cross(dv).normalized(); t = du.normalized()
    def f(u, v): return origin + du * u + dv * v, n, t
    f.area = du.cross(dv).length
    return f

def _fin_surface(rot, z0, z1, reach):
    """Both faces of a blade fin, as one surface (alternates sides)."""
    a = plane_surface(rot @ Vector((5, 0.3, z0)), rot @ Vector((reach * 0.6, 0, 0)), Vector((0, 0, z1 - z0)))
    b = plane_surface(rot @ Vector((5, -0.3, z1)), rot @ Vector((reach * 0.6, 0, 0)), Vector((0, 0, z0 - z1)))
    flip = [0]
    def f(u, v):
        flip[0] ^= 1
        return a(u, v) if flip[0] else b(u, v)
    f.area = a.area * 2
    return f

def _tube_top(a, r0, r1, h):
    rot = Rz(a)
    return plane_surface(rot @ Vector((r0, -2.0, h)), rot @ Vector((r1 - r0, 0, 0)), rot @ Vector((0, 4.0, 0)))

def _side_surface(c, rot, L, h, half):
    """Both long sides of a box hull centred at c, oriented by rot (length along local X)."""
    a = plane_surface(c + rot @ Vector((-L / 2, half, -h / 2)), rot @ Vector((L, 0, 0)), rot @ Vector((0, 0, h)))
    b = plane_surface(c + rot @ Vector((L / 2, -half, -h / 2)), rot @ Vector((-L, 0, 0)), rot @ Vector((0, 0, h)))
    flip = [0]
    def f(u, v):
        flip[0] ^= 1
        return a(u, v) if flip[0] else b(u, v)
    f.area = a.area * 2
    return f

_UV_FACES = ((3, 2, 1, 0), (5, 6, 7, 4), (1, 5, 4, 0), (2, 6, 5, 1), (3, 7, 6, 2), (0, 4, 7, 3))     # wound outward (glTF culls back faces)
def fast_box(bm, size, at, rot, mi, uv, bottom=False):
    """A box straight into the bmesh (no operators): five faces (bottom skipped, it sits on the hull), UVs inline.
    rot: 3x3 or 4x4 whose columns are the box's local x, y, z (z = up, away from the surface)."""
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    x = Vector((rot[0][0], rot[1][0], rot[2][0])) * hx
    y = Vector((rot[0][1], rot[1][1], rot[2][1])) * hy
    z = Vector((rot[0][2], rot[1][2], rot[2][2])) * hz
    c = Vector(at)
    v = [bm.verts.new(c + sx * x + sy * y + sz * z) for (sx, sy, sz) in
         ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))]
    w, d, h = size[0] / TILE, size[1] / TILE, size[2] / TILE
    # faces: bottom, top, -y, +x, +y, -x ; uv extents per face
    ext = ((w, d), (w, d), (w, h), (d, h), (w, h), (d, h))
    for k, idx in enumerate(_UV_FACES):
        if k == 0 and not bottom: continue
        f = bm.faces.new([v[i] for i in idx])
        f.material_index = mi
        f.tag = True                    # UVs done; obj() leaves it alone
        eu, ev = ext[k]
        for l, (a, b) in zip(f.loops, ((0, 0), (eu, 0), (eu, ev), (0, ev))):
            l[uv].uv = (a + c.x / TILE, b + c.z / TILE)

def fast_prism(bm, outline, h, at, rot, mi, uv, top=(1.0, 1.0, 0.0, 0.0), base_cap=False):
    """Extrude a CCW outline (local x, y) along local z by h, straight into the bmesh. top=(sx, sy, ox, oy) scales
    and shifts the top face, giving sloped, tapered and wedge shapes. rot's columns are local x, y, z."""
    X = Vector((rot[0][0], rot[1][0], rot[2][0])); Y = Vector((rot[0][1], rot[1][1], rot[2][1])); Z = Vector((rot[0][2], rot[1][2], rot[2][2]))
    c = Vector(at)
    sx, sy, ox, oy = top
    bot = [bm.verts.new(c + X * px + Y * py) for px, py in outline]
    tv = [bm.verts.new(c + X * (px * sx + ox) + Y * (py * sy + oy) + Z * h) for px, py in outline]
    n = len(outline)
    o2 = (c.x + c.y) / TILE
    f = bm.faces.new(tv); f.material_index = mi; f.tag = True
    for l, (px, py) in zip(f.loops, outline): l[uv].uv = (px / TILE + o2, py / TILE + c.z / TILE)
    run = 0.0
    for i in range(n):
        j = (i + 1) % n
        e = math.hypot(outline[j][0] - outline[i][0], outline[j][1] - outline[i][1]) / TILE
        f = bm.faces.new((bot[i], bot[j], tv[j], tv[i])); f.material_index = mi; f.tag = True
        for l, uvv in zip(f.loops, ((run, 0), (run + e, 0), (run + e, h / TILE), (run, h / TILE))): l[uv].uv = (uvv[0] + o2, uvv[1])
        run += e
    if base_cap:
        f = bm.faces.new(list(reversed(bot))); f.material_index = mi; f.tag = True
        for l in f.loops: l[uv].uv = (0, 0)

def octo(w, d, k):
    """A rectangle with its corners cut off (CCW)."""
    k = min(k, w * 0.45, d * 0.45)
    w2, d2 = w / 2, d / 2
    return [(-w2 + k, -d2), (w2 - k, -d2), (w2, -d2 + k), (w2, d2 - k), (w2 - k, d2), (-w2 + k, d2), (-w2, d2 - k), (-w2, -d2 + k)]

def ellipse(w, d, n=10):
    return [(math.cos(i * 2 * math.pi / n) * w / 2, math.sin(i * 2 * math.pi / n) * d / 2) for i in range(n)]

def arch(r, n=7):
    """A half-disc standing on the x axis (CCW), for cowls and conduits lying along the surface."""
    return [(math.cos(math.pi * i / n) * r, math.sin(math.pi * i / n) * r) for i in range(n + 1)]

def greeble(bm, surf, density, seed, size=(0.6, 3.0), height=(0.15, 1.2), mats=(0, 1), window=None, glow=None,
            window_frac=0.06, glow_frac=0.0, stack=0.25, pipes=0.04, dark=0.15):
    """Scatter kitbash detail over a surface: chamfered plates with sloped sides, wedges, round pods, hexagonal
    housings and curved cowlings (only a few plain boxes), some stacked, some with lit slots or glowing vents,
    and runs of pipe. density = pieces per square metre."""
    rnd = random.Random(seed)
    uv = bm.loops.layers.uv.verify()
    n = int(surf.area * density)
    for _ in range(n):
        p, nrm, t = surf(rnd.random(), rnd.random())
        b = nrm.cross(t).normalized()
        rot = Matrix((t, b, nrm)).transposed()
        w = rnd.uniform(*size) * (1.6 if rnd.random() < 0.3 else 1.0)
        d = rnd.uniform(*size) * rnd.uniform(0.3, 1.0)
        h = rnd.uniform(*height)
        r = rnd.random()
        if pipes and r < pipes:
            L = rnd.uniform(4, 12)
            cyl(bm, h * 0.3, h * 0.3, L, p + nrm * h * 0.35, rot.to_4x4() @ Ry(math.pi / 2), seg=6, mi=mats[1])
            continue
        mi = mats[rnd.random() < dark]
        shape = rnd.random()
        if shape < 0.42:          # armour plate: chamfered corners, sloped sides
            fast_prism(bm, octo(w, d, min(w, d) * rnd.uniform(0.15, 0.35)), h, p, rot, mi, uv, top=(1 - rnd.uniform(0.08, 0.25), 1 - rnd.uniform(0.15, 0.4), 0, 0))
        elif shape < 0.56:        # wedge: one end slopes down to the hull
            k = rnd.choice((-1, 1))
            fast_prism(bm, octo(w, d, min(w, d) * 0.15), h, p, rot, mi, uv, top=(0.35, 0.9, k * w * 0.3, 0))
        elif shape < 0.72:        # round pod with a domed taper
            e = ellipse(w * 0.8, d, 8)
            fast_prism(bm, e, h * 0.7, p, rot, mi, uv, top=(0.85, 0.85, 0, 0))
            fast_prism(bm, [(x * 0.85, y * 0.85) for x, y in e], h * 0.3, p + nrm * h * 0.7, rot, mi, uv, top=(0.45, 0.45, 0, 0))
        elif shape < 0.82:        # hexagonal housing
            fast_prism(bm, ellipse(min(w, d) * 1.1, min(w, d) * 1.1, 6), h, p, rot, mi, uv, top=(0.8, 0.8, 0, 0))
        elif shape < 0.94:        # curved cowling lying along the hull
            crot = Matrix((b, nrm, t)).transposed()
            fast_prism(bm, arch(min(d, h * 2) * 0.6), w * 1.4, p - t * w * 0.7, crot, mi, uv, base_cap=True)
            continue
        else:
            fast_box(bm, (w, d, h), p + nrm * h / 2, rot, mi, uv)
        if window is not None and r < pipes + window_frac:
            fast_prism(bm, octo(w * 0.7, 0.08 + d * 0.15, 0.03), 0.05, p + nrm * h * 0.98 - b * d * 0.1, rot, window, uv)
        elif glow is not None and r < pipes + window_frac + glow_frac:
            fast_prism(bm, ellipse(w * 0.45, d * 0.45, 8), 0.05, p + nrm * h * 0.96, rot, glow, uv)
        if rnd.random() < stack:
            w2, d2, h2 = w * rnd.uniform(0.3, 0.6), d * rnd.uniform(0.3, 0.6), h * rnd.uniform(0.4, 1.0)
            off = t * rnd.uniform(-0.15, 0.15) * w + b * rnd.uniform(-0.15, 0.15) * d
            fast_prism(bm, octo(w2, d2, min(w2, d2) * 0.3), h2, p + off + nrm * h * 0.85, rot, mats[rnd.random() < 0.5], uv, top=(0.7, 0.7, 0, 0))

def detail(bm, surf, seed, scale=1.0, **kw):
    """The usual two-tier kitbash: sparse large low plates, then dense fine detail on top."""
    greeble(bm, surf, 0.035 / (scale * scale), seed, size=(2.5 * scale, 6 * scale), height=(0.12 * scale, 0.35 * scale), stack=0.0, pipes=0.0,
            dark=0.1, mats=kw.get("mats", (0, 1)))
    greeble(bm, surf, 0.26 / (scale * scale), seed + 1, size=(0.35 * scale, 1.3 * scale), height=(0.08 * scale, 0.45 * scale), **kw)

def window_rows(bm, surf, rows, per_row, size=(1.2, 0.5), mi=0, seed=0, lit=0.75, v0=0.1, v1=0.9):
    """Rows of lit windows (some dark) across a surface."""
    rnd = random.Random(seed)
    for r in range(rows):
        v = v0 + (v1 - v0) * (r + 0.5) / rows
        for k in range(per_row):
            if rnd.random() > lit: continue
            p, nrm, t = surf((k + 0.5) / per_row, v)
            rot = Matrix((t, nrm.cross(t).normalized(), nrm)).transposed()
            fast_box(bm, (size[0], size[1], 0.08), p + nrm * 0.03, rot, mi, bm.loops.layers.uv.verify())

def truss(bm, a, b, w=1.2, seg=None, mi=0, rod=0.08):
    """A square lattice beam from a to b: four chords, rungs and diagonals."""
    a, b = Vector(a), Vector(b)
    d = b - a; L = d.length
    if L < 1e-3: return
    z = d.normalized()
    x = z.cross(Vector((0, 0, 1)) if abs(z.z) < 0.9 else Vector((1, 0, 0))).normalized()
    y = z.cross(x)
    seg = seg or max(2, int(L / w))
    rot = Matrix((x, y, z)).transposed().to_4x4()
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for cx, cy in corners:
        cyl(bm, rod, rod, L, a + d / 2 + (x * cx + y * cy) * w / 2, rot, seg=5, mi=mi)
    for k in range(seg + 1):
        c = a + d * k / seg
        for i in range(4):
            (x0, y0), (x1, y1) = corners[i], corners[(i + 1) % 4]
            p0 = c + (x * x0 + y * y0) * w / 2; p1 = c + (x * x1 + y * y1) * w / 2
            _rod(bm, p0, p1, rod * 0.7, mi)
            if k < seg:
                q1 = a + d * (k + 1) / seg + (x * x1 + y * y1) * w / 2
                _rod(bm, p0, q1, rod * 0.6, mi)

def _rod(bm, p0, p1, r, mi):
    d = p1 - p0
    if d.length < 1e-4: return
    q = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    cyl(bm, r, r, d.length, (p0 + p1) / 2, q, seg=4, mi=mi, caps=False)

def antennas(bm, at, up, n, height, seed, mi=0, light=None):
    """A cluster of masts, whip antennas and small dishes standing on a surface."""
    rnd = random.Random(seed)
    up = Vector(up).normalized()
    t = up.cross(Vector((0, 0, 1)) if abs(up.z) < 0.9 else Vector((1, 0, 0))).normalized()
    b = up.cross(t)
    rot = Matrix((t, b, up)).transposed().to_4x4()
    for k in range(n):
        off = (t * rnd.uniform(-1, 1) + b * rnd.uniform(-1, 1)) * height * 0.15
        h = height * rnd.uniform(0.3, 1.0)
        base = Vector(at) + off
        cyl(bm, h * 0.02, h * 0.008, h, base + up * h / 2, rot, seg=6, mi=mi)
        for j in range(rnd.randint(1, 4)):
            z = h * rnd.uniform(0.3, 0.9)
            box(bm, (h * rnd.uniform(0.08, 0.3), h * 0.01, h * 0.01), base + up * z, rot @ Rz(rnd.uniform(0, math.pi)), mi=mi)
        if light is not None:
            sphere(bm, h * 0.02, base + up * h, mi=light, seg=6)

# ------------------------------------------------------------------ ships and docking
UPX = Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))).transposed()   # loft's local X pointing along +Z

def loft(bm, sections, rot, at, mi=0, caps=True):
    """A hull lofted through octagonal sections [(x, half_width, half_height, chamfer_frac)] along local X.
    UPX (below) as rot lofts straight up the Z axis."""
    at = Vector(at)
    rings = []
    for x, hw, hh, k in sections:
        c = min(hw, hh) * k
        pts = [(hw - c, -hh), (hw, -hh + c), (hw, hh - c), (hw - c, hh), (-hw + c, hh), (-hw, hh - c), (-hw, -hh + c), (-hw + c, -hh)]
        rings.append([bm.verts.new(at + rot @ Vector((x, py, pz))) for py, pz in pts])
    made = []
    for A, B in zip(rings, rings[1:]):
        for i in range(8):
            j = (i + 1) % 8
            f = bm.faces.new((A[i], B[i], B[j], A[j])); f.material_index = mi; made.append(f)
    if caps:
        for ring in (rings[0], rings[-1]):
            f = bm.faces.new(ring); f.material_index = mi; made.append(f)
    bm.normal_update()
    # every face points away from the section's own centreline
    for f in made:
        c = f.calc_center_median()
        lx = (rot.transposed() @ (c - at)).x
        axis = at + rot @ Vector((lx, 0, 0))
        d = c - axis
        if d.length < 1e-6:     # an end cap: point along the hull's axis, away from the middle
            mid = (sections[0][0] + sections[-1][0]) / 2
            d = rot @ Vector((1 if lx > mid else -1, 0, 0))
        if f.normal.dot(d) < 0: f.normal_flip()
    return made

def ship(bm, at, rot, L, mats=(0, 1, 2, 3), seed=0):
    """A sleek angular ship, nose toward local +X: lofted hull, a raised spine, swept fins, engine bells with
    glowing drives, lit windows down the flanks. mats: (hull, dark, drive glow, windows)."""
    rnd = random.Random(seed)
    W = L * rnd.uniform(0.16, 0.22); H = W * rnd.uniform(0.55, 0.8)
    at = Vector(at)
    loft(bm, [(L * 0.5, 0.05 * W, 0.05 * H, 0.3), (L * 0.32, 0.45 * W, 0.4 * H, 0.35), (L * 0.05, 0.6 * W, 0.5 * H, 0.3),
              (-L * 0.25, 0.62 * W, 0.55 * H, 0.25), (-L * 0.42, 0.55 * W, 0.6 * H, 0.2), (-L * 0.5, 0.5 * W, 0.55 * H, 0.2)], rot, at, mats[0])
    loft(bm, [(L * 0.2, 0.05 * W, 0.4 * H, 0.3), (0, 0.18 * W, 0.75 * H, 0.3), (-L * 0.3, 0.2 * W, 0.8 * H, 0.3), (-L * 0.42, 0.12 * W, 0.6 * H, 0.3)], rot, at, mats[1])
    for sy in (-1, 1):
        # swept fins: a thin slab
        loft(bm, [(-L * 0.06, 0.04 * H, 0.03 * H, 0.0), (-L * 0.42, 0.04 * H, 0.03 * H, 0.0)],
             rot @ Matrix(((1, 0, 0), (0, 1, 0), (0, 0, 1))), at + rot @ Vector((0, sy * 0.9 * W, -0.1 * H)), mats[1])
        for sz in (-0.22, 0.22):
            e = at + rot @ Vector((-L * 0.5, sy * 0.28 * W, sz * H))
            r4 = rot.to_4x4() @ Ry(math.pi / 2)
            cyl(bm, 0.2 * W, 0.24 * W, 0.08 * L, e - rot @ Vector((0.04 * L, 0, 0)), r4, seg=10, mi=mats[1])
            cyl(bm, 0.17 * W, 0.17 * W, 0.01 * L, e - rot @ Vector((0.081 * L, 0, 0)), r4, seg=10, mi=mats[2])
    # fin panels: broad swept wings out to each side
    for sy in (-1, 1):
        q = [Vector((-L * 0.05, sy * 0.58 * W, -0.05 * H)), Vector((-L * 0.4, sy * 1.3 * W, -0.15 * H)),
             Vector((-L * 0.47, sy * 1.3 * W, -0.15 * H)), Vector((-L * 0.42, sy * 0.58 * W, -0.05 * H))]
        lo = [bm.verts.new(at + rot @ v) for v in q]
        hi = [bm.verts.new(at + rot @ (v + Vector((0, 0, 0.07 * H)))) for v in q]
        fs = [bm.faces.new(hi), bm.faces.new(list(reversed(lo)))]
        for i in range(4):
            j = (i + 1) % 4
            fs.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        bm.normal_update()
        cen = at + rot @ sum(q, Vector()) / 4
        for f in fs:
            f.material_index = mats[1]
            if f.normal.dot(f.calc_center_median() - cen) < 0: f.normal_flip()
    uv = bm.loops.layers.uv.verify()
    for sy in (-1, 1):
        n = rnd.randint(6, 14)
        for i in range(n):
            if rnd.random() < 0.25: continue
            x = L * (0.2 - 0.55 * (i + 0.5) / n)
            fast_box(bm, (L * 0.025, 0.02 * W, 0.07 * H), at + rot @ Vector((x, sy * 0.615 * W, 0.15 * H)), rot, mats[3], uv)
    fast_box(bm, (L * 0.06, 0.3 * W, 0.08 * H), at + rot @ Vector((L * 0.08, 0, 0.79 * H)), rot, mats[3], uv)

def hangar(bm, at, out, up, w, h, depth, mats=(0, 1, 2, 3, 4), ship_len=None, seed=0):
    """A docking bay standing out from a hull: an angular hood around a lit cavern, a landing apron with guide lights
    reaching out in front, and a ship berthed inside. at = middle of the bay's back wall on the hull; out = the way the
    mouth faces; up = the hood's roof direction. mats: (hull, dark, interior light, guide lights, drive glow)."""
    out = Vector(out).normalized(); up = Vector(up).normalized()
    side = up.cross(out).normalized(); up = out.cross(side).normalized()
    rot = Matrix((out, side, up)).transposed()          # local x = out, y = side, z = up
    at = Vector(at)
    uv = bm.loops.layers.uv.verify()
    t = max(w, h) * 0.08
    cen = at + out * depth / 2
    # hood: roof with a raked lip, floor deck, side walls
    loft(bm, [(-depth / 2, w / 2 + t * 1.4, t * 0.7, 0.45), (depth / 2, w / 2 + t * 1.4, t * 0.7, 0.45), (depth / 2 + t * 2.5, w / 2 + t * 0.4, t * 0.3, 0.45)], rot, cen + up * (h / 2 + t * 0.7), mats[0])
    loft(bm, [(-depth / 2, w / 2 + t * 1.4, t * 0.7, 0.45), (depth / 2, w / 2 + t * 1.4, t * 0.7, 0.45)], rot, cen - up * (h / 2 + t * 0.7), mats[1])
    for sy in (-1, 1):
        loft(bm, [(-depth / 2, t * 0.7, h / 2 + t * 1.4, 0.35), (depth / 2, t * 0.7, h / 2 + t * 1.4, 0.35), (depth / 2 + t * 1.8, t * 0.5, h / 2 * 0.8, 0.35)], rot, cen + side * sy * (w / 2 + t * 0.7), mats[0])
    # the lit back wall and ceiling light strips inside
    fast_box(bm, (0.3, w * 0.95, h * 0.95), at + out * 0.15, rot, mats[2], uv)
    for k in range(4):
        fast_box(bm, (depth * 0.9, 0.35, 0.15), cen + up * (h / 2 - 0.1) + side * (k - 1.5) * w / 4.5, rot, mats[2], uv)
    # the apron: a chamfered deck out in front, approach lights down its centre and edges
    apron = depth * 0.9
    fast_prism(bm, octo(apron, w * 0.9, w * 0.2), t * 0.6, at + out * (depth + apron / 2) - up * (h / 2 + t * 1.2), rot, mats[1], uv)
    for k in range(7):
        x = depth + apron * (k + 0.5) / 7
        for sy in (-1, 0, 1):
            fast_box(bm, (apron * 0.03, w * (0.06 if sy == 0 else 0.02), 0.2), at + out * x - up * (h / 2 + t * 0.55) + side * sy * w * 0.4, rot, mats[3], uv)
    # a lit frame round the mouth, and hull detail over the hood
    for sy in (-1, 1):
        fast_box(bm, (0.3, 0.3, h + t * 2), at + out * (depth + t * 0.3) + side * sy * (w / 2 + t * 0.2), rot, mats[2], uv)
    fast_box(bm, (0.3, w + t * 0.6, 0.3), at + out * (depth + t * 0.3) + up * (h / 2 + t * 0.2), rot, mats[2], uv)
    greeble(bm, plane_surface(at + up * (h / 2 + t * 1.4) - side * w / 2, out * depth, side * w), 0.35, seed + 1, size=(0.3, 1.4), height=(0.1, 0.5), mats=(mats[0], mats[1]))
    for sy in (-1, 1):
        o = at + side * sy * (w / 2 + t * 1.4) - up * h / 2
        surf = plane_surface(o, up * h, out * depth) if sy > 0 else plane_surface(o, out * depth, up * h)
        greeble(bm, surf, 0.35, seed + 2 + sy, size=(0.3, 1.4), height=(0.1, 0.5), mats=(mats[0], mats[1]))
    # a ship berthed inside, nose to the back wall
    L = depth * 0.7 if ship_len is None else ship_len
    if L > 0:
        ship(bm, cen - up * (h / 2 - L * 0.13) - out * depth * 0.05, Matrix((-out, -side, up)).transposed(), L, (mats[0], mats[1], mats[4], mats[2]), seed)
    return cen          # the middle of the bay, for a lamp

# ------------------------------------------------------------------ lights + export
def lamp(name, at, watts, color, radius=0.3, kind="POINT", aim=None, spot=1.2):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = watts; ld.color = color; ld.shadow_soft_size = radius
    if kind == "SPOT":
        ld.spot_size = spot; ld.spot_blend = 0.6
    lo = bpy.data.objects.new(name, ld)
    _col.objects.link(lo); lo.parent = _root
    lo.location = Vector(at)
    if aim is not None:
        lo.rotation_euler = (Vector(aim) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    return lo

WARM, COOL, GROW, FIRE = (1.0, 0.85, 0.7), (0.75, 0.88, 1.0), (1.0, 0.45, 0.85), (1.0, 0.45, 0.15)

def export(col, root, slug):
    """Save the .blend once (all stations live in it) and export this station to <slug>/<slug>.glb."""
    folder = os.path.join(STATIONS, slug)
    os.makedirs(folder, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(STATIONS, "stations.blend"))
    for o in bpy.context.scene.objects: o.select_set(False)
    root.select_set(True)
    for o in col.objects: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(folder, slug + ".glb"), export_format="GLB", use_selection=True,
                              export_apply=True, export_image_format="AUTO", export_lights=True)
    meshes = [o for o in col.objects if o.type == "MESH"]
    return {"objects": len(col.objects), "tris": sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)}

exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit2.py").read())
