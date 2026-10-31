"""
Leviathan, 80 km. A tamed space-beast flagship (tech HUL-12, Bonded Leviathan). References (Bing sheet "space
leviathan creature armored harness concept art": the Chitauri leviathan of the Avengers concept sheets, Jacob Allenza's
and Bryant Koshu's leviathans): a long segmented body like an armoured eel, overlapping plated scales down the back,
ribbed flanks, a heavy jawed head with glowing eyes, ray-like flank fins and a long tapering tail. Humanity rides it:
steel harness bands round the body, a command citadel and gun decks bolted to its back, drive pods strapped to its
flanks. Hide in dark bronze; the harness in the navy's gunmetal and paint, with its windows and lights.
Crew ~1,500,000 (in the harness).
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/leviathan.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipLeviathan")
hull_scale(80000)
M["LevHide"] = painted("Hull", "LevHide", 0.25, tint=(0.42, 0.33, 0.26), rough=0.5)
M["LevScale"] = painted("HullDark", "LevScale", 0.3, tint=(0.55, 0.42, 0.32), rough=0.45)
M["LevEye"] = bpy.data.materials.get("LevEye") or mat("LevEye", (1.0, 0.55, 0.2), emit=(1.0, 0.45, 0.12), strength=0.8)
SM2 = SM + ["LevHide", "LevScale", "LevEye"]
HIDE, SCALE, EYE = len(SM), len(SM) + 1, len(SM) + 2
bm = bmesh.new()


def wave(x):
    """The body's slow S-curve in the vertical: where its centre line sits at x."""
    return 1800 * math.sin(x / 14000.0)


# --- the body: ten overlapping segments, thickest behind the head, each a little proud of the next
segs = []
for k in range(11):
    x1 = 30000 - k * 6200; x0 = x1 - 6900
    r1 = 3600 * (1 - 0.075 * k); r0 = r1 * 0.94
    keel(bm, [(x0, r0, r0 * 0.85, wave(x0)), (x0 + 600, r1, r1 * 0.9, wave(x0 + 600)), (x1 - 300, r1 * 1.02, r1 * 0.92, wave(x1 - 300)),
              (x1, r1 * 0.9, r1 * 0.8, wave(x1))], HIDE, 0.4)
    segs.append((x0, x1, r1))
    # the scales: overlapping plates down the back, a row along each flank
    for j in range(3):
        xs = x0 + 900 + j * 2000
        plate(bm, mirror([(xs + 1800, 0), (xs + 400, r1 * 0.55), (xs - 200, r1 * 0.45), (xs, 0)]), wave(xs) + r1 * 0.88, 260, SCALE, 120)
        # a dorsal spine raked back out of each scale (the leviathans' ridge)
        if k not in (1, 2) or j == 2:
            fast_prism(bm, [(500, -180), (500, 180), (-500, 180), (-500, -180)], r1 * (0.7 if j == 1 else 0.45), Vector((xs + 600, 0, wave(xs) + r1 * 1.1)),
                       UP, SCALE, UVL(bm), top=(0.05, 0.3, -r1 * 0.5, 0.0))
    for s in (1, -1):
        ribs(bm, x0 + 800, x1 - 800, s * r1 * 1.0, wave((x0 + x1) / 2) - r1 * 0.5, wave((x0 + x1) / 2) + r1 * 0.4, s, 600, depth=140, seed=900 + k * 3 + s, mi=(SCALE, HIDE), width=300)
# --- the head: a heavy wedge skull, the upper jaw over a lower jaw hung open, two pairs of glowing eyes
keel(bm, [(30000, 3500, 3000, wave(30000)), (34000, 3300, 2400, 800), (38000, 2400, 1500, 900), (40000, 900, 600, 700)], HIDE, 0.45)
plate(bm, mirror([(39500, 300), (36000, 2800), (31000, 3300), (30500, 0)]), 2600, 500, SCALE, 200)             # brow plate
n0 = set(bm.verts)
keel(bm, [(30500, 2600, 1100, 0), (35000, 2300, 800, 0), (38500, 1300, 400, 0)], HIDE, 0.4)                      # lower jaw
bmesh.ops.rotate(bm, cent=Vector((30500, 0, 0)), matrix=Matrix.Rotation(0.18, 3, "Y"), verts=since(bm, n0))
bmesh.ops.translate(bm, vec=Vector((0, 0, -1500)), verts=since(bm, n0))
for x in range(31500, 38500, 900):
    for s in (1, -1):
        cyl(bm, 160, 10, 900, Vector((x, s * (2200 - (x - 31500) * 0.18), -600)), seg=5, mi=LIGHT)                 # teeth
