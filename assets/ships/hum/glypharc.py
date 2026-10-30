"""
Glyph Ark, 200 km. A runic super-ark (tech HUL-13, after the Glyph line of techs). References (Bing sheet "ancient ark
starship glowing runes monolith concept art": the glowing ancient monuments, the "ancient monolith starship" with its
banded spire, the runed monoliths lit teal and blue): a long dark obelisk of a hull, square in section with chamfered
edges, ringed by bands of glowing glyphs; three rings of monolith slabs held round it by nothing visible, each slab
carved with its own line of glyphs; a spire at the prow and a ring of light round the stern drives. Stone-dark plate,
teal light; the navy's windows and drives where people live and steer. Crew ~600,000,000 (an ark of the archive).
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/glypharc.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipGlyphArk")
hull_scale(200000)
M["ArkStone"] = painted("HullDark", "ArkStone", 0.2, tint=(0.4, 0.42, 0.46), rough=0.6)
M["Glyph"] = bpy.data.materials.get("Glyph") or mat("Glyph", (0.3, 0.95, 0.9), emit=(0.2, 0.9, 0.85), strength=0.7)
SM2 = SM + ["ArkStone", "Glyph"]
STONE, GLYPH = len(SM), len(SM) + 1
bm = bmesh.new()
rnd = random.Random(1400)


def glyphs(at, along, across, out, length, height, cell):
    """A line of glyphs carved into a face: each cell a few strokes, upright and crossbar, read along `along`."""
    F = Matrix((along, across, out)).transposed()
    n = int(length / cell)
    for i in range(n):
        c = at + along * ((i + 0.5) * cell - length / 2)
        for _ in range(rnd.randint(2, 4)):
            if rnd.random() < 0.5:
                fast_box(bm, (cell * 0.12, height * rnd.uniform(0.5, 1.0), height * 0.04), c + along * rnd.uniform(-0.35, 0.35) * cell + across * rnd.uniform(-0.2, 0.2) * height, F, GLYPH, UVL(bm))
            else:
                fast_box(bm, (cell * rnd.uniform(0.4, 0.8), height * 0.1, height * 0.04), c + across * rnd.uniform(-0.45, 0.45) * height, F, GLYPH, UVL(bm))


# --- the obelisk: square in section, chamfered, tapering to the prow; a spire past it
keel(bm, [(-100000, 9000, 9000, 0), (-94000, 12500, 12500, 0), (60000, 11000, 11000, 0), (86000, 6000, 6000, 0), (96000, 1800, 1800, 0)], STONE, 0.18)
cyl(bm, 1500, 40, 18000, Vector((104000, 0, 0)), Ry(math.pi / 2), seg=8, mi=STONE)
for x in (88000, 93000):
    band(bm, Vector((x, 0, 0)), X_, 5200 if x < 90000 else 2600, 500, 300, GLYPH, 8)
# glyph bands round the hull: on all four faces at each station, with a raised stone frame either side
faces = [(Vector((0, 1, 0)), Vector((0, 0, 1))), (Vector((0, -1, 0)), Vector((0, 0, -1))), (Vector((0, 0, 1)), Vector((0, -1, 0))), (Vector((0, 0, -1)), Vector((0, 1, 0)))]
for x in range(-84000, 56000, 14000):
    t = (x + 94000) / 154000.0
    hw = 12500 + (11000 - 12500) * t
    for out, across in faces:
        glyphs(Vector((x, 0, 0)) + out * (hw + 40), X_, across, out, 9000 if abs(x) % 28000 else 5000, 4200, 1100)
    for dx in (-3000, 3000):
        band(bm, Vector((x + dx, 0, 0)), X_, hw * 1.08, 500, 260, STONE, 8)
# a long glyph line down each face between the bands, and the archive's lit decks along the flanks
for out, across in faces:
    glyphs(Vector((-15000, 0, 0)) + out * 12040 - across * 6000, X_, across, out, 140000, 900, 900)
for s in (1, -1):
    edge_windows(bm, (-90000, s * 12560), (55000, s * 11040), -3000, 6, (0, s), lit=0.35, seed=1410 + s, strip=(40, 900))
# --- three rings of monolith slabs, eight to a ring, standing off the hull, each carved down its outer face
for k, x in enumerate((45000, -5000, -55000)):
    for j in range(8):
        a = j * math.pi / 4 + k * math.pi / 8
        out = Vector((0, math.cos(a), math.sin(a))); across = out.cross(X_)
        c = Vector((x, 0, 0)) + out * 26000
        F = Matrix((X_, across, out)).transposed()
        fast_box(bm, (16000, 6000, 2400), c, F, STONE, UVL(bm), bottom=True)
        fast_box(bm, (17000, 6600, 600), c - out * 900, F, DARK, UVL(bm), bottom=True)
        glyphs(c + out * 1220, X_, across, out, 13000, 1800, 1100)
# --- the stern: a deep drive block, four engines and a ring of light round them
plate(bm, rect(-104000, -98000, -10000, 10000), -10000, 20000, DARK, 800)
for y, z in ((5500, 5500), (-5500, 5500), (5500, -5500), (-5500, -5500)):
    engine(bm, (-104500, y, z), 4200, 9000, mi=STONE, bands=3, seg=16)
n0 = set(bm.verts)
solid_ring(bm, 15500, 17000, -600, 600, 48, mats=(GLYPH, GLYPH, STONE, GLYPH))
bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=since(bm, n0))
bmesh.ops.translate(bm, vec=Vector((-106000, 0, 0)), verts=since(bm, n0))
nav(bm, [(0, 13000, 0)], [(0, -13000, 0)], [(110000, 0, 0)])
o = obj("glypharc", bm, SM2)
result = export_ship(col, root, "GlyphArk")
