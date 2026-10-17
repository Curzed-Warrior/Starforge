"""
Outpost interior concept sketches, built in Blender from the blueprints (docs/blueprints/sketch/blocking.json, written by
export_blocking.py). Run inside Blender:  exec(open(r".../sketch_interior.py").read()); then build(...) and shot(...).

Developed plan coords (x along the deck at its floor radius, from theta = -15 deg; y = axial) are wrapped back onto the
ring: theta = -15 deg + x / r_floor, radius = r_floor - height. 'Down' is radially outward; the floor curves UP ahead.
Look: flat marker greys (emission, no lighting) + Freestyle ink lines. The DESIGN layer (frames, light strips, panels,
rails, window surrounds) is procedural and parameterised in STYLE, so iterations change STYLE, not the blueprint.
"""
import bpy, json, math, os
from mathutils import Vector, Matrix

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)) if "__file__" in dir() else r"C:\Users\diamo\Desktop\starforge\tools\blueprints",
                    "..", "..", "docs", "blueprints", "sketch")
ROOT = os.path.normpath(r"C:\Users\diamo\Desktop\starforge\docs\blueprints\sketch")
TH0 = math.radians(-15.0)
STEP = math.radians(3.75)          # structural frame pitch (build.py: 96 frames)

STYLE = dict(
    frame_w=0.30, frame_d=0.20, frame_chamfer=0.42, frame_foot=0.28,      # corridor portal frames at every structural frame
    strip_w=0.44, strip_recess=0.08, rib=0.3,                     # ceiling light strip
    tile=1.2, tile_gap=0.012,                            # floor plates
    rail_h=0.95, rail_r=0.025, rail_off=0.06,            # handrails (low g: everywhere)
    win_trim=0.08, win_round=0.25,                       # window surround
    panel_h=(0.25, 1.15, 2.1),                           # corridor wall panel joints (above floor)
    kick=0.12,                                           # kick plate / base
)

SHADE = {"floor": (0.20, 0.21, 0.23), "wall": (0.50, 0.52, 0.55), "ceil": (0.32, 0.33, 0.36), "hull": (0.28, 0.29, 0.32),
         "furn": (0.58, 0.55, 0.50), "eqpm": (0.38, 0.40, 0.44), "frame": (0.17, 0.18, 0.21), "light": (1.0, 1.0, 1.0),
         "door": (0.36, 0.38, 0.42), "rail": (0.07, 0.07, 0.08), "glass": (0.45, 0.62, 0.85), "accent": (0.92, 0.52, 0.08),
         "grow": (0.95, 0.45, 0.95), "plant": (0.25, 0.55, 0.22)}


# ------------------------------------------------------------------ helpers
R_MID = 40.0
PBR = [False]        # True: the furnished build (assets/stations/outpost/interior.py): real materials, no ink, no own glass
FFL = [0.0]          # z of the deck being built (REV B: flat decks, down = -Z)
WIN = {"A": {1: (0.8, 2.6), -1: (0.8, 2.4)}, "B": {1: (0.8, 2.0)}}     # window sill/head above FFL: outer (+1), hub (-1)


def P(x, y, h, rf=None):
    """Developed (x along the ring at R_MID, y = r - R_MID outward) and height above the deck -> world."""
    th = TH0 + x / R_MID
    r = R_MID + y
    return (r * math.cos(th), r * math.sin(th), FFL[0] + h)


class Acc:
    """Accumulates geometry per material class; one object each at the end."""
    def __init__(self): self.g = {}

    def add(self, cls, verts, faces):
        V, F = self.g.setdefault(cls, ([], []))
        o = len(V); V.extend(verts); F.extend([tuple(i + o for i in f) for f in faces])

    def prism(self, cls, pts, h0, h1, rf, seg=0.5):
        """Footprint polygon pts (developed x, y), extruded from h0 to h1 above the floor, curved with the ring."""
        fp = []
        n = len(pts)
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            k = max(1, int(abs(b[0] - a[0]) / seg + 0.999))
            for j in range(k): fp.append((a[0] + (b[0] - a[0]) * j / k, a[1] + (b[1] - a[1]) * j / k))
        m = len(fp)
        verts = [P(x, y, h0, rf) for x, y in fp] + [P(x, y, h1, rf) for x, y in fp]
        faces = [tuple(reversed(range(m))), tuple(range(m, 2 * m))] + [(i, (i + 1) % m, m + (i + 1) % m, m + i) for i in range(m)]
        self.add(cls, verts, faces)

    def profile(self, cls, x0, x1, prof, rf, seg=0.5):
        """Extrude a closed cross-section prof = [(y, h), ...] along the deck from x0 to x1 (follows the curve)."""
        k = max(1, int(abs(x1 - x0) / seg + 0.999)); m = len(prof)
        verts = [P(x0 + (x1 - x0) * i / k, y, h, rf) for i in range(k + 1) for (y, h) in prof]
        faces = [tuple(reversed(range(m))), tuple(range(k * m, k * m + m))]
        for i in range(k):
            for j in range(m):
                a0, a1 = i * m + j, i * m + (j + 1) % m
                faces.append((a0, a1, a1 + m, a0 + m))
        self.add(cls, verts, faces)

    def box(self, cls, x0, y0, x1, y1, h0, h1, rf, seg=0.5):
        """A box following the ring's curve: segmented along x (no long n-gons, whose chords would cut the curve)."""
        if cls == "light": self.__dict__.setdefault("lights", []).append((x0, y0, x1, y1, h0, h1, FFL[0]))
        self.profile(cls, x0, x1, [(y0, h0), (y1, h0), (y1, h1), (y0, h1)], rf, seg)

    def sheet(self, cls, x0, x1, y0, y1, h, rf, nx=None):
        """A curved surface at height h (floor / ceiling), facing toward +h if h<1 else down."""
        k = nx or max(1, int((x1 - x0) / 0.5))
        verts, faces = [], []
        for i in range(k + 1):
            x = x0 + (x1 - x0) * i / k
            verts += [P(x, y0, h, rf), P(x, y1, h, rf)]
        for i in range(k): faces.append((2 * i, 2 * i + 2, 2 * i + 3, 2 * i + 1))
        self.add(cls, verts, faces)

    def tube(self, cls, x0, x1, y, h, r, rf, n=8, h1=None):
        k = max(1, int((x1 - x0) / 0.5))
        verts, faces = [], []
        for i in range(k + 1):
            x = x0 + (x1 - x0) * i / k
            hh = h + ((h1 if h1 is not None else h) - h) * i / k
            for j in range(n):
                a = 2 * math.pi * j / n
                verts.append(P(x, y + r * math.cos(a), hh + r * math.sin(a), rf))
        for i in range(k):
            for j in range(n):
                a, b = i * n + j, i * n + (j + 1) % n
                faces.append((a, b, b + n, a + n))
        self.add(cls, verts, faces)

    def build(self, coll):
        for cls, (V, F) in self.g.items():
            me = bpy.data.meshes.new(f"sk_{cls}")
            me.from_pydata(V, [], F); me.validate(); me.update()
            ob = bpy.data.objects.new(f"sk_{cls}", me)
            ob.data.materials.append(mat(cls))
            coll.objects.link(ob)