for x, y in ((35600, 2750), (33600, 3150)):
    for s in (1, -1):
        sphere(bm, 420 if x > 34000 else 330, Vector((x, s * y, 1300)), mi=EYE, seg=10)
# --- the fins: ray-like blades along the flanks, swept back
for x, span in ((22000, 13000), (4000, 10500), (-14000, 7500)):
    for s in (1, -1):
        n0 = set(bm.verts)
        plate(bm, [(x, s * 2800), (x - 5500, s * span), (x - 8000, s * span * 0.8), (x - 6500, s * 2600)], wave(x) - 300, 300, HIDE, 100)
        for f in range(4):
            fast_box(bm, (2000, 140, 160), Vector((x - 2600 - f * 900, s * (3800 + f * 900 * (span - 2800) / 5500), wave(x))), UP, SCALE, UVL(bm))
# --- the tail: tapering on past the last segment to a forked fluke
keel(bm, [(-38000, 1600, 1300, wave(-38000)), (-44000, 700, 600, wave(-44000) - 600), (-48000, 120, 120, wave(-48000) - 1200)], HIDE, 0.4)
for s in (1, -1):
    plate(bm, [(-45000, 0), (-47500, s * 2600), (-48800, s * 2900), (-47500, s * 400)], wave(-46000) - 1000, 220, SCALE, 80)

# --- the harness: steel bands round five segments, a citadel and gun decks bolted to the back, drive pods strapped on
for k in (1, 3, 5, 7, 9):
    x0, x1, r1 = segs[k]
    xm = (x0 + x1) / 2
    for dx in (-500, 500):
        band(bm, Vector((xm + dx, 0, wave(xm + dx))), X_, r1 * 1.09, 260, 160, GUN, 32)
    for s in (1, -1):
        edge_windows(bm, (xm - 1500, s * r1 * 1.1), (xm + 1500, s * r1 * 1.1), wave(xm) - 300, 3, (0, s), lit=0.5, seed=950 + k + s, strip=(10, 160))
# the command citadel over the shoulders: a stepped armoured block with a bridge and masts
cx = 21000; cz = wave(cx) + 3400
plate(bm, rect(cx - 6000, cx + 3000, -2400, 2400), cz - 400, 1200, GUN, 260)
plate(bm, rect(cx - 4500, cx + 1500, -1700, 1700), cz + 700, 1100, HULL, 220)
bridge(bm, cx - 800, cz + 1800, 1200, 500, 1400, mi=LIGHT)
for s in (1, -1):
    edge_windows(bm, (cx - 5800, s * 2410), (cx + 2800, s * 2410), cz - 300, 6, (0, s), lit=0.5, seed=970 + s, strip=(6, 90))
    edge_windows(bm, (cx - 4300, s * 1710), (cx + 1300, s * 1710), cz + 750, 5, (0, s), lit=0.5, seed=972 + s, strip=(6, 90))
    fast_box(bm, (6000, 120, 300), Vector((cx - 1500, s * 2420, cz + 600)), flank(s), ACC, UVL(bm))
cyl(bm, 60, 30, 2200, Vector((cx - 2500, 600, cz + 2900)), seg=6, mi=DARK)
# gun decks down the back between the scales
for x in (8000, -4500, -17000):
    z = wave(x) + 3000 * (1 - 0.075 * (30000 - x) / 6200)
    plate(bm, rect(x - 1800, x + 1800, -1300, 1300), z, 400, GUN, 120)
    for t in (-1000, 0, 1000):
        turret(bm, (x + t, 0, z + 400), 900)
# drive pods strapped to the flanks behind the fins
for x in (-6000, -24000):
    r = 3600 * (1 - 0.075 * (30000 - x) / 6200)
    for s in (1, -1):
        made = keel(bm, [(x + 3000, 500, 500, 0), (x + 1500, 1100, 1100, 0), (x - 3500, 1100, 1100, 0)], GUN, 0.4)
        bmesh.ops.translate(bm, vec=Vector((0, s * (r + 1300), wave(x) - 400)), verts=list({v for f in made for v in f.verts}))
        engine(bm, (x - 3600, s * (r + 1300), wave(x) - 400), 900, 1800, mi=GUN, bands=3, seg=16)
        band(bm, Vector((x, 0, wave(x))), X_, r * 1.09, 300, 160, DARK, 32)
nav(bm, [(4000 - 8000, 7000, wave(4000))], [(4000 - 8000, -7000, wave(4000))], [(cx - 2500, 600, cz + 4000)])
o = obj("leviathan", bm, SM2)
result = export_ship(col, root, "Leviathan")
