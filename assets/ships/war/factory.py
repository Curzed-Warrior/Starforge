"""
War factory (foundry), 470 m: one military foundry of a colony's war economy. References (the Behance "Orbital
factory" sheets, the smelting-furnace interiors, assets/stations/renders/refs/foundry_ferrand.jpg): a long armoured
spine with a raw-ore intake funnel at the bow, three banded smelter drums glowing through furnace slots, conveyor
pipe bundles running aft along the spine, a stacked press hall at the stern, radiator fins and caged tanks along
the flanks. Owner colour on a collar of every drum. Lean: about 5,000 triangles.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/war/factory.py").read())
"""
exec(open(globals().get("SF_ROOT", r"C:/Users/diamo/Desktop/starforge") + "/assets/ships/war/warkit.py").read())
col, root = setup("WarFactory")
hull_scale(400)
bm = bmesh.new()
AX = Ry(math.pi / 2)                    # cylinders along X
# --- the spine
keel(bm, [(-200, 16, 16, 0), (190, 16, 16, 0)], DARK, 0.35)
# --- the ore intake: a wide funnel at the bow, rimmed
cyl(bm, 18, 46, 44, Vector((214, 0, 0)), AX, seg=12, mi=GUN, caps=False)
cyl(bm, 46, 46, 4, Vector((238, 0, 0)), AX, seg=12, mi=ACC, caps=False)
cyl(bm, 18, 18, 30, Vector((186, 0, 0)), AX, seg=12, mi=HULL)
# --- three smelter drums, each with banding, an owner collar and furnace slots glowing between the bands
for i, x in enumerate((120, 40, -40)):
    cyl(bm, 36, 36, 64, Vector((x, 0, 0)), AX, seg=16, mi=HULL if i % 2 == 0 else GUN)
    for e in (-1, 1):
        cyl(bm, 30, 36, 8, Vector((x + e * 36, 0, 0)), Ry(math.pi / 2 if e > 0 else -math.pi / 2), seg=16, mi=GUN, caps=True)
    band(bm, Vector((x - 18, 0, 0)), (1, 0, 0), 36, 4, 1.8, mi=DARK, seg=16)
    band(bm, Vector((x + 18, 0, 0)), (1, 0, 0), 36, 4, 1.8, mi=DARK, seg=16)
    owner_band(bm, Vector((x, 0, 0)), (1, 0, 0), 36, 7, seg=16)
    for k in range(8):
        a = (k + 0.5) * math.pi / 4
        n = Vector((0, math.cos(a), math.sin(a)))
        z = n; y = z.cross(Vector((1, 0, 0)))
        F = Matrix((Vector((1, 0, 0)), y, z)).transposed()
        for dx in (-9, 9):
            fast_box(bm, (8, 10, 0.6), Vector((x + dx, 0, 0)) + n * 35.9, F, FURN, UVL(bm))           # furnace slots
# --- conveyor bundles from the drums aft along the spine, on both flanks
for s in (1, -1):
    pipe_run(bm, [(150, s * 20, 10), (-150, s * 20, 10)], 2.2, n=3, mi=GUN, clamp_mi=DARK, clamp_every=40)
    pipe_run(bm, [(150, s * 20, -10), (-150, s * 20, -10)], 2.2, n=2, mi=PANEL, clamp_mi=DARK, clamp_every=40)
# --- the press hall at the stern: stacked blocks with lit window bands and a loading mouth
for (x, w, h, z, m) in ((-130, 70, 60, 0, LIGHT), (-150, 50, 30, 42, GUN), (-112, 30, 20, -38, DARK)):
    fast_box(bm, (60 if m == LIGHT else 40, w, h), Vector((x, 0, z)), UP, m, UVL(bm), bottom=True)
for z in (-14, -4, 6, 16):
    for s in (1, -1):
        fast_box(bm, (50, 0.4, 1.6), Vector((-130, s * 35.2, z)), flank(s), WIN, UVL(bm))
for s in (1, -1):
    fast_box(bm, (40, 0.5, 3.0), Vector((-130, s * 35.3, 24)), flank(s), FURN, UVL(bm))                  # the pour line, glowing through
cyl(bm, 16, 16, 40, Vector((-182, 0, 0)), AX, seg=12, mi=HULL)
engine(bm, (-196, 0, 0), 10, 16, mi=GUN, bands=2, seg=12)                                                # station-keeping drive
# --- radiators above and below, tanks on the flanks
for z in (1, -1):
    radiator_bank(bm, Vector((80, 0, z * 38)), (0, 0, z), (1, 0, 0), 6, 24, 22, 22, mi_fin=DARK, mi_man=GUN)
for x in (160, -80):
    for s in (1, -1):
        cyl(bm, 11, 11, 26, Vector((x, s * 34, 0)), AX, seg=10, mi=LIGHT, caps=False)                    # tanks: lean capsules
        for e in (-1, 1):
            cyl(bm, 11, 4, 8, Vector((x + e * 17, s * 34, 0)), Ry(e * math.pi / 2), seg=10, mi=LIGHT)
        cyl(bm, 11.8, 11.8, 3, Vector((x, s * 34, 0)), AX, seg=10, mi=DARK, caps=False)
# --- a freighter tied up at the stern hall
fast_box(bm, (30, 6, 6), Vector((-150, 0, -48)), UP, DARK, UVL(bm), bottom=True)
keel(bm, [(-190, 8, 7, -64), (-110, 8, 7, -64), (-104, 4, 4, -64)], TAN, 0.4)
nav(bm, [(238, 46, 0)], [(238, -46, 0)], [(-150, 0, 60), (120, 0, 72)])
o = obj("factory", bm, SM)
result = export_war(col, root, "Factory")