PBR_MAT = {  # cls: (colour, metal, rough, emission strength)
    "floor": ((0.09, 0.095, 0.1), 0.7, 0.38, 0), "wall": ((0.56, 0.57, 0.59), 0.1, 0.45, 0), "ceil": ((0.36, 0.37, 0.39), 0.1, 0.6, 0),
    "hull": ((0.3, 0.31, 0.33), 0.5, 0.42, 0), "frame": ((0.06, 0.065, 0.075), 0.9, 0.3, 0), "furn": ((0.42, 0.36, 0.3), 0.0, 0.85, 0),
    "eqpm": ((0.22, 0.23, 0.25), 0.4, 0.45, 0), "door": ((0.28, 0.3, 0.33), 0.6, 0.35, 0), "rail": ((0.6, 0.6, 0.62), 1.0, 0.2, 0),
    "light": ((1.0, 0.95, 0.88), 0.0, 0.5, 4), "glass": ((0.25, 0.6, 0.95), 0.0, 0.2, 1.2), "accent": ((0.9, 0.45, 0.05), 0.2, 0.4, 1.2),
    "grow": ((1.0, 0.4, 0.9), 0.0, 0.5, 6), "plant": ((0.13, 0.36, 0.1), 0.0, 0.75, 0)}


def pbr_mat(cls):
    name = f"IN_{cls}"
    m = bpy.data.materials.get(name)
    if m: return m
    col, metal, rough, em = PBR_MAT[cls]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*col, 1); b.inputs["Metallic"].default_value = metal; b.inputs["Roughness"].default_value = rough
    if em:
        b.inputs["Emission Color"].default_value = (*col, 1); b.inputs["Emission Strength"].default_value = em
    nt = m.node_tree
    if cls in ("wall", "hull"):     # horizontal panel seams every 600 (world z), a darker kick band up to 120 above the floor
        geo = nt.nodes.new("ShaderNodeNewGeometry"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Position"], sep.inputs[0])
        wv = nt.nodes.new("ShaderNodeTexWave"); wv.wave_type = "BANDS"; wv.bands_direction = "Z"; wv.wave_profile = "SAW"
        wv.inputs["Scale"].default_value = 1.0
        mapz = nt.nodes.new("ShaderNodeCombineXYZ"); fr = nt.nodes.new("ShaderNodeMath"); fr.operation = "FRACT"
        dv = nt.nodes.new("ShaderNodeMath"); dv.operation = "DIVIDE"; dv.inputs[1].default_value = 0.6
        nt.links.new(sep.outputs["Z"], dv.inputs[0]); nt.links.new(dv.outputs[0], fr.inputs[0])
        seam = nt.nodes.new("ShaderNodeMapRange"); seam.inputs["From Min"].default_value = 0.0; seam.inputs["From Max"].default_value = 0.015
        nt.links.new(fr.outputs[0], seam.inputs["Value"])
        mixc = nt.nodes.new("ShaderNodeMix"); mixc.data_type = "RGBA"
        mixc.inputs[6].default_value = (col[0] * 0.35, col[1] * 0.35, col[2] * 0.38, 1); mixc.inputs[7].default_value = (*col, 1)
        nt.links.new(seam.outputs["Result"], mixc.inputs[0]); nt.links.new(mixc.outputs[2], b.inputs["Base Color"])
    if cls == "glass":              # wall displays: a dark screen with scan bands and a bright header strip
        geo = nt.nodes.new("ShaderNodeNewGeometry"); wv = nt.nodes.new("ShaderNodeTexWave"); wv.bands_direction = "Z"
        wv.inputs["Scale"].default_value = 18.0; wv.inputs["Distortion"].default_value = 2.0
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (0.01, 0.04, 0.08, 1); ramp.color_ramp.elements[1].color = (0.2, 0.75, 1.0, 1)
        ramp.color_ramp.elements[1].position = 0.9
        nt.links.new(geo.outputs["Position"], wv.inputs["Vector"]); nt.links.new(wv.outputs["Fac"], ramp.inputs[0])
        nt.links.new(ramp.outputs[0], b.inputs["Emission Color"]); b.inputs["Base Color"].default_value = (0.01, 0.01, 0.015, 1)
        b.inputs["Roughness"].default_value = 0.08
    if cls in ("floor", "wall", "hull", "eqpm"):     # a little surface break-up so big panels don't read as flat CG
        tex = nt.nodes.new("ShaderNodeTexNoise"); tex.inputs["Scale"].default_value = 3.0
        bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 0.06
        nt.links.new(tex.outputs["Fac"], bump.inputs["Height"]); nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
        rr = nt.nodes.new("ShaderNodeMapRange"); rr.inputs["To Min"].default_value = rough * 0.8; rr.inputs["To Max"].default_value = rough * 1.2
        nt.links.new(tex.outputs["Fac"], rr.inputs["Value"]); nt.links.new(rr.outputs["Result"], b.inputs["Roughness"])
    return m


def mat(cls):
    if PBR[0]: return pbr_mat(cls)
    name = f"SK_{cls}"
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (*SHADE[cls], 1); em.inputs[1].default_value = 1.0
    # facing ratio darkens grazing surfaces a touch: reads like marker shading
    lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs[0].default_value = 0.35
    mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"
    mix.inputs[1].default_value = (*SHADE[cls], 1)
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (1, 1, 1, 1); ramp.color_ramp.elements[1].color = (0.72, 0.72, 0.74, 1)
    nt.links.new(lw.outputs["Facing"], ramp.inputs[0]); nt.links.new(ramp.outputs[0], mix.inputs[2]); mix.inputs[0].default_value = 1.0
    nt.links.new(mix.outputs[0], em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
    if cls == "light": em.inputs[1].default_value = 1.15
    return m


# ------------------------------------------------------------------ blueprint -> blocking
def inside(pt, bb):
    return bb[0] - 1e-3 <= pt[0] <= bb[2] + 1e-3 and bb[1] - 1e-3 <= pt[1] <= bb[3] + 1e-3


LABEL_H = [("BUNK", 0.45), ("BED", 0.5), ("WR", 2.1), ("DESK", 0.75), ("SUIT", 1.95), ("RACK", 2.4), ("SINK", 0.9), ("BENCH", 0.45),
           ("SCRUBBER", 1.6), ("LOCKER", 2.0), ("COUNTER", 1.05), ("COOKING", 0.9), ("ISLAND", 0.9), ("AHU", 2.0), ("SEATING", 0.45),
           ("TABLE", 0.75), ("CABINET", 1.9), ("WORKBENCH", 0.9), ("EXAM", 0.8), ("TRAUMA", 0.85), ("DB-", 1.9), ("SWITCHBOARD", 2.0)]


def furn_height(pr, bb, texts, layer):
    w, d = bb[2] - bb[0], bb[3] - bb[1]
    lab = " ".join(t["s"] for t in texts if inside(t["p"], bb)).upper()
    for key, h in LABEL_H:
        if key in lab: return h, lab
    if len(pr["p"]) > 8: return 0.42, lab                      # WC pans / basin bowls (ellipses)
    a, b = sorted((w, d))
    if b <= 0.5: return 0.45, lab                              # chair
    if a >= 0.8 and b >= 1.8: return 0.5, lab                  # bed
    if a <= 0.62 and 0.9 <= b <= 1.4: return 0.75, lab         # desk
    if a >= 0.55 and b <= 0.8: return 2.1, lab                 # wardrobe
    return (1.4 if layer == "A-EQPM" else 0.75), lab


def blocking(acc, data, deck, x_lo, x_hi):
    D = data[deck]; rf = D["r"]; H = rf - D["clg"]; HW, T = data["HW"], data["T"]
    clip = lambda pts: any(x_lo <= p[0] <= x_hi for p in pts)
    texts = D["texts"]
    furn = []
    for pr in D["prims"]:
        if pr["k"] != "poly" or not pr["c"] or not clip(pr["p"]): continue
        L = pr["l"]
        xs = [p[0] for p in pr["p"]]; ys = [p[1] for p in pr["p"]]
        bb = (min(xs), min(ys), max(xs), max(ys))
        if L in ("A-WALL", "A-WALL-SHFT"): acc.prism("wall", pr["p"], 0, H, rf)
        elif L == "A-WALL-BHD": acc.prism("hull", pr["p"], 0, H, rf)
        elif L == "A-WALL-PRHU":
            if PBR[0]:      # inside the shell's skin only; the spoke-tunnel walls outside the ring belong to the shell
                if min(abs(q[1]) for q in pr["p"]) >= HW + T - 1e-3: continue
                lim = HW + T - 0.05
                pr = dict(pr, p=[(q[0], max(-lim, min(lim, q[1]))) for q in pr["p"]])
            acc.prism("hull", pr["p"], 0, H, rf)
        elif L == "A-DOOR" and not STYLE.get("doors_closed", True): acc.prism("door", pr["p"], 0, 2.1, rf)
        elif L in ("A-FURN", "A-EQPM"): furn.append((pr, bb, L))
    # furniture: children (pillows, bowls) sit a little proud of their parent
    for pr, bb, L in furn:
        h, lab = furn_height(pr, bb, texts, L)
        parent = [q for q in furn if q[1] != bb and q[1][0] <= bb[0] and q[1][1] <= bb[1] and q[1][2] >= bb[2] and q[1][3] >= bb[3]]
        if parent:
            ph, _ = furn_height(parent[0][0], parent[0][1], texts, parent[0][2])
            if ph < 1.2: acc.prism("furn", pr["p"], ph, ph + 0.1, rf); continue
            continue
        if "RACK" in lab:          # hydroponic rack: 4 trays on end posts, grow light under each tray, crop on top
            for hs in (0.35, 0.9, 1.45, 2.0):
                acc.prism("eqpm", pr["p"], hs, hs + 0.05, rf)
                for f in (0.3, 0.7):
                    ym = bb[1] + (bb[3] - bb[1]) * f
                    acc.box("grow", bb[0] + 0.1, ym - 0.03, bb[2] - 0.1, ym + 0.03, hs + 0.5, hs + 0.51, rf)
                acc.prism("plant", pr["p"], hs + 0.05, hs + 0.22, rf)
            for xe in (bb[0], bb[2] - 0.06):
                acc.box("frame", xe, bb[1], xe + 0.06, bb[3], 0, 2.55, rf)
            continue
        if "BUNK" in lab:
            acc.prism("furn", pr["p"], 0.25, 0.45, rf); acc.prism("furn", pr["p"], 1.35, 1.55, rf)
            continue
        if h == 0.75 and min(bb[2] - bb[0], bb[3] - bb[1]) < 0.65:   # desk: a slab on a leg
            acc.prism("furn", pr["p"], 0.72, 0.76, rf); continue
        cx_, cy_ = (bb[0] + bb[2]) / 2, (bb[1] + bb[3]) / 2
        if h == 0.75 and not lab:          # table: top on a pedestal
            acc.prism("furn", pr["p"], 0.72, 0.76, rf); acc.box("frame", cx_ - 0.08, cy_ - 0.08, cx_ + 0.08, cy_ + 0.08, 0, 0.72, rf)
            acc.box("frame", cx_ - 0.25, cy_ - 0.25, cx_ + 0.25, cy_ + 0.25, 0, 0.03, rf); continue
        if h == 0.45 and not lab:          # chair / stool: seat on a stem
            acc.prism("furn", pr["p"], 0.42, 0.47, rf); acc.box("frame", cx_ - 0.03, cy_ - 0.03, cx_ + 0.03, cy_ + 0.03, 0, 0.42, rf); continue
        acc.prism("eqpm" if L == "A-EQPM" else "furn", pr["p"], 0, h, rf)
    # side walls of the hull: Deck A has the window band (sill 0.8 / head 2.8); the blueprint already drew the solid
    # hull pieces between the windows full height, so fill below the sill and above the head everywhere
    Lc = R_MID * math.radians(30)
    spokes = [((k - 0.5) * Lc - 2.7, (k - 0.5) * Lc + 2.7) for k in (1, 4, 7, 10)] if deck == "A" else []
    for s in (-1, 1):
        y0, y1 = sorted((s * HW, s * (HW + T - (0.05 if PBR[0] else 0))))
        w = WIN[deck].get(s)
        if w and s < 0 and spokes:
            xs_ = [x_lo] + [v for a_, b_ in spokes for v in (a_, b_)] + [x_hi]
            for i_, (u0, u1) in enumerate(zip(xs_, xs_[1:])):
                if u1 <= u0: continue
                if i_ % 2 == 0: acc.box("hull", u0, y0, u1, y1, 0, w[0], rf); acc.box("hull", u0, y0, u1, y1, w[1], H, rf)
                else: acc.box("hull", u0, y0, u1, y1, 2.7, H, rf)
        elif w:
            acc.box("hull", x_lo, y0, x_hi, y1, 0, w[0], rf); acc.box("hull", x_lo, y0, x_hi, y1, w[1], H, rf)
        else:
            acc.box("hull", x_lo, y0, x_hi, y1, 0, H, rf)
    # headers over every door opening, and over the pressure doors in each bulkhead
    for a, b, y in D["doors"]:
        if b < x_lo or a > x_hi: continue
        acc.box("wall", a, y - 0.06, b, y + 0.06, 2.1, H, rf)
    L = rf * math.radians(30)
    for kb in range(13):
        xb = kb * L
        if x_lo - 1 <= xb <= x_hi + 1:
            a = 0.6 if deck == "A" else 0.5
            acc.box("hull", xb - 0.3, -a, xb + 0.3, a, 2.1, H, rf)
    # floor plates and ceiling
    tile, gap = STYLE["tile"], STYLE["tile_gap"]
    nx = int((x_hi - x_lo) / tile)
    tw = tile / 2
    for i in range(nx):
        for j in range(int(2 * HW / tw)):
            x0, y0 = x_lo + i * tile, -HW + j * tw
            acc.sheet("floor", x0 + gap, x0 + tile - gap, y0 + gap, y0 + tw - gap, 0.0, rf, nx=2)
    if deck == "A":     # skylight wells over the corridor, up through the 1 000 roof void to the glass
        acc.sheet("ceil", x_lo, x_hi, -HW, -0.96, H, rf); acc.sheet("ceil", x_lo, x_hi, 0.96, HW, H, rf)
        for s in (-1, 1): acc.box("wall", x_lo, s * 0.96 - 0.03, x_hi, s * 0.96 + 0.03, H, H + 1.0, rf)
        if not PBR[0]: acc.box("glass", x_lo, -0.96, x_hi, 0.96, H + 1.0, H + 1.02, rf)
    else:
        acc.sheet("ceil", x_lo, x_hi, -HW, HW, H, rf)
    return rf, H


# ------------------------------------------------------------------ design layer (sci-fi), driven by STYLE
CORRIDOR_PROGS = {"A": (5, 6, 12), "B": (5, 6, 9, 12)}       # compartments whose central corridor gets the portal frames


def spans(xa, xb, cuts, pad=0.1):
    """[xa, xb] minus the door openings in cuts [(a, b)], each widened by pad."""
    out, x = [], xa
    for a, b in sorted(cuts):
        if b + pad < xa or a - pad > xb: continue
        if a - pad > x: out.append((x, a - pad))
        x = max(x, b + pad)
    if x < xb: out.append((x, xb))
    return out


def design(acc, data, deck, x_lo, x_hi):
    D = data[deck]; rf = D["r"]; H = rf - D["clg"]; HW = data["HW"]
    S = STYLE
    L = rf * math.radians(30)
    cw = 0.9                                     # corridor wall inner face (walls at +/-0.96, 120 thick)
    c = S["frame_chamfer"]
    for k in CORRIDOR_PROGS[deck]:
        xa, xb = (k - 1) * L + 0.3, k * L - 0.3
        if xb < x_lo or xa > x_hi: continue
        xa, xb = max(xa, x_lo), min(xb, x_hi)
        # octagonal profile: continuous upper chamfers (cable trays behind) and a low kick chamfer
        for s in (-1, 1):
            acc.profile("wall", xa, xb, [(s * cw, H - c), (s * cw, H), (s * (cw - c), H)], rf)
            # wall panelling: proud trim bands (service band 1.05-1.95 with locker seams), base kick
            cuts = [(a_, b_) for a_, b_, y_ in D["doors"] if abs(y_ - s * 0.96) < 0.05]
            for r0, r1 in spans(xa, xb, cuts):
                for h0, h1 in ((1.02, 1.05), (1.95, 1.98)):
                    acc.box("frame", r0, s * cw - (0.012 if s > 0 else 0), r1, s * cw + (0 if s > 0 else 0.012), h0, h1, rf)
            acc.prism("frame", [(xa, s * cw), (xb, s * cw), (xb, s * (cw - 0.25)), (xa, s * (cw - 0.25))], 0, 0.05, rf)   # floor grille band
            for g in range(int((xb - xa) / 0.08)):
                xg = xa + g * 0.08
                acc.box("rail", xg, min(s * cw, s * (cw - 0.25)), xg + 0.015, max(s * cw, s * (cw - 0.25)), 0.05, 0.056, rf)
        # portal frames on the structural frame rhythm, following the profile
        th_a = TH0 + xa / rf
        n0 = math.ceil((th_a - math.radians(-15)) / STEP)
        x = (math.radians(-15) + n0 * STEP - TH0) * rf
        fw, fd = S["frame_w"], S["frame_d"]
        while x <= xb:
            for s in (-1, 1):
                yi, yo = s * (cw - fd), s * cw
                acc.box("frame", x - fw / 2, min(yi, yo), x + fw / 2, max(yi, yo), S["frame_foot"], H - c - 0.05, rf)
                ff = S["frame_foot"]
                acc.profile("frame", x - fw / 2, x + fw / 2, [(yo, 0), (s * (cw - fd - ff * 0.5), 0), (yi, ff), (yo, ff)][::(1 if s > 0 else -1)], rf)
                # chamfer leg: a true wedge following the soffit, proud of it by the frame depth
                hb = H - c - 0.05
                acc.profile("frame", x - fw / 2, x + fw / 2, [(yo, hb), (yi, hb), (s * (cw - fd - c), H - 0.14), (s * (cw - c), H)][::(1 if s > 0 else -1)], rf)
            acc.box("frame", x - fw / 2, -(cw - c - fd), x + fw / 2, cw - c - fd, H - 0.14, H, rf)
            # a small downlight in each frame head
            acc.box("light", x - 0.05, -0.25, x + 0.05, 0.25, H - 0.145, H - 0.14, rf)
            x += STEP * rf
        # ribbed ceiling between the soffits (acoustic / service slats), the light trough down the middle
        for s in (-1, 1):          # longitudinal ceiling ribs either side of the light trough
            for f in (0.25, 0.5, 0.75):
                yy = s * (S["strip_w"] / 2 + 0.04 + (cw - c - S["strip_w"] / 2 - 0.04) * f)
                acc.box("frame", xa, yy - 0.02, xb, yy + 0.02, H - 0.06, H, rf)
        # ceiling light strip (recessed trough with a lit diffuser)
        sw = S["strip_w"]
        for s in (-1, 1): acc.box("frame", xa, s * sw / 2 - 0.02, xb, s * sw / 2 + 0.02, H - S["strip_recess"], H, rf)
        acc.box("light", xa, -sw / 2, xb, sw / 2, H - S["strip_recess"] + 0.01, H - S["strip_recess"] + 0.02, rf)
        # handrails both sides on brackets
        for s in (-1, 1):
            yr = s * (cw - fd - S["rail_off"])
            cuts = [(a_, b_) for a_, b_, y_ in D["doors"] if abs(y_ - s * 0.96) < 0.05]
            for r0, r1 in spans(xa, xb, cuts, 0.15):
                if r1 - r0 > 0.3: acc.tube("rail", r0, r1, yr, S["rail_h"], S["rail_r"], rf)
    # closed door leaves in the corridor walls: a leaf, a vision / light slit, a number plate, a jamb trim
    for a, b, y in D["doors"]:
        if b < x_lo or a > x_hi or abs(abs(y) - 0.96) > 0.05: continue
        s = 1 if y > 0 else -1
        acc.box("door", a, y - 0.03, b, y + 0.03, 0, 2.1, rf)
        xm = (a + b) / 2
        acc.box("light", b - 0.12, s * (0.96 - 0.035) - 0.004, b - 0.08, s * (0.96 - 0.035) + 0.004, 0.3, 1.95, rf)
        acc.box("accent", a - 0.18, s * 0.9 - 0.01, a - 0.06, s * 0.9 + 0.01, 1.45, 1.6, rf)
        for xe in (a, b):
            acc.box("frame", xe - 0.04, s * 0.9 - 0.02 if s > 0 else s * 0.9, xe + 0.04, s * 0.9 if s > 0 else s * 0.9 + 0.02, 0, 2.18, rf)
        acc.box("frame", a - 0.04, s * 0.9 - 0.02 if s > 0 else s * 0.9, b + 0.04, s * 0.9 if s > 0 else s * 0.9 + 0.02, 2.1, 2.18, rf)
    # pressure doors: amber hazard surround on both bulkhead faces, a closed heavy leaf
    for kb in range(13):
        xb_ = kb * L
        if not (x_lo - 1 <= xb_ <= x_hi + 1): continue
        a = 0.6 if deck == "A" else 0.5
        for f in (-1, 1):
            xf = xb_ + f * 0.3
            for (y0, y1, h0, h1) in ((-a - 0.15, -a, 0, 2.25), (a, a + 0.15, 0, 2.25), (-a - 0.15, a + 0.15, 2.1, 2.25)):
                acc.box("accent", xf - (0.02 if f < 0 else 0), y0, xf + (0.02 if f > 0 else 0), y1, h0, h1, rf)
    # window surrounds on Deck A: a proud rounded-feel trim (sill, head, jambs) round each glazed run
    if deck == "A" and not PBR[0]:
        runs = []
        for pr in D["prims"]:
            if pr["l"] != "A-GLAZ" or pr["k"] != "line": continue
            (x0, y0), (x1, y1) = pr["p"]
            if abs(abs(y0) - HW) > 0.02 or not (x_lo <= x0 <= x_hi): continue
            if (1 if y0 > 0 else -1) in WIN[deck]: runs.append((min(x0, x1), max(x0, x1), 1 if y0 > 0 else -1))
        t = S["win_trim"]
        for x0, x1, s in runs:
            yin = s * (HW - 0.06)
            lo, hi = min(yin, s * HW), max(yin, s * HW)
            w0, w1 = WIN[deck][s]
            acc.box("frame", x0, lo, x1, hi, w0 - t, w0, rf); acc.box("frame", x0, lo, x1, hi, w1, w1 + t, rf)
            acc.box("glass", x0, s * (HW + 0.25) - 0.01, x1, s * (HW + 0.25) + 0.01, w0, w1, rf)
        # jambs where a glazed run starts or stops (merge runs that touch)
        ends = sorted(runs)
        merged = []
        for x0, x1, s in ends:
            if merged and merged[-1][2] == s and x0 - merged[-1][1] < 0.2: merged[-1][1] = x1
            else: merged.append([x0, x1, s])
        for x0, x1, s in merged:
            for xe in (x0, x1):
                acc.box("frame", xe - 0.06, min(s * (HW - 0.06), s * HW), xe + 0.06, max(s * (HW - 0.06), s * HW), WIN[deck][s][0] - 0.08, WIN[deck][s][1] + 0.08, rf)


JUNCTIONS = (1, 4, 7, 10)
BIG_ROOMS = {"A": (1, 2, 3, 4, 7, 8, 9, 10, 11, 12), "B": (1, 4, 7, 10, 8, 11, 12)}


def rooms_fitout(acc, data, deck, x_lo, x_hi):
    """Junction stair (21 risers to Deck B), ceiling light grid in the big rooms."""
    D = data[deck]; rf = D["r"]; H = rf - D["clg"]; HW = data["HW"]
    L = rf * math.radians(30)
    if deck == "A":
        for k in JUNCTIONS:
            xc = (k - 0.5) * L
            sxa = (L / 2 - 0.3) / 11.062                  # Deck A rooms are fitted to the ring (outpost_design.SX_A)
            sx0, sw, y0 = xc + 3.0 * sxa, 1.1, 1.6
            if sx0 + 6 < x_lo or sx0 > x_hi: continue
            # REV B: the stair goes DOWN 18 risers to Deck B: a guarded well with the top treads showing
            acc.box("rail", sx0 - 0.1, y0, sx0 + 18 * 0.26, y0 + sw, -0.02, 0.003, rf)
            for i in range(6):
                acc.box("floor", sx0 + i * 0.26, y0, sx0 + (i + 1) * 0.26, y0 + sw, -0.6 - (i + 1) * 0.183, -(i + 1) * 0.183, rf)
            for yy in (y0 - 0.05, y0 + sw + 0.05):
                acc.tube("rail", sx0, sx0 + 18 * 0.26, yy, 1.05, 0.025, rf)
                for i in range(0, 19, 3):
                    acc.box("frame", sx0 + i * 0.26, yy - 0.02, sx0 + i * 0.26 + 0.04, yy + 0.02, 0, 1.05, rf)
            # planters along the window band, a bench line, the lift landing and a floor wayfinding stripe
            for (a_, b_) in ((xc + 2.0, xc + 2.6), (xc + 3.4, xc + 8.4)):
                pass
            for x0p in (xc + 2.0, xc + 4.6, xc + 6.4):
                acc.box("eqpm", x0p, 3.3, x0p + 1.5, 3.95, 0, 0.75, rf)
                acc.box("plant", x0p + 0.08, 3.38, x0p + 1.42, 3.87, 0.75, 1.15, rf)
            acc.box("accent", xc - 10.5, -0.12, xc + 10.5, 0.12, 0.0, 0.004, rf)
            # lift landing doors (shaft x -2.15..-0.15 rel, landing on +y at y 1.1+0.15) with an amber surround and a sign
            yl = 1.1 + 0.16
            acc.box("door", xc - 1.75, yl, xc - 0.55, yl + 0.03, 0, 2.1, rf)
            for (a_, b_, h0, h1) in ((xc - 1.9, xc - 1.75, 0, 2.25), (xc - 0.55, xc - 0.4, 0, 2.25), (xc - 1.9, xc - 0.4, 2.1, 2.25)):
                acc.box("accent", a_, yl, b_, yl + 0.04, h0, h1, rf)
            acc.box("light", xc - 1.6, yl, xc - 0.7, yl + 0.045, 2.4, 2.7, rf)
    for k in BIG_ROOMS[deck]:
        xa, xb = (k - 1) * L + 1.0, k * L - 1.0
        if xb < x_lo or xa > x_hi: continue
        x = xa
        while x + 1.2 <= xb:
            for yc in ((-2.6, 0.0, 2.6) if k in JUNCTIONS else (-2.6, 2.6)):
                acc.box("light", x, yc - 0.3, x + 1.2, yc + 0.3, H - 0.012, H - 0.002, rf)
                acc.box("frame", x - 0.04, yc - 0.34, x + 1.24, yc + 0.34, H - 0.008, H, rf)
            x += 2.4


QUARTERS = {"A": (5, 6), "B": (5, 6)}


def cabins(acc, data, deck, x_lo, x_hi):
    """Cabin fit-out (same cell layout as outpost_design.quarters): bed alcove, lockers, display, light."""
    D = data[deck]; rf = D["r"]; H = rf - D["clg"]; HW = data["HW"]
    L = rf * math.radians(30)
    hyg = {1: 3, -1: 4}
    for k in QUARTERS[deck]:
        xc = (k - 0.5) * L
        e = [xc - L / 2 + i * L / 8 for i in range(9)]       # REV B: one cabin per frame bay
        e[0], e[8] = xc - L / 2 + 0.3, xc + L / 2 - 0.3
        for sgn in (1, -1):
            for i in range(8):
                if i == hyg[sgn]: continue
                cx0, cx1 = e[i], e[i + 1]
                if cx1 < x_lo or cx0 > x_hi: continue
                yb0, yb1 = sorted((sgn * (HW - 0.95), sgn * (HW - 0.05)))
                # headboard at the head (x0) end and drawer fronts on the bed's room-side face
                acc.box("furn", cx0 + 0.1, yb0, cx0 + 0.16, yb1, 0.5, 1.15 if deck == "A" else 1.25, rf)
                yf = sgn * (HW - 0.95)
                for xd in (cx0 + 0.75, cx0 + 1.45):
                    acc.box("frame", xd - 0.005, yf - 0.012 if sgn > 0 else yf, xd + 0.005, yf if sgn > 0 else yf + 0.012, 0.08, 0.42, rf)
                acc.box("frame", cx0 + 0.1, yf - 0.012 if sgn > 0 else yf, cx0 + 2.1, yf if sgn > 0 else yf + 0.012, 0.24, 0.25, rf)
                if deck == "A":
                    # locker bank along the partition at the bed's foot is the wardrobe; over the bed: a shelf with a light
                    ys0, ys1 = sorted((sgn * (HW - 0.22), sgn * HW))
                    acc.box("eqpm", cx0 + 0.1, ys0, cx1 - 0.08, ys1, 0.72, 0.8, rf)            # sill ledge over the bed head
                    acc.box("light", cx0 + 0.1, ys0, cx1 - 0.08, ys1, 2.86, 2.88, rf)          # cove light in the window head
                else:
                    # upper bunk: a second platform, a privacy curtain rail, a ladder at the foot
                    acc.box("furn", cx0 + 0.1, yb0, cx0 + 2.1, yb1, 1.35, 1.55, rf)
                    acc.box("light", cx0 + 0.3, sgn * (HW - 0.12) - 0.01, cx0 + 1.9, sgn * (HW - 0.12) + 0.01, 1.3, 1.33, rf)
                    for xl in (cx0 + 1.95, cx0 + 2.05):
                        acc.box("rail", xl - 0.015, yf - 0.03, xl + 0.015, yf + 0.03, 0, 1.6, rf)
                # wall display above the desk, on the partition at the cabin's x1 end
                acc.box("glass", cx1 - 0.1, min(sgn * 1.75, sgn * 2.45), cx1 - 0.07, max(sgn * 1.75, sgn * 2.45), 1.05, 1.5, rf)
                acc.box("frame", cx1 - 0.07, min(sgn * 1.72, sgn * 2.48), cx1 - 0.06, max(sgn * 1.72, sgn * 2.48), 1.02, 1.53, rf)
                # ceiling light panel and a wall panel seam band
                yc = sgn * 2.4
                acc.box("light", cx0 + 0.7, yc - 0.45, cx1 - 0.7, yc + 0.45, H - 0.012, H - 0.002, rf)
                acc.box("frame", cx0 + 0.65, yc - 0.5, cx1 - 0.65, yc + 0.5, H - 0.01, H - 0.0, rf)


# ------------------------------------------------------------------ scene, camera, render
def reset():
    coll = bpy.data.collections.get("Sketch")
    if coll:
        for ob in list(coll.objects): bpy.data.objects.remove(ob, do_unlink=True)
    else:
        coll = bpy.data.collections.new("Sketch"); bpy.context.scene.collection.children.link(coll)
    for me in list(bpy.data.meshes):
        if me.name.startswith("sk_") and me.users == 0: bpy.data.meshes.remove(me)
    return coll


def build(deck="A", x_lo=None, x_hi=None):
    data = json.load(open(os.path.join(ROOT, "blocking.json")))
    FFL[0] = data[deck]["ffl"]
    coll = reset()
    acc = Acc()
    blocking(acc, data, deck, x_lo, x_hi)
    design(acc, data, deck, x_lo, x_hi)
    cabins(acc, data, deck, x_lo, x_hi)
    rooms_fitout(acc, data, deck, x_lo, x_hi)
    acc.build(coll)
    return {k: len(v[0]) for k, v in acc.g.items()}


def camera(deck_r, x, y, eye=1.6, yaw=0.0, pitch=0.0, lens=18.0):
    """Camera standing on the deck at developed (x, y), eye height above the floor, looking spinward (+x) turned by
    yaw (deg, + toward +y) and pitch (deg, + up toward the hub)."""
    th = TH0 + x / R_MID
    pos = Vector(P(x, y, eye))
    fwd_t = Vector((-math.sin(th), math.cos(th), 0))       # along the ring (+theta)
    up = Vector((0, 0, 1))                                 # REV B: up is +Z
    ax = Vector((math.cos(th), math.sin(th), 0))           # yaw + turns outward
    f = fwd_t * math.cos(math.radians(yaw)) + ax * math.sin(math.radians(yaw))
    f = (f * math.cos(math.radians(pitch)) + up * math.sin(math.radians(pitch))).normalized()
    upv = (up - f * up.dot(f)).normalized()
    z = -f; xr = upv.cross(z)
    M = Matrix((xr, upv, z)).transposed()
    cam = bpy.data.objects.get("SK_cam")
    if not cam:
        cam = bpy.data.objects.new("SK_cam", bpy.data.cameras.new("SK_cam")); bpy.context.scene.collection.objects.link(cam)
    cam.matrix_world = Matrix.Translation(pos) @ M.to_4x4()
    cam.data.lens = lens; cam.data.clip_start = 0.05; cam.data.clip_end = 200
    bpy.context.scene.camera = cam
    return cam


def look(res=(1600, 900)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.use_freestyle = True
    sc.render.line_thickness_mode = "ABSOLUTE"; sc.render.line_thickness = 1.1
    sc.view_settings.view_transform = "Standard"
    vl = bpy.context.view_layer
    fs = vl.freestyle_settings
    fs.crease_angle = math.radians(140)
    ls = fs.linesets[0] if fs.linesets else fs.linesets.new("ink")
    ls.select_by_visibility = True; ls.select_by_edge_types = True
    ls.select_silhouette = ls.select_border = ls.select_crease = True
    ls.linestyle.color = (0.08, 0.08, 0.1); ls.linestyle.thickness = 1.1
    w = sc.world or bpy.data.worlds.new("SK_world"); sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background"); bg.inputs[0].default_value = (0.02, 0.025, 0.04, 1)
    try: sc.eevee.taa_render_samples = 16
    except Exception: pass


def shot(name, deck, x, y, **kw):
    data_r = 43.4 if deck == "A" else 39.6
    camera(data_r, x, y, **kw)
    look()
    out = os.path.join(ROOT, f"{name}.png")
    bpy.context.scene.render.filepath = out
    hidden = []
    for c in bpy.context.scene.collection.children:       # only the sketch renders (the station shell shares the file)
        if c.name != "Sketch" and not c.hide_render: c.hide_render = True; hidden.append(c)
    try:
        bpy.ops.render.render(write_still=True)
    finally:
        for c in hidden: c.hide_render = False
    return out
