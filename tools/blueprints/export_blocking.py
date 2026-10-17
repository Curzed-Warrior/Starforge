"""Blueprint geometry -> docs/blueprints/sketch/blocking.json for the Blender interior sketches (sketch_interior.py).
Developed plan coordinates: x along the deck at its floor radius from theta = -15 deg (bulkhead B12), y = axial (Z)."""
import json, os, sys
sys.path.insert(0, os.path.dirname(__file__))
from draft import View
import outpost_design as O

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "blueprints", "sketch")
KEEP = ("A-WALL", "A-WALL-SHFT", "A-WALL-BHD", "A-WALL-PRHU", "A-FURN", "A-EQPM", "A-FLOR-STRS", "A-DOOR", "A-DOOR-PRES", "A-GLAZ")


def deck(dk):
    v, doors, rooms, props = View(dk), [], [], []
    O.ring_plan(v, dk, list(range(1, 13)), -15.0, grid=False, doors_out=doors, rooms_out=rooms, props_out=props)  # developed: the sketch bends it
    prims = []
    for pr in v.prims:
        if pr.layer not in KEEP: continue
        d = pr.data
        if pr.kind == "poly": prims.append({"k": "poly", "l": pr.layer, "p": d["pts"], "c": d["closed"], "f": bool(d.get("fill"))})
        elif pr.kind == "line": prims.append({"k": "line", "l": pr.layer, "p": d["pts"]})
        elif pr.kind == "circle": prims.append({"k": "circle", "l": pr.layer, "c": d["c"], "r": d["r"]})
    texts = [{"p": pr.data["p"], "s": pr.data["s"]} for pr in v.prims if pr.kind == "text" and pr.layer == "A-ANNO-NOTE"]
    tags = [{"p": pr.data["p"], "n": pr.data["name"], "num": pr.data["number"]} for pr in v.prims if pr.kind == "tag"]
    ffl, clg = O.FFL[dk], O.CLG[dk]
    # "r" / "clg" keep the sketch's H = r - clg: the plan radius and that minus the clear height
    return {"r": O.R_MID, "clg": O.R_MID - (clg - ffl), "ffl": ffl, "prims": prims, "doors": doors, "texts": texts, "tags": tags, "props": props}


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    data = {"HW": O.HW, "T": O.T, "A": deck("A"), "B": deck("B"), "radii": {"hull_out": O.R_HULL_OUT, "RO": O.RO, "RI": O.RI}}
    with open(os.path.join(OUT, "blocking.json"), "w") as f: json.dump(data, f)
    print({k: len(data[k]["prims"]) for k in ("A", "B")}, len(data["A"]["doors"]))
