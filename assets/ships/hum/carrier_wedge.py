"""
Carrier, 4 km. References (assets/ships/refs/carrier_ref*): the Secutor's long narrow wedge (sd_hull) with a
red-striped landing strip down the ridge, a small superstructure set far aft and a row of drives across the stern;
the Battlestar Pegasus's flight pods: long armoured hangar decks along both flanks, open at the bow, lit down their
length. Carries ~2,000 strike craft and a dozen corvettes. Crew ~60,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/carrier.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipCarrier")
hull_scale(4000)
bm = bmesh.new()
L = 4000.0
H = sd_hull(bm, L, 1250.0, 700, up_h=200, lo_h=140, batteries=10)
NOSE, STERN, HW, RZ, UPZ, LOZ, LOH, sc = H["NOSE"], H["STERN"], H["HW"], H["RZ"], H["UPZ"], H["LOZ"], H["LOH"], H["sc"]
# --- the landing strip: a flat deck along the ridge with signal-red stripes and guide lights (Secutor)
SX0, SX1 = -1500.0, 1700.0
plate(bm, mirror([(SX1, 0), (SX1 - 120, 70), (SX0, 80), (SX0 - 60, 0)]), RZ - 14, 16, HULL, 4.0)
for k in range(9):
    x = SX1 - 220 - k * 340
    for s in (1, -1):
        fast_box(bm, (180, 16, 1.0), Vector((x, s * 44, RZ + 2.5)), Matrix.Rotation(s * 0.5, 3, "Z"), ACC, UVL(bm))
fast_box(bm, (SX1 - SX0 - 200, 4, 1.0), Vector(((SX0 + SX1) / 2 - 60, 0, RZ + 2.5)), UP, DRIVE, UVL(bm))
# --- flight pods: two long hangar decks on pylons off the flanks (Battlestar)
PX0, PX1 = -1750.0, 300.0
for s in (1, -1):
    y = s * (HW * 0.93)
    loft(bm, [(PX1 + 120, 60, 40, 0.3), (PX1, 150, 90, 0.3), (PX0 + 100, 160, 95, 0.3), (PX0, 140, 85, 0.3)], I3, Vector((0, y, UPZ - 20)), LIGHT)
    # the open bow: a lit hangar mouth
    fast_box(bm, (110, 200, 2), Vector((PX1 + 122, y, UPZ - 20)), Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))), DARK, UVL(bm))
    for zz in (-50, 10, 60):
        fast_box(bm, (3, 190, 3), Vector((PX1 + 124, y, UPZ - 20 + zz)), Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))), WIN, UVL(bm))
    # pylons back to the hull, and armour down the pod
    for x in (PX1 - 200, -700, PX0 + 250):
        hw_x = HW * (NOSE - x) / (NOSE - STERN - L * 0.05)
        y0, y1 = sorted((s * min(hw_x * 0.8, abs(y) - 100), y - s * 140))
        plate(bm, rect(x - 80, x + 80, y0, y1), UPZ - 60, 70, HULL, 4.0)
    armour_grid(bm, ((PX1 - 40, y - s * 130, UPZ + 75.5), (PX0 + 80, y - s * 130, UPZ + 75.5), (PX0 + 80, y + s * 130, UPZ + 75.5)) if s > 0 else
                ((PX1 - 40, y - s * 130, UPZ + 75.5), (PX0 + 80, y + s * 130, UPZ + 75.5), (PX0 + 80, y - s * 130, UPZ + 75.5)), (130, 40), (3, 10), 710 + s, (LIGHT, HULL), fill=0.7)
    edge_windows(bm, (PX1 - 20, y + s * 160.5), (PX0 + 60, y + s * 160.5), UPZ - 90, 40, (0, s), lit=0.45, seed=720 + s, strip=(10, 60))
# --- a small superstructure set far aft (Secutor)
plate(bm, mirror([(-1550, 0), (-1580, 120), (-1820, 130), (-1850, 0)]), RZ - 20, 70, LIGHT, 8.0)
plate(bm, mirror([(-1640, 0), (-1660, 40), (-1780, 44), (-1800, 0)]), RZ + 50, 120, LIGHT, 8.0, slope=(-16, 0))
plate(bm, mirror([(-1620, 0), (-1640, 150), (-1790, 160), (-1810, 0)]), RZ + 170, 64, LIGHT, 8.0)
for s in (1, -1):
    edge_windows(bm, (-1621, s * 148), (-1621, s * 2), RZ + 173, 16, (1, 0), lit=0.55, seed=730 + s, strip=(6, 30))
    sphere(bm, 46, Vector((-1720, s * 110, RZ + 280)), mi=LIGHT, seg=14)
    cyl(bm, 9, 9, 30, Vector((-1720, s * 110, RZ + 248)), seg=10, mi=DARK)
bridge(bm, -1616, RZ + 234, 150, 16.0, 56)
# --- drives: ten across a deep stern block
plate(bm, rect(STERN - 180, STERN + 40, -560, 560), LOZ - 260, 440, HULL, 10.0)
for k in range(10):
    engine(bm, (STERN - 110, -495 + k * 110, -30 + (50 if k % 2 else 0)), 56, 200)
nav(bm, [(STERN + 200, HW + 4, 0), (NOSE - 8, 5, 0), (PX1, HW * 0.93 + 160, UPZ)], [(STERN + 200, -HW - 4, 0), (NOSE - 8, -5, 0), (PX1, -HW * 0.93 - 160, UPZ)],
    [(-1720, 110, RZ + 330), (-1720, -110, RZ + 330)])
o = obj("carrier", bm, SM)
result = export_ship(col, root, "Carrier")
