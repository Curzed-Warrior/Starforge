---
name: blueprints
description: Rebuild and QA the outpost drawing set (tools/blueprints) — sheets, DXF underlays, room schedule, openings.json, reference comparisons, concept sketches. Use when outpost_design.py / outpost_views.py / outpost_set.py change, or the user asks about the blueprints, floor plans, sections or ventilation sheets.
argument-hint: [SHEET ...] e.g. A-202 M-206, or "all"
---

# Outpost blueprints: rebuild and check

Code: `tools/blueprints/` — `draft.py` (ISO drafting kit), `outpost_design.py` (source of truth: dimensions,
programmes, ventilation, `openings()`), `outpost_views.py` (sections, spoke, spine, hub, GA), `outpost_sheets.py` /
`outpost_set.py` (sheets). Output: `docs/blueprints/outpost/` (PDF, PNG per sheet, `dxf/`, `room_schedule.csv`).

1. Build: `cd tools/blueprints && python -X utf8 outpost_set.py $ARGUMENTS`
   - with sheet numbers: builds those only -> `partial.pdf` + their PNGs
   - no args / `all`: full set + DXF + CSV. Run this before declaring the work done.
2. If `outpost_design.py` changed, also refresh what depends on it:
   - `python -X utf8 -c "import json,outpost_design as O; json.dump(O.openings(), open('../../docs/blueprints/openings.json','w'), indent=1)"`
     (the exterior build.py reads it: rebuild the outpost with /station-asset outpost)
   - `python -X utf8 export_blocking.py` (interior sketches / furnished build read blocking.json)
3. QA every changed sheet PNG with the `render-qa` subagent: overlapping views/titles, text clipped by the border,
   dimension strings on top of geometry, empty views, labels upside down. Fix the layout in `outpost_set.py` /
   `outpost_sheets.py` (`sh.place(view, origin, scale, ..., title_at=...)`) and rebuild those sheets.
4. Reference rounds: `python bpcmp.py <iter> [keys]` -> `docs/blueprints/cmp/cmp_i<iter>_<key>.jpg`.
5. Concept sketches: in Blender, `exec(open(r".../tools/blueprints/sketch_interior.py").read())`, `build(deck, x_lo, x_hi)`,
   `shot(name, deck, x, y, yaw=, pitch=, lens=)`; the A-601 sheet places `docs/blueprints/sketch/final_*.png`.

Conventions: ISO 128/129/3098/5455/5457/7200, A1 sheets, mm dims with oblique ticks, layers AIA-style.
Rev B geometry: flat decks, down = -Z; Deck A FFL z -0.2, Deck B FFL z -3.5; plans developed at R_MID 40 m and drawn
in true plan through `PolarView`. Python edits with non-ASCII or `\n` in strings: use the Edit tool, not heredocs.
