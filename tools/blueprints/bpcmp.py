"""bpcmp.py ITER : each reference (docs/blueprints/refs/bp_ref*) beside a high-res crop of the matching sheet of ours
-> docs/blueprints/cmp/cmp_i<ITER>_<key>.jpg (one pair per file, readable) and cmp_i<ITER>.jpg (all five)."""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from PIL import Image, ImageDraw
import matplotlib.pyplot as plt
from matplotlib.transforms import Bbox
from draft import MplRenderer
import outpost_set as S

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "blueprints")
# key: (reference file, sheet builder, crop box in paper mm x0, y0, x1, y1)
PAIRS = {
    "enlarged": ("bp_ref1_enlarged.png", S.sheet_enlarged, (40, 350, 330, 565)),
    "cdplan":   ("bp_ref2_cdplan.png", lambda: S.sheet_ring_quadrant(2)[0], (25, 120, 430, 290)),
    "duct":     ("bp_ref3_duct.png", lambda: S.sheet_vent_quadrant(2), (60, 150, 330, 275)),
    "section":  ("bp_ref4_section.png", S.sheet_ring_sections, (20, 60, 470, 580)),
    "cd50":     ("bp_ref2_cdplan.png", lambda: __import__("outpost_sheets").sheet_compartment(5), (40, 300, 560, 560)),
    "ga":       ("bp_ref5_ga.png", lambda: S.sheet_ring_quadrant(2)[0], (0, 0, 841, 594)),
}


def crop(sheet, box, path, px=1500):
    fig = MplRenderer(sheet).render()
    x0, y0, x1, y1 = box
    dpi = px / ((x1 - x0) / 25.4)
    fig.savefig(path, dpi=min(dpi, 400), bbox_inches=Bbox([[x0 / 25.4, y0 / 25.4], [x1 / 25.4, y1 / 25.4]]))
    plt.close(fig)


def pair(ref, ours, label, H=900):
    a, b = Image.open(ref).convert("RGB"), Image.open(ours).convert("RGB")
    a = a.resize((int(a.width * H / a.height), H)); b = b.resize((int(b.width * H / b.height), H))
    o = Image.new("RGB", (a.width + b.width + 12, H + 26), (30, 30, 34))
    d = ImageDraw.Draw(o)
    d.text((6, 6), f"REFERENCE · {label}", fill=(240, 240, 240)); d.text((a.width + 18, 6), "OURS", fill=(240, 240, 240))
    o.paste(a, (0, 26)); o.paste(b, (a.width + 12, 26))
    return o


if __name__ == "__main__":
    it = sys.argv[1]
    keys = sys.argv[2:] or list(PAIRS)
    out = os.path.join(ROOT, "cmp"); os.makedirs(out, exist_ok=True)
    rows = []
    for k in keys:
        ref, build, box = PAIRS[k]
        p = os.path.join(out, f"ours_i{it}_{k}.png")
        crop(build(), box, p)
        im = pair(os.path.join(ROOT, "refs", ref), p, k)
        im.save(os.path.join(out, f"cmp_i{it}_{k}.jpg"), quality=88); rows.append(im)
        print(os.path.join(out, f"cmp_i{it}_{k}.jpg"))
    W = max(r.width for r in rows)
    sheet = Image.new("RGB", (W, sum(r.height + 8 for r in rows)), (30, 30, 34)); y = 0
    for r in rows: sheet.paste(r, (0, y)); y += r.height + 8
    sheet.thumbnail((2400, 6000)); sheet.save(os.path.join(out, f"cmp_i{it}.jpg"), quality=85)
