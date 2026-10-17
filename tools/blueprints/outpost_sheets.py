"""
Outpost starbase drawing set. Run:  python tools/blueprints/outpost_sheets.py
Writes docs/blueprints/outpost/: the set as one PDF, a PNG per sheet, and DXF underlays for modelling.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
from draft import View, Sheet, save_set, to_dxf, MplRenderer, scale_bar, LAYERS
import outpost_design as O
import outpost_views as V
from outpost_design import D

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "blueprints", "outpost")

QUADS = {1: [1, 2, 3], 2: [4, 5, 6], 3: [7, 8, 9], 4: [10, 11, 12]}


def key_plan(v, cx, cy, R, highlight=None, note=None):
    """A small ring diagram (paper mm at 1:1, drawn into a paper-space view): compartments, spokes, the part shown."""
    for k in range(1, 13):
        a0, a1 = O.COMP_ANGLE(k) - 15, O.COMP_ANGLE(k) + 15
        pts = [(cx + R * math.cos(math.radians(a)), cy + R * math.sin(math.radians(a))) for a in [a0 + i * (a1 - a0) / 12 for i in range(13)]]
        pts += [(cx + R * 0.8 * math.cos(math.radians(a)), cy + R * 0.8 * math.sin(math.radians(a))) for a in [a1 - i * (a1 - a0) / 12 for i in range(13)]]
        v.poly(pts, "A-ANNO-TTLB", closed=True, fill="#9ab7d6" if highlight and k in highlight else "#ffffff")
        am = math.radians(O.COMP_ANGLE(k))
        v.text((cx + R * 0.9 * math.cos(am), cy + R * 0.9 * math.sin(am)), f"{k:02d}", h=1.4)
    for a in (0, 90, 180, 270):
        ar = math.radians(a)
        v.line((cx + R * 0.15 * math.cos(ar), cy + R * 0.15 * math.sin(ar)), (cx + R * 0.8 * math.cos(ar), cy + R * 0.8 * math.sin(ar)), "A-ANNO-TTLB")
    v.circle((cx, cy), R * 0.15, "A-ANNO-TTLB", fill="#ffffff")
    v.text((cx, cy - R - 4), note or "KEY PLAN · VIEWED FROM +Z (DOME)", h=1.6, bold=True)
    v.text((cx, cy + R + 3.5), "↺ SPIN (SPINWARD = +θ)", h=1.5)


def legend_arch(v, x, y):
    items = [("A-WALL-PRHU", "PRESSURE HULL (CUT) / WINDOW BAND", "hull"), ("A-WALL-BHD", "PRESSURE BULKHEAD 600 + SLIDING PRESSURE DOOR", "hull"),
             ("A-WALL", "PARTITION 120 (CUT)", "wall"), ("A-WALL-SHFT", "RATED SHAFT ENCLOSURE 150", "shaft"),
             ("A-CLNG-OVHD", "OVERHEAD (SPOKE / HATCH ABOVE)", "line"), ("A-HIDN", "HIDDEN / BELOW", "line"),
             ("A-GRID", "BULKHEAD GRID B1–B12 · AXIAL GRID A–E", "line"), ("A-DOOR", "SLIDING DOOR (SHOWN OPEN) / HINGED DOOR", "line")]
    v.text((x, y + 6), "LEGEND", h=2.4, ha="left", bold=True)
    for i, (lay, txt, kind) in enumerate(items):
        yy = y - i * 5.2
        if kind == "hull": v.rect(x, yy - 1.0, x + 10, yy + 1.0, lay, fill=O.FILL_HULL)
        elif kind == "wall": v.rect(x, yy - 0.6, x + 10, yy + 0.6, lay, fill=O.FILL_WALL)
        elif kind == "shaft": v.rect(x, yy - 0.8, x + 10, yy + 0.8, lay, fill=O.FILL_SHAFT)
        else: v.line((x, yy), (x + 10, yy), lay)
        v.text((x + 13, yy), txt, h=1.6, ha="left")


def notes(v, x, y, title, lines, h=1.7, width=None):
    v.text((x, y), title, h=2.4, ha="left", bold=True)
    for i, l in enumerate(lines):
        v.text((x, y - 5.5 - i * h * 2.0), l, h=h, ha="left")


DECK_NAME = {"A": "Deck A (main habitat) · FFL z −200", "B": "Deck B (services & berthing) · FFL z −3 500"}


def sheet_ring_quadrant(q, deck="A"):
    """A quadrant of one deck at 1:100, in TRUE PLAN (A-201..A-204 Deck A, A-205..A-208 Deck B)."""
    comps = QUADS[q]
    th0 = O.COMP_ANGLE(comps[0]) - 15
    num = f"A-20{q if deck == 'A' else q + 4}"
    sh = Sheet(num, f"Ring Deck {deck} · Quadrant {q}", f"Compartments C{comps[0]:02d}–C{comps[-1]:02d} · plan", "Architecture")
    rooms = []
    L = O.LA
    v = O.PolarView(f"ring-{deck}-q{q}", xm=1.5 * L)
    O.ring_plan(v, deck, comps, th0, rooms_out=rooms)
    xs = [i * L for i in range(len(comps) + 1)]
    v.chain([(x, -O.HW - O.T) for x in xs], -23)
    v.chain([(xs[-1], y) for y in (-O.HW - O.T, -O.HW, O.HW, O.HW + O.T)], -8)
    if deck == "A":
        xq = O.compartment_x(comps[1], "A", th0)
        v.section_mark((xq - 5.5, -O.HW - O.T - 4.5), (xq - 5.5, O.HW + O.T + 4.5), "1", "A-301", look=1)
        xj = O.compartment_x(comps[0], "A", th0)
        v.section_mark((xj + 0.0, -O.HW - O.T - 4.5), (xj + 0.0, O.HW + O.T + 4.5), "2", "A-301", look=-1)
    sh.place(v, (420 - 1.5 * L * 10, 470), 100, f"{DECK_NAME[deck]} · quadrant {q}", "1", title_at=(30, 286))
    p = sh.paper
    key_plan(p, 590, 64, 22, highlight=comps)
    legend_arch(p, 30, 92)
    notes(p, 260, 98, "NOTES", [
        "1. TRUE PLAN FROM +Z. GRAVITY 1.0 g DOWN = −Z (GENERATED); FLOORS FLAT, THE HULL'S CURVED FACES ARE WALLS.",
        "2. OUTWARD (+r) IS UP THE SHEET AT THE MIDDLE OF THE VIEW. ONE WINDOW PER FRAME BAY (3.75°), SEE openings.json.",
        "3. PLAN CUT 1 200 ABOVE FLOOR. DIMENSIONS mm TO FACE OF STRUCTURE; ALONG THE RING, CHORDS AT THE HUB-WALL SKIN.",
        "4. PARTITIONS 120 LIGHTWEIGHT, NON-STRUCTURAL; BULKHEADS ARE PRESSURE BOUNDARIES (SEE G-002).",
        "5. EVERY BULKHEAD PENETRATION (DUCT, PIPE, CABLE) IS FITTED WITH AN ISOLATION DEVICE (SEE M-20x).",
        "6. CABIN DOORS 800 CLEAR, SLIDING, 25 mm UNDERCUT FOR RETURN AIR TRANSFER.",
        "7. WINDOWS: W1 OUTER WALL DECK A 1 600×1 800 · W2 OUTER WALL DECK B 1 400×1 200 · W3 HUB WALL DECK A 1 200×1 600.",
    ], h=1.55)
    scale_bar(p, (260, 30), 100, 20, 2)
    p.text((260, 25), "GRAPHIC SCALE 1:100", h=1.6, ha="left")
    return sh, rooms


PROG_NAME = {"J": "SPOKE JUNCTION · CONCOURSE · SAFE HAVEN", "HYD": "HYDROPONICS", "QA": "QUARTERS · SINGLE CABINS", "GAL": "GALLEY · WARDROOM",
             "REC": "GYM · LOUNGE", "WKS": "FABRICATION WORKSHOP", "MED": "MEDICAL CENTRE", "JB": "SPOKE LOBBY · AHU PLANT · SUBSTATION",
             "HYP": "NUTRIENTS · HARVEST · SEED BANK", "WAT": "WATER STORE · ALGAE · PUMPS", "QB": "QUARTERS · DOUBLE CABINS",
             "GST": "COLD / DRY STORES · LAUNDRY", "TRB": "TRANSIENT BERTHING", "STO": "STORES · DATA NODE", "LAB": "LABORATORY"}


def sheet_compartment(k):
    """One compartment at 1:50, Deck B above Deck A, with its own room schedule, key plan and notes (A-211..A-222)."""
    th0 = O.COMP_ANGLE(k) - 15
    sh = Sheet(f"A-2{10 + k}", f"Compartment C{k:02d} · plans 1:50",
               f"θ {th0:.0f}°–{th0 + 30:.0f}° · Deck A {PROG_NAME[O.PROGRAM_A[k]].lower()} · Deck B {PROG_NAME[O.PROGRAM_B[k]].lower()}", "Architecture")
    rooms = []
    O.TS[0] = 1.45
    vb, va = O.PolarView(f"c{k:02d}-B", xm=O.LB / 2), O.PolarView(f"c{k:02d}-A", xm=O.LA / 2)
    O.ring_plan(vb, "B", [k], th0, rooms_out=rooms, scale=50)
    O.ring_plan(va, "A", [k], th0, rooms_out=rooms, scale=50)
    O.TS[0] = 1.0
    for v, r in ((va, O.R_A), (vb, O.R_B)):
        L = O.L_of(r)
        v.chain([(0, -O.HW - O.T), (L, -O.HW - O.T)], -23)
        v.chain([(L, y) for y in (-O.HW - O.T, -O.HW, -0.96, 0.96, O.HW, O.HW + O.T)], -8)
    ox = 71 - (O.LA / 2 - 11.54) * 20
    sh.place(vb, (ox, 468), 50, f"Deck B plan · FFL z −3 500 · {PROG_NAME[O.PROGRAM_B[k]].lower()}", "1", title_at=(560, 512))
    sh.place(va, (ox, 206), 50, f"Deck A plan · FFL z −200 · {PROG_NAME[O.PROGRAM_A[k]].lower()}", "2", title_at=(560, 248))
    p = sh.paper
    key_plan(p, 770, 545, 17, highlight=[k], note="KEY PLAN")
    # room schedule for this compartment, Deck B then Deck A, two columns
    cols = [("No.", 17, "c"), ("ROOM", 60, "l"), ("m²", 12, "c")]
    for i, deck in enumerate(("B", "A")):
        rows = [[num, name, f"{a:.1f}"] for dk, kk_, num, name, a in rooms if dk == deck]
        rows.sort(key=lambda r: r[0])
        x = 560 + i * 96
        p.text((x, 470), f"ROOM SCHEDULE · DECK {deck}", h=1.9, ha="left", bold=True)
        notes_h = 3.1
        from outpost_set import table
        table(p, x, 465, cols, rows, h=1.15, rowh=notes_h)
    notes(p, 560, 225, "NOTES", [
        "1. TRUE PLANS FROM +Z; OUTWARD (+r) IS UP THE SHEET. GRAVITY 1.0 g, DOWN = −Z.",
        "2. PLAN CUT 1 200 ABOVE FFL. DIMENSIONS mm TO FACE OF STRUCTURE.",
        "3. DOOR TAGS C.nn: SEE DOOR TYPES, A-501. PD = PRESSURE DOOR.",
        "4. CEILING +3 200 (DECK A) / +2 600 (DECK B). VENTILATION: M-20x.",
        "5. ONE CABIN PER FRAME BAY, EACH WITH ITS OWN WINDOW (W1/W2 OUTER, W3 HUB).",
    ], h=1.4)
    legend_arch(p, 560, 165)
    scale_bar(p, (560, 112), 50, 10, 1)
    p.text((560, 107), "GRAPHIC SCALE 1:50", h=1.5, ha="left")
    return sh
