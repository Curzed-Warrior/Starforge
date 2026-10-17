"""
Outpost starbase interior drawing set: builds every sheet and writes the PDF set, PNG previews, DXF underlays and
the room schedule to docs/blueprints/outpost/.   Run:  python tools/blueprints/outpost_set.py
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
from draft import View, Sheet, save_set, to_dxf, scale_bar
import outpost_design as O
import outpost_views as V
from outpost_design import D
from outpost_sheets import QUADS, OUT, key_plan, legend_arch, notes, sheet_ring_quadrant, sheet_compartment


# ------------------------------------------------------------------ helpers
def table(v, x, y, cols, rows, h=1.6, rowh=5.0, header=True):
    """A ruled table in paper space: cols = [(title, width_mm, align)], rows = lists of strings. Top-left at (x, y)."""
    W = sum(c[1] for c in cols)
    n = len(rows) + (1 if header else 0)
    v.rect(x, y - n * rowh, x + W, y, "A-ANNO-TTLB")
    for i in range(1, n): v.line((x, y - i * rowh), (x + W, y - i * rowh), "A-ANNO-NOTE")
    cx = x
    for c in cols[:-1]:
        cx += c[1]; v.line((cx, y), (cx, y - n * rowh), "A-ANNO-NOTE")
    allrows = ([[c[0] for c in cols]] if header else []) + rows
    for ri, row in enumerate(allrows):
        cx = x
        for ci, (title, w, al) in enumerate(cols):
            t = row[ci] if ci < len(row) else ""
            tx = cx + 1.5 if al == "l" else (cx + w - 1.5 if al == "r" else cx + w / 2)
            v.text((tx, y - (ri + 0.5) * rowh), t, h=h, ha={"l": "left", "r": "right", "c": "center"}[al], bold=header and ri == 0)
            cx += w
    return y - n * rowh


def clip(view, x0, y0, x1, y1, name):
    """A copy of a view cut to a box: rectangles are trimmed, lines clipped, points kept if inside (for enlarged plans)."""
    from draft import Prim
    out = View(name)
    def inside(p): return x0 - 1e-6 <= p[0] <= x1 + 1e-6 and y0 - 1e-6 <= p[1] <= y1 + 1e-6
    def clip_line(a, b):
        (ax, ay), (bx, by) = a, b
        t0, t1 = 0.0, 1.0
        dx, dy = bx - ax, by - ay
        for p, q in ((-dx, ax - x0), (dx, x1 - ax), (-dy, ay - y0), (dy, y1 - ay)):
            if abs(p) < 1e-12:
                if q < 0: return None
                continue
            t = q / p
            if p < 0: t0 = max(t0, t)
            else: t1 = min(t1, t)
        if t0 > t1: return None
        return (ax + t0 * dx, ay + t0 * dy), (ax + t1 * dx, ay + t1 * dy)
    for pr in view.prims:
        d = dict(pr.data)
        if pr.kind == "poly":
            pts = d["pts"]
            xs, ys = [p[0] for p in pts], [p[1] for p in pts]
            rectish = d["closed"] and len(pts) == 4 and len(set(round(x, 6) for x in xs)) == 2 and len(set(round(y, 6) for y in ys)) == 2
            if rectish:
                a, b, c, e = max(min(xs), x0), max(min(ys), y0), min(max(xs), x1), min(max(ys), y1)
                if c - a > 1e-6 and e - b > 1e-6:
                    d["pts"] = [(a, b), (c, b), (c, e), (a, e)]
                    out.prims.append(Prim("poly", pr.layer, d))
            elif all(inside(p) for p in pts):
                out.prims.append(pr)
            elif not d["closed"]:
                for a, b in zip(pts, pts[1:]):
                    seg = clip_line(a, b)
                    if seg: out.prims.append(Prim("line", pr.layer, {"pts": list(seg)}))
        elif pr.kind == "line":
            seg = clip_line(*d["pts"])
            if seg: d["pts"] = list(seg); out.prims.append(Prim("line", pr.layer, d))
        else:
            pts = [d[k] for k in ("c", "p", "p0", "p1") if k in d]
            if pts and all(inside(p) for p in pts): out.prims.append(pr)
    return out


def shift(view, dx, dy, name=None):
    out = View(name or view.name)
    out.extend(view, -dx, -dy)
    return out


# ------------------------------------------------------------------ G sheets
def sheet_cover(index, rooms):
    sh = Sheet("G-001", "Cover · index · notes · legend", "Outpost starbase interior fit-out · drawing set", "General")
    p = sh.paper
    p.text((30, 565), "STARFORGE", h=6, ha="left", bold=True)
    p.text((30, 553), "OUTPOST STARBASE (TIER 0) — INTERIOR FIT-OUT DRAWINGS", h=4.2, ha="left", bold=True)
    p.text((30, 545), "Architectural and ventilation (HVAC) design at true scale, for modelling the full interior.", h=2.4, ha="left")
    table(p, 30, 535, [("SHEET", 20, "l"), ("TITLE", 132, "l"), ("SCALE", 28, "l")], index, h=1.6, rowh=4.9)
    notes(p, 230 + 0, 535, "GENERAL NOTES", [], h=1.6)
    gen = [
        "1. REV B · ONE BUILD: THE EXTERIOR (assets/stations/outpost/build.py) READS docs/blueprints/openings.json, WRITTEN",
        "   FROM THESE PLANS: EVERY WINDOW, SKYLIGHT, SPOKE PORTAL AND HANGAR DOOR OUTSIDE IS A ROOM INSIDE.",
        "2. UNITS: DIMENSIONS IN MILLIMETRES; RADII IN mm FROM THE STATION AXIS; ANGLES IN DEGREES.",
        "3. AXES: +Z = STATION AXIS TOWARD THE COMMAND DOME. θ = 0 ON +X (SPOKE 1), POSITIVE COUNTER-",
        "   CLOCKWISE SEEN FROM +Z. SPOKES AT 0 / 90 / 180 / 270°.",
        "4. GRAVITY IS GENERATED: 1.0 g, DOWN = −Z EVERYWHERE (SPINE, SPOKES, RING, HUB). ALL FLOORS ARE FLAT; THE RING'S",
        "   CURVED FACES ARE WALLS. RING DECK A FFL z −200 (= SPOKE FLOOR = SPINE HUB DECK), DECK B FFL z −3 500.",
        "5. RING PLANS ARE TRUE PLANS FROM +Z; THEIR GEOMETRY IS WRITTEN AT R 40 000 (x ALONG THE RING, y = r − 40 000).",
        "6. 12 PRESSURE BULKHEADS B1–B12 (θ = 15° + 30°·(n−1)) MAKE 12 COMPARTMENTS C01–C12, EACH ABLE TO HOLD",
        "   101.3 kPa WITH ITS NEIGHBOURS EVACUATED. EVERY OPENING IN A BULKHEAD CLOSES AUTOMATICALLY.",
        "7. ISO 128 LINES · ISO 129 DIMENSIONS · ISO 3098 LETTERING · ISO 5455 SCALES · ISO 5457 / 7200 SHEETS.",
        "8. DXF UNDERLAYS (1:1, mm, LAYERED) ISSUED WITH THIS SET FOR MODELLING: docs/blueprints/outpost/dxf/.",
        "9. ROOM NUMBERS: DECK · COMPARTMENT · ROOM, e.g. A05-03 = DECK A, C05, ROOM 03. SCHEDULE: room_schedule.csv.",
    ]
    for i, l in enumerate(gen): p.text((230, 529 - i * 3.6), l, h=1.6, ha="left")
    abb = [
        "AHU  AIR HANDLING UNIT             BW  BLACKWATER              CHWS/R  CHILLED WATER SUPPLY/RETURN",
        "EA   EXHAUST AIR                   ECLSS  ENV. CONTROL & LIFE SUPPORT        EL  EMERGENCY LOCKER",
        "ER   ELECTRICAL RISER              FFL  FINISHED FLOOR LEVEL   FP  FIRE POINT   FSD  FIRE/SMOKE/PRESSURE DAMPER",
        "GW   GREYWATER                     OA  REVITALISED AIR FROM THE HUB ECLSS    PBA  PORTABLE BREATHING APPARATUS",
        "PD   PRESSURE DOOR                 PW  POTABLE WATER       RA  RETURN AIR    RH  RETURN-TO-HUB AIR",
        "RS   RETURN-AIR SHAFT              SA  SUPPLY AIR          SH  SPOKE ISOLATION HATCH   TCCS  TRACE CONTAM. CONTROL",
        "WR   WARDROBE / WATER RISER        4BMS  4-BED MOLECULAR SIEVE (CO₂)        OGA  O₂ GENERATION ASSEMBLY",
    ]
    p.text((230, 474), "ABBREVIATIONS", h=2.4, ha="left", bold=True)
    for i, l in enumerate(abb): p.text((230, 468 - i * 3.6), l, h=1.55, ha="left")
    legend_arch(p, 230, 420)
    p.text((470, 426), "LEGEND · VENTILATION", h=2.4, ha="left", bold=True)
    items = (("M-DUCT-SUPP", "SUPPLY AIR DUCT (SA)"), ("M-DUCT-RETN", "RETURN AIR (RA) / RETURN-TO-HUB (RH)"),
             ("M-DUCT-EXHS", "EXHAUST DUCT (EA) TO TCCS"), ("M-DUCT-OAIR", "REVITALISED AIR FROM HUB (OA)"), ("M-DUCT-HIDN", "DUCT IN SUBFLOOR PLENUM (BELOW)"))
    for i, (lay, txt) in enumerate(items):
        yy = 420 - i * 5.2
        p.rect(470, yy - 1.0, 480, yy + 1.0, lay); p.text((483, yy), txt, h=1.6, ha="left")
    pd = D(p)
    O.diffuser(pd, 475, 420 - 5 * 5.2, s=2.4); p.text((483, 420 - 5 * 5.2), "CEILING DIFFUSER (SUPPLY)", h=1.6, ha="left")
    O.grille(pd, 475, 420 - 6 * 5.2, w=4, hgt=1.6); p.text((483, 420 - 6 * 5.2), "RETURN / EXHAUST GRILLE", h=1.6, ha="left")
    p.line((472, 420 - 7.2 * 5.2 - 1.5), (478, 420 - 7.2 * 5.2 + 1.5), "M-HVAC-DAMP"); p.rect(474, 420 - 7.2 * 5.2 + 1.5, 476, 420 - 7.2 * 5.2 + 2.5, "M-HVAC-DAMP", fill="#000000")
    p.text((483, 420 - 7.2 * 5.2), "ISOLATION DAMPER AT BULKHEAD (FSD)", h=1.6, ha="left")
    singles = sum(1 for r in rooms if r[3] == "CABIN · 1"); doubles = sum(1 for r in rooms if r[3] == "CABIN · 2"); bunks = sum(1 for r in rooms if r[3].startswith("BUNK ROOM"))
    occ = singles + 2 * doubles + 4 * bunks
    p.text((230, 360), "OCCUPANCY (COUNTED FROM THE PLANS)", h=2.4, ha="left", bold=True)
    table(p, 230, 354, [("BERTHS", 70, "l"), ("ROOMS", 16, "r"), ("×", 10, "c"), ("PERSONS", 18, "r")], [
        ["SINGLE CABINS · DECK A · WINDOWED", f"{singles}", "1", f"{singles}"], ["DOUBLE CABINS · DECK B", f"{doubles}", "2", f"{doubles * 2}"],
        ["TRANSIENT BUNK ROOMS · DECK B C09", f"{bunks}", "4", f"{bunks * 4}"], ["DESIGN OCCUPANCY (ECLSS SIZED FOR 120)", "", "", f"{occ}"]], h=1.6)
    # orientation diagram
    cx, cy, R = 640, 330, 40
    p.circle((cx, cy), R, "A-ANNO-TTLB"); p.circle((cx, cy), R * 0.82, "A-ANNO-TTLB"); p.circle((cx, cy), R * 0.15, "A-ANNO-TTLB")
    for a in (0, 90, 180, 270):
        ar = math.radians(a)
        p.line((cx + R * 0.15 * math.cos(ar), cy + R * 0.15 * math.sin(ar)), (cx + R * 0.82 * math.cos(ar), cy + R * 0.82 * math.sin(ar)), "A-ANNO-TTLB")
        p.text((cx + (R + 7) * math.cos(ar), cy + (R + 7) * math.sin(ar)), f"SPOKE {a // 90 + 1}\nθ {a}°", h=1.5)
    p.line((cx, cy), (cx + R * 1.5, cy), "A-ANNO-DIMS"); p.text((cx + R * 1.55, cy - 3), "+X", h=1.6)
    p.line((cx, cy), (cx, cy + R * 1.5), "A-ANNO-DIMS"); p.text((cx + 3, cy + R * 1.55), "+Y", h=1.6)
    p.arc((cx, cy), R * 1.2, 20, 70, "A-ANNO-TTLB"); p.text((cx + R * 1.0, cy + R * 1.0), "+θ ↺", h=1.8, bold=True)
    p.text((cx, cy - R - 12), "STATION AXES · VIEWED FROM +Z", h=2.0, bold=True)
    return sh


def sheet_criteria(rooms):
    sh = Sheet("G-002", "Design criteria · compartmentation · egress", "Spin, gravity, atmosphere, areas; pressure compartments, safe havens", "General")
    p = sh.paper
    rows = [[lab, f"{z * 1000:+,.0f}".replace(",", " "), clr, note] for lab, z, clr, note in (
        ("RING ROOF SKIN", O.ZH + O.T, "", "SKYLIGHTS OVER THE CORRIDOR"), ("DECK A CEILING", O.Z_A_CLG, "", "ROOF VOID 1 000 ABOVE"),
        ("DECK A FFL · SPOKE FLOOR · SPINE HUB DECK", O.Z_A, "3 200", "MAIN HABITAT · W1 / W3 WINDOWS"),
        ("DECK B CEILING", O.Z_B_CLG, "", "INTERSTITIAL VOID 400 + SLAB 300"), ("DECK B FFL", O.Z_B, "2 600", "SERVICES / BERTHING · W2 · HANGARS"),
        ("RING FLOOR SKIN", -O.ZH - O.T, "", "SUBFLOOR PLENUM 500 ABOVE"))]
    p.text((30, 565), "GRAVITY AND LEVELS  (GENERATED 1.0 g, DOWN = −Z)", h=2.4, ha="left", bold=True)
    y = table(p, 30, 558, [("LEVEL", 68, "l"), ("z mm", 22, "r"), ("CLEAR", 16, "r"), ("NOTE", 50, "l")], rows, h=1.55, rowh=4.8)
    for i, l in enumerate(["ONE GRAVITY FOR THE WHOLE STATION: WALK LEVEL FROM THE SPINE HUB DECK ALONG A SPOKE INTO RING DECK A.",
                           "THE RING STILL TURNS SLOWLY (COSMETIC / STATION-KEEPING); IT CARRIES NO LOAD FOR GRAVITY.",
                           "STAIRS DECK A ↔ B: 18 RISERS 183, GOINGS 260, IN EVERY SPOKE JUNCTION, BESIDE THE LIFT."]):
        p.text((30, y - 5 - i * 3.6), l, h=1.5, ha="left")
    p.text((30, 450), "ATMOSPHERE AND ENVIRONMENT", h=2.4, ha="left", bold=True)
    y2 = table(p, 30, 443, [("PARAMETER", 56, "l"), ("DESIGN VALUE", 98, "l")], [
        ["TOTAL PRESSURE", "101.3 kPa"], ["O₂ PARTIAL PRESSURE", "21.3 kPa (19.5–23.1)"], ["CO₂ PARTIAL PRESSURE", "≤ 0.4 kPa (3 mmHg)"],
        ["TEMPERATURE", "18–27 °C · 22 °C NOMINAL"], ["RELATIVE HUMIDITY", "25–70 % · 45 % NOMINAL"], ["VENTILATION", "6 AIR CHANGES / h, ALL HABITABLE ZONES"],
        ["AIR VELOCITY, OCCUPIED ZONE", "0.08–0.20 m/s"], ["FILTRATION", "HEPA H13 AT EVERY AHU"], ["NOISE", "≤ NC-40 BERTHS · ≤ NC-50 WORK AREAS"],
        ["REVITALISED AIR EXCHANGE", "12 L/s PER PERSON (HUB ECLSS)"]], h=1.55, rowh=4.8)
    from collections import defaultdict
    area = defaultdict(float)
    for deck, k, num, name, a in rooms: area[deck] += a
    names_a = {"J": "SPOKE JUNCTION · CONCOURSE · SAFE HAVEN", "HYD": "HYDROPONICS", "QA": "QUARTERS (SINGLES)", "GAL": "GALLEY · WARDROOM",
               "REC": "GYM · LOUNGE · HANGAR AIRLOCK", "WKS": "FABRICATION WORKSHOP", "MED": "MEDICAL CENTRE"}
    names_b = {"JB": "SPOKE LOBBY · AHU PLANT · SUBSTATION", "HYP": "NUTRIENTS · HARVEST · SEED BANK", "WAT": "WATER STORE · ALGAE · PUMPS",
               "QB": "QUARTERS (DOUBLES)", "GST": "COLD / DRY STORES · LAUNDRY", "TRB": "TRANSIENT BERTHING", "STO": "STORES · DATA NODE", "LAB": "LABORATORY"}
    def ahu_for(k):
        for j, a in O.AHU_OF.items():
            if k in O.served(j): return f"AHU-R{a}"
    rows = [[f"C{k:02d}", f"{O.COMP_ANGLE(k) - 15:.0f}–{O.COMP_ANGLE(k) + 15:.0f}°", names_a[O.PROGRAM_A[k]], names_b[O.PROGRAM_B[k]], ahu_for(k)] for k in range(1, 13)]
    p.text((30, y2 - 10), "COMPARTMENT PROGRAMME", h=2.4, ha="left", bold=True)
    y3 = table(p, 30, y2 - 17, [("COMP.", 14, "c"), ("θ", 20, "c"), ("DECK A", 68, "l"), ("DECK B", 66, "l"), ("SERVED BY", 20, "c")], rows, h=1.45, rowh=4.6)
    p.text((30, y3 - 8), f"AREAS FROM THE PLANS:  DECK A {area['A']:.0f} m²  ·  DECK B {area['B']:.0f} m²  ·  RING {area['A'] + area['B']:.0f} m²  ·  SPINE DECK PLATE ≈ {math.pi * (O.SRI ** 2 - 1.8 ** 2) * 10:.0f} m²",
           h=1.6, ha="left", bold=True)
    # compartmentation and egress diagram
    cx, cy, R = 560, 360, 140
    for k in range(1, 13):
        a0, a1 = O.COMP_ANGLE(k) - 15, O.COMP_ANGLE(k) + 15
        pts = [(cx + R * math.cos(math.radians(a)), cy + R * math.sin(math.radians(a))) for a in [a0 + i * 2.5 for i in range(13)]]
        pts += [(cx + R * 0.82 * math.cos(math.radians(a)), cy + R * 0.82 * math.sin(math.radians(a))) for a in [a1 - i * 2.5 for i in range(13)]]
        p.poly(pts, "A-ANNO-TTLB", closed=True, fill="#f2d7a6" if k in O.JUNCTION else "#ffffff")
        am = math.radians(O.COMP_ANGLE(k))
        p.text((cx + R * 0.91 * math.cos(am), cy + R * 0.91 * math.sin(am)), f"C{k:02d}", h=1.9, bold=True)
        b = math.radians(O.COMP_ANGLE(k) + 15)
        p.line((cx + R * 0.78 * math.cos(b), cy + R * 0.78 * math.sin(b)), (cx + R * 1.05 * math.cos(b), cy + R * 1.05 * math.sin(b)), "A-WALL-PRHU")
        p.text((cx + R * 1.1 * math.cos(b), cy + R * 1.1 * math.sin(b)), f"B{k}", h=1.6)
        if k not in O.JUNCTION:
            j = min(O.JUNCTION, key=lambda j: min((k - j) % 12, (j - k) % 12))
            dirn = 1 if (j - k) % 12 < 6 else -1
            a_from, a_to = O.COMP_ANGLE(k), O.COMP_ANGLE(k) + dirn * 22
            pts = [(cx + R * 0.75 * math.cos(math.radians(a_from + (a_to - a_from) * t / 6)), cy + R * 0.75 * math.sin(math.radians(a_from + (a_to - a_from) * t / 6))) for t in range(7)]
            p.poly(pts, "E-POWR"); p.circle(pts[-1], 1.4, "E-POWR", fill="#a80000")
    for a in (0, 90, 180, 270):
        ar = math.radians(a)
        p.line((cx + R * 0.15 * math.cos(ar), cy + R * 0.15 * math.sin(ar)), (cx + R * 0.82 * math.cos(ar), cy + R * 0.82 * math.sin(ar)), "A-WALL-BHD")
        p.text((cx + R * 0.5 * math.cos(ar) - 5 * math.sin(ar), cy + R * 0.5 * math.sin(ar) + 5 * math.cos(ar)), f"SPOKE {a // 90 + 1}", h=1.7, rot=0 if a in (0, 180) else 90)
    p.circle((cx, cy), R * 0.15, "A-ANNO-TTLB", fill="#dddddd"); p.text((cx, cy), "SPINE\nHUB NODE S3a\n(REFUGE)", h=1.6)
    p.text((cx, cy - R - 12), "PRESSURE COMPARTMENTATION AND EGRESS · DIAGRAM VIEWED FROM +Z · NOT TO SCALE", h=2.2, bold=True)
    for i, l in enumerate([
        "• 12 RING COMPARTMENTS, EACH A PRESSURE ZONE BETWEEN TWO 600 BULKHEADS AND THE HULL.",
        "• EACH BULKHEAD HAS A SLIDING PRESSURE DOOR ON EACH DECK (PD-nA 1200×2100 · PD-nB 1000×2000) THAT CLOSES ON 2 kPa",
        "  DIFFERENTIAL OR ON COMMAND AND OPENS MANUALLY FROM EITHER SIDE.",
        "• SHADED: SPOKE JUNCTION COMPARTMENTS — LIFT AND LADDER TO THE HUB, AND A 30-PERSON SAFE HAVEN (O₂ + LiOH, 4 h) ON DECK A.",
        "• RED: PRIMARY EGRESS FROM EACH COMPARTMENT TO THE NEAREST JUNCTION (ONE BULKHEAD). SECONDARY: THE OTHER WAY ROUND.",
        "• SPOKE ISOLATION HATCHES SH AT BOTH ENDS OF EVERY SPOKE. HUB NODE S3a IS THE LAST REFUGE; DOCKED CRAFT ARE THE LIFEBOATS.",
        "• SMOKE: THE ZONE'S AHU CLOSES ITS DAMPERS AND RUNS EXHAUST-ONLY. DETECTION IN EVERY ROOM AND PLENUM.",
    ]):
        p.text((400, 185 - i * 3.8), l, h=1.55, ha="left")
    return sh


# ------------------------------------------------------------------ A sheets
def sheet_ga():
    sh = Sheet("A-101", "General arrangement", "Station plan and elevation · exterior envelope", "Architecture")
    sh.place(V.general_plan(), (230, 340), 500, "Station plan · viewed from +Z", "1", title_at=(40, 190))
    sh.place(V.general_elevation(), (610, 370), 500, "Station elevation · viewed along +Y", "2", title_at=(450, 190))
    for i, l in enumerate([
        "RING: OUTER RADIUS 44 600 · HUB SIDE 36 400 · AXIAL WIDTH 9 200 · CHAMFERS 1 400 · 12 BULKHEADS · 96 FRAMES.",
        "SPINE: Ø16 000 OUTSIDE, Ø14 800 INSIDE, z −26 000 TO +30 000; COMMAND DOME Ø16 000 HEMISPHERE ON TOP.",
        "SPOKES: 4 × 4 800 × 3 200 TUBES AT 0/90/180/270°, FLOOR z −200, FROM THE SPINE (r 8 000) TO THE RING (r 35 400).",
        "DOCKING HUB: Ø20 000 × 6 000 BELOW THE SPINE, 4 ARMS TO PORTS AT r 25 350, AXIAL PORT P0 BELOW.",
        "SOLAR WINGS ±X AT z 22 000 (8 PANELS 11 × 9 m EACH SIDE) · RADIATORS ±Y AT z −12 000 · TANK FARM z −18 000.",
        "RIM HANGARS H1 (θ 65°) / H2 (θ 245°): 12 000 W × 6 000 H × 14 000 DEEP, OUT FROM THE RING'S FLOOR SIDE.",
    ]):
        sh.paper.text((40, 140 - i * 4.0), l, h=1.7, ha="left")
    sh.paper.text((40, 148), "ENVELOPE (FROM build.py)", h=2.4, ha="left", bold=True)
    return sh


def sheet_section_aa():
    sh = Sheet("A-102", "Section A-A", "Meridional section on X–Z through Spokes 1 and 3", "Architecture")
    sh.place(V.meridional_section(), (400, 330), 200, "Section A-A", "A", title_at=(40, 90))
    return sh


def sheet_ring_sections():
    sh = Sheet("A-301", "Ring sections · bulkhead", "Typical tube section · junction section · pressure bulkhead elevation", "Architecture")
    sh.place(V.ring_section("typical"), (285, 318), 25, "Typical ring section · quarters (C05)", "1", title_at=(40, 72))
    sh.place(V.ring_section("junction"), (722, 448), 50, "Section through spoke junction (C04)", "2", title_at=(555, 340))
    sh.place(V.bulkhead_elevation(), (700, 212), 50, "Pressure bulkhead · elevation looking spinward", "3", title_at=(555, 108))
    for i, l in enumerate(["SECTIONS LOOK ALONG THE CIRCUMFERENCE (SPINWARD); HUB WALL LEFT, OUTER WALL RIGHT; 'UP' IS +Z (GRAVITY 1.0 g DOWN).",
                           "REV B: TWO FLAT DECKS; WINDOWS IN THE CURVED WALLS (W1/W2 OUTER, W3 HUB), SKYLIGHTS IN THE ROOF OVER THE CORRIDOR.",
                           "VOIDS: SUBFLOOR PLENUM 500 · DECK B / DECK A INTERSTITIAL VOID 400 + SLAB 300 · ROOF VOID 1 000.",
                           "FLOORS: RAISED ACCESS FLOOR ON PEDESTALS @ 600 (DECK B); SANDWICH SLAB ON BEAMS (DECK A)."]):
        sh.paper.text((40, 52 - i * 3.8), l, h=1.55, ha="left")
    return sh


def sheet_spoke():
    sh = Sheet("A-302", "Spoke", "Spoke plan from +Z and cross-section", "Architecture")
    sh.place(V.spoke_plan(), (40 - O.SRI * 20 + 30, 460), 50, "Spoke plan · cut 1 200 above the walkway · typical of spokes 1–4", "1", title_at=(40, 358))
    sh.place(V.spoke_cross(), (210, 150), 20, "Spoke cross-section · looking outward", "2", title_at=(40, 92))
    p = sh.paper
    p.text((420, 330), "SPOKE", h=2.4, ha="left", bold=True)
    for i, l in enumerate([
        "1. A LEVEL WALKWAY: ITS FLOOR (z −200) IS THE SPINE HUB DECK AND RING DECK A, SO THE CREW WALK STRAIGHT THROUGH.",
        "2. 28.0 m FROM THE SPINE WALL TO THE RING'S HUB WALL; HANDRAILS BOTH SIDES; FLOOR LIGHT STRIPS; 2 VIEW PORTS EACH SIDE.",
        "3. ISOLATION DOORS AT BOTH ENDS SEAL THE SPOKE FROM THE HUB AND FROM THE RING.",
        "4. THE CHASE CARRIES OA 400×200 (HUB→AHU), RH 400×200 (AHU→HUB), EA Ø250, PW, BW, CHWS/R,",
        "   N₂/O₂ MAKE-UP, 400 V DC BUSWAY AND DATA; ALL PASS THE END BULKHEADS THROUGH ISOLATING FITTINGS.",
        "5. AT THE RING THE CHASE DROPS INTO SHAFT RS-n TO THE DECK B PLANT ROOM (A-21x).",
    ]):
        p.text((420, 270 - i * 3.8), l, h=1.55, ha="left")
    return sh


def sheet_spine(keys, n):
    names = []
    sh = Sheet(f"A-40{n}", "Spine levels", "", "Architecture")
    for i, key in enumerate(keys):
        v, title = V.spine_level(key)
        names.append(title.split(" · ")[0])
        sh.place(v, (220 + i * 380, 340), 50, title, str(i + 1), title_at=(40 + i * 380, 140))
    sh.subtitle = " · ".join(names) + " · plans viewed from +Z"
    for i, l in enumerate([
        "1. GRAVITY 1.0 g DOWN = −Z, AS IN THE RING: EVERY SPINE LEVEL IS AN ORDINARY FLOOR. S3a (z −200) IS LEVEL WITH THE SPOKES",
        "   AND THE RING DECK A, SO THE HUB IS ONE CONTINUOUS FLOOR WITH ALL FOUR SPOKES.",
        "2. CENTRAL TRANSFER TRUNK Ø3 600 THROUGH EVERY DECK; THE TRANSFER CAR RUNS ON FOUR RAILS AT r 1 300.",
        "3. RISERS AT EVERY LEVEL: SA-S / RA-S Ø630 (SPINE AHU), OA / RH Ø500 (HUB ↔ ECLSS), ELECTRICAL (ER), WATER (WR).",
        "4. 16 PORTHOLES Ø1 400 PER LEVEL AT MID-HEIGHT (NOT ON S3a, WHICH HAS THE SPOKE PORTALS).",
    ]):
        sh.paper.text((40, 110 - i * 3.8), l, h=1.55, ha="left")
    return sh


def sheet_dock():
    sh = Sheet("A-406", "Docking hub", "Plan at z −29 000 (arm axis)", "Architecture")
    sh.place(V.dock_hub(), (300, 330), 100, "Docking hub plan", "1", title_at=(40, 100))
    for i, l in enumerate([
        "1. THE HUB TURNS WITH THE STATION; PORTS P1–P4 AT r 25 350 SEE 0.45 g. VISITING CRAFT MATCH",
        "   ROTATION BEFORE CONTACT. P0 (AXIAL) IS THE PRIMARY PORT FOR LARGE OR UNPOWERED VEHICLES.",
        "2. EACH ARM: Ø3 600 TUBE, Ø3 100 CLEAR, PRESSURE HATCH AT BOTH ENDS, RING FRAMES @ 3 500.",
        "3. HUB VESTIBULE Ø19 200 CLEAR × 5 200: CARGO STAGING, CREW RECEPTION, AIRLOCK SUPPORT, STORES.",
        "4. VENTILATED FROM THE SPINE AHU (SA-S / RA-S EXTENDED DOWN THROUGH S0).",
    ]):
        sh.paper.text((560, 520 - i * 3.8), l, h=1.55, ha="left")
    sh.paper.text((560, 528), "NOTES", h=2.4, ha="left", bold=True)
    return sh


def sheet_enlarged():
    sh = Sheet("A-501", "Enlarged plans", "Single cabin · double cabin · hygiene module · galley", "Architecture")
    va, vb, vg = View("enl-A"), View("enl-B"), View("enl-G")
    O.TS[0] = 1.9
    O.ring_plan(va, "A", [5], O.COMP_ANGLE(5) - 15, grid=False)
    O.ring_plan(vb, "B", [5], O.COMP_ANGLE(5) - 15, grid=False)
    O.TS[0] = 1.4
    O.ring_plan(vg, "A", [8], O.COMP_ANGLE(8) - 15, grid=False)
    O.TS[0] = 1.0
    cA = O.LA / 2 - 10.0
    e1 = clip(va, cA - 0.2, -0.3, cA + 2 * 2.5 + 0.2, O.HW + O.T + 0.05, "single cabins")
    e1.chain([(cA + i * 2.5, O.HW + O.T + 0.1) for i in range(3)], 6)
    e1.chain([(cA + 5.25, y) for y in (0.96, O.HW, O.HW + O.T)], -6)
    hx = O.LA / 2 - 10.0 + 3 * 2.5
    e2 = clip(va, hx - 0.1, 0.85, hx + 2.6, O.HW + O.T + 0.05, "hygiene")
    e2.chain([(hx, O.HW + O.T + 0.1), (hx + 1.25, O.HW + O.T + 0.1), (hx + 2.5, O.HW + O.T + 0.1)], 6)
    e2.chain([(hx + 2.6, y) for y in (0.96, 2.3, O.HW)], -6)
    cB = O.LB / 2 - 9.6
    e3 = clip(vb, cB - 0.2, -0.3, cB + 2 * 2.4 + 0.2, O.HW + O.T + 0.05, "double cabins")
    e3.chain([(cB + i * 2.4, O.HW + O.T + 0.1) for i in range(3)], 6)
    eg = clip(vg, 0.0, -O.HW - O.T - 0.05, 13.0, O.HW + O.T + 0.05, "galley")
    sh.place(shift(e1, cA, 0), (55, 366), 25, "Single cabins · Deck A (C05)", "1", title_at=(40, 335))
    sh.place(shift(e3, cB, 0), (310, 366), 25, "Double cabins · Deck B (C05)", "2", title_at=(295, 335))
    sh.place(shift(e2, hx, 0), (570, 366), 25, "Hygiene module · Deck A", "3", title_at=(560, 335))
    sh.place(eg, (60, 210), 50, "Galley and wardroom · Deck A (C08)", "4", title_at=(40, 98))
    sh.paper.text((560, 300), "FIT-OUT NOTES", h=2.4, ha="left", bold=True)
    for i, l in enumerate([
        "SINGLE CABIN 2 500 × 2 980 (7.4 m²): BED 900×2 000 UNDER THE WINDOW, WARDROBE 670×600,",
        "  FOLD-DOWN DESK 520×1 200, SLIDING DOOR 800, CEILING DIFFUSER, DOOR UNDERCUT 25.",
        "DOUBLE CABIN 2 400 × 2 980 (7.2 m²): BUNKS 900×2 000 × 2 HIGH (1 000 BETWEEN), NO WINDOW.",
        "HYGIENE MODULE 2 500 × 2 980: WC 1 250×1 700, SHOWER 1 250×1 700 (MIST, RECIRCULATING),",
        "  BASIN LOBBY; EXHAUST 25 L/s EACH WC AND SHOWER; FLOOR DRAINS TO GREYWATER.",
        "GALLEY: INDUCTION LINE + COMBI OVENS UNDER A FILTERED HOOD (EA 0.25 m³/s), PREP ISLAND,",
        "  SERVERY COUNTER TO THE WARDROOM; SCULLERY OPPOSITE; COLD STORE ON DECK B (C08) ABOVE.",
        "LOOSE FURNITURE IS SECURED (GRAVITY-LOSS EMERGENCIES); WORKTOP HEIGHT 900.",
    ]):
        sh.paper.text((560, 292 - i * 3.8), l, h=1.55, ha="left")
    # door-type schedule, counted from a fresh drawing of the whole ring
    O.DOORS.clear()
    for deck in ("A", "B"): O.ring_plan(View("count"), deck, list(range(1, 13)), -15, grid=False)
    from collections import Counter
    cnt = Counter(k for _, k in O.DOORS)
    DESC = {"S": "SLIDING POCKET, Al HONEYCOMB, 25 UNDERCUT, PRIVACY LATCH", "H": "HINGED, SOLID CORE, CLOSER, VISION PANEL WHERE PUBLIC"}
    rows = [[f"D{i + 1}", ("SLIDING" if k[0] == "S" else "HINGED") + f" {k[1:]}", DESC[k[0]], "2 100", str(n)] for i, (k, n) in enumerate(sorted(cnt.items()))]
    rows.append(["PD", "PRESSURE 1200 / 1000", "SLIDING PRESSURE DOOR IN BULKHEAD, FAIL-CLOSED, 2 kPa", "2 100 / 2 000", "24"])
    sh.paper.text((560, 252), "DOOR TYPES (TAGS: COMPARTMENT.NUMBER, e.g. A05.03)", h=2.2, ha="left", bold=True)
    table(sh.paper, 560, 246, [("TYPE", 12, "c"), ("LEAF", 30, "l"), ("DESCRIPTION", 92, "l"), ("HEIGHT", 18, "c"), ("No.", 12, "c")], rows, h=1.4, rowh=4.6)
    return sh


def sheet_concepts():
    """A-601: interior concept sketches built from these drawings (tools/blueprints/sketch_interior.py, Blender)."""
    sh = Sheet("A-601", "Interior concept sketches", "Rev B: built in Blender from the plans · flat decks, curved walls · design language", "Architecture")
    p = sh.paper
    SK = os.path.join(OUT, "..", "sketch")
    views = [("final_corridorA", "DECK A CORRIDOR · C05 · SKYLIGHT OVERHEAD (A-215)"), ("final_corridorB", "DECK B CORRIDOR · C05 · 2 600 CLEAR (A-215)"),
             ("final_cabinA", "SINGLE CABIN · C05 · BED UNDER ITS OWN WINDOW W1 (A-215)"), ("final_bunkB", "DOUBLE CABIN B05 · BUNKS, OWN WINDOW W2 (A-215)"),
             ("final_concourse", "SPOKE 2 CONCOURSE · C04 · STAIR DOWN TO DECK B, LIFT (A-214)"), ("final_wardroom", "WARDROOM · C08 · 48 SEATS, WINDOWS BOTH SIDES (A-218)"),
             ("final_hydro", "HYDROPONICS BAY 1 · C02 · 4-TIER RACKS, GROW LIGHTS (A-212)")]
    W, Hh, gx, gy = 256.0, 144.0, 8.0, 22.0
    for i, (f, cap) in enumerate(views):
        c, r = i % 3, i // 3
        x0 = 30 + c * (W + gx); y1 = 572 - r * (Hh + gy)
        p.image(os.path.join(SK, f + ".png"), x0, y1 - Hh, x0 + W, y1)
        p.bubble((x0 + 5, y1 - Hh - 7), str(i + 1), 6.5)
        p.text((x0 + 11, y1 - Hh - 7), cap, h=1.9, ha="left", bold=True)
    notes(p, 30 + (W + gx), 236, "DESIGN LANGUAGE", [
        "1. PORTAL FRAMES ON EVERY STRUCTURAL FRAME (3.75°, 2 840 AT DECK A): POSTS 300×200, CHAMFERED HEAD AND FOOT.",
        "2. OCTAGONAL CORRIDOR: 420 SOFFIT CHAMFERS CARRY THE CABLE TRAYS; DECK A HAS THE ROOF SKYLIGHT DOWN ITS MIDDLE.",
        "3. LINEAR FLOOR GRILLES ALONG BOTH WALLS = THE SUBFLOOR RETURN-AIR PLENUM (M-20x).",
        "4. CONTINUOUS HANDRAILS AT +950 (LOW g): BROKEN AT DOORS, BRACKETED OFF THE FRAMES.",
        "5. AMBER = SAFETY: PRESSURE-DOOR SURROUNDS, LIFT LANDINGS, ROOM-NUMBER PLATES, WAYFINDING STRIPE.",
        "6. PALETTE: DARK GUNMETAL FRAMES, MID-GREY PANELS, WARM UPHOLSTERY, COOL WHITE LIGHT; PLANTS AT WINDOWS.",
        "7. REV B: FLAT FLOORS, GRAVITY DOWN = −Z; THE CORRIDOR BENDS AWAY SIDEWAYS WITH THE RING (r 40 000).",
    ], h=1.5)
    notes(p, 30 + 2 * (W + gx), 236, "REV B · ONE BUILD WITH THE EXTERIOR", [
        "A. EVERY WINDOW IN THESE VIEWS IS A WINDOW ON THE SHELL: build.py READS",
        "   docs/blueprints/openings.json (172 OUTER, 88 HUB, 96 SKYLIGHTS).",
        "B. ONE CABIN PER FRAME BAY, EACH WITH ITS OWN WINDOW; DECK B CABINS",
        "   ON THE OUTER WALL SEE OUT TOO (W2).",
        "C. THE SPOKE FLOOR IS DECK A AND THE SPINE HUB DECK (z −200).",
        "D. OPEN: GLAZE THE CONCOURSE-SIDE PARTITIONS OF THE JUNCTION ROOMS?",
    ], h=1.5)
    return sh


# ------------------------------------------------------------------ M sheets
def sheet_vent_quadrant(q, deck="A"):
    comps = QUADS[q]
    th0 = O.COMP_ANGLE(comps[0]) - 15
    num = f"M-20{q if deck == 'A' else q + 4}"
    sh = Sheet(num, f"Ring ventilation · Deck {deck} · quadrant {q}", f"C{comps[0]:02d}–C{comps[-1]:02d} · ductwork", "Mechanical (HVAC)")
    v = O.PolarGrey(f"vent-{deck}-q{q}", xm=1.5 * O.LA)
    O.ring_plan(v, deck, comps, th0)
    O.vent_plan(v, deck, comps, th0)
    title = ("Deck A ventilation · SA in the 1 000 roof void · RA in the interstitial void below" if deck == "A"
             else "Deck B ventilation · SA and RA in the 400 interstitial void overhead")
    sh.place(v, (420 - 1.5 * O.LA * 10, 470), 100, title, "1", title_at=(30, 286))
    p = sh.paper
    key_plan(p, 590, 64, 22, highlight=comps)
    p.text((30, 104), "VENTILATION NOTES", h=2.4, ha="left", bold=True)
    for i, l in enumerate([
        "1. EACH SPOKE-JUNCTION AHU (AHU-R1..R4, IN THE DECK B PLANT ROOM) SERVES ITS OWN COMPARTMENT AND THE ONE EITHER SIDE, BOTH DECKS.",
        "2. DECK A: SUPPLY IN THE 1 000 ROOF VOID (BESIDE THE SKYLIGHT WELLS) TO CEILING DIFFUSERS; RETURN THROUGH FLOOR GRILLES INTO",
        "   DUCTS IN THE 400 INTERSTITIAL VOID BELOW, BACK UNDER THE CORRIDOR TO SHAFT RS-n AND DOWN TO THE AHU.",
        "3. DECK B: SUPPLY AND RETURN BOTH IN THE 400 INTERSTITIAL VOID OVERHEAD, TO CEILING DIFFUSERS AND CEILING RETURN GRILLES.",
        "4. EVERY DUCT CROSSING A BULKHEAD HAS A FIRE/SMOKE/PRESSURE DAMPER (FSD), FAIL-CLOSED.",
        "5. WC / SHOWER / GALLEY / LAB / WELD EXHAUST (EA) IS DUCTED SEPARATELY TO THE SPOKE CHASE AND THE HUB TCCS — NEVER RECIRCULATED.",
        "6. REVITALISED AIR (OA) COMES ALONG THE SPOKE FROM THE HUB ECLSS; AN EQUAL FLOW (RH) GOES BACK ALONG IT FOR CO₂ REMOVAL.",
        "7. SIZES WIDTH × DEPTH IN mm; FLOWS FROM M-002 (6 ACH). VELOCITY ≤ 6 m/s IN MAINS, ≤ 3 m/s IN BRANCHES.",
    ]):
        p.text((30, 97 - i * 3.6), l, h=1.45, ha="left")
    return sh


def sheet_eclss():
    sh = Sheet("M-001", "ECLSS and air distribution schematic", "Ring AHUs · spokes · hub manifolds · revitalisation · water recovery", "Mechanical (HVAC)")
    p = sh.paper
    p.text((60, 520), "AIR AND WATER FLOW SCHEMATIC  ·  NOT TO SCALE", h=3.0, ha="left", bold=True)
    V.eclss_schematic(p, 60, 300)
    p.text((60, 275), "SYSTEM DESCRIPTION", h=2.4, ha="left", bold=True)
    for i, l in enumerate([
        "1. RECIRCULATION: EACH RING AHU MOVES ITS ZONES' AIR OVER A CHILLED-WATER COIL (COOL, DEHUMIDIFY) AND HEPA H13 FILTERS.",
        "2. REVITALISATION: A FIXED FLOW (12 L/s PER PERSON SERVED) LEAVES EACH AHU UP ITS SPOKE (RH) TO THE HUB, IS STRIPPED OF CO₂ (4BMS)",
        "   AND TRACE CONTAMINANTS (TCCS), TOPPED UP WITH O₂ (OGA), AND COMES BACK DOWN THE SPOKE AS OA.",
        "3. O₂: 0.84 kg/PERSON/DAY × 120 = 101 kg/DAY, FROM ≈ 113 kg/DAY OF WATER ELECTROLYSED (OGA-1 + OGA-2 STANDBY).",
        "4. CO₂: 1.0 kg/PERSON/DAY × 120 = 120 kg/DAY; THE SABATIER RECOVERS ≈ 44 kg/DAY OF WATER; METHANE IS VENTED.",
        "5. WATER: CONDENSATE, GREYWATER AND URINE TO WATER RECOVERY (93 %); POTABLE BUFFER IN S2a AND TANKS PWT-1/2 (C03 DECK B).",
        "6. HEAT: AHU COILS AND COLD PLATES ON CHWS/CHWR LOOPS TO THE ATCS PUMPS (S2a) AND THE RADIATOR FINS (±Y, z −12 000).",
        "7. EXHAUST (EA) FROM WCs, SHOWERS, GALLEY, LAB FUME HOODS AND THE WELD BOOTH GOES ONLY TO TCCS.",
        "8. EMERGENCY: ON SMOKE OR DEPRESSURISATION THE ZONE'S FSDs CLOSE, ITS AHU STOPS, AND THE SAFE-HAVEN SCRUBBER STARTS.",
    ]):
        p.text((60, 268 - i * 3.8), l, h=1.55, ha="left")
    return sh


def sheet_schedules():
    sh = Sheet("M-002", "Schedules and calculations", "AHU · damper · terminal schedules; airflow and ECLSS sizing", "Mechanical (HVAC)")
    p = sh.paper
    fl = O.zone_flows()
    p.text((30, 565), "ZONE AIRFLOW (6 AIR CHANGES / HOUR)", h=2.4, ha="left", bold=True)
    rows = []
    for k in range(1, 13):
        qa, qb, va, vb = fl[k]
        rows.append([f"C{k:02d}", f"{va:.0f}", f"{qa:.3f}", f"{vb:.0f}", f"{qb:.3f}", f"{qa + qb:.3f}"])
    tot = [sum(fl[k][i] for k in fl) for i in range(4)]
    rows.append(["TOTAL", f"{tot[2]:.0f}", f"{tot[0]:.2f}", f"{tot[3]:.0f}", f"{tot[1]:.2f}", f"{tot[0] + tot[1]:.2f}"])
    y = table(p, 30, 558, [("ZONE", 18, "c"), ("DECK A m³", 24, "r"), ("SA A m³/s", 24, "r"), ("DECK B m³", 24, "r"), ("SA B m³/s", 24, "r"), ("TOTAL m³/s", 26, "r")], rows, h=1.55, rowh=4.6)
    p.text((30, y - 8), "AHU SCHEDULE", h=2.4, ha="left", bold=True)
    rows = []
    for j, a in O.AHU_OF.items():
        q = sum(fl[c][0] + fl[c][1] for c in O.served(j))
        rows.append([f"AHU-R{a}", f"C{j:02d} DECK B", " ".join(f"C{c:02d}" for c in O.served(j)), f"{q:.2f}", f"{0.012 * 27:.2f}", "750", f"{q * 750 / 0.6 / 1000:.1f}", f"{q * 1.2 * 1.006 * 10:.0f}", "H13"])
    rows.append(["AHU-S", "SPINE S2b", "SPINE · DOME · HUB", "3.20", "—", "600", f"{3.2 * 600 / 0.6 / 1000:.1f}", f"{3.2 * 1.2 * 1.006 * 8:.0f}", "H13"])
    y = table(p, 30, y - 15, [("TAG", 16, "l"), ("LOCATION", 24, "l"), ("SERVES", 30, "l"), ("SA m³/s", 17, "r"), ("OA m³/s", 17, "r"), ("ESP Pa", 15, "r"),
                              ("FAN kW", 15, "r"), ("COIL kW", 16, "r"), ("FILTER", 14, "c")], rows, h=1.45, rowh=4.6)
    p.text((30, y - 8), "DAMPER AND TERMINAL SCHEDULE", h=2.4, ha="left", bold=True)
    table(p, 30, y - 15, [("TYPE", 20, "l"), ("DESCRIPTION", 130, "l"), ("QTY", 14, "r")], [
        ["FSD", "FIRE/SMOKE/PRESSURE ISOLATION DAMPER AT A BULKHEAD · MOTORISED · FAIL-CLOSED · 2 kPa SEAL", f"{12 * 5}"],
        ["CD-300", "CEILING SQUARE DIFFUSER 300×300, 4-WAY, OPPOSED-BLADE DAMPER", "≈ 290"],
        ["RG", "RETURN GRILLE 500×150 (DECK A FLOOR) · 400×400 (DECK B CEILING)", "≈ 260"],
        ["EG-200", "EXHAUST GRILLE 200×200 WITH ODOUR FILTER (WC, SHOWER)", "≈ 60"],
        ["SH-1..4", "SPOKE ISOLATION HATCH Ø1 000 AT BOTH ENDS OF EACH SPOKE", "8"]], h=1.45, rowh=4.6)
    p.text((440, 565), "ECLSS SIZING (120 PERSONS)", h=2.4, ha="left", bold=True)
    y4 = table(p, 440, 558, [("QUANTITY", 70, "l"), ("PER PERSON / DAY", 34, "r"), ("STATION / DAY", 40, "r")], [
        ["O₂ CONSUMED", "0.84 kg", "101 kg"], ["CO₂ PRODUCED", "1.00 kg", "120 kg"], ["DRINKING + FOOD WATER", "2.5 kg", "300 kg"],
        ["HYGIENE WATER", "7.0 kg", "840 kg"], ["METABOLIC HEAT", "11.8 MJ", "1 416 MJ (16.4 kW)"], ["REVITALISED AIR EXCHANGE", "12 L/s", "1.44 m³/s"],
        ["WATER RECOVERY 93 % · MAKE-UP", "—", "≈ 74 kg"]], h=1.5, rowh=4.6)
    p.text((440, y4 - 8), "CALCULATION NOTES", h=2.4, ha="left", bold=True)
    for i, l in enumerate([
        "ZONE VOLUME = (DEVELOPED LENGTH − 600 BULKHEAD) × 8 000 × CLEAR HEIGHT (3 000 DECK A · 2 200 DECK B).",
        "SA = VOLUME × 6 / 3 600.   FAN kW = Q × ESP / η, η = 0.60.   COIL kW = SENSIBLE AT ΔT 10 K (8 K SPINE).",
        "OA PER AHU = 12 L/s × 27 PERSONS (108 DESIGN + 10 % SPREAD OVER 4 AHUs).",
        "DUCTS SIZED FOR ≤ 6 m/s IN MAINS. METABOLIC, EQUIPMENT AND LIGHTING HEAT GO TO THE RADIATORS",
        "THROUGH THE CHILLED-WATER LOOPS.",
    ]):
        p.text((440, y4 - 15 - i * 3.6), l, h=1.45, ha="left")
    return sh


def sheet_risers():
    sh = Sheet("M-301", "Hub node · spoke chase · spine risers", "Ventilation at the hub transfer node and in the spokes", "Mechanical (HVAC)")
    v, title = V.spine_level("s3a")
    sh.place(v, (220, 340), 50, "Hub transfer node S3a · manifolds", "1", title_at=(40, 140))
    sh.place(V.spoke_cross(), (640, 475), 20, "Spoke chase (typical)", "2", title_at=(520, 356))
    p = sh.paper
    x0, y0 = 500, 110
    p.text((x0 - 40, 325), "SPINE RISER DIAGRAM · NOT TO SCALE", h=2.4, ha="left", bold=True)
    levels = [(lid, z) for lid, _, z, _, _ in V.SPINE_LEVELS] + [("TOP", 38.0)]
    zmin, zmax = -26.0, 38.0
    Y = lambda z: y0 + (z - zmin) / (zmax - zmin) * 190
    for lid, z in levels:
        p.line((x0, Y(z)), (x0 + 300, Y(z)), "A-ANNO-NOTE"); p.text((x0 - 2, Y(z)), f"{lid}  z {z:+.1f}", h=1.4, ha="right")
    for i, (lay, lab, bot, top) in enumerate((("M-DUCT-SUPP", "SA-S Ø630", -26.0, 38.0), ("M-DUCT-RETN", "RA-S Ø630", -26.0, 38.0),
                                              ("M-DUCT-OAIR", "OA Ø500", -7.5, 4.5), ("M-DUCT-RETN", "RH Ø500", -7.5, 4.5))):
        x = x0 + 40 + i * 50
        p.rect(x - 2, Y(bot), x + 2, Y(top), lay)
        p.text((x, Y(top) + 3), lab, h=1.4)
        if "-S" in lab:
            for lid, z in levels[:-1]:
                p.line((x + 2, Y(z) + 4), (x + 12, Y(z) + 4), lay)
    p.rect(x0 + 220, Y(-7.5) - 4, x0 + 300, Y(-7.5) + 4, "M-HVAC-EQPM", fill="#fff6dc"); p.text((x0 + 260, Y(-7.5)), "ARS / ECLSS · S2b", h=1.4, bold=True)
    p.rect(x0 + 220, Y(1.5) - 4, x0 + 300, Y(1.5) + 4, "M-HVAC-EQPM", fill="#f4f4f4"); p.text((x0 + 260, Y(1.5)), "HUB MANIFOLDS · S3a ↔ 4 SPOKES", h=1.3, bold=True)
    p.rect(x0 + 220, Y(-4.5) - 4, x0 + 300, Y(-4.5) + 4, "M-HVAC-EQPM", fill="#e8eef8"); p.text((x0 + 260, Y(-4.5)), "AHU-S · S2b", h=1.4, bold=True)
    return sh


# ------------------------------------------------------------------ DXF underlays
def write_dxf():
    d = os.path.join(OUT, "dxf"); os.makedirs(d, exist_ok=True)
    for deck in ("A", "B"):
        v = View(f"ring deck {deck} developed C01-C12")
        O.ring_plan(v, deck, list(range(1, 13)), -15.0)
        vv = View(f"ring deck {deck} ventilation")
        O.ring_plan(vv, deck, list(range(1, 13)), -15.0, contents=False, grid=False)
        O.vent_plan(vv, deck, list(range(1, 13)), -15.0)
        to_dxf([v], os.path.join(d, f"ring_deck_{deck}.dxf"))
        to_dxf([vv], os.path.join(d, f"ring_deck_{deck}_vent.dxf"))
    to_dxf([V.spine_level(l[4])[0] for l in V.SPINE_LEVELS], os.path.join(d, "spine_levels.dxf"))
    to_dxf([V.spoke_plan(), V.spoke_cross()], os.path.join(d, "spoke.dxf"))
    to_dxf([V.ring_section("typical"), V.ring_section("junction"), V.bulkhead_elevation()], os.path.join(d, "ring_sections.dxf"))
    to_dxf([V.dock_hub()], os.path.join(d, "dock_hub.dxf"))
    to_dxf([V.meridional_section()], os.path.join(d, "section_AA.dxf"))


SCALES = {"G-001": "—", "G-002": "NTS", "A-101": "1:500", "A-102": "1:200", "A-301": "1:25 · 1:50", "A-302": "1:50 · 1:20", "A-406": "1:100", "A-601": "NTS",
          "A-501": "1:25 · 1:50", "M-001": "NTS", "M-002": "—", "M-301": "1:50 · 1:20 · NTS"}


def build(only=None):
    os.makedirs(OUT, exist_ok=True)
    rooms, quads = [], []
    for deck in ("A", "B"):
        for q in (1, 2, 3, 4):
            sh, r = sheet_ring_quadrant(q, deck)
            quads.append(sh); rooms += r
    body = [sheet_ga(), sheet_section_aa()] + quads + [sheet_compartment(k) for k in range(1, 13)] + [sheet_ring_sections(), sheet_spoke()]
    body += [sheet_spine(["s0", "s1"], 1), sheet_spine(["s2a", "s2b"], 2), sheet_spine(["s3a", "s3b"], 3), sheet_spine(["s4a", "s4b"], 4), sheet_spine(["s5", "dome"], 5)]
    body += [sheet_dock(), sheet_enlarged(), sheet_concepts(), sheet_eclss(), sheet_schedules()] + [sheet_vent_quadrant(q, dk) for dk in ("A", "B") for q in (1, 2, 3, 4)] + [sheet_risers()]
    heads = [("G-001", "Cover · index · notes · legend"), ("G-002", "Design criteria · compartmentation · egress")]
    index = [[n, t.upper(), SCALES.get(n, "")] for n, t in heads] + \
            [[s.number, s.title.upper(), SCALES.get(s.number, "1:100" if s.number[:4] in ("A-20", "M-20") else "1:50")] for s in body]
    sheets = [sheet_cover(index, rooms), sheet_criteria(rooms)] + body
    if only: sheets = [s for s in sheets if s.number in only]
    if not only:
        for f in os.listdir(OUT):
            if f.endswith(".png") or f == "partial.pdf": os.remove(os.path.join(OUT, f))
    save_set(sheets, os.path.join(OUT, "SF-OUT_Outpost_Interior_RevA.pdf" if not only else "partial.pdf"), OUT, dpi=100)
    if not only:
        write_dxf()
        with open(os.path.join(OUT, "room_schedule.csv"), "w", encoding="utf-8") as f:
            f.write("deck,compartment,room,name,area_m2\n")
            for deck, k, num, name, a in rooms: f.write(f"{deck},C{k:02d},{num},{name},{a:.2f}\n")
    return sheets, rooms


if __name__ == "__main__":
    only = sys.argv[1:] or None
    sheets, rooms = build(only)
    print(len(sheets), "sheets;", len(rooms), "rooms")
