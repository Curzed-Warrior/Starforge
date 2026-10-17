"""
Starforge ship hulls: the nine human classes, built at real scale with the station kit, then normalised to
2 units long (nose toward Blender -Y, which is the game's +Z) and exported one hull per file, one mesh each,
for the game's instanced traffic. Run inside Blender:
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/build.py").read())
Set ONLY = [k, ...] before exec to rebuild just some classes.

Ship space while building: +X is forward (the nose), +Y is port, +Z is dorsal.
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())

SHIPS = r"C:/Users/diamo/Desktop/starforge/assets/ships"
MATS = ["Hull", "HullDark", "Glow", "Window", "NavRed", "NavGreen", "Trim", "WindowCool", "Glass", "UmbraVein", "Rune", "Lucent", "LucentGlow"]
HULL, DARK, GLOW, WIN, RED, GREEN, TRIM, WCOOL, GLASS, UMBRA, RUNE, LUCENT, LGLOW = range(13)
M["Glow"] = mat("DriveGlow", (0.6, 0.8, 1.0), emit=(0.55, 0.78, 1.0), strength=20)
M["UmbraVein"] = mat("UmbraVein", (0.35, 0.1, 0.6), emit=(0.55, 0.2, 1.0), strength=6)
M["Rune"] = mat("Rune", (1.0, 0.75, 0.3), emit=(1.0, 0.72, 0.28), strength=3)
M["Lucent"] = mat("Lucent", (0.85, 0.88, 0.95), 0.6, 0.2)
M["LucentGlow"] = mat("LucentGlow", (0.9, 0.95, 1.0), emit=(0.85, 0.92, 1.0), strength=2.5)
I3 = Matrix.Identity(3)
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))

# ------------------------------------------------------------------ parts
def sphere_surface(r, lat=1.1):
    """A sphere's skin between latitudes -lat..lat (about the X axis, so the bow and stern poles stay clear)."""
    def f(u, v):
        a = u * 2 * math.pi; b = -lat + 2 * lat * v
        n = Vector((math.sin(b), math.cos(b) * math.cos(a), math.cos(b) * math.sin(a)))
        t = Vector((0, -math.sin(a), math.cos(a)))
        return n * r, n, t
    f.area = 4 * math.pi * r * r * math.sin(lat)
    return f

def body(bm, sections, at=(0, 0, 0), mi=HULL):
    loft(bm, sections, I3, Vector(at), mi)

def top(x0, x1, hw, z, y0=0.0):
    return plane_surface((x0, y0 - hw, z), (x1 - x0, 0, 0), (0, 2 * hw, 0))

def flank(x0, x1, y, z0, z1):
    """A flank face at side y (sign picks port or starboard), facing outward."""
    if y > 0: return plane_surface((x0, y, z0), (0, 0, z1 - z0), (x1 - x0, 0, 0))
    return plane_surface((x0, y, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0))

def belly(x0, x1, hw, z):
    return plane_surface((x0, -hw, z), (0, 2 * hw, 0), (x1 - x0, 0, 0))

def engine(bm, at, r, length):
    """A drive bell pointing aft (-X) with a glowing throat."""
    at = Vector(at)
    cyl(bm, r * 0.9, r * 1.15, length, at - X * length / 2, Ry(math.pi / 2), seg=14, mi=DARK)
    cyl(bm, r * 0.95, r * 0.95, length * 0.12, at - X * length * 0.9, Ry(math.pi / 2), seg=14, mi=GLOW)
    cyl(bm, r * 0.55, r * 0.75, length * 0.5, at + X * length * 0.15, Ry(math.pi / 2), seg=10, mi=HULL)

def turret(bm, at, s, barrels=2, up=1):
    """A chamfered gun turret with barrels pointing forward."""
    at = Vector(at)
    uv = bm.loops.layers.uv.verify()
    rot = I3 if up > 0 else Matrix(((1, 0, 0), (0, -1, 0), (0, 0, -1)))
    fast_prism(bm, octo(s * 1.4, s * 1.2, s * 0.35), s * 0.35, at, rot, DARK, uv, top=(0.8, 0.8, -s * 0.1, 0))
    fast_prism(bm, octo(s * 0.9, s * 0.8, s * 0.25), s * 0.3, at + Z * up * s * 0.35, rot, HULL, uv, top=(0.75, 0.8, -s * 0.1, 0))
    for b in range(barrels):
        off = (b - (barrels - 1) / 2) * s * 0.3
        cyl(bm, s * 0.07, s * 0.06, s * 1.6, at + X * s * 1.0 + Y * off + Z * up * s * 0.48, Ry(math.pi / 2), seg=6, mi=DARK)

def navs(bm, port, star, top_at=None, r=0.4):
    sphere(bm, r, port, mi=RED, seg=6)
    sphere(bm, r, star, mi=GREEN, seg=6)
    if top_at is not None: sphere(bm, r, top_at, mi=WIN, seg=6)

def wing(bm, root_x0, root_x1, tip_x0, tip_x1, y0, span, z, thick, mi=HULL, sign=1):
    """A flat swept wing/fin from the hull side out to span."""
    pts = [(root_x0, y0), (root_x1, y0), (tip_x1, y0 + span), (tip_x0, y0 + span)]
    lo = [bm.verts.new((x, sign * y, z - thick / 2)) for x, y in pts]
    hi = [bm.verts.new((x, sign * y, z + thick / 2)) for x, y in pts]
    fs = [bm.faces.new(hi), bm.faces.new(list(reversed(lo)))]
    for i in range(4):
        j = (i + 1) % 4
        fs.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
    bm.normal_update()
    cen = sum((v.co for v in lo + hi), Vector()) / 8
    for f in fs:
        f.material_index = mi
        if f.normal.dot(f.calc_center_median() - cen) < 0: f.normal_flip()

def strip_windows(bm, x0, x1, y, z, n, h, seed, lit=0.75, mi=WIN):
    for s in (1, -1):
        window_rows(bm, flank(x0, x1, s * y, z - h, z + h), 1, n, size=(min(2.0, (x1 - x0) / n * 0.6), h * 1.2), mi=mi, seed=seed + s, lit=lit)

