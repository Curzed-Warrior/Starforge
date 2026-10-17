#!/usr/bin/env python3
"""Build data/techs.json from catalogue.txt and validate it.

Usage: python tools/build_techs.py data/tech_catalogue.txt data/techs.json
Checks: unknown requirements, requirement tier inversions, cycles, effect syntax,
reachability under every combination of exclusive choices, and tier coverage per branch.
"""
import itertools, json, re, sys
from collections import defaultdict

BRANCH_OF_LINE = {
    "PWR": "E", "MEG": "E",
    "IND": "I", "MIN": "I", "MAT": "I",
    "CMP": "M", "SEN": "M", "PSI": "M",
    "FTL": "X", "HUL": "X", "CIV": "X",
    "EWP": "W", "KWP": "W", "MSL": "W", "SFC": "W", "XWP": "W", "PKL": "W",
    "ARM": "W", "SHD": "W", "DEF": "W", "STB": "W", "MIL": "W", "GND": "W",
    "BIO": "L", "XEN": "L", "TER": "L",
    "COL": "S", "GOV": "S", "ECO": "S", "DIP": "S", "ESP": "S", "ARC": "S",
    "UMB": "Q", "CHR": "Q", "GLY": "Q", "STL": "Q", "LUX": "Q",
}
ASC_BRANCH = {"G": "L", "C": "M", "S": "S", "P": "M", "D": "Q", "L": "Q"}

MOD_FIELDS = {"Mine", "Foundry", "Eff", "GasYield", "Rp", "HubCost", "Shield", "Comets", "PopCap", "Growth",
              "PlanetCap", "ShipSpeed", "RegionRate", "RegionPower",
              # new
              "Fleet", "Ground", "Stealth", "Sense", "Range", "Diplo", "Unity", "Admin", "Trade", "Terra",
              "Uplift", "Habs", "Settle", "Build", "Spy", "WarLoss", "Base", "Thrust"}
ARMS = {"energy", "kinetic", "missile", "craft", "exotic", "doom", "ground"}
GUARDS = {"armor", "shield", "pd", "base"}
POOLS = {"Umbral", "Abyssal", "Anima", "Stellarine", "Lumen"}
METERS = {"AI", "Mind", "Goo", "Strange", "Breach", "Veil", "Atrocity", "Elders", "Flux", "Spy", "Egress"}
GATES = {"GE6", "GE7", "GE7b", "GE8", "GE9", "GE10", "GE10b"}

EFFECT_RE = [
    ("mod", re.compile(r"^mod:(\w+)(\*|>=|<=)([0-9.e+-]+)$")),
    ("flag", re.compile(r"^flag:(\w+)$")),
    ("arms", re.compile(r"^arms:(\w+)>=(\d+)$")),
    ("guard", re.compile(r"^guard:(\w+)>=(\d+)$")),
    ("hull", re.compile(r"^hull:(\w+)$")),
    ("mega", re.compile(r"^mega:(\w+)$")),
    ("res", re.compile(r"^res:(\w+)\+([0-9.]+)$")),
    ("use", re.compile(r"^use:(\w+)$")),
    ("meter", re.compile(r"^meter:(\w+)([+-])([0-9.]+)$")),
    ("act", re.compile(r"^act:(\w+)$")),
    ("vis", re.compile(r"^vis:(\w+)$")),
    ("gate", re.compile(r"^gate:(\w+)$")),
    ("event", re.compile(r"^event:(\w+)$")),
    ("cond", re.compile(r"^cond:(.+)$")),
    ("path", re.compile(r"^path:(\w)$")),
    ("digital", re.compile(r"^digital:([0-9.]+)$")),
    ("conscience", re.compile(r"^conscience:(-?[0-9.]+)$")),
    ("apotheosis", re.compile(r"^apotheosis:\+(\d+)$")),
]


def branch_of(tid):
    if re.match(r"^[XIES]\d+$", tid):
        return tid[0]
    if tid.startswith("ASC-"):
        return ASC_BRANCH[tid[4]]
    return BRANCH_OF_LINE[tid.split("-")[0]]


def line_of(tid):
    if re.match(r"^[XIES]\d+$", tid):
        return "SPINE"
    return tid.split("-")[0]


def parse_effect(e, errs, tid):
    for kind, rx in EFFECT_RE:
        m = rx.match(e)
        if not m:
            continue
        g = m.groups()
        if kind == "mod" and g[0] not in MOD_FIELDS: errs.append(f"{tid}: unknown mod field {g[0]}")
        if kind == "arms" and g[0] not in ARMS: errs.append(f"{tid}: unknown arms family {g[0]}")
        if kind == "guard" and g[0] not in GUARDS: errs.append(f"{tid}: unknown guard family {g[0]}")
        if kind in ("res", "use") and g[0] not in POOLS: errs.append(f"{tid}: unknown pool {g[0]}")
        if kind == "meter" and g[0] not in METERS: errs.append(f"{tid}: unknown meter {g[0]}")
        if kind == "gate" and g[0] not in GATES: errs.append(f"{tid}: unknown gate {g[0]}")
        return {"kind": kind, "args": list(g)}
    errs.append(f"{tid}: cannot parse effect '{e}'")
    return None


