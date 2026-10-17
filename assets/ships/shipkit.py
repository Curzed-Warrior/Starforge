"""
Ship kit v2: real-scale hulls for every class, in metres, nose toward +X, dorsal +Z, port +Y. Built on the station kit
(kit.py, kit2.py). Ships are NOT normalised any more: a corvette is 120 m long in the file, a titan twelve kilometres,
so a hull can one day be walked through and refitted. Human-scale rules every design follows:
    decks 3.6 m apart; windows 1.4 x 0.7 m in rows one deck apart; airlock doors 2.2 x 2.6 m; hangar doors sized to
    the craft they take; railings and ladders never modelled (interiors come later), but nothing on the hull may
    contradict a person standing next to it.
House style (humanity): angular, wedge and blade hulls after the Stellaris Imperial set and the Star Wars kitbash
look: light weathered grey plate over darker trenches, dense greebles in the trenches, big round engine clusters,
a raised bridge, a little signal red. Every class uses the same parts so the fleet reads as one navy.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())

SHIPS = r"C:/Users/diamo/Desktop/starforge/assets/ships"
DECK = 3.6
M["DriveBlue"] = mat("DriveBlue", (0.35, 0.6, 1.0), emit=(0.25, 0.5, 1.0), strength=1.4)
M["EngineIn"] = mat("EngineIn", (0.08, 0.08, 0.09), 0.8, 0.5)
if "Panel" not in M: M["Panel"] = mat("Panel", (0.42, 0.43, 0.45), 0.6, 0.5)
for _k in ("HullLight", "Accent"):
    if _k not in M: M[_k] = bpy.data.materials.get(_k) or mat(_k, (0.8, 0.8, 0.8))
def painted(src, name, metal=0.15, tint=None, rough=0.62):
    """A painted (mostly non-metallic) copy of a station material: flat painted plate catches the light from any
    angle, where bare metal mirrors black space and turns flat hull faces black."""
    old = bpy.data.materials.get(name)
    if old is not None and old.get("painted_v") == 3:
        return old                                   # already made: never tint twice (each pass would darken it again)
    m = bpy.data.materials[src].copy()
    if old is not None:
        old.user_remap(m); bpy.data.materials.remove(old)
    m.name = name
    m["painted_v"] = 3
    b = m.node_tree.nodes.get("Principled BSDF")
    for sock, val in (("Metallic", metal), ("Roughness", rough)):
        for l in list(b.inputs[sock].links): m.node_tree.links.remove(l)
        b.inputs[sock].default_value = val
    if tint is not None:
        for l in list(b.inputs["Base Color"].links):
            mix = m.node_tree.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1.0
            mix.inputs[2].default_value = (*tint, 1)
            m.node_tree.links.new(l.from_socket, mix.inputs[1]); m.node_tree.links.remove(l)
            m.node_tree.links.new(mix.outputs[0], b.inputs["Base Color"])
    return m
M["ShipPaint"] = painted("HullLight", "ShipPaint", 0.1)
M["ShipPaint2"] = painted("Hull", "ShipPaint2", 0.2)
M["ShipTan"] = painted("HullLight", "ShipTan", 0.1, tint=(0.86, 0.8, 0.68))
M["ShipGun"] = painted("Hull", "ShipGun", 0.15, tint=(0.46, 0.47, 0.49), rough=0.55)
SM = ["ShipPaint", "ShipPaint2", "HullDark", "Accent", "DriveBlue", "Window", "NavRed", "NavGreen", "NavWhite", "EngineIn", "Glass", "Panel", "ShipTan", "ShipGun"]
(LIGHT, HULL, DARK, ACC, DRIVE, WIN, RED, GREEN, WHITE, ENGIN, GLASS, PANEL, TAN, GUN) = range(len(SM))
I3 = Matrix.Identity(3)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
UP = Matrix(((1, 0, 0), (0, 1, 0), (0, 0, 1)))
DOWN = Matrix(((1, 0, 0), (0, -1, 0), (0, 0, -1)))          # local z pointing down (ventral parts)
def UVL(bm): return bm.loops.layers.uv.verify()


def plate(bm, outline, z, h, mi=LIGHT, bevel=0.8, down=False, slope=(0.0, 0.0)):
    """An angular armour plate: a top-down outline [(x, y)] extruded h up from z (down: from z downward), its top
    face inset by bevel metres all round, so every edge is a chamfer. slope=(dx, dy) shifts the top (raked faces)."""
    # wind counter-clockwise whatever order the outline came in (a clockwise outline would face inward and be culled)
    area2 = sum(outline[i][0] * outline[(i + 1) % len(outline)][1] - outline[(i + 1) % len(outline)][0] * outline[i][1] for i in range(len(outline)))
    if area2 < 0: outline = list(reversed(outline))
    cx = sum(p[0] for p in outline) / len(outline); cy = sum(p[1] for p in outline) / len(outline)
    ext = max(max(abs(p[0] - cx) for p in outline), max(abs(p[1] - cy) for p in outline), 1e-3)
    k = max(0.05, 1 - bevel / ext)
    loc = [(x - cx, y - cy) for x, y in outline]
    rot = DOWN if down else UP
    at = Vector((cx, cy, z))
    fast_prism(bm, loc if not down else [(x, -y) for x, y in reversed(loc)], h, at, rot, mi, UVL(bm), top=(k, k, slope[0], slope[1]), base_cap=True)


def rect(x0, x1, y0, y1):
    """An axis-aligned rectangle as a counter-clockwise outline."""
    return [(x1, y0), (x1, y1), (x0, y1), (x0, y0)]


def mirror(pts):
    """Close a starboard half-outline [(x, y>=0)...] into a full symmetric outline."""
    return list(pts) + [(x, -y) for x, y in reversed(pts) if abs(y) > 1e-6]


def keel(bm, sections, mi=LIGHT, chamfer=0.38):
    """The main hull: an octagonal loft along X through [(x, half_width, half_height, z_centre)]."""
    made = []
    rings = []
    for x, hw, hh, zc in sections:
        c = min(hw, hh) * chamfer
        pts = [(hw - c, -hh), (hw, -hh + c), (hw, hh - c), (hw - c, hh), (-hw + c, hh), (-hw, hh - c), (-hw, -hh + c), (-hw + c, -hh)]
        rings.append([bm.verts.new(Vector((x, py, pz + zc))) for py, pz in pts])
    for A, B in zip(rings, rings[1:]):
        for i in range(8):
            j = (i + 1) % 8
            f = bm.faces.new((A[j], B[j], B[i], A[i])); f.material_index = mi; made.append(f)
    for ring, back in ((rings[0], True), (rings[-1], False)):
        f = bm.faces.new(ring if back else list(reversed(ring))); f.material_index = mi; made.append(f)
    bm.normal_update()
    for f in made:
        c = f.calc_center_median()
        # outward: away from the local axis at that x, or along X at the caps
        xs = [s[0] for s in sections]
        if abs(c.x - xs[0]) < 1e-3 or abs(c.x - xs[-1]) < 1e-3:
            d = Vector((-1 if abs(c.x - xs[0]) < 1e-3 else 1, 0, 0))
        else:
            # interpolate the centre z at c.x
            zc = sections[0][3]
            for (xa, _, _, za), (xb, _, _, zb) in zip(sections, sections[1:]):
                if min(xa, xb) - 1e-6 <= c.x <= max(xa, xb) + 1e-6:
                    t = (c.x - xa) / (xb - xa) if xb != xa else 0
                    zc = za + (zb - za) * t
            d = Vector((0, c.y, c.z - zc))
        if f.normal.dot(d) < 0: f.normal_flip()
    return made


def engine(bm, at, r, L, mi=HULL, glow=DRIVE, bands=3, seg=20):
    """A round engine pod pointing aft (-X): housing, banding collars, a flared bell, a recessed glowing core."""
    at = Vector(at)
    R = Ry(-math.pi / 2)                 # cylinder axis (local z) -> -X
    cyl(bm, r, r * 0.92, L, at, R, seg=seg, mi=mi)
    for k in range(bands):
        x = at.x + L * (0.35 - 0.5 * k / max(bands - 1, 1))
        cyl(bm, r * 1.06, r * 1.06, L * 0.04, Vector((x, at.y, at.z)), R, seg=seg, mi=DARK)
    cyl(bm, r * 0.9, r * 1.08, L * 0.22, at + Vector((-L * 0.6, 0, 0)), R, seg=seg, mi=DARK, caps=False)      # bell
    cyl(bm, r * 0.8, r * 0.8, 0.4, at + Vector((-L * 0.62, 0, 0)), R, seg=seg, mi=glow)                        # core
    cyl(bm, r * 0.35, r * 0.2, r * 0.6, at + Vector((-L * 0.6, 0, 0)), R, seg=10, mi=ENGIN)                   # spike


def turret(bm, at, s, twin=True, down=False, aim=0.0):
    """A gun turret, s = barrel length in metres: an angular base, a raked housing, one or two barrels."""
    at = Vector(at); sg = -1 if down else 1
    base = [(s * 0.35, 0), (s * 0.2, s * 0.32), (-s * 0.3, s * 0.32), (-s * 0.4, 0), (-s * 0.3, -s * 0.32), (s * 0.2, -s * 0.32)]
    plate(bm, [(at.x + x, at.y + y) for x, y in base], at.z, s * 0.18, HULL, s * 0.04, down)
    hz = at.z + sg * s * 0.18
    house = [(s * 0.3, 0), (s * 0.12, s * 0.22), (-s * 0.25, s * 0.22), (-s * 0.25, -s * 0.22), (s * 0.12, -s * 0.22)]
    plate(bm, [(at.x + x, at.y + y) for x, y in house], hz, s * 0.16, LIGHT, s * 0.05, down)
    for k in ((-1, 1) if twin else (0,)):
        p = Vector((at.x + s * 0.25 + s * 0.45, at.y + k * s * 0.08, hz + sg * s * 0.08))
        R = Ry(math.pi / 2) @ Rx(0)
        cyl(bm, s * 0.035, s * 0.028, s * 0.9, p, Rz(aim) @ R, seg=8, mi=DARK)


def bridge(bm, x, z, w, h, depth, mi=LIGHT):
    """A raised command block: raked front with a band of bridge windows, stepped back toward the stern."""
    plate(bm, mirror([(x, 0), (x - depth * 0.15, w * 0.5), (x - depth, w * 0.5)]), z, h, mi, h * 0.25, slope=(-depth * 0.06, 0))
    rows = max(1, int(h / DECK))
    for r in range(rows):
        zz = z + h * 0.35 + r * DECK
        if zz > z + h * 0.85: break
        for k in range(int(w * 0.8 / 2.2)):
            y = -w * 0.4 + 1.1 + k * 2.2
            fast_box(bm, (0.3, 1.4, 0.7), Vector((x - depth * 0.15 * abs(y) / (w * 0.5) - 0.05, y, zz)), UP, WIN, UVL(bm))


def flank(side):
    """Orientation for parts lying on a vertical flank facing +Y*side (local x along the hull, y up/down, z out)."""
    return Matrix((X_, Vector((0, 0, -side)), Vector((0, side, 0)))).transposed()


def windows_side(bm, x0, x1, y, z0, decks, side=1, lit=0.7, seed=0, spacing=2.4):
    """Rows of human-scale windows on a vertical flank (side=+1 faces port +Y, -1 starboard), one row per deck."""
    rnd = random.Random(seed)
    R = flank(side)
    for d in range(decks):
        z = z0 + d * DECK
        x = x0
        while x < x1:
            if rnd.random() < lit:
                fast_box(bm, (1.4, 0.7, 0.12), Vector((x, y, z)), R, WIN, UVL(bm))
            x += spacing


def airlock(bm, x, y, z, side=1):
    """An airlock door on a flank: a dark 2.2 x 2.6 m door in a light frame with a lit sill (z = deck level)."""
    R = flank(side)
    fast_box(bm, (3.4, 3.6, 0.2), Vector((x, y, z + 1.3)), R, HULL, UVL(bm))
    fast_box(bm, (2.2, 2.6, 0.3), Vector((x, y + side * 0.06, z + 1.3)), R, DARK, UVL(bm))
    fast_box(bm, (2.2, 0.15, 0.36), Vector((x, y + side * 0.08, z - 0.05)), R, WIN, UVL(bm))


def trench(bm, x0, x1, y0, y1, z, depth, seed, scale=1.0):
    """A recessed greeble trench between two plates: a dark floor packed with kitbash."""
    plate(bm, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z - depth, depth * 0.6, DARK, 0.01)
    surf = plane_surface((x0, y0, z - depth * 0.4), (x1 - x0, 0, 0), (0, y1 - y0, 0))
    greeble(bm, surf, 0.5 / (scale * scale), seed, size=(0.4 * scale, 1.8 * scale), height=(0.2 * scale, depth * 0.9), mats=(HULL, DARK),
            window=WIN, glow=None, window_frac=0.04, stack=0.35, pipes=0.12, dark=0.45)


def panels(bm, surf, seed, scale=1.0, accent=0.04, density=1.0):
    """Low panel plates over a hull face (the light/mid-grey patchwork of a kitbash model), a few accent ones."""
    # big, low, clean plates (the eye rests on these), a few tan ones, and a light dusting of fine detail
    greeble(bm, surf, 0.03 * density / (scale * scale), seed, size=(5 * scale, 12 * scale), height=(0.05 * scale, 0.12 * scale),
            mats=(LIGHT, HULL), stack=0.0, pipes=0.0, dark=0.3)
    greeble(bm, surf, 0.006 * density / (scale * scale), seed + 3, size=(5 * scale, 10 * scale), height=(0.04 * scale, 0.1 * scale),
            mats=(TAN, LIGHT), stack=0.0, pipes=0.0, dark=0.0)
    greeble(bm, surf, 0.06 * density / (scale * scale), seed + 7, size=(0.35 * scale, 1.4 * scale), height=(0.06 * scale, 0.4 * scale),
            mats=(HULL, DARK), window=WIN, window_frac=0.02, stack=0.15, pipes=0.02, dark=0.3)
    rnd = random.Random(seed + 99)
    for _ in range(int(surf.area * 0.004 * accent / 0.04)):
        p, n, t = surf(rnd.random(), rnd.random())
        fast_box(bm, (rnd.uniform(2, 6) * scale, rnd.uniform(1, 2.5) * scale, 0.15), p + n * 0.1, Matrix((t, n.cross(t), n)).transposed(), ACC, UVL(bm))


def nav(bm, port, starboard, tops=()):
    """Red to port, green to starboard, white strobes on top."""
    for p in port: strobe(bm, p, 0.5, RED)
    for p in starboard: strobe(bm, p, 0.5, GREEN)
    for p in tops: strobe(bm, p, 0.4, WHITE)


def wedge(bm, outline, z, h, ridge=0.06, along=0.9, ox=0.0, mi=LIGHT, down=False):
    """A Star Destroyer slab: the outline extruded h (up, or down) with its top squeezed to a narrow ridge along X
    (ridge = fraction of the width left on top, along = fraction of the length), so the faces slope like a pyramid."""
    area2 = sum(outline[i][0] * outline[(i + 1) % len(outline)][1] - outline[(i + 1) % len(outline)][0] * outline[i][1] for i in range(len(outline)))
    if area2 < 0: outline = list(reversed(outline))
    cx = sum(p[0] for p in outline) / len(outline); cy = sum(p[1] for p in outline) / len(outline)
    loc = [(x - cx, y - cy) for x, y in outline]
    if down:
        loc = [(x, -y) for x, y in reversed(loc)]
    fast_prism(bm, loc, h, Vector((cx, cy, z)), DOWN if down else UP, mi, UVL(bm), top=(along, ridge, ox, 0.0), base_cap=True)


def wedge_faces(outline, z, h, ridge=0.06, along=0.9, ox=0.0, down=False, min_area=50.0):
    """The sloped side faces of a wedge() (same arguments), as triangle surfaces for detail, normals outward."""
    cx = sum(p[0] for p in outline) / len(outline); cy = sum(p[1] for p in outline) / len(outline)
    sg = -1 if down else 1
    def top(p): return Vector(((p[0] - cx) * along + cx + ox, (p[1] - cy) * ridge + cy, z + sg * h))
    def bot(p): return Vector((p[0], p[1], z))
    out = []
    n = len(outline)
    for i in range(n):
        p, q = outline[i], outline[(i + 1) % n]
        A, B, C, D = bot(p), bot(q), top(q), top(p)
        mid = (A + B + C + D) / 4
        for t in ((A, B, C), (A, C, D)):
            f = tri_surface(*t)
            pt, nrm, _ = f(0.3, 0.3)
            if nrm.dot(Vector((mid.x - cx, mid.y - cy, sg * 0.3 * h))) < 0: f = tri_surface(t[0], t[2], t[1])
            if f.area >= min_area: out.append(f)
    return out


def armour_grid(bm, tri, cell, height, seed, mats=(None, None), fill=0.65, gap=0.12, stack=0.2):
    """Orderly armour plating on a triangle (a, b, c): rectangles aligned with the ship's length (X projected into the
    face), laid in rows of cell = (length, width) metres, some missing, some doubled, in two tones."""
    a, b, c = (Vector(v) for v in tri)
    n = (b - a).cross(c - a).normalized()
    t = (X_ - n * X_.dot(n)); t = t.normalized() if t.length > 1e-6 else (b - a).normalized()
    w = n.cross(t)
    rnd = random.Random(seed)
    uv = UVL(bm)
    P = [((p - a).dot(t), (p - a).dot(w)) for p in (a, b, c)]
    def inside(x, y):
        (x1, y1), (x2, y2), (x3, y3) = P
        d = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
        if abs(d) < 1e-9: return False
        l1 = ((y2 - y3) * (x - x3) + (x3 - x2) * (y - y3)) / d
        l2 = ((y3 - y1) * (x - x3) + (x1 - x3) * (y - y3)) / d
        return l1 > 0 and l2 > 0 and 1 - l1 - l2 > 0
    xs = [p[0] for p in P]; ys = [p[1] for p in P]
    R = Matrix((t, w, n)).transposed()
    m0, m1 = mats[0] if mats[0] is not None else LIGHT, mats[1] if mats[1] is not None else HULL
    y = min(ys)
    while y < max(ys):
        L = cell[0] * rnd.uniform(0.6, 1.6)
        x = min(xs) + rnd.uniform(0, L)
        while x < max(xs):
            ln = cell[0] * rnd.uniform(0.5, 1.8)
            cx, cy = x + ln / 2, y + cell[1] / 2
            if rnd.random() < fill and all(inside(cx + dx, cy + dy) for dx in (-ln / 2, ln / 2) for dy in (-cell[1] / 2, cell[1] / 2)):
                h = rnd.uniform(*height)
                p = a + t * cx + w * cy + n * (h / 2)
                fast_box(bm, (ln * (1 - gap), cell[1] * (1 - gap), h), p, R, m0 if rnd.random() < 0.7 else m1, uv)
                if rnd.random() < stack:
                    fast_box(bm, (ln * 0.5, cell[1] * 0.45, h * 0.8), p + n * h * 0.8 + t * ln * rnd.uniform(-0.2, 0.2), R, m1, uv)
            x += ln
        y += cell[1]


def tri_surface(a, b, c):
    """A triangle as a greeble surface (uniform samples inside a, b, c; normal (b-a)x(c-a))."""
    a, b, c = Vector(a), Vector(b), Vector(c)
    n = (b - a).cross(c - a); area = n.length / 2; n.normalize(); t = (b - a).normalized()
    def f(u, v):
        su = math.sqrt(u)
        return a * (1 - su) + b * (su * (1 - v)) + c * (su * v), n, t
    f.area = area
    f.tri = (a, b, c)
    return f


def edge_windows(bm, a, b, z0, decks, out, lit=0.65, seed=0, spacing=2.4, strip=None):
    """Window rows along a straight flank from a to b (xy), facing `out` (xy unit), one row per deck from z0.
    strip=(min, max) metres: lay lit runs instead of single windows (far fewer faces on kilometre-long hulls; at
    that size a run of windows reads as one line anyway)."""
    rnd = random.Random(seed)
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    t = (b - a).normalized(); n = Vector((out[0], out[1], 0)).normalized()
    R = Matrix((t, n.cross(t).normalized(), n)).transposed()
    L = (b - a).length
    if strip:
        for d in range(decks):
            k = rnd.uniform(0, strip[1])
            while k < L:
                run = min(rnd.uniform(*strip), L - k)
                if rnd.random() < lit and run > 2:
                    fast_box(bm, (run, 0.7, 0.12), a + t * (k + run / 2) + n * 0.05 + Vector((0, 0, z0 + d * DECK)), R, WIN, UVL(bm))
                k += run + rnd.uniform(strip[0] * 0.3, strip[1] * 0.6)
        return
    for d in range(decks):
        k = 0.0
        while k < L:
            if rnd.random() < lit:
                fast_box(bm, (1.4, 0.7, 0.12), a + t * k + n * 0.05 + Vector((0, 0, z0 + d * DECK)), R, WIN, UVL(bm))
            k += spacing


def sd_hull(bm, L, beam, seed, up_h=None, lo_h=None, batteries=8, gun=None, waist_decks=None, nose_trim=0.05):
    """The navy's capital wedge (destroyer and up): an upper pyramid over a dark windowed waist and a shallower
    keel, plated in orderly two-tone armour, with turret batteries down both edges. L is the length, beam the full
    width at the stern. Returns the numbers the class script needs to place its own features."""
    sc = TILE / 8.0
    NOSE, STERN, HW = L / 2, -L / 2, beam / 2
    up_h = up_h or L * 0.072; lo_h = lo_h or L * 0.048
    UPZ, LOZ = L * 0.009, -L * 0.012
    half = [(NOSE, 0.0), (STERN + L * 0.05, HW), (STERN, HW - L * 0.02)]
    ox = -L * 0.05
    wedge(bm, mirror(half), UPZ, up_h, ridge=0.06, along=0.86, ox=ox, mi=LIGHT)
    wedge(bm, mirror([(NOSE - L * 0.025, 0.0), (STERN + L * 0.047, HW - L * 0.012), (STERN + L * 0.004, HW - L * 0.028)]), LOZ, UPZ - LOZ, ridge=1.0, along=1.0, mi=DARK)
    wedge(bm, mirror(half), LOZ, lo_h, ridge=0.12, along=0.84, ox=ox, mi=HULL, down=True)
    cell = (L * 0.043, L * 0.0135)
    for k, f in enumerate(wedge_faces(mirror(half), UPZ, up_h, ridge=0.06, along=0.86, ox=ox)):
        if f.area < L * L * 0.005: continue
        panels(bm, f, seed + k, scale=sc * 1.6, accent=0.0, density=0.1)
        armour_grid(bm, f.tri, cell, (L * 0.001, L * 0.0033), seed + 20 + k, (LIGHT, HULL), fill=0.7)
    for k, f in enumerate(wedge_faces(mirror(half), LOZ, lo_h, ridge=0.12, along=0.84, ox=ox, down=True)):
        if f.area < L * L * 0.005: continue
        armour_grid(bm, f.tri, (cell[0] * 1.15, cell[1] * 1.1), (L * 0.001, L * 0.003), seed + 40 + k, (HULL, DARK), fill=0.6)
    waist_decks = waist_decks or max(2, int((UPZ - LOZ) / DECK) - 1)
    gun = gun or L * 0.022
    for s in (1, -1):
        a, b = (NOSE - L * 0.027, 0.0), (STERN + L * 0.048, s * (HW - L * 0.012))
        o = Vector((-(b[1] - a[1]), b[0] - a[0], 0)).normalized()
        if o.y * s < 0: o = -o
        edge_windows(bm, (a[0], a[1] + o.y), (b[0], b[1] + o.y), LOZ + 2, waist_decks, (o.x, o.y), seed=seed + 60 + s, spacing=3.0, strip=(8, 40) if L >= 1500 else None)
        for k in range(batteries):
            u = 0.12 + 0.8 * k / max(batteries - 1, 1)
            p = Vector((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, UPZ)) - o * L * 0.01
            turret(bm, (p.x, p.y, p.z + L * 0.002), gun * (1.0 if k % 2 else 1.35))
    return {"NOSE": NOSE, "STERN": STERN, "HW": HW, "UPZ": UPZ, "UPH": up_h, "LOZ": LOZ, "LOH": lo_h, "RZ": UPZ + up_h, "sc": sc}


def ribs(bm, x0, x1, y, z0, z1, side, pitch, depth=0.8, width=None, mi=None, gap=0.0, seed=0):
    """A slatted armour belt (Halo UNSC): vertical ribs every `pitch` metres along a flank at y (side +1/-1),
    from z0 to z1, standing `depth` proud. gap>0 skips that fraction of ribs at random."""
    rnd = random.Random(seed)
    width = width or pitch * 0.45
    R = flank(side)
    x = x0 + pitch / 2
    while x < x1:
        if rnd.random() >= gap:
            m = mi if mi is not None else HULL
            if isinstance(m, tuple): m = m[0] if rnd.random() < 0.8 else m[1]
            fast_box(bm, (width, z1 - z0, depth), Vector((x, y, (z0 + z1) / 2)), R, m, UVL(bm))
        x += pitch


def strips(bm, x0, x1, y0, y1, z, n, h=0.6, mi=None, seed=0, down=False):
    """Long raised panel strips along the hull (the UNSC's lengthwise plating), n of them across y0..y1."""
    rnd = random.Random(seed)
    w = (y1 - y0) / n
    for k in range(n):
        a = x0 + rnd.uniform(0, (x1 - x0) * 0.15); b = x1 - rnd.uniform(0, (x1 - x0) * 0.15)
        yy = y0 + (k + 0.5) * w
        hh = h * rnd.uniform(0.6, 1.4)
        fast_box(bm, (b - a, w * 0.82, hh), Vector(((a + b) / 2, yy, z + (-hh / 2 if down else hh / 2))), DOWN if down else UP,
                 mi if mi is not None else (LIGHT if rnd.random() < 0.35 else HULL), UVL(bm))


def hull_scale(length_m):
    """Texture and detail scale for a hull of this length: panel textures repeat every 8 m on a 120 m corvette and
    proportionally larger on bigger ships (sqrt, so a titan still shows plating, not a smooth sheet)."""
    global TILE
    TILE = 8.0 * max(1.0, math.sqrt(length_m / 120.0))
    return TILE / 8.0


def export_ship(col, root, cls, style="hum"):
    """Save the .blend and export this hull at real scale to assets/ships/<style>/<cls>.glb."""
    folder = os.path.join(SHIPS, style); os.makedirs(folder, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(STATIONS, "stations.blend"))
    for o in bpy.context.scene.objects: o.select_set(False)
    root.select_set(True)
    for o in col.objects: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(folder, cls + ".glb"), export_format="GLB", use_selection=True, export_apply=True, export_lights=False)
    meshes = [o for o in col.objects if o.type == "MESH"]
    dims = None
    if meshes:
        lo = Vector((1e9,) * 3); hi = -lo
        for o in meshes:
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        dims = tuple(round(v, 1) for v in (hi - lo))
    return {"tris": sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes), "dims_m": dims}


def render_views(cls, views, it, res=(1200, 700)):
    """Render named views [(tag, cam_pos, target, lens)] to renders/ships/<cls>_i<it>_<tag>.png."""
    out = []
    sun = bpy.data.objects.get("Sun")
    if sun: sun.data.use_shadow = False          # EEVEE shadow acne blackens big flat plates; ships are lit flat in game anyway
    for tag, cam, tgt, lens in views:
        g = {"OUT": f"ships/{cls}_i{it}_{tag}", "CAM": (cam, tgt), "LENS": lens, "RES": res, "UP": (0, 0, 1)}
        exec(open(os.path.join(STATIONS, "render.py")).read(), {**globals(), **g})
        out.append(g["OUT"])
    return out