# ------------------------------------------------------------------ the classes (lengths in metres)
def shuttle(bm):      # 24 m: lifting body, cockpit canopy, twin engines, swept wings
    body(bm, [(-11, 2.0, 1.4, 0.4), (-6, 2.6, 1.6, 0.4), (4, 2.3, 1.5, 0.4), (9, 1.4, 1.0, 0.5), (12.5, 0.3, 0.35, 0.5)])
    body(bm, [(5, 0.7, 0.4, 0.5), (8.5, 0.9, 0.5, 0.5), (10.5, 0.3, 0.2, 0.5)], (0, 0, 1.25), GLASS)
    for s in (1, -1):
        wing(bm, -9, -1, -10.5, -7.5, 2.2, 5.5, -0.6, 0.35, HULL, s)
        engine(bm, (-11, s * 1.1, 0), 0.8, 2.2)
    navs(bm, (-9, 7.7, -0.6), (-9, -7.7, -0.6), (-6, 0, 1.6), 0.25)
    strip_windows(bm, -4, 4, 2.6, 0.4, 7, 0.25, 1)
    return 24

def corvette(bm):     # 70 m: an arrowhead with one big drive, dorsal fin, chin guns
    body(bm, [(-33, 5.5, 4.0, 0.35), (-20, 7.5, 4.5, 0.35), (5, 6.0, 3.6, 0.35), (25, 2.5, 1.8, 0.45), (37, 0.4, 0.4, 0.5)])
    body(bm, [(-24, 2.5, 3, 0.4), (-8, 2.0, 3.5, 0.4), (4, 0.6, 1.2, 0.4)], (0, 0, 4.0), DARK)
    for s in (1, -1):
        wing(bm, -28, -8, -32, -24, 7.0, 9, -1.0, 0.8, HULL, s)
        turret(bm, (14, s * 3.0, -2.6), 1.8, 2, up=-1)
    engine(bm, (-33, 0, 0), 4.2, 9)
    for s in (1, -1): engine(bm, (-30, s * 6.2, -0.5), 1.4, 4)
    detail(bm, top(-26, 18, 4.5, 4.5 * 0.97), 11, scale=1.1, mats=(HULL, DARK), window=WIN, window_frac=0.05)
    strip_windows(bm, -18, 6, 7.3, 0.6, 14, 0.5, 12)
    navs(bm, (-28, 16.2, -1), (-28, -16.2, -1), (-8, 0, 7.6), 0.5)
    return 70

def frigate(bm):      # 130 m: forward command section, long spine, twin drive nacelles on pylons
    body(bm, [(-60, 4, 4, 0.4), (25, 4, 4, 0.4)], (0, 0, 0), DARK)
    body(bm, [(20, 8, 6, 0.35), (35, 10, 7, 0.35), (55, 7, 5, 0.4), (68, 2, 1.6, 0.5)])
    body(bm, [(-40, 6, 5, 0.35), (-10, 6, 5, 0.35)], (0, 0, 1), HULL)
    for s in (1, -1):
        body(bm, [(-62, 4.5, 4.5, 0.4), (-30, 5, 5, 0.4), (-12, 3.5, 3.5, 0.45)], (0, s * 18, -1), HULL)
        wing(bm, -40, -22, -40, -24, 4, 12, -1, 1.2, DARK, s)
        engine(bm, (-62, s * 18, -1), 3.6, 7)
        turret(bm, (40, s * 6, 5.4), 2.0, 2)
    engine(bm, (-60, 0, 0), 3.0, 6)
    detail(bm, top(22, 52, 7, 7 * 0.98), 21, scale=1.2, mats=(HULL, DARK), window=WIN, window_frac=0.05)
    detail(bm, top(-38, -12, 4.5, 6 * 0.97), 22, scale=1.2, mats=(HULL, DARK))
    strip_windows(bm, 24, 52, 10, 1.5, 18, 0.6, 23)
    antennas(bm, (-25, 0, 6), (0, 0, 1), 3, 10, 24, mi=DARK, light=RED)
    navs(bm, (-36, 34, -1), (-36, -34, -1), (45, 0, 7.5), 0.7)
    return 130

def destroyer(bm):    # 260 m: a broad wedge with two stepped deck tiers, turret rows, side sponsons, triple drive
    body(bm, [(-125, 26, 14, 0.3), (-90, 32, 16, 0.3), (40, 24, 13, 0.35), (110, 8, 6, 0.45), (135, 1.2, 1.2, 0.5)])
    body(bm, [(-115, 18, 6, 0.35), (-20, 16, 6, 0.35), (40, 6, 3, 0.4)], (0, 0, 18), HULL)            # deck tier 1
    body(bm, [(-110, 10, 7, 0.35), (-60, 9, 9, 0.35), (-40, 4, 5, 0.4)], (0, 0, 30), DARK)            # the bridge tier
    for s in (1, -1):
        body(bm, [(-90, 7, 9, 0.4), (10, 8, 9, 0.4), (35, 4, 4, 0.45)], (0, s * 33, -2), HULL)
        for i in range(3): turret(bm, (-70 + i * 32, s * 33, 7), 5.0, 2)
        wing(bm, -110, -70, -118, -96, 30, 16, -6, 2.0, DARK, s)
    for i in range(4): turret(bm, (-10 + i * 24, 0, 24 - i * 2), 6.5, 3)
    for k in (-1, 0, 1): engine(bm, (-125, k * 15, 0), 8.5, 16)
    detail(bm, top(-100, 30, 18, 16 * 0.97), 31, scale=3.5, mats=(HULL, DARK), window=WIN, window_frac=0.05, glow=GLOW, glow_frac=0.01)
    detail(bm, top(-110, -25, 14, 24), 35, scale=3.0, mats=(HULL, DARK), window=WIN, window_frac=0.06)
    detail(bm, belly(-100, 60, 18, -16 * 0.97), 32, scale=4.0, mats=(DARK, HULL))
    strip_windows(bm, -90, 30, 32.2, 3, 30, 1.0, 33)
    strip_windows(bm, -108, -62, 10.2, 32, 10, 1.0, 36)
    antennas(bm, (-80, 0, 39), (0, 0, 1), 4, 18, 34, mi=DARK, light=RED)
    navs(bm, (-100, 46.5, -6), (-100, -46.5, -6), (-80, 0, 40), 1.2)
    return 260

