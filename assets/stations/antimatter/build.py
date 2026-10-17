"""
Antimatter Factory v2 (asset overhaul, docs/ASSET_REFERENCES.md). After particle-accelerator and orbital industry
references: a collider ring 300 m across built of bending-magnet blocks and RF cavities, hazard-banded every few
segments, with the glowing beam pipe as the object named "ring" (the game pulses it); four spokes to a hub where the
Penning trap sits in a heavy cage; four containment bottles on struts; cryo tank clusters; a control module with
windows; four tangential solar wings; radiator banks; a containment tanker docked; strobes. Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/antimatter/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Antimatter")
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Solar", "Plasma", "Glow", "NavRed", "Chrome"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, SOLAR, PLASMA, GLOW, RED, CHROME = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()
R0 = 150.0

# ---------------------------------------------------------------- the collider ring: magnets and cavities round a vacuum tube
bm = bmesh.new()
n0 = set(bm.verts)
solid_ring(bm, R0 - 4.5, R0 + 4.5, -4.5, 4.5, 96, mats=(DARK, DARK, DARK, DARK))
N = 48
for k in range(N):
    a = k * 2 * math.pi / N
    d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
    R = Matrix((t, d, Z_)).transposed()
    c = d * R0
    if k % 6 == 0:
        # an RF cavity: a fat drum on the beamline, banded hazard
        cyl(bm, 16, 16, 14, c, R.to_4x4() @ Ry(math.pi / 2), seg=16, mi=LIGHT)
        for e in (-1, 1): band(bm, c + t * e * 6.4, t, 16, 1.6, 1.0, HAZ, 16)
        strobe(bm, c + Z_ * 12, 0.8, STROBE)
    else:
        # a bending magnet: a C-shaped yoke block round the tube
        fast_prism(bm, octo(15, 26, 5), 24, c - Z_ * 12, R, HULL if k % 2 else DARK, U(bm), top=(0.9, 0.85, 0, 0), base_cap=True)
        if k % 2 == 0: module(bm, c + d * 18 + Z_ * 4, t, Z_, 10, 8, 8, mats=(LIGHT, DARK, WIN), seed=k)
        fast_box(bm, (12, 0.6, 3), c + d * 13.2, R, ACC if k % 3 == 1 else HULL, U(bm), bottom=True)
        fast_box(bm, (2.4, 1.0, 1.0), c - d * 8.2 + Z_ * 3, R, WCOOL, U(bm), bottom=True)
# an outer service ring on brackets, with a walkway and lights
solid_ring(bm, R0 + 26, R0 + 30, -2, 2, 96, mats=(DARK, HULL, HULL, LIGHT))
for k in range(24):
    a = k * 2 * math.pi / 24 + 0.06
    d = Vector((math.cos(a), math.sin(a), 0))
    _rod(bm, d * (R0 + 13), d * (R0 + 26), 0.9, DARK)
    strobe(bm, d * (R0 + 30.5) + Z_ * 2.2, 0.5, STROBE)
greeble(bm, disc_surface(R0 - 7, R0 + 7, 12.1), 0.1, 3, size=(0.6, 2.4), height=(0.3, 1.2), mats=(HULL, DARK), window=WCOOL, window_frac=0.06)
obj("Collider", bm, MATS)
bm = bmesh.new()
solid_ring(bm, R0 - 1.0, R0 + 1.0, -1.0, 1.0, 96, mats=(PLASMA,) * 4)       # the beam, pulsed by the game
obj("ring", bm, MATS)

# ---------------------------------------------------------------- spokes, hub and the trap in its cage
bm = bmesh.new()
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    truss(bm, d * 30, d * (R0 - 10), w=7, mi=DARK, rod=0.5)
    pipe_run(bm, [d * 32 + Z_ * 5, d * (R0 - 12) + Z_ * 5], 0.9, n=2, mi=CHROME, clamp_mi=DARK)
loft(bm, [(-34, 24, 24, 0.4), (-20, 36, 36, 0.4), (20, 36, 36, 0.4), (34, 24, 24, 0.4)], UPX, Vector((0, 0, 0)), HULL)
detail(bm, cyl_surface(36.2, -18, 18), 7, scale=1.4, mats=(HULL, DARK), window=WIN, window_frac=0.08)
tank_cage(bm, Vector((0, 0, 44)), 16, axis=Z_, capsule=0.0, mi_tank=CHROME, mi_cage=ACC, ribs=6)
loft(bm, [(24, 10, 10, 0.4), (30, 8, 8, 0.4)], UPX, Vector((0, 0, 0)), DARK)
for k in range(4):                                   # magnet coils round the trap
    band(bm, Vector((0, 0, 32 + k * 8)), Z_, 17.5, 1.6, 1.5, HAZ if k % 2 else DARK, 24)
sphere(bm, 5, Vector((0, 0, 44)), mi=GLOW, seg=12)
obj("Hub", bm, MATS)

# ---------------------------------------------------------------- containment bottles, cryo tanks, control module, radiators
bm = bmesh.new()
for k in range(4):
    a = k * math.pi / 2
    d = Vector((math.cos(a), math.sin(a), 0))
    p = d * 70 - Z_ * 22
    truss(bm, d * 26 - Z_ * 10, p, w=4, mi=DARK, rod=0.35)
    tank_cage(bm, p, 9, axis=Vector((-d.y, d.x, 0)), capsule=1.6, mi_tank=LIGHT, mi_cage=HAZ)
    for j in (-1, 1):
        tank_cage(bm, d * 100 + Vector((-d.y, d.x, 0)) * j * 12 + Z_ * 14, 5, axis=Z_, capsule=1.2, mi_tank=ACC if j > 0 else HULL, mi_cage=DARK)
    _rod(bm, d * 100 + Z_ * 6, d * (R0 - 8) + Z_ * 2, 0.7, DARK)
module(bm, Vector((0, -40, -26)), X_, Z_, 46, 16, 12, mats=(HULL, DARK, WIN), seed=21)
dish(bm, Vector((14, -40, -18)), Vector((0.3, -0.5, -0.8)), 6, mi_dish=LIGHT, mi_frame=DARK)
for sx in (1, -1):
    radiator_bank(bm, Vector((sx * 30, 0, -14)), Vector((sx, 0, -0.3)), Y_, 10, 24, 8, 3.4, mi_fin=LIGHT, mi_man=DARK)
dock_arm(bm, Vector((0, 26, -10)), Vector((0, 1, -0.5)), 34, 30, mats=(ACC, DARK, GLOW, WIN), seed=31)
antennas(bm, Vector((0, 0, 60)), Z_, 5, 18, 9, mi=DARK, light=RED)
obj("Works", bm, MATS)

# ---------------------------------------------------------------- four solar wings tangent to the ring, powering it
bm = bmesh.new()
for k in range(4):
    a = k * math.pi / 2
    d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
    base = d * (R0 + 6)
    solar_wing(bm, base, (d * 0.15 + t).normalized(), Z_, 120, 34, panels=8, mi_cell=SOLAR, mi_frame=DARK)
    strobe(bm, base + (d * 0.15 + t).normalized() * 122, 1.0, RED)
obj("Wings", bm, MATS)

lamp("L_Trap", (0, 0, 44), 1.5e5, (0.55, 0.75, 1.0), 10)
result = export(col, root, "antimatter")
