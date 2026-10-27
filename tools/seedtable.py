# usage: python tools/seedtable.py DIR [DIR2]  -> a markdown table of the end lines of tools/seeds.sh runs (DIR2: after)
import re, sys, os

SEEDS = ["20610", "7", "4242", "1", "99", "2026"]

def stats(d, s):
    p = os.path.join(d, s + ".txt")
    if not os.path.exists(p): return None
    txt = open(p, encoding="utf-8", errors="replace").read()
    def kv(tag):
        m = re.search(r"^" + tag + r" (.*)$", txt, re.M)
        return dict(re.findall(r"(\w+)=(\S+)", m.group(1))) if m else {}
    w, st, fr, sp = kv("WARSTATS"), kv("STRIFESTATS"), kv("FRACTURESTATS"), kv("SPECOPSSTATS")
    t = {}
    tp = os.path.join(d, "times.txt")
    if os.path.exists(tp):
        for line in open(tp):
            a = line.split()
            if len(a) == 2: t[a[0]] = a[1]
    emp = re.search(r"^EMPIRESTATS (.*)$", txt, re.M)
    return {
        "wars": w.get("wars"), "battles": w.get("battles"), "revolts": st.get("revolts"), "civil": st.get("civilwars"),
        "peaceful": st.get("peaceful"), "movements": st.get("movements"), "fractured": st.get("fractured"),
        "human": fr.get("humanSystems"), "hsplit": f"{fr.get('humanPeaceful')}/{fr.get('humanWar')}/{fr.get('humanCivil')}",
        "spec": f"{sp.get('successTrained')}/{sp.get('successRushed')}", "chron": st.get("chronicle"),
        "time": str(round(int(t[s][:-2]) / 1000, 1)) + " s" if s in t else "-", "empire": emp.group(1) if emp else "-",
    }

cols = ["wars", "battles", "revolts", "civil", "peaceful", "movements", "fractured", "hsplit", "spec", "chron", "time"]
dirs = sys.argv[1:]
print("| seed | run | " + " | ".join(cols) + " |")
print("|---|---|" + "---|" * len(cols))
for s in SEEDS:
    for i, d in enumerate(dirs):
        r = stats(d, s)
        if r: print(f"| {s} | {'before' if i == 0 and len(dirs) > 1 else 'after'} | " + " | ".join(str(r[c]) for c in cols) + " |")
for s in SEEDS:
    for d in dirs:
        r = stats(d, s)
        if r and r["empire"] != "-": print(f"{s} {os.path.basename(d)}: {r['empire']}")