def cruiser(bm):      # 460 m: a long hull with a spin-gravity ring amidships, sensor mast, four drives
    body(bm, [(-220, 14, 14, 0.35), (-150, 18, 16, 0.35), (120, 16, 14, 0.35), (200, 8, 7, 0.4), (240, 1.5, 1.5, 0.5)])
    for s in (1, -1):
        body(bm, [(-200, 6, 6, 0.4), (-60, 6, 6, 0.4)], (0, s * 22, 0), DARK)
    # the spin ring (a separate band round the hull, on struts)
    n0 = set(bm.verts)
    c = solid_ring(bm, 52, 62, -7, 7, 48, mats=(HULL, DARK, HULL, HULL))
    bmesh.ops.transform(bm, matrix=Matrix.Translation((-10, 0, 0)) @ Ry(math.pi / 2), verts=since(bm, n0))
    for k in range(6):
        a = k * math.pi / 3
        p = Vector((0, math.cos(a), math.sin(a)))
        _rod(bm, Vector((-10, 0, 0)) + p * 15, Vector((-10, 0, 0)) + p * 52, 1.4, DARK)
    for k in range(48):
        if k % 3 == 0: continue
        a = k * 2 * math.pi / 48
        box(bm, (5, 0.3, 2.2), Vector((-10, math.cos(a) * 62.1, math.sin(a) * 62.1)), Rx(a), mi=WIN)
    body(bm, [(20, 4, 3, 0.4), (60, 3, 2, 0.4)], (0, 0, 15), DARK)
    cyl(bm, 1.2, 0.5, 40, (40, 0, 36), seg=6, mi=DARK)
    for i in range(3): turret(bm, (60 + i * 32, 0, 14.2 - i * 0.8), 7.0, 3)
    for i in range(2):
        for s in (1, -1): turret(bm, (100 + i * 30, s * 11, -12), 5.5, 2, up=-1)
    for (y, z) in ((10, 8), (-10, 8), (10, -8), (-10, -8)): engine(bm, (-220, y, z), 6, 14)
    detail(bm, top(-150, 110, 13, 16 * 0.97), 41, scale=4.0, mats=(HULL, DARK), window=WIN, window_frac=0.05)
    detail(bm, belly(-150, 110, 13, -16 * 0.97), 42, scale=5.0, mats=(DARK, HULL))
    strip_windows(bm, -140, 110, 18.2, 3, 50, 1.0, 43)
    navs(bm, (-150, 28, 0), (-150, -28, 0), (40, 0, 56), 1.4)
    return 460

def battleship(bm):   # 900 m: an armoured stepped slab, a spinal gun, three tiers of batteries, a massive drive block
    body(bm, [(-400, 90, 45, 0.3), (-300, 100, 52, 0.3), (250, 85, 45, 0.3), (380, 40, 24, 0.4), (430, 14, 10, 0.5)])
    body(bm, [(-360, 70, 16, 0.3), (180, 60, 14, 0.3), (250, 24, 8, 0.4)], (0, 0, 62), HULL)          # tier 1
    body(bm, [(-330, 44, 16, 0.3), (40, 38, 14, 0.3), (90, 16, 8, 0.4)], (0, 0, 90), HULL)            # tier 2
    body(bm, [(-260, 26, 20, 0.35), (-140, 20, 30, 0.35), (-90, 10, 16, 0.4)], (0, 0, 118), DARK)     # the citadel
    cyl(bm, 9, 9, 80, (460, 0, 0), Ry(math.pi / 2), seg=12, mi=DARK)
    cyl(bm, 7, 7, 4, (500, 0, 0), Ry(math.pi / 2), seg=12, mi=GLOW)          # the spinal gun's muzzle
    for i in range(6):
        for s in (1, -1): turret(bm, (-200 + i * 70, s * 50, 78), 15, 3)
    for i in range(4): turret(bm, (-60 + i * 50, 0, 104), 17, 3)
    for i in range(5):
        for s in (1, -1): turret(bm, (-140 + i * 70, s * 50, -51), 12, 2, up=-1)
    body(bm, [(-440, 104, 56, 0.3), (-390, 108, 58, 0.3)], (0, 0, 0), DARK)
    for y in (-75, -25, 25, 75):
        for z in (-28, 28): engine(bm, (-440, y, z), 18, 36)
    detail(bm, top(-280, 230, 84, 52 * 0.97), 51, scale=8.0, mats=(HULL, DARK), window=WIN, window_frac=0.05, glow=GLOW, glow_frac=0.01)
    detail(bm, top(-340, 170, 66, 78 * 0.99), 56, scale=8.0, mats=(HULL, DARK), window=WIN, window_frac=0.05)
    detail(bm, belly(-280, 230, 84, -52 * 0.97), 52, scale=10.0, mats=(DARK, HULL))
    for zz in (-20, 0, 20): strip_windows(bm, -280, 230, 100.2, zz, 80, 2.4, 53 + zz)
    antennas(bm, (-200, 0, 168), (0, 0, 1), 6, 60, 55, mi=DARK, light=RED)
    navs(bm, (-300, 101, 0), (-300, -101, 0), (-200, 0, 170), 3.4)
    return 900

def carrier(bm):      # 900 m: a broad flight deck over a hull, glowing hangar mouths fore and aft, a command island
    body(bm, [(-420, 70, 10, 0.25), (380, 70, 10, 0.25), (440, 40, 7, 0.35)], (0, 0, 22), HULL)       # the deck
    body(bm, [(-400, 40, 22, 0.3), (300, 42, 24, 0.3), (400, 16, 12, 0.4)], (0, 0, -4), DARK)          # the hull below
    for s in (1, -1):
        box(bm, (16, 60, 12), (439, 0, 22), mi=WIN) if s > 0 else box(bm, (16, 60, 12), (-421, 0, 22), mi=WIN)
    for i in range(10): box(bm, (60, 1.2, 0.4), (-340 + i * 75, 0, 32.3), mi=WCOOL)                    # deck lights
    for s in (1, -1):
        for i in range(12): box(bm, (2, 0.5, 0.4), (-360 + i * 64, s * 50, 32.3), mi=WCOOL)
    body(bm, [(-120, 14, 30, 0.35), (-40, 12, 36, 0.35), (-10, 6, 18, 0.4)], (0, 45, 62), DARK)       # the island
    window_rows(bm, flank(-110, -30, 45 + 14, 50, 80), 6, 14, size=(3, 1.2), mi=WIN, seed=61, lit=0.8)
    antennas(bm, (-70, 45, 98), (0, 0, 1), 5, 40, 62, mi=DARK, light=RED)
    for y in (-50, -25, 0, 25, 50): engine(bm, (-420, y, 0), 10, 24)
    detail(bm, top(-380, 360, 66, 32 * 0.98), 63, scale=9.0, mats=(HULL, DARK), window=WIN, window_frac=0.03)
    detail(bm, belly(-380, 280, 36, -28 * 0.97), 64, scale=10.0, mats=(DARK, HULL))
    for s in (1, -1):
        for i in range(6): turret(bm, (-300 + i * 110, s * 64, 32), 7, 2)
    navs(bm, (-380, 71, 22), (-380, -71, 22), (-70, 45, 100), 2.4)
    return 900