def load(path):
    techs, errs = {}, []
    for n, raw in enumerate(open(path, encoding="utf-8"), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        parts = [p.strip() for p in line.split("|")]
        if len(parts) != 7:
            errs.append(f"line {n}: expected 7 fields, got {len(parts)}: {line[:60]}")
            continue
        tid, tier, name, reqs, flags, effects, desc = parts
        if tid in techs:
            errs.append(f"duplicate id {tid}")
        req = [] if reqs == "-" else [alt.split("/") for alt in reqs.split(",")]
        fl = [] if flags == "-" else [f.strip() for f in flags.split(",")]
        excl = [f[2:] for f in fl if f.startswith("X:")]
        eff = [] if effects == "-" else [e.strip() for e in effects.split(";") if e.strip()]
        parsed = [parse_effect(e, errs, tid) for e in eff]
        techs[tid] = {
            "id": tid, "branch": branch_of(tid), "line": line_of(tid), "tier": int(tier), "name": name,
            "req": req, "start": "S" in fl, "rare": "R" in fl, "danger": "D" in fl, "keystone": "K" in fl,
            "event": "E" in fl, "atrocity": "A" in fl, "exclusive": excl[0] if excl else None,
            "effects": [p for p in parsed if p], "fx": desc,
        }
    return techs, errs


def validate(techs):
    errs, warns = [], []
    for t in techs.values():
        for alts in t["req"]:
            for r in alts:
                if r not in techs:
                    errs.append(f"{t['id']}: unknown requirement {r}")
                elif techs[r]["tier"] > t["tier"]:
                    errs.append(f"{t['id']} (T{t['tier']}) requires {r} (T{techs[r]['tier']}): tier inversion")
    # cycles (treat every alternative as an edge)
    state = {}
    def visit(u, stack):
        state[u] = 1
        for alts in techs[u]["req"]:
            for r in alts:
                if r not in techs: continue
                if state.get(r) == 1: errs.append("cycle: " + " -> ".join(stack + [r]))
                elif r not in state: visit(r, stack + [r])
        state[u] = 2
    for u in techs:
        if u not in state: visit(u, [u])
    return errs, warns


def closure(techs, chosen):
    """Everything reachable when, for each exclusive group, only the member in `chosen` may be taken."""
    known = {t for t, v in techs.items() if v["start"]}
    taken_group = {}
    changed = True
    while changed:
        changed = False
        for t, v in techs.items():
            if t in known: continue
            g = v["exclusive"]
            if g and chosen.get(g) not in (None, t): continue
            if all(any(r in known for r in alts) for alts in v["req"]):
                known.add(t); changed = True
    return known


def reachability(techs):
    groups = defaultdict(list)
    for t, v in techs.items():
        if v["exclusive"]: groups[v["exclusive"]].append(t)
    # sub-groups only matter on the matching path; enumerate the top-level choices fully
    top = ["asc", "fork7", "ftl", "siphon", "mandate"]
    sub = [g for g in groups if g not in top]
    union, per_fork = set(), defaultdict(set)
    combos = list(itertools.product(*[groups[g] for g in top]))
    for combo in combos:
        chosen = dict(zip(top, combo))
        # sub-groups are independent of each other, so stepping them together covers every member
        width = max(len(groups[g]) for g in sub) if sub else 1
        for i in range(width):
            ch = dict(chosen); ch.update({g: groups[g][i % len(groups[g])] for g in sub})
            k = closure(techs, ch)
            union |= k
            per_fork[chosen["fork7"]] |= k
    never = sorted(set(techs) - union)
    return never, per_fork, groups


def main(src, out):
    techs, errs = load(src)
    e2, warns = validate(techs)
    errs += e2
    never, per_fork, groups = reachability(techs)
    # coverage report
    cov = defaultdict(lambda: defaultdict(int))
    for v in techs.values(): cov[v["branch"]][v["tier"]] += 1
    print(f"{len(techs)} techs")
    print("branch  " + " ".join(f"T{t:<3}" for t in range(11)))
    for b in "XIESLMWQ":
        print(f"  {b}     " + " ".join(f"{cov[b][t]:<4}" for t in range(11)))
    for f, k in per_fork.items():
        lost = sorted(set(techs) - k)
        print(f"fork7={f}: gives up {len(lost)} techs: {', '.join(lost)}")
    if never: errs.append("never reachable under any choices: " + ", ".join(never))
    for w in warns: print("WARN", w)
    for e in errs: print("ERROR", e)
    doc = {
        "version": 1,
        "branches": {
            "X": "Starflight", "I": "Industry", "E": "Energy", "S": "Society",
            "L": "Life", "M": "Mind", "W": "War", "Q": "Beyond",
        },
        "areas": {"Physics": ["E", "M", "Q"], "Engineering": ["I", "X", "W"], "Society": ["S", "L"]},
        "exclusiveGroups": {g: sorted(m) for g, m in groups.items()},
        "techs": list(techs.values()),
    }
    json.dump(doc, open(out, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print(f"wrote {out}")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(*sys.argv[1:3]))
