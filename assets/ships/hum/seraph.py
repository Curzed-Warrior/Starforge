"""
Seraph, 8 km. The lucent avatar-ship, one per civilisation (tech HUL-15). References (Bing sheet "seraph angel
starship concept art wings white gold": the six-winged gold seraph figures, Jonas Pfeiffer's white seraphim): a slim
white lance of a hull with a crowned prow, a gold halo standing round it behind the head, and three pairs of long
swept wings built from overlapping blade feathers (the upper pair raised, the lower pair lowered, as the seraph's
six wings), a lantern of light on the back, and long tail blades past the drives. The navy's plating, windows and
drives, but white and gold where the line ships are gunmetal and grey. Crew ~120,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/seraph.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipSeraph")
hull_scale(8000)
M["SeraphGold"] = painted("HullLight", "SeraphGold", 0.6, tint=(1.0, 0.78, 0.4), rough=0.4)
M["Lucent"] = bpy.data.materials.get("Lucent") or mat("Lucent", (1.0, 0.93, 0.75), emit=(1.0, 0.86, 0.58), strength=0.7)
SM2 = SM + ["SeraphGold", "Lucent"]
GOLD, LUCENT = len(SM), len(SM) + 1
bm = bmesh.new()


def turn(vs, a, axis="X", at=(0, 0, 0)):
    bmesh.ops.rotate(bm, cent=Vector(at), matrix=Matrix.Rotation(a, 3, axis), verts=vs)


# --- the lance: a slim octagonal hull, a long fine prow, fullest over the wing roots
keel(bm, [(4000, 8, 8, 40), (3500, 110, 120, 30), (2600, 220, 250, 0), (300, 270, 300, 0), (-2400, 230, 260, 0),
          (-3500, 250, 250, 0), (-3800, 210, 210, 0)], LIGHT, 0.42)
for s in (1, -1):
    ribs(bm, -3300, 2300, s * 272, -200, 200, s, 90, depth=14, seed=1500 + s, gap=0.2, mi=(HULL, LIGHT))
    edge_windows(bm, (-3200, s * 286), (2200, s * 286), -40, 12, (0, s), lit=0.4, seed=1502 + s, strip=(5, 60))
strips(bm, -3300, 2300, -180, 180, 300, 6, h=14, seed=1504, mi=HULL)
# the crown: gold blade plates raked back over the prow
for k, (x, w) in enumerate(((3300, 90), (2950, 150), (2600, 210))):
    plate(bm, mirror([(x + 260, 0), (x - 120, w), (x - 260, w * 0.8), (x - 200, 0)]), 230 - k * 6 + k * 20, 40, GOLD, 12)
# --- the halo: a gold ring held up over the head on a raked mast, tilted back like a saint's
n0 = set(bm.verts)
solid_ring(bm, 520, 600, -35, 35, 64, mats=(LUCENT, GOLD, GOLD, GOLD), bevel=8)
turn(since(bm, n0), 0.5, "Y")
bmesh.ops.translate(bm, vec=Vector((2050, 0, 900)), verts=since(bm, n0))
n0 = set(bm.verts)
cyl(bm, 40, 22, 700, Vector((0, 0, 0)), seg=8, mi=GOLD)
turn(since(bm, n0), -0.5, "Y")
bmesh.ops.translate(bm, vec=Vector((2250, 0, 560)), verts=since(bm, n0))


# --- six wings in three pairs: a broad covert panel from the hull, gold along its leading edge, and long tapered
# primary feathers fanned from its trailing edge, outward at the tip to straight back at the root
def wing(x0, span, chord, n, dihedral, s):
    n0 = set(bm.verts)
    tip = (x0 - chord * 0.55, 200 + span * 0.5)
    plate(bm, [(x, s * y) for x, y in [(x0, 200), tip, (tip[0] - 520, tip[1] - 40), (x0 - chord, 200)]], -20, 40, LIGHT, 14)
    plate(bm, [(x, s * y) for x, y in [(x0 + 20, 200), (tip[0] + 20, tip[1] + 10), (tip[0] - 110, tip[1]), (x0 - 140, 200)]], -12, 40, GOLD, 10)
    plate(bm, [(x, s * y) for x, y in [(x0 + 30, 200), (tip[0] + 30, tip[1] + 14), (tip[0] + 10, tip[1] + 14), (x0 + 10, 200)]], -22, 8, LUCENT, 2)
    for k in range(n):
        t = k / max(n - 1, 1)
        bx = tip[0] - 380 + (x0 - chord * 0.9 - tip[0] + 380) * t
        by = tip[1] - 30 + (330 - tip[1]) * t
        phi = math.radians(108 + 62 * t)
        d = Vector((math.cos(phi), math.sin(phi), 0)); q = Vector((-d.y, d.x, 0))
        L = span * 0.6 * (1 - 0.4 * t); w = 300 - 60 * t
        b0 = Vector((bx, by, 0))
        out = [b0 - q * w / 2, b0 + q * w / 2, b0 + q * w * 0.42 + d * L * 0.78, b0 + d * L, b0 - q * w * 0.38 + d * L * 0.84]
        plate(bm, [(v.x, s * v.y) for v in out], -30 - k * 5, 26, GOLD if k == 0 else LIGHT if k % 2 else HULL, 8)
    turn(since(bm, n0), s * dihedral)


for x0, span, chord, n, dih in ((1500, 2600, 1300, 5, 0.7), (600, 3600, 1700, 7, 0.0), (-700, 2200, 1200, 5, -0.7)):
    for s in (1, -1):
        wing(x0, span, chord, n, dih, s)
# --- the lantern on the back: a glazed lucent dome in a gold cage
sphere(bm, 200, Vector((-500, 0, 300)), mi=LUCENT, seg=20)
for k in range(6):
    a = k * math.pi / 3
    n0 = set(bm.verts)
    cyl(bm, 14, 14, 520, Vector((-500, 0, 300)), Rx(math.pi / 2), seg=6, mi=GOLD)
    turn(since(bm, n0), a, "Z", at=(-500, 0, 300))
# --- the drives: three gold-collared engines in the stern, two long tail blades past them
for y, z in ((0, 90), (150, -90), (-150, -90)):
    engine(bm, (-3750, y, z), 120, 380, mi=HULL, bands=3, seg=20)
    cyl(bm, 136, 136, 40, Vector((-3700, y, z)), Ry(math.pi / 2), seg=20, mi=GOLD, caps=False)
for s in (1, -1):
    n0 = set(bm.verts)
    plate(bm, [(-3300, 0), (-3600, 0), (-4600, s * 520), (-4500, s * 560)], -15, 30, GOLD, 8)
    turn(since(bm, n0), s * 0.35)
nav(bm, [(-800, 4300, 0)], [(-800, -4300, 0)], [(-3900, 0, 260)])
o = obj("seraph", bm, SM2)
result = export_ship(col, root, "Seraph")