def titan(bm):        # 1800 m: a cathedral of a ship, a lance down its spine, flying buttresses, a crown of drives
    body(bm, [(-760, 120, 90, 0.3), (-600, 150, 110, 0.3), (300, 130, 90, 0.3), (650, 60, 46, 0.35), (820, 16, 16, 0.5)])
    cyl(bm, 18, 18, 200, (850, 0, 0), Ry(math.pi / 2), seg=16, mi=DARK)
    cyl(bm, 14, 14, 6, (952, 0, 0), Ry(math.pi / 2), seg=16, mi=GLOW)
    for i in range(6):                                  # the lance's coils glowing through the bow
        n0 = set(bm.verts)
        solid_ring(bm, 34, 44, -4, 4, 24, mats=(GLOW, DARK, DARK, DARK))
        bmesh.ops.transform(bm, matrix=Matrix.Translation((440 + i * 55, 0, 0)) @ Ry(math.pi / 2), verts=since(bm, n0))
    body(bm, [(-640, 110, 22, 0.3), (200, 96, 20, 0.3)], (0, 0, 120), HULL)                           # upper deck
    body(bm, [(-520, 60, 80, 0.35), (-220, 46, 120, 0.35), (-110, 24, 60, 0.4)], (0, 0, 200), DARK)   # the cathedral
    body(bm, [(-400, 22, 60, 0.4), (-310, 18, 90, 0.4), (-280, 6, 16, 0.5)], (0, 0, 380), HULL)      # the spire
    for s in (1, -1):
        for i in range(5):                              # buttresses down to the flanks
            x = -480 + i * 70
            _rod(bm, Vector((x, s * 50, 290)), Vector((x + 20, s * 120, 140)), 7, DARK)
        body(bm, [(-650, 36, 36, 0.4), (-200, 40, 40, 0.4), (-110, 18, 18, 0.45)], (0, s * 175, -30), HULL)
        for i in range(6): turret(bm, (-150 + i * 80, s * 105, 143), 22, 3)
        for i in range(4): turret(bm, (-500 + i * 90, s * 175, 7), 16, 2)
    for y in (-90, -30, 30, 90):
        for z in (-45, 40): engine(bm, (-760, y, z), 24, 56)
    for s in (1, -1): engine(bm, (-650, s * 175, -30), 30, 60)
    detail(bm, top(-600, 180, 104, 142 * 0.99), 71, scale=12.0, mats=(HULL, DARK), window=WIN, window_frac=0.04, glow=GLOW, glow_frac=0.01)
    detail(bm, belly(-560, 280, 120, -110 * 0.97), 72, scale=14.0, mats=(DARK, HULL))
    for zz in (-40, 0, 40): strip_windows(bm, -560, 280, 150.3, zz, 90, 3.2, 73 + zz)
    window_rows(bm, flank(-500, -120, 60.2, 140, 300), 12, 30, size=(7, 3), mi=WIN, seed=75, lit=0.7)
    window_rows(bm, flank(-500, -120, -60.2, 140, 300), 12, 30, size=(7, 3), mi=WIN, seed=76, lit=0.7)
    antennas(bm, (-295, 0, 560), (0, 0, 1), 6, 100, 77, mi=DARK, light=RED)
    navs(bm, (-600, 212, -30), (-600, -212, -30), (-295, 0, 565), 5)
    return 1800

def colossus(bm):     # 3000 m: a world-ship, a moon-sized core ringed by city decks, a crown of drives
    sphere(bm, 900, (0, 0, 0), mi=HULL, seg=48)
    for k, (r, w, z) in enumerate(((1100, 60, 0), (1020, 40, 240), (1020, 40, -240))):
        n0 = set(bm.verts)
        solid_ring(bm, r - w, r + w, -35, 35, 96, mats=(HULL, DARK, HULL, HULL))
        bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0, z)) @ Ry(math.pi / 2), verts=since(bm, n0))
    for k in range(160):
        a = k * 2 * math.pi / 160
        box(bm, (18, 4, 6), Vector((0, math.cos(a) * 1160.5, math.sin(a) * 1160.5)), Rx(a), mi=WIN)
    for k in range(8):
        a = k * math.pi / 4
        p = Vector((0, math.cos(a), math.sin(a)))
        _rod(bm, p * 880, p * 1040, 20, DARK)
    body(bm, [(600, 260, 260, 0.35), (1100, 180, 180, 0.35), (1350, 70, 70, 0.45)], (0, 0, 0), DARK)   # the bow citadel
    cyl(bm, 50, 50, 40, (1380, 0, 0), Ry(math.pi / 2), seg=16, mi=GLOW)
    for k in range(8):
        a = k * math.pi / 4
        p = Vector((0, math.cos(a) * 520, math.sin(a) * 520))
        body(bm, [(-1300, 90, 90, 0.4), (-800, 110, 110, 0.4), (-600, 60, 60, 0.45)], tuple(p), HULL)
        engine(bm, Vector((-1300, 0, 0)) + p, 85, 160)
    detail(bm, sphere_surface(900.5, 0.9), 81, scale=32, mats=(HULL, DARK), window=WIN, window_frac=0.05)
    return 3000


