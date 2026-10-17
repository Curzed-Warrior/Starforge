"""
Orbital Foundry v2 (asset overhaul, docs/ASSET_REFERENCES.md). After orbital factory and refinery references:
a molten crucible hung in a ribbed cage inside a structural ring, over an octagonal smelting deck; four smelter towers
banded and piped to the crucible; an ore mass driver of coil rings reaching out along -Y to a catch funnel; a rolling
mill along +Y with glowing slots, feeding stacks of hull plate under a gantry; radiator banks off both flanks; parts
tanks slung beneath; ore freighters tied up at docking arms; strobes. ~280 m across. Z is up (the game's Y).
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/foundry/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Foundry")
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Heat", "Glow", "NavRed", "Trim"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, RAD, HEAT, GLOW, RED, TRIM = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()

# ---------------------------------------------------------------- the smelting deck
bm = bmesh.new()
fast_prism(bm, octo(170, 170, 48), 22, Vector((0, 0, -22)), Matrix.Identity(3), DARK, U(bm), top=(0.94, 0.94, 0, 0), base_cap=True)
fast_prism(bm, octo(150, 150, 40), 6, Vector((0, 0, 0)), Matrix.Identity(3), HULL, U(bm), top=(0.97, 0.97, 0, 0))
detail(bm, plane_surface(Vector((-70, -70, 6)), Vector((140, 0, 0)), Vector((0, 140, 0))), 3, scale=1.6, mats=(HULL, DARK), window=WIN, window_frac=0.05, glow=HEAT, glow_frac=0.01)
for k in range(8):                                  # the deck's flanks: window bands and accent plates
    a0 = k * math.pi / 4 + math.pi / 8
    p = Vector((math.cos(a0), math.sin(a0), 0))
    t = Vector((-p.y, p.x, 0))
    c = p * 83.2 - Z_ * 11
    greeble(bm, plane_surface(c - t * 28 - Z_ * 9, Z_ * 18, t * 56), 0.12, 20 + k, size=(1.0, 4), height=(0.3, 1.6), mats=(DARK, HULL), window=WIN, window_frac=0.1)
    window_rows(bm, plane_surface(c - t * 26 - Z_ * 7, Z_ * 14, t * 52), 22, 3, size=(1.4, 0.6), mi=WIN, seed=10 + k, lit=0.75)
    if k % 2: fast_box(bm, (30, 6, 0.8), c + p * 0.6 + Z_ * 6, Matrix((t, Z_, p)).transposed(), ACC, U(bm), bottom=True)
for k in range(8):
    a0 = k * math.pi / 4 + math.pi / 8
    p = Vector((math.cos(a0), math.sin(a0), 0)); t = Vector((-p.y, p.x, 0))
    for z, r in ((-1.5, 80.6), (-20.5, 85.2)):
        fast_box(bm, (60, 0.8, 1.0), p * r + Z_ * z, Matrix((t, p, Z_)).transposed(), GLOW, U(bm), bottom=True)
obj("Deck", bm, MATS)

# ---------------------------------------------------------------- the crucible: a cup of molten metal in a ribbed cage, under a ring
bm = bmesh.new()
cup = [(0.1, 18), (20, 20), (32, 34), (36, 52), (37, 58)]
made = revolve(bm, cup, 48, lambda i, s: DARK, closed=False)
bmesh.ops.reverse_faces(bm, faces=[f for f in made if f.normal.dot(f.calc_center_median() - Vector((0, 0, 40))) < 0])
n0 = set(bm.verts)
revolve(bm, [(0.1, 55), (35, 55)], 48, lambda i, s: HEAT, closed=False)       # the molten surface
for k in range(12):                                  # the cage: ribs rising past the cup to a crown ring
    a = k * math.pi / 6
    d = Vector((math.cos(a), math.sin(a), 0))
    _rod(bm, d * 40 + Z_ * 16, d * 44 + Z_ * 64, 1.8, HULL)
    _rod(bm, d * 44 + Z_ * 64, d * 30 + Z_ * 86, 1.4, HULL)
    if k % 3 == 0: fast_box(bm, (4, 4, 10), d * 41 + Z_ * 40, basis(d), HAZ, U(bm), bottom=True)
n0 = set(bm.verts)
solid_ring(bm, 70, 82, 70, 80, 64, mats=(DARK, HULL, DARK, HULL))       # the structural ring the crucible hangs from
solid_ring(bm, 82, 82.6, 73.5, 76.5, 96, mats=(GLOW,) * 4)                  # a warm lit band round it
greeble(bm, cyl_surface(82.1, 71.5, 78.5), 0.25, 31, size=(0.8, 3), height=(0.3, 1.4), mats=(HULL, DARK), window=WIN, window_frac=0.08)
for k in range(4):                                   # four pylons carrying the ring, and hangers to the cage crown
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    loft(bm, [(0, 6, 6, 0.4), (60, 4, 4, 0.4), (72, 7, 7, 0.4)], UPX, d * 76, HULL)
    _rod(bm, d * 70 + Z_ * 74, d * 30 + Z_ * 86, 1.6, DARK)
    strobe(bm, d * 88 + Z_ * 81, 1.2, STROBE)
# the spine: a hoist column up through the crucible, crowned with a spire, and a keel spire below the deck
loft(bm, [(80, 8, 8, 0.4), (110, 6, 6, 0.4), (116, 10, 10, 0.4), (124, 10, 10, 0.4), (130, 4, 4, 0.4), (190, 1.2, 1.2, 0.4)], UPX, Vector((0, 0, 0)), HULL)
for j in range(6): band(bm, Vector((0, 0, 136 + j * 9)), Z_, 3.6 - j * 0.4, 1.4, 1.4, DARK, 12)
for k in range(4):
    a = k * math.pi / 2
    _rod(bm, Vector((0, 0, 86)), Vector((math.cos(a) * 30, math.sin(a) * 30, 86)), 1.2, DARK)
window_rows(bm, plane_surface(Vector((10.05, -8, 116)), Vector((0, 16, 0)), Vector((0, 0, 8))), 2, 8, size=(1.0, 0.6), mi=WIN, seed=91, lit=0.9)
loft(bm, [(-22, 30, 30, 0.4), (-40, 18, 18, 0.4), (-56, 10, 10, 0.4), (-120, 1.5, 1.5, 0.4)], UPX, Vector((0, 0, 0)), DARK)
for j in range(5): band(bm, Vector((0, 0, -62 - j * 10)), Z_, 7 - j * 1.1, 1.6, 1.4, HULL, 12)
strobe(bm, Vector((0, 0, 191)), 1.4, RED); strobe(bm, Vector((0, 0, -121)), 1.4, RED)
obj("Crucible", bm, MATS)

# ---------------------------------------------------------------- four smelter towers, banded, piped to the crucible, venting at the crown
bm = bmesh.new()
for k in range(4):
    a = k * math.pi / 2
    d = Vector((math.cos(a), math.sin(a), 0))
    base = d * 58 + Vector((-d.y, d.x, 0)) * 26
    loft(bm, [(0, 13, 13, 0.4), (70, 9, 9, 0.4), (78, 11, 11, 0.4), (96, 6, 6, 0.4)], UPX, base, HULL if k % 2 else DARK)
    for j in range(4): band(bm, base + Z_ * (14 + j * 16), Z_, 11.5 - j, 2.4, 1.2, DARK if j % 2 else ACC, 24)
    for j in range(3):                               # vent stacks at the crown, glowing in their throats
        off = Vector((math.cos(j * 2.1), math.sin(j * 2.1), 0)) * 4
        cyl(bm, 1.8, 1.4, 12, base + off + Z_ * 100, Matrix.Identity(4), seg=10, mi=DARK)
        cyl(bm, 1.3, 1.3, 0.4, base + off + Z_ * 106.2, Matrix.Identity(4), seg=10, mi=HEAT)
    pipe_run(bm, [base + Z_ * 30 - d * 10, d * 34 + Z_ * 30], 1.4, n=2, mi=DARK, clamp_mi=HAZ, up=Z_)
    window_rows(bm, plane_surface(base + d * 9.6 + Vector((-d.y, d.x, 0)) * -6 + Z_ * 10, Vector((-d.y, d.x, 0)) * 12, Z_ * 50), 10, 4, size=(1.0, 0.6), mi=WIN, seed=40 + k, lit=0.7)
    antennas(bm, base + Z_ * 97, Z_, 3, 14, 50 + k, mi=DARK, light=RED)
obj("Towers", bm, MATS)

# ---------------------------------------------------------------- the ore mass driver out along -Y, ending in a catch funnel
bm = bmesh.new()
truss(bm, Vector((0, -86, -8)), Vector((0, -240, -8)), w=9, mi=DARK, rod=0.55)
for j in range(16):                                  # accelerator coils
    y = -92 - j * 9.5
    band(bm, Vector((0, y, -8)), Y_, 6.5, 2.6, 2.2, ACC if j % 4 == 0 else HULL, 16)
made_y = -250
n0 = set(bm.verts)
f = revolve(bm, [(10, 0), (16, 4), (34, 22), (40, 32)], 32, lambda i, s: HULL, closed=False)
bmesh.ops.reverse_faces(bm, faces=f)
revolve(bm, [(10.6, 0.5), (16.6, 4.5), (34.6, 22.5), (40.6, 32.5)], 32, lambda i, s: DARK, closed=False)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, -240, -8)) @ Rx(math.pi / 2), verts=since(bm, n0))
for k in range(8):
    a = k * math.pi / 4
    sphere(bm, 1.2, Vector((math.cos(a) * 41, -272, -8 + math.sin(a) * 41)), mi=STROBE, seg=6)
obj("MassDriver", bm, MATS)

# ---------------------------------------------------------------- the rolling mill along +Y, slots glowing, plate stacked under a gantry
bm = bmesh.new()
module(bm, Vector((0, 120, -14)), Y_, Z_, 80, 30, 22, mats=(HULL, DARK, WIN), seed=61, detail_scale=1.0)
for j in range(9):
    for sx in (1, -1):
        fast_box(bm, (0.6, 5, 2.2), Vector((sx * 15.2, 88 + j * 8, -12)), basis(Y_), HEAT, U(bm), bottom=True)
for j in range(5):                                   # stacks of finished hull plate
    for k in range(4):
        fast_box(bm, (22, 14, 1.4), Vector((0, 172 + j * 9, -24 + k * 1.6)), basis(X_), LIGHT if k % 2 else HULL, U(bm), bottom=True)
gantry(bm, Vector((0, 160, -24)), Vector((0, 222, -24)), Z_, 30, 14, mi_rail=DARK, mi_crane=HAZ, cranes=2, seed=7)
obj("Mill", bm, MATS)

# ---------------------------------------------------------------- radiators, tanks, freighters, strobes
bm = bmesh.new()
for sx in (1, -1):
    for row, z in enumerate((-6, -16)):
        radiator_bank(bm, Vector((sx * 86, -20 + row * 34, z)), Vector((sx, 0, 0)), Y_, 12, 42, 9, 3.6, mi_fin=LIGHT, mi_man=DARK)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    tank_cage(bm, Vector((math.cos(a) * 46, math.sin(a) * 46, -38)), 11, axis=Vector((math.cos(a), math.sin(a), 0)), capsule=0.8, mi_tank=ACC if k % 2 else LIGHT, mi_cage=DARK)
dock_arm(bm, Vector((60, -60, -20)), Vector((1, -0.4, -0.2)), 30, 34, mats=(HULL, DARK, GLOW, WIN), seed=71)
dock_arm(bm, Vector((-60, 60, -20)), Vector((-1, 0.3, -0.3)), 26, 28, mats=(LIGHT, DARK, GLOW, WIN), seed=72)
for k in range(8):
    a = k * math.pi / 4 + math.pi / 8
    strobe(bm, Vector((math.cos(a) * 92, math.sin(a) * 92, -11)), 1.0, RED if k % 2 else STROBE)
obj("Works", bm, MATS)

lamp("L_Melt", (0, 0, 62), 3e5, FIRE, 20)
for k in range(4):
    a = k * math.pi / 2
    lamp(f"L_Tower{k}", (math.cos(a) * 58, math.sin(a) * 58, 108), 2e4, FIRE, 4)
result = export(col, root, "foundry")
