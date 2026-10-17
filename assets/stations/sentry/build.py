"""
Sentry Array v2 (asset overhaul, docs/ASSET_REFERENCES.md). After giant radio telescope references: a dish 520 m wide
of radial ribs and panel rings (panels in two tones with gaps), opening toward -Y (the game's +Z); a feed cabin at the
focus on a quadrupod of trusses; an alt-az yoke behind it on a turntable over a base drum of modules, with a cluster
of smaller dishes, solar wings, radiators, docked tenders and strobes. Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/sentry/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Sentry")
M["Panel"] = mat("Panel", (0.72, 0.74, 0.76), 0.5, 0.35)
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Panel", "Solar", "Radiator", "NavRed", "Glow"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, PANEL, SOLAR, RADI, RED, GLOW = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()
R, F = 260.0, 300.0
def D(r, a): return Vector((math.cos(a) * r, -r * r / (4 * F), math.sin(a) * r))

# ---------------------------------------------------------------- the dish: panel rings between radial ribs
bm = bmesh.new()
RINGS, SPOKES = 9, 36
for i in range(RINGS):
    r0, r1 = 24 + (R - 24) * i / RINGS, 24 + (R - 24) * (i + 1) / RINGS
    for k in range(SPOKES):
        a0, a1 = k * 2 * math.pi / SPOKES, (k + 1) * 2 * math.pi / SPOKES
        g = 0.006
        q = [D(r0 + 0.8, a0 + g), D(r1 - 0.8, a0 + g), D(r1 - 0.8, a1 - g), D(r0 + 0.8, a1 - g)]
        f = bm.faces.new([bm.verts.new(v) for v in q])
        f.material_index = PANEL if (i + k) % 7 else LIGHT
bm.normal_update()
bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f.normal.y > 0])
for k in range(SPOKES):                               # ribs behind the panels
    a = k * 2 * math.pi / SPOKES
    pts = [D(24 + (R - 24) * i / RINGS, a) + Y_ * 2.5 for i in range(RINGS + 1)]
    for pa, pb in zip(pts, pts[1:]): _rod(bm, pa, pb, 1.6 if k % 3 == 0 else 0.9, DARK)
for i in range(1, RINGS + 1, 2):                      # hoop ribs
    r = 24 + (R - 24) * i / RINGS
    n0 = set(bm.verts)
    solid_ring(bm, r - 1.2, r + 1.2, -1.2, 1.2, 72, mats=(DARK,) * 4)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, D(r, 0).y + 3, 0)) @ Rx(math.pi / 2), verts=since(bm, n0))
n0 = set(bm.verts)
solid_ring(bm, R, R + 8, -4, 4, 96, mats=(DARK, HULL, ACC, HULL))
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, D(R, 0).y, 0)) @ Rx(math.pi / 2), verts=since(bm, n0))
for k in range(12):
    a = k * math.pi / 6
    strobe(bm, Vector((math.cos(a) * (R + 9), D(R, 0).y, math.sin(a) * (R + 9))), 1.4, RED if k % 3 == 0 else STROBE)
# the backing structure: a deep white lattice behind the panels, as dense as the real thing
for k in range(0, SPOKES, 2):
    a = k * 2 * math.pi / SPOKES
    for i in range(2, RINGS + 1, 2):
        r = 24 + (R - 24) * i / RINGS
        p = D(r, a) + Y_ * 3
        q = Vector((math.cos(a) * r * 0.7, D(r, a).y + 26 + (R - r) * 0.12, math.sin(a) * r * 0.7))
        _rod(bm, p, q, 0.7, LIGHT)
        a2 = (k + 2) * 2 * math.pi / SPOKES
        _rod(bm, q, Vector((math.cos(a2) * r * 0.7, D(r, a2).y + 26 + (R - r) * 0.12, math.sin(a2) * r * 0.7)), 0.6, LIGHT)
obj("Dish", bm, MATS)

# ---------------------------------------------------------------- the feed cabin on its quadrupod
bm = bmesh.new()
fy = -F * 0.92
# after the Green Bank Telescope: one great offset feed arm rising from the lower rim, not a symmetric quadrupod
base_l, base_r = D(R * 0.98, -math.pi / 2 - 0.22) + Y_ * 4, D(R * 0.98, -math.pi / 2 + 0.22) + Y_ * 4
knee = Vector((0, fy * 0.55, -R * 0.55))
for b in (base_l, base_r):
    truss(bm, b, knee, w=10, mi=LIGHT, rod=0.7)
truss(bm, knee, Vector((0, fy + 10, -14)), w=9, mi=LIGHT, rod=0.65)
for t in (0.25, 0.5, 0.75):
    _rod(bm, base_l.lerp(knee, t), base_r.lerp(knee, t), 0.8, LIGHT)
module(bm, Vector((0, fy, 0)), Y_, Z_, 26, 22, 22, mats=(LIGHT, DARK, WIN), seed=5)
cyl(bm, 6, 12, 18, Vector((0, fy - 20, 0)), Rx(math.pi / 2), seg=16, mi=HULL)                     # the feed horn
cyl(bm, 5.5, 5.5, 0.6, Vector((0, fy - 29.4, 0)), Rx(math.pi / 2), seg=16, mi=GLOW)
antennas(bm, Vector((0, fy, 12)), Z_, 4, 14, 6, mi=DARK, light=RED)
obj("Feed", bm, MATS)

# ---------------------------------------------------------------- the yoke, turntable and base drum
bm = bmesh.new()
for sx in (1, -1):
    loft(bm, [(0, 10, 10, 0.4), (110, 8, 8, 0.4)], basis(Vector((sx * 0.55, 1, 0))), Vector((sx * 30, 8, 0)), HULL)
    cyl(bm, 14, 14, 10, Vector((sx * 92, 70, 0)), Ry(math.pi / 2), seg=20, mi=ACC)
loft(bm, [(0, 30, 30, 0.35), (16, 34, 34, 0.35)], basis(Y_), Vector((0, 70, 0)), DARK)
cyl(bm, 60, 60, 12, Vector((0, 100, 0)), Rx(math.pi / 2), seg=40, mi=HULL)                          # turntable
loft(bm, [(0, 64, 64, 0.3), (60, 70, 70, 0.3), (80, 50, 50, 0.3)], basis(Y_), Vector((0, 106, 0)), LIGHT)   # base drum
detail(bm, plane_surface(Vector((-60, 112, 70.5)), Vector((0, 50, 0)), Vector((120, 0, 0))), 21, scale=1.3, mats=(LIGHT, DARK), window=WIN, window_frac=0.07)
for sx in (1, -1):
    window_rows(bm, plane_surface(Vector((sx * 70.5, 115, -40)), Vector((0, 40, 0)) * sx, Vector((0, 0, 80))), 22, 5, size=(1.6, 0.8), mi=WIN, seed=22 + sx, lit=0.7)
    radiator_bank(bm, Vector((sx * 70, 150, -40)), Vector((sx, 0, -0.4)), Y_, 10, 46, 14, 4, mi_fin=RADI, mi_man=DARK)
    solar_wing(bm, Vector((sx * 70, 140, 40)), Vector((sx, 0.3, 0.6)).normalized(), Y_, 110, 30, panels=7, mi_cell=SOLAR, mi_frame=DARK)
for k, (x, z) in enumerate(((30, 72), (-36, 70), (0, -74))):      # a cluster of smaller dishes on the drum
    dish(bm, Vector((x, 140, z)), Vector((x * 0.01, -0.4, z * 0.012)).normalized(), 12 + k * 3, mi_dish=LIGHT, mi_frame=DARK)
dock_arm(bm, Vector((0, 186, -40)), Vector((0, 1, -0.6)), 30, 30, mats=(HULL, DARK, GLOW, WIN), seed=31)
obj("Base", bm, MATS)
lamp("L_Feed", (0, fy - 26, 0), 4e4, COOL, 6)
result = export(col, root, "sentry")