# ------------------------------------------------------------------ the tech bible's hulls (exported by name: ship<Name>.glb)
def battlecruiser(bm):  # 650 m: a long knife of a cruiser built for speed, heavy forward batteries, six drives
    body(bm, [(-310, 22, 16, 0.3), (-200, 30, 18, 0.3), (180, 22, 12, 0.35), (290, 8, 6, 0.45), (335, 1.5, 1.5, 0.5)])
    body(bm, [(-260, 16, 8, 0.35), (60, 14, 8, 0.35), (120, 5, 3, 0.4)], (0, 0, 20), HULL)
    body(bm, [(-230, 10, 10, 0.35), (-150, 8, 18, 0.35), (-120, 4, 8, 0.4)], (0, 0, 32), DARK)
    for s in (1, -1):
        wing(bm, -290, -150, -320, -260, 28, 40, -4, 3, DARK, s)
        wing(bm, -120, 40, -150, -100, 26, 18, 2, 2, HULL, s)
        for i in range(3): turret(bm, (60 + i * 50, s * 16, 12), 9, 3)
    for i in range(2): turret(bm, (130 + i * 45, 0, 26 - i * 3), 11, 3)
    for y in (-18, 0, 18):
        for z in (-7, 7): engine(bm, (-310, y, z), 8, 18)
    detail(bm, top(-260, 150, 26, 18 * 0.97), 91, scale=5, mats=(HULL, DARK), window=WIN, window_frac=0.05, glow=GLOW, glow_frac=0.01)
    detail(bm, belly(-260, 150, 26, -18 * 0.97), 92, scale=6, mats=(DARK, HULL))
    strip_windows(bm, -200, 150, 30.2, 2, 50, 1.4, 93)
    navs(bm, (-300, 68, -4), (-300, -68, -4), (-150, 0, 51), 1.8)
    return 650

def juggernaut(bm):     # 2200 m: an armoured mountain with a ram prow, broadside decks and a wall of drives
    body(bm, [(-1000, 240, 170, 0.25), (-700, 280, 200, 0.25), (500, 260, 180, 0.25), (900, 160, 110, 0.3), (1100, 30, 30, 0.45)])
    for i, (x0, x1, w, z) in enumerate(((-900, 400, 200, 200), (-850, 150, 140, 260), (-700, -200, 90, 320))):
        body(bm, [(x0, w, 30, 0.25), (x1, w * 0.9, 26, 0.25), (x1 + 80, w * 0.4, 14, 0.35)], (0, 0, z), HULL if i % 2 == 0 else DARK)
    for k in range(5):                                   # ram plates stacked on the prow
        x = 700 + k * 80
        box(bm, (40, 300 - k * 50, 240 - k * 40), Vector((x, 0, 0)), mi=DARK if k % 2 else HULL)
    for s in (1, -1):
        for row, z in enumerate((-80, 0, 80)):
            for i in range(8): turret(bm, (-600 + i * 140 + row * 40, s * 268, z), 26, 3, up=1)
        body(bm, [(-950, 60, 60, 0.35), (300, 70, 70, 0.35), (420, 30, 30, 0.45)], (0, s * 300, -140), DARK)
    for y in (-180, -60, 60, 180):
        for z in (-110, 0, 110): engine(bm, (-1000, y, z), 42, 90)
    detail(bm, top(-850, 450, 220, 200 * 0.99), 101, scale=20, mats=(HULL, DARK), window=WIN, window_frac=0.04, glow=GLOW, glow_frac=0.01)
    detail(bm, belly(-850, 450, 220, -170 * 0.98), 102, scale=24, mats=(DARK, HULL))
    for zz in (-120, -40, 40, 120): strip_windows(bm, -800, 450, 280.3, zz, 120, 5, 103 + zz)
    antennas(bm, (-500, 0, 350), (0, 0, 1), 8, 140, 104, mi=DARK, light=RED)
    navs(bm, (-900, 300, -140), (-900, -300, -140), (-500, 0, 360), 9)
    return 2200

def colossus2(bm):      # 2400 m: a siege ship built round its gun: a spinal accelerator of glowing rings, a hull hung beneath
    cyl(bm, 60, 60, 2000, (100, 0, 120), Ry(math.pi / 2), seg=20, mi=DARK)
    for i in range(14):                                  # accelerator rings
        n0 = set(bm.verts)
        solid_ring(bm, 120, 170, -16, 16, 32, mats=(GLOW if i % 3 == 0 else DARK, HULL, HULL, HULL))
        bmesh.ops.transform(bm, matrix=Matrix.Translation((-750 + i * 130, 0, 120)) @ Ry(math.pi / 2), verts=since(bm, n0))
    cyl(bm, 80, 80, 20, (1110, 0, 120), Ry(math.pi / 2), seg=20, mi=GLOW)       # the muzzle
    body(bm, [(-1050, 160, 120, 0.3), (-600, 190, 140, 0.3), (300, 150, 110, 0.3), (500, 50, 40, 0.45)], (0, 0, -120), HULL)
    for s in (1, -1):
        for i in range(6): _rod(bm, Vector((-700 + i * 220, s * 40, 60)), Vector((-700 + i * 220, s * 120, -40)), 12, DARK)
        for i in range(5): turret(bm, (-600 + i * 180, s * 175, -60), 24, 2)
    for y in (-110, 0, 110):
        for z in (-160, -80): engine(bm, (-1050, y, z), 40, 90)
    detail(bm, top(-950, 250, 160, 20 * 0.98 - 120 + 140), 111, scale=18, mats=(HULL, DARK), window=WIN, window_frac=0.04)
    detail(bm, belly(-950, 250, 160, -140 * 0.98 - 120), 112, scale=22, mats=(DARK, HULL))
    strip_windows(bm, -900, 250, 190.3, -120, 90, 5, 113)
    navs(bm, (-1000, 200, -120), (-1000, -200, -120), (0, 0, 300), 8)
    return 2400

def dreadnought(bm):    # 2600 m: an Umbral dreadnought: a black wedge, asymmetric, veined with dark-matter light
    body(bm, [(-1100, 260, 120, 0.15), (-600, 320, 150, 0.15), (600, 180, 90, 0.2), (1200, 20, 20, 0.5)], (0, 0, 0), DARK)
    body(bm, [(-900, 120, 60, 0.2), (200, 90, 50, 0.2), (420, 20, 16, 0.4)], (0, -60, 150), DARK)
    body(bm, [(-700, 60, 120, 0.25), (-400, 40, 200, 0.25), (-300, 10, 40, 0.4)], (0, 80, 160), HULL)
    for k in range(7):                                   # umbral veins along the flanks
        for s in (1, -1):
            _rod(bm, Vector((-1000 + k * 300, s * 300 * (1 - k / 10), 0)), Vector((-800 + k * 300, s * 260 * (1 - k / 10), 90)), 10, UMBRA)
    for i in range(6):
        for s in (1, -1): turret(bm, (-500 + i * 160, s * 220, 110), 30, 3)
    for y in (-200, -70, 70, 200): engine(bm, (-1100, y, 0), 55, 110)
    for k in range(5): sphere(bm, 30, (-1150, -160 + k * 80, 60), mi=UMBRA, seg=10)
    detail(bm, top(-900, 400, 230, 150 * 0.97), 121, scale=22, mats=(DARK, HULL), window=UMBRA, window_frac=0.03)
    detail(bm, belly(-900, 400, 230, -120 * 0.97), 122, scale=26, mats=(DARK, DARK))
    navs(bm, (-1000, 330, 0), (-1000, -330, 0), (-400, 80, 365), 9)
    return 2600

def leviathan(bm):      # 2600 m: a super-carrier: three stacked flight decks, hangar mouths aglow, a tower of command
    for k, z in enumerate((-160, 0, 160)):
        body(bm, [(-1150, 300, 26, 0.2), (1050, 300, 26, 0.2), (1250, 160, 18, 0.3)], (0, 0, z), HULL if k != 1 else DARK)
        box(bm, (30, 260, 30), Vector((1265, 0, z)), mi=WIN)
        box(bm, (30, 260, 30), Vector((-1165, 0, z)), mi=WIN)
        for i in range(14): box(bm, (120, 3, 1), Vector((-1000 + i * 150, 0, z + 27)), mi=WCOOL)
    for s in (1, -1):
        for i in range(7): _rod(bm, Vector((-1000 + i * 330, s * 280, -160)), Vector((-1000 + i * 330, s * 280, 160)), 22, DARK)
        for i in range(8): turret(bm, (-900 + i * 270, s * 300, 190), 22, 2)
    body(bm, [(-500, 60, 120, 0.3), (-250, 50, 200, 0.3), (-180, 20, 60, 0.4)], (0, 230, 300), DARK)
    window_rows(bm, flank(-480, -220, 230 + 60, 220, 460), 10, 20, size=(9, 4), mi=WIN, seed=131, lit=0.8)
    for y in (-200, -100, 0, 100, 200):
        for z in (-160, 0, 160): engine(bm, (-1150, y, z), 32, 70)
    detail(bm, top(-1050, 1000, 290, 186 * 0.99), 132, scale=24, mats=(HULL, DARK), window=WIN, window_frac=0.03)
    detail(bm, belly(-1050, 1000, 290, -186 * 0.99), 133, scale=26, mats=(DARK, HULL))
    navs(bm, (-1100, 305, -160), (-1100, -305, -160), (-300, 230, 520), 9)
    return 2600

def glyph_ark(bm):      # 3500 m: a runic super-ark: a long curved keel wrapped in rings of glowing script
    pts = []
    for i in range(9):
        x = -1700 + i * 425
        hw = 180 + 160 * math.sin(math.pi * i / 8)
        pts.append((x, hw, hw * 0.7, 0.35))
    pts.append((1800, 30, 30, 0.5))
    body(bm, pts, (0, 0, 0), HULL)
    for i in range(9):                                   # rune rings
        x = -1500 + i * 360
        r = 300 + 140 * math.sin(math.pi * (i + 0.5) / 9)
        n0 = set(bm.verts)
        solid_ring(bm, r, r + 30, -12, 12, 64, mats=(RUNE, DARK, DARK, DARK))
        bmesh.ops.transform(bm, matrix=Matrix.Translation((x, 0, 0)) @ Ry(math.pi / 2), verts=since(bm, n0))
        for k in range(4):
            a = k * math.pi / 2 + i * 0.3
            p = Vector((x, math.cos(a), math.sin(a)))
            _rod(bm, Vector((x, math.cos(a) * r * 0.5, math.sin(a) * r * 0.5)), Vector((x, math.cos(a) * r, math.sin(a) * r)), 8, DARK)
    body(bm, [(-600, 60, 220, 0.3), (0, 50, 320, 0.3), (200, 10, 80, 0.45)], (0, 0, 260), DARK)       # the reliquary spire
    for k in range(6): box(bm, (60, 8, 30), Vector((-500 + k * 130, 0, 620)), mi=RUNE)
    for y in (-180, 0, 180):
        for z in (-120, 120): engine(bm, (-1750, y, z), 60, 130)
    detail(bm, top(-1300, 1300, 200, 230 * 0.98), 141, scale=30, mats=(HULL, DARK), window=RUNE, window_frac=0.03)
    detail(bm, belly(-1300, 1300, 200, -230 * 0.98), 142, scale=34, mats=(DARK, HULL))
    navs(bm, (-1600, 300, 0), (-1600, -300, 0), (0, 0, 680), 12)
    return 3500

def devourer(bm):       # 4000 m: a world-eater: two great jaws open around a burning maw, a long body of furnaces behind
    body(bm, [(-1900, 260, 260, 0.35), (-600, 420, 420, 0.3), (400, 480, 480, 0.3), (700, 420, 420, 0.3)], (0, 0, 0), DARK)
    cyl(bm, 330, 300, 80, (760, 0, 0), Ry(math.pi / 2), seg=32, mi=GLOW)                            # the maw
    for k in range(4):                                   # four jaws reaching forward
        a = k * math.pi / 2 + math.pi / 4
        d = Vector((0, math.cos(a), math.sin(a)))
        base = Vector((650, 0, 0)) + d * 420
        tip = Vector((1900, 0, 0)) + d * 240
        _rod(bm, base, Vector((1300, 0, 0)) + d * 620, 70, HULL)
        _rod(bm, Vector((1300, 0, 0)) + d * 620, tip, 46, DARK)
        for t in range(5):
            q = base.lerp(tip, 0.3 + t * 0.15)
            _rod(bm, q, q - d * 120, 14, DARK)          # teeth
    for i in range(6):                                   # furnace rings down the body
        n0 = set(bm.verts)
        solid_ring(bm, 380 - i * 25, 440 - i * 25, -30, 30, 48, mats=(GLOW if i % 2 == 0 else DARK, HULL, HULL, HULL))
        bmesh.ops.transform(bm, matrix=Matrix.Translation((300 - i * 330, 0, 0)) @ Ry(math.pi / 2), verts=since(bm, n0))
    for y in (-150, 0, 150):
        for z in (-150, 0, 150): engine(bm, (-1950, y, z), 50, 120)
    detail(bm, sphere_surface(470, 0.8), 151, scale=30, mats=(DARK, HULL), window=GLOW, window_frac=0.02)
    return 4000

def seraph(bm):         # 1500 m: a Lucent avatar-ship: a pale needle, a haloed heart, three pairs of swept wings edged with light
    body(bm, [(-700, 40, 40, 0.5), (-300, 80, 80, 0.45), (300, 60, 60, 0.45), (760, 4, 4, 0.5)], (0, 0, 0), LUCENT)
    sphere(bm, 95, (-200, 0, 0), mi=LGLOW, seg=24)
    for k, a in enumerate((math.pi / 6, math.pi / 2, 5 * math.pi / 6)):
        for s in (1, -1):
            n0 = set(bm.verts)
            wing(bm, 40 - k * 60, -320 - k * 60, -520 - k * 50, -600 - k * 50, 60, 420 - k * 70, 0, 8, LUCENT, s)
            wing(bm, -500 - k * 50, -600 - k * 50, -560 - k * 50, -640 - k * 50, 60, 430 - k * 70, 0, 9, LGLOW, s)   # the trailing edge glows
            bmesh.ops.transform(bm, matrix=Matrix.Rotation(a - math.pi / 2, 4, 'X'), verts=since(bm, n0))
    for k in range(3):
        n0 = set(bm.verts)
        solid_ring(bm, 150 + k * 40, 158 + k * 40, -4, 4, 48, mats=(LGLOW, LUCENT, LUCENT, LUCENT))
        bmesh.ops.transform(bm, matrix=Matrix.Translation((-200, 0, 0)) @ Ry(math.pi / 2) @ Matrix.Rotation(0.5 * k, 4, 'X'), verts=since(bm, n0))
    engine(bm, (-700, 0, 0), 36, 60)
    return 1500

NAMED = {"Battlecruiser": battlecruiser, "Juggernaut": juggernaut, "Colossus": colossus2, "Dreadnought": dreadnought,
         "Leviathan": leviathan, "GlyphArk": glyph_ark, "Devourer": devourer, "Seraph": seraph}

BUILDERS = [shuttle, corvette, frigate, destroyer, cruiser, battleship, carrier, titan, colossus]

def build_class(k):
    """k: a legacy index (0-8, exported as ship<k>.glb) or a class name from NAMED (exported as ship<Name>.glb)."""
    named = isinstance(k, str)
    col, root = setup(f"Ship{k}")
    bm = bmesh.new()
    length = (NAMED[k] if named else BUILDERS[k])(bm)
    # UVs in metres first (so plating keeps its real scale), then shrink to 2 units long and turn the nose to -Y
    uv_map(bm, "box")
    for f in bm.faces: f.tag = True
    xs = [v.co.x for v in bm.verts]
    mid = (max(xs) + min(xs)) / 2
    span = max(xs) - min(xs)
    turn = Matrix(((0, 1, 0), (-1, 0, 0), (0, 0, 1))).to_4x4()          # +X (nose) -> -Y
    bmesh.ops.transform(bm, matrix=turn @ Matrix.Scale(2.0 / span, 4) @ Matrix.Translation((-mid, 0, 0)), verts=bm.verts[:])
    o = obj("Hull", bm, MATS)
    os.makedirs(SHIPS, exist_ok=True)
    for ob in bpy.context.scene.objects: ob.select_set(False)
    o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(SHIPS, f"ship{k}.glb"), export_format="GLB", use_selection=True, export_apply=True, export_image_format="AUTO")
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

result = {}
for k in globals().get("NAMES", []):
    result[k] = build_class(k)
for k in globals().get("ONLY", range(9) if not globals().get("NAMES") and not globals().get("ALIENS") else []):
    result[k] = build_class(k)

if globals().get("GALLERY"):
    # all nine side by side at their in-game relative sizes, for a preview render
    scales = [1, 1.4, 1.9, 2.5, 3.3, 4.4, 4.8, 7, 10]
    x = 0.0
    for k in range(9):
        c = bpy.data.collections[f"Ship{k}"]
        c.hide_render = c.hide_viewport = False
        rt = next(o for o in c.objects if o.parent is None)
        sc = scales[k] * 0.5
        x += sc * 1.2
        rt.location = (x, 0, 0); rt.scale = (sc, sc, sc); rt.rotation_euler = (0.25, 0, 2.4)
        x += sc * 1.2
    OUT = "ships/gallery"; CAM = ((x * 0.55, -x * 0.42, x * 0.16), (x * 0.5, 0, 0)); LENS = 35; RES = (1800, 700); EXPOSURE = 0.6
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/render.py").read())

# ------------------------------------------------------------------ alien hull families (style 1 organic, 2 crystalline)
M["Bronze"] = mat("Bronze", tex="hull", tint=(0.62, 0.46, 0.3))
M["Chitin"] = mat("Chitin", (0.14, 0.11, 0.08), 0.5, 0.3)
M["Vein"] = mat("Vein", (0.4, 1.0, 0.7), emit=(0.35, 1.0, 0.65), strength=1.5)
M["Crystal"] = mat("Crystal", (0.32, 0.26, 0.52), 0.35, 0.2)
M["CrystalLit"] = mat("CrystalLit", (0.55, 0.45, 0.85), 0.2, 0.25)
M["Prism"] = mat("Prism", (0.9, 0.6, 1.0), emit=(0.85, 0.5, 1.0), strength=1.2)
AMATS = {1: ["Bronze", "Chitin", "Vein"], 2: ["Crystal", "CrystalLit", "Prism"]}

def organic(bm, k):
    """A seed-pod body: a rounded, segmented hull with ribs, swept crescent wings and glowing veins; bigger
    classes grow more segments, more wings, and dorsal sails."""
    L = 40 * (1 + k * 0.9)
    W = L * (0.16 + 0.015 * k)
    segs = 4 + k
    secs = []
    for i in range(segs + 1):
        u = i / segs
        x = -L / 2 + L * u
        prof = math.sin(math.pi * min(1, 0.08 + u * 0.95)) ** 0.7
        secs.append((x, W * prof + 0.5, W * 0.75 * prof + 0.4, 0.45))
    body(bm, secs, mi=0)
    for i in range(1, segs):                          # chitin ribs between the segments
        x, hw, hh, _ = secs[i]
        n0 = set(bm.verts)
        m = 2 if i % 2 else 1                             # alternate ribs glow: bands of light down the body
        c = solid_ring(bm, hw * 0.98, hw * (1.04 if m == 2 else 1.08), -L * 0.012, L * 0.012, 24, mats=(m, m, m, m))
        bmesh.ops.transform(bm, matrix=Matrix.Translation((x, 0, 0)) @ Ry(math.pi / 2) @ Matrix.Scale(1.0, 4), verts=since(bm, n0))
        for v in since(bm, n0): v.co.z *= hh / hw
    for pair in range(1 + k // 3):                     # crescent wings, sweeping back in pairs
        x0 = L * (0.12 - pair * 0.2)
        span = W * (0.7 + 0.08 * k) / (1 + pair * 0.4)
        for s in (1, -1):
            wing(bm, x0, x0 - L * 0.24, x0 - L * 0.36, x0 - L * 0.3, W * 0.45, span, -W * 0.2 + pair * W * 0.3, W * 0.05, 1, s)
    if k >= 4:                                        # a dorsal sail
        body(bm, [(-L * 0.3, W * 0.05, W * 0.4, 0.4), (-L * 0.05, W * 0.05, W * 0.9, 0.4), (L * 0.15, W * 0.04, W * 0.3, 0.4)], (0, 0, W * 0.7), 1)
    sphere(bm, W * 0.22, (-L / 2 + W * 0.05, 0, 0), mi=2, seg=12)            # the heart-light at the stern
    for i in range(k):                                # pods along the flanks
        for s in (1, -1): sphere(bm, W * 0.16, (L * (0.25 - i * 0.12), s * W * 0.95, -W * 0.2), mi=1, seg=8)
    return L

def crystal(bm, k):
    """A cluster of faceted shards round a long central prism, lit from within; bigger classes grow more shards."""
    rnd = random.Random(k * 13 + 5)
    L = 40 * (1 + k * 0.9)
    R = L * 0.1
    uv = bm.loops.layers.uv.verify()
    def shard(at, d, length, w, mi_body=0, tip=True):
        d = d.normalized()
        y = d.cross(Z if abs(d.z) < 0.9 else X).normalized(); z = d.cross(y)
        rot = Matrix((y, z, d)).transposed()
        fast_prism(bm, ellipse(w, w * 0.8, 6), length * 0.7, at, rot, mi_body, uv, top=(0.75, 0.75, 0, 0), base_cap=True)
        fast_prism(bm, [(x * 0.75, y2 * 0.75) for x, y2 in ellipse(w, w * 0.8, 6)], length * 0.3, at + d * length * 0.7, rot, 2 if tip else mi_body, uv, top=(0.02, 0.02, 0, 0))
    shard(Vector((-L / 2, 0, 0)), X, L, R * 2, 0)
    shard(Vector((-L / 2 + 0.01, 0, 0)), -X, L * 0.2, R * 1.4, 1)
    n = 3 + int(k * 1.5)
    for i in range(n):
        a = i * 2 * math.pi / n + rnd.uniform(-0.3, 0.3)
        x = rnd.uniform(-L * 0.2, L * 0.3)
        out = Vector((0, math.cos(a), math.sin(a)))
        d = (-X * rnd.uniform(0.8, 1.6) + out).normalized()      # swept back, like a comet's tail
        shard(Vector((x, 0, 0)) + out * R * 0.6, d, rnd.uniform(L * 0.2, L * 0.45), R * rnd.uniform(0.5, 1.0), rnd.choice((0, 1)))
    for i in range(2 + k):                            # inner light showing between the shards
        sphere(bm, R * 0.35, (rnd.uniform(-L * 0.3, L * 0.2), rnd.uniform(-R, R) * 0.5, rnd.uniform(-R, R) * 0.5), mi=2, seg=8)
    return L

def build_alien(style, k, name=None):
    col, root = setup(f"Ship{style}_{name or k}")
    bm = bmesh.new()
    (organic if style == 1 else crystal)(bm, k)
    uv_map(bm, "box")
    for f in bm.faces: f.tag = True
    xs = [v.co.x for v in bm.verts]
    mid = (max(xs) + min(xs)) / 2; span = max(xs) - min(xs)
    turn = Matrix(((0, 1, 0), (-1, 0, 0), (0, 0, 1))).to_4x4()
    bmesh.ops.transform(bm, matrix=turn @ Matrix.Scale(2.0 / span, 4) @ Matrix.Translation((-mid, 0, 0)), verts=bm.verts[:])
    o = obj("Hull", bm, AMATS[style], smooth=style == 1)
    for ob in bpy.context.scene.objects: ob.select_set(False)
    o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(SHIPS, f"ship{style}_{name or k}.glb"), export_format="GLB", use_selection=True, export_apply=True, export_image_format="AUTO")
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

if globals().get("ALIENS"):
    for st in (1, 2):
        for k in range(9):
            result[f"{st}_{k}"] = build_alien(st, k)

# the tech bible's hulls for the alien families: the same growth rules pushed further, one size step per class
ALIEN_K = {"Battlecruiser": 5, "Juggernaut": 9, "Colossus": 10, "Dreadnought": 10, "Leviathan": 11, "GlyphArk": 12, "Devourer": 13, "Seraph": 8}
if globals().get("ALIENS_NAMED"):
    for st in (1, 2):
        for nm, kk in ALIEN_K.items():
            result[f"{st}_{nm}"] = build_alien(st, kk, nm)
