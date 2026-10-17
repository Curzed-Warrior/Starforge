# War Foundation (Stage 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the war system's foundation in place: tunable war data, war culture, the war-reasons-and-endings model, fleets/squadrons/admirals and armies as units, and a WW2-style war economy (civilian vs military industry, mobilisation, materiel, shipyards with slips, barracks and manpower), saved, visible as colour-coded placeholder cubes, and controllable from the Empire screen.

**Architecture:** New partial-class files on `Simulation` (`src/Sim/WarEconomy.cs`, `Forces.cs`) and plain model files (`WarData.cs`, `Culture.cs`, `WarModel.cs`), driven yearly from `Simulation.StepOnce`. Every civ runs the same code. The old coin-flip war (`War.cs` `Battle`, `GalaxyCheck` declarations) stays untouched in this stage; stage 2 replaces it using what this stage builds. Tests are a headless self-test runner (`--selftest`) in the style of the existing `--savetest`.

**Tech Stack:** Godot 4.7.2 .NET, C# (.NET 8), System.Text.Json. Godot binary path in `.godotpath`.

**Spec:** `docs/superpowers/specs/2026-10-18-war-and-strife-design.md` (sections 3, 4.1–4.5, 4.8, placeholder assets).

## Global Constraints

- Same rules for everyone: humanity, splinters and aliens run through the same code.
- Per-colony work runs at most yearly; a 940-year headless `--warp=940 --trace` run must stay under ~40 s (25 s today).
- Saves round-trip byte-identically (`--warp=300 --savetest` prints `SAVETEST equal=True`); saves from before this stage still load.
- Tunable numbers live in `data/war.json` (owner chose JSON rules for modding).
- New `Area` values are appended to the end of the enum (saved `Policy.Manual` arrays are indexed by it).
- Placeholder assets are colour-coded cubes: factories orange, shipyards blue, barracks green, army transports olive, special forces purple, missing hull classes grey; tinted by the owner's empire colour on one face.
- Interface copy follows the existing house style: plain sentences, British spelling (mobilisation, colour), no jargon.
- Build: `dotnet build -v q` from `C:\Users\diamo\Desktop\starforge` prints `Build succeeded.`
- Self-test command (Git Bash, from the repo root): `"$(cat .godotpath)" --headless --path . -- --selftest` ; passing output ends `SELFTEST DONE pass=<n> fail=0` and the process exits 0.

## Review Focus

1. **A save made before this stage** loads with no war fields: every civ gets culture from its species' lore, mobilisation `peace`, empty materiel, and fleets are raised on the first yearly step. (Test: `old_save_defaults`, Task 8.)
2. **Dead civs and civs with no colonies** pass through the yearly war economy without exceptions or fleets. (Test: `dead_civ_skipped`, Task 6.)
3. **A splinter** (schism, AI breakaway) inherits its parent's culture and laws, starts its own navy, and does not take the parent's fleets. (Test: `splinter_inherits_culture`, Task 3.)
4. **Late-game industry of ~1e25 kg/yr** must not turn into millions of ships: hull output is bounded by slips and build time, and nothing overflows to Infinity/NaN. (Test: `slips_bound_output`, Task 5.)
5. **The player choosing a level above the culture's comfort** is allowed (manual Production), and costs more strain instead of being refused. (Test: `manual_override_strains`, Task 4.)

---

## File Structure

| File | Responsibility |
|---|---|
| `data/war.json` (create) | Every tunable: culture lore and drift, mobilisation levels, economy constants, hull build data, reasons, terms, stakes, placeholder colours |
| `src/Sim/WarData.cs` (create) | Loads `war.json` once; typed accessors |
| `src/Sim/Culture.cs` (create) | `Culture` (three axes), starting lore, drift, events, descriptions |
| `src/Sim/WarModel.cs` (create) | War reasons, stakes tiers, terms with score prices, `Concede` and `ChooseTerms` (pure functions; used by stage 2) |
| `src/Sim/Forces.cs` (create) | `Admiral`, `Squadron`, `Fleet`, `Army` units; navy organisation (raising fleets, admirals, assigning new hulls) |
| `src/Sim/WarEconomy.cs` (create) | Mobilisation and retooling, materiel, shipyard slips and hull production, upkeep, barracks and manpower, strain, council AI for Production |
| `src/Tests/SelfTest.cs` (create) | Headless test runner (`--selftest[=filter]`) |
| `src/Tests/WarTests.cs` (create) | The tests below |
| `src/View/WorksWar.cs` (create) | `partial class Works`: placeholder cubes for factories, slips, barracks, parked squadrons and armies |
| `src/Sim/Civ.cs`, `Colony.cs`, `Construction.cs`, `Simulation.cs`, `Policy.cs`, `SaveGame.cs` (modify) | Fields, hooks, saves |
| `src/Main.cs`, `src/View/Works.cs`, `src/View/EmpireScreen.cs` (modify) | Test flag, cube layer, controls and readouts |

---

### Task 1: Self-test runner and war data

**Files:**
- Create: `src/Tests/SelfTest.cs`, `src/Tests/WarTests.cs`, `data/war.json`, `src/Sim/WarData.cs`
- Modify: `src/Main.cs` (top of `_Ready`, before the save/galaxy setup)

**Interfaces:**
- Produces:
  - `public static class SelfTest { public static int Run(string filter); public static void Check(bool ok, string what); public static Simulation NewSim(int seed = 20610); }` — `Run` prints `SELFTEST PASS <name>` / `SELFTEST FAIL <name>: <what>` per test, then `SELFTEST DONE pass=<n> fail=<m>`, returns m. `Check` throws on failure (caught per test). `NewSim` builds a galaxy exactly as `Main._Ready` does (`new Galaxy(seed)`, `PlaceSolAndNeighbours()`, `GenerateStars(2600)`, `new Simulation(gal, seed)`).
  - `public static class WarTests { public static readonly List<(string Name, Action Body)> All; }`
  - `public static class WarData { public const string Path = "res://data/war.json"; public static JsonElement Root; public static JsonElement P(params string[] path); public static double D(params string[] path); public static string S(params string[] path); public static Color Colour(string kind); }` — loads lazily on first access, same pattern as `TechData.P`.
  - Main: `--selftest` or `--selftest=<filter>` (substring of test name) runs `SelfTest.Run` and quits with exit code `fail > 0 ? 1 : 0`, before anything else is set up.

- [ ] **Step 1: Write the failing test** in `WarTests.All`:

```csharp
("wardata_loads", () => {
    SelfTest.Check(WarData.P("mobilisation").GetArrayLength() == 4, "four mobilisation levels");
    SelfTest.Check(WarData.S("mobilisation", "3", "id") == "total", "last level is total war");   // P accepts array indices as strings
    SelfTest.Check(WarData.P("reasons").GetArrayLength() >= 10, "at least ten reasons");
    SelfTest.Check(WarData.Colour("factory").R > 0.8f && WarData.Colour("factory").B < 0.4f, "factories are orange");
}),
```

- [ ] **Step 2: Run** `"$(cat .godotpath)" --headless --path . -- --selftest=wardata` — expected: build or run fails (no `WarData`, no flag).

- [ ] **Step 3: Create `data/war.json`** with exactly these values (the builder's calls from the spec, all tunable):

```json
{
  "culture": {
    "start": {
      "Human":    { "Martial": 0.0,  "Duty": -0.3, "Resolve": 0.1 },
      "Vessh":    { "Martial": 0.7,  "Duty": 0.8,  "Resolve": 0.5 },
      "Orun":     { "Martial": -0.4, "Duty": 0.2,  "Resolve": 0.8 },
      "Tallow":   { "Martial": -0.7, "Duty": -0.5, "Resolve": -0.2 },
      "Hask":     { "Martial": 0.3,  "Duty": 0.6,  "Resolve": 0.9 },
      "Ninefold": { "Martial": 0.1,  "Duty": -0.2, "Resolve": -0.4 },
      "Lattice":  { "Martial": -0.2, "Duty": 0.9,  "Resolve": 0.6 },
      "default":  { "Martial": 0.0,  "Duty": 0.0,  "Resolve": 0.0 }
    },
    "drift": {
      "warYear":   { "Martial": 0.006, "Resolve": 0.004 },
      "peaceYear": { "Martial": -0.003 },
      "victory":   { "Martial": 0.06 },
      "defeat":    { "Martial": -0.05, "Resolve": 0.04 },
      "exhausted": { "Martial": -0.08, "Resolve": -0.05 }
    },
    "names": {
      "Martial": ["a pacifist people", "a peaceable people", "", "a martial people", "a warrior people"],
      "Duty":    ["fiercely free", "volunteers by habit", "", "dutiful", "every citizen a soldier"],
      "Resolve": ["brittle in defeat", "quick to tire", "", "stubborn", "unbreakable"]
    }
  },
  "mobilisation": [
    { "id": "peace",   "name": "Peacetime",            "military": 0.04, "manpower": 0.002, "growth": 1.0,  "research": 1.0,  "strain": 0.0 },
    { "id": "partial", "name": "Partial mobilisation", "military": 0.18, "manpower": 0.010, "growth": 0.95, "research": 0.95, "strain": 1.0 },
    { "id": "war",     "name": "War economy",          "military": 0.45, "manpower": 0.030, "growth": 0.85, "research": 0.85, "strain": 2.5 },
    { "id": "total",   "name": "Total war",            "military": 0.75, "manpower": 0.080, "growth": 0.60, "research": 0.70, "strain": 5.0 }
  ],
  "economy": {
    "retoolYears": 3.0,
    "efficiencyStart": 0.3, "efficiencyPerYear": 0.15,
    "upkeepPerYear": 0.04,
    "mandatoryManpower": 1.5, "mandatoryStrain": 0.6,
    "slipsHome": 1, "slipsPerYard": 2, "slipsMegaYard": 4,
    "frigateKg": 3.0e7, "frigateLengthKm": 0.3,
    "buildYearsPerKm": 2.5, "buildYearsMin": 0.5, "buildYearsMax": 40
  },
  "armies": {
    "troopsPerArmy": 0.001,
    "trainYears": 2.0, "rushedYears": 0.75,
    "trainedQuality": 1.0, "rushedQuality": 0.6,
    "kgPerArmy": 2.0e7, "upkeepPerYear": 0.03,
    "garrisonPerBillion": 0.5
  },
  "experience": { "levels": ["Green", "Regular", "Veteran", "Elite"], "at": [0.0, 0.25, 0.5, 0.8] },
  "stakes": {
    "limited":   { "concede": 1.0, "ceasefire": true },
    "principle": { "concede": 0.5, "ceasefire": true },
    "survival":  { "concede": 0.0, "ceasefire": false }
  },
  "terms": [
    { "id": "status_quo",   "name": "Status quo",                "price": 0,  "core": false },
    { "id": "ceasefire",    "name": "Ceasefire on the front",    "price": 0,  "core": false },
    { "id": "reparations",  "name": "Reparations",               "price": 15, "core": false },
    { "id": "cede_claims",  "name": "Cede the claimed systems",  "price": 30, "core": true },
    { "id": "cede_occupied","name": "Cede what is occupied",     "price": 45, "core": false },
    { "id": "tribute",      "name": "Tribute for fifty years",   "price": 20, "core": true },
    { "id": "humiliation",  "name": "Public humiliation",        "price": 10, "core": true },
    { "id": "autonomy",     "name": "Autonomy within the union", "price": 25, "core": false },
    { "id": "independence", "name": "Recognised independence",   "price": 40, "core": true },
    { "id": "reunion",      "name": "Return to the union",       "price": 50, "core": true },
    { "id": "partition",    "name": "Partition",                 "price": 30, "core": false },
    { "id": "vassal",       "name": "Become a subject",          "price": 60, "core": true },
    { "id": "regime",       "name": "Change of government",      "price": 45, "core": true },
    { "id": "release",      "name": "Release a subject",         "price": 35, "core": true }
  ],
  "reasons": [
    { "id": "border_claim", "tier": "limited",   "name": "Border claim",          "terms": ["cede_claims", "cede_occupied", "reparations", "ceasefire", "status_quo"] },
    { "id": "key_system",   "tier": "limited",   "name": "A key system",          "terms": ["cede_claims", "cede_occupied", "reparations", "ceasefire", "status_quo"] },
    { "id": "revenge",      "tier": "limited",   "name": "Revenge",               "terms": ["humiliation", "reparations", "cede_occupied", "ceasefire", "status_quo"] },
    { "id": "tribute",      "tier": "limited",   "name": "Tribute",               "terms": ["tribute", "reparations", "ceasefire", "status_quo"] },
    { "id": "independence", "tier": "principle", "name": "Independence",          "terms": ["independence", "autonomy", "partition", "ceasefire", "reunion"] },
    { "id": "ideology",     "tier": "principle", "name": "Ideology",              "terms": ["regime", "reparations", "ceasefire", "status_quo"] },
    { "id": "overlord",     "tier": "principle", "name": "Break the overlord",    "terms": ["release", "autonomy", "ceasefire", "status_quo"] },
    { "id": "subjugation",  "tier": "principle", "name": "Subjugation",           "terms": ["vassal", "tribute", "cede_occupied", "ceasefire", "status_quo"] },
    { "id": "liberation",   "tier": "principle", "name": "Liberation of a people","terms": ["cede_occupied", "independence", "ceasefire", "status_quo"] },
    { "id": "extermination","tier": "survival",  "name": "Extermination",         "terms": [] },
    { "id": "holy_war",     "tier": "survival",  "name": "Holy war",              "terms": [] },
    { "id": "purge",        "tier": "survival",  "name": "Purge of a species",    "terms": [] },
    { "id": "existential",  "tier": "survival",  "name": "An existential threat", "terms": [] }
  ],
  "placeholders": {
    "factory": "#ff8c2e", "shipyard": "#3a7dff", "barracks": "#3fbf5a", "transport": "#7d8a36", "specops": "#9a4fe0", "hull": "#8c8c8c"
  }
}
```

- [ ] **Step 4: Implement `SelfTest`, `WarTests` (the list holding the test above), `WarData`, and the `--selftest` flag in `Main._Ready`.** `WarData.P` walks objects by name and arrays by integer index (`"3"`); `Colour` parses the hex from `placeholders`.

- [ ] **Step 5: Run** `dotnet build -v q` then `"$(cat .godotpath)" --headless --path . -- --selftest=wardata` — expected `SELFTEST PASS wardata_loads` and `SELFTEST DONE pass=1 fail=0`.

- [ ] **Step 6: Commit**

```bash
git add data/war.json src/Sim/WarData.cs src/Tests src/Main.cs
git commit -m "War foundation: war.json data and headless self-test runner"
```

---

### Task 2: War culture

**Files:**
- Create: `src/Sim/Culture.cs`
- Modify: `src/Sim/Civ.cs` (field), `src/Sim/Simulation.cs` (`Splinter`, `SpawnAliens`, humanity setup, yearly block in `StepOnce`, `NewCiv` in `Powers.cs`)
- Test: `src/Tests/WarTests.cs`

**Interfaces:**
- Produces:
  - `public class Culture { public float Martial, Duty, Resolve; public Culture Copy(); public void Add(string axis, float d); }` — each axis clamped to [-1, 1].
  - `public static class Cultures { public static Culture Lore(string species); public static void Event(Civ c, string kind); public static void Drift(Civ c, double years, bool atWar); public static string Describe(Culture k); }` — `kind` is a key of `culture.drift` (`victory`, `defeat`, `exhausted`); `Lore` falls back to `default`; `Describe` joins the non-empty names from `culture.names` for each axis (index = `(int)Math.Round((v + 1) * 2)`, 0..4), e.g. "a martial people, dutiful".
  - `Civ.Culture` (non-null after construction; humans from `Human`, aliens from their `Species`), `Civ.MandatoryService` (bool, starts `Duty > 0.5`).
  - Yearly: `Cultures.Drift(civ, yd, civ.AtWar.Count > 0)` in the per-civ yearly block of `StepOnce`.

- [ ] **Step 1: Write the failing tests**

```csharp
("culture_start", () => {
    var sim = SelfTest.NewSim();
    SelfTest.Check(Math.Abs(sim.Humanity.Culture.Duty - (-0.3f)) < 1e-4, "humanity starts from Human lore");
    var vessh = sim.Civs.First(c => c.Species == "Vessh");
    SelfTest.Check(vessh.Culture.Martial > 0.6f && vessh.MandatoryService, "the Vessh are a conscript warrior people");
}),
("culture_drift", () => {
    var sim = SelfTest.NewSim(); var h = sim.Humanity; float m0 = h.Culture.Martial;
    Cultures.Drift(h, 10, atWar: true);
    SelfTest.Check(h.Culture.Martial > m0, "war hardens");
    Cultures.Event(h, "victory"); Cultures.Event(h, "victory");
    for (int i = 0; i < 100; i++) Cultures.Event(h, "victory");
    SelfTest.Check(h.Culture.Martial <= 1f, "clamped");
    SelfTest.Check(Cultures.Describe(h.Culture).Contains("warrior"), "described");
}),
("splinter_inherits_culture", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400);                                  // Warp is a new public test helper: Step in 0.5-year chunks
    var h = sim.Humanity; h.Culture.Martial = 0.42f;
    var cols = h.Colonies.Where(c => !c.Home).Take(2).ToList();
    if (cols.Count < 2) return;                                                  // seed has no colonies yet: nothing to check
    var s = sim.SplinterForTest(h, h.Drive, cols);                               // public wrapper over the private Splinter
    SelfTest.Check(Math.Abs(s.Culture.Martial - 0.42f) < 1e-4 && s.Culture != h.Culture, "copied, not shared");
    SelfTest.Check(sim.FleetsOf(h).All(f => f.Civ == h.Id), "parent keeps its fleets");
}),
```

`splinter_inherits_culture` also needs `FleetsOf` from Task 3; add it to `WarTests.All` now and it will pass once Task 3 lands (mark the step done in Task 3).

- [ ] **Step 2: Run** `--selftest=culture` — expected FAIL (no `Culture`).

- [ ] **Step 3: Implement** `Culture.cs`; set `Civ.Culture`/`MandatoryService` where civs are created (humanity in the `Simulation` constructor, aliens in `SpawnAliens`, `NewCiv`, and `Splinter` = `parent.Culture.Copy()` and the parent's `MandatoryService`); add `public void Warp(double years)` and `public Civ SplinterForTest(...)` to `Simulation`; call `Drift` yearly.

- [ ] **Step 4: Run** `--selftest=culture` — expected `culture_start` and `culture_drift` PASS.

- [ ] **Step 5: Commit** `git commit -m "War foundation: war culture from species lore, drift and events"`

---

### Task 3: Forces: fleets, squadrons, admirals, armies

**Files:**
- Create: `src/Sim/Forces.cs`
- Modify: `src/Sim/Simulation.cs` (call `NavyStep` in the yearly block; remove a dead civ's forces in `Extinct`)
- Test: `src/Tests/WarTests.cs`

**Interfaces:**
- Consumes: `WarData` (Task 1), `Civ.Culture` (Task 2).
- Produces:
  - `public class Admiral { public string Name, Trait; public float Skill, Loyalty; public double Born; public int Wins, Losses; }`
  - `public class Squadron { public int Hull; public double Ships; public float Xp; public string Level => Forces.Level(Xp); }`
  - `public class Fleet { public int Id, Civ, At; public string Name, Mission = "defend", Stance = "balanced"; public List<Squadron> Squadrons = new(); public Admiral Admiral; public float Morale = 1, Supply = 1; public double Ships => Squadrons.Sum(s => s.Ships); public void Add(int hull, double ships, float xp); }` — `Add` merges into the squadron of the same hull, averaging `Xp` by ship count.
  - `public class Army { public int Id, Civ, At; public double Troops; public float Quality, Xp; public bool Rushed; }`
  - `public static class Forces { public static string Level(float xp); public static readonly string[] Traits; }` — `Level` uses `experience.at`; traits: Aggressive, Cautious, Tactician, Logistician, Raider, Beloved, Ambitious, Butcher (Butcher never for Reverent).
  - Hull data (moved here from `View/Ships.cs` so the simulation owns it; `Ships` keeps calling it): `public static float LengthKm(int cls)` (the existing `LengthsKm` table), `public static double HullKg(int cls) => frigateKg * Math.Pow(LengthKm(cls) / frigateLengthKm, 3);`, `public static double BuildYears(int cls) => Math.Clamp(buildYearsPerKm * LengthKm(cls), buildYearsMin, buildYearsMax);` — all on `Simulation`, values from `economy` in `war.json`.
  - `public double NavyStrength(Civ c) => FleetsOf(c).Sum(f => f.Squadrons.Sum(s => s.Ships * HullKg(s.Hull)))` (the council's yardstick until stage 2 brings real battle strength).
  - On `Simulation`: `public readonly List<Fleet> Fleets; public readonly List<Army> Armies; public int NextFleetId, NextArmyId; public IEnumerable<Fleet> FleetsOf(Civ c); public IEnumerable<Army> ArmiesOf(Civ c); void NavyStep(Civ civ, double dy)`.
  - `NavyStep` keeps `Math.Clamp(1 + (int)Math.Log2(1 + civ.Colonies.Count), 1, 6)` fleets per civ, all at the home star; fleet 1 is "Home Fleet" (mission `defend`), others "First Fleet", "Second Fleet"… (missions alternate `attack`, `attack`, `raid`); each has an admiral from `civ.NewLeader(...).Name`, `Skill` in [0.85, 1.2], `Loyalty` 0.45 if Ambitious else [0.65, 0.95]; admirals retire past 85 years (aliens 300, digital >0.9: 400) and are replaced.

- [ ] **Step 1: Write the failing tests**

```csharp
("fleets_raised", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2);
    var fl = sim.FleetsOf(sim.Humanity).ToList();
    SelfTest.Check(fl.Count == 1 && fl[0].Name == "Home Fleet" && fl[0].Admiral != null, "one home fleet with an admiral");
    var vessh = sim.Civs.First(c => c.Species == "Vessh");
    SelfTest.Check(sim.FleetsOf(vessh).Count() == Math.Clamp(1 + (int)Math.Log2(1 + vessh.Colonies.Count), 1, 6), "fleets scale with size");
}),
("squadrons_merge", () => {
    var f = new Fleet(); f.Add(2, 10, 0f); f.Add(2, 10, 0.6f); f.Add(4, 1, 0f);
    SelfTest.Check(f.Squadrons.Count == 2 && Math.Abs(f.Squadrons[0].Xp - 0.3f) < 1e-4 && f.Ships == 21, "same hull merges, xp averages");
    SelfTest.Check(Forces.Level(0.9f) == "Elite" && Forces.Level(0.1f) == "Green", "levels");
}),
```

Also re-run `splinter_inherits_culture` (Task 2).

- [ ] **Step 2: Run** `--selftest=fleets` and `--selftest=squadrons` — expected FAIL.
- [ ] **Step 3: Implement** `Forces.cs` and the hooks.
- [ ] **Step 4: Run** `--selftest` — expected all tests so far PASS, including `splinter_inherits_culture`.
- [ ] **Step 5: Commit** `git commit -m "War foundation: fleets, squadrons, admirals and armies"`

---

### Task 4: Mobilisation, retooling and strain

**Files:**
- Create: `src/Sim/WarEconomy.cs`
- Modify: `src/Sim/Civ.cs` (fields), `src/Sim/Policy.cs` (append `Production` to `Area`; `Describe`; field `Mobilisation`), `src/Sim/Construction.cs` (`LocalWorks`), `src/Sim/Colony.cs` (swarm `build`, growth, `RpRate`), `src/Sim/Simulation.cs` (yearly hook)
- Test: `src/Tests/WarTests.cs`

**Interfaces:**
- Consumes: `WarData`, `Civ.Culture`, `Civ.MandatoryService`.
- Produces:
  - `Civ` fields: `public int Mobilisation;` (target level 0..3), `public double MilShare = 0.04, Efficiency = 1, Materiel, Manpower, Strain;`
  - `Policy`: `Area.Production` appended (after `Diplomacy`, or after `Interior` if it exists); `public int Mobilisation;` (player's chosen level when Production is manual); `Describe(Area.Production)` = "How much of the economy goes to war. Manual: you set the mobilisation level and mandatory service."
  - `WarEconomy` (partial `Simulation`): `public static JsonElement Level(int i)`; `public int CouncilMobilisation(Civ c)`; `void MobiliseStep(Civ c, double dy)`; `public double WarGrowth(Civ c)`, `public double WarResearch(Civ c)` (interpolate `growth`/`research` between levels by `MilShare`, then soften by culture: `1 - (1 - x) * (1 - 0.5 * Martial)`).
  - Rules: the level in force is `Policy.Mobilisation` if Production is manual, else `CouncilMobilisation`. `MilShare` moves linearly from where it stood when the level last changed to the level's `military`, arriving `retoolYears` later (store `Civ.MilFrom` and `Civ.MilSince`). When `MilShare` rises by `d`, `Efficiency = (Efficiency * old + efficiencyStart * d) / new`; otherwise `Efficiency += efficiencyPerYear * dy` up to 1. `Strain = level.strain * (1 - 0.5 * Martial) + (MandatoryService && Duty < 0 ? mandatoryStrain : 0)`, plus `+1` per level above the culture's comfort (`Martial > 0.3 → 3`, `> -0.3 → 2`, else `1`).
  - The council also sets `MandatoryService`: on at war-economy or above when `Duty > 0`, off at peace when `Duty < 0.5`; manual Production leaves it to the player.
  - `CouncilMobilisation`: `peace` at peace with no neighbour below −40 relations; `partial` if one is; at war: `war` if the enemy's `NavyStrength` is ≥ 0.6× ours, else `partial`; `total` only when a home-system or capital-adjacent colony is occupied or `Emergency` is set; never above comfort+1 for the council.
  - Effects: `LocalWorks = IndustryKg * (1 - Civ.MilShare) * WorksShare`; swarm `build *= 1 - Civ.MilShare`; growth uses `M.Growth * Sim.WarGrowth(Civ)`; `RpRate *= Sim.WarResearch(Civ)`.

- [ ] **Step 1: Write the failing tests**

```csharp
("mobilisation_retools", () => {
    var sim = SelfTest.NewSim(); var h = sim.Humanity;
    sim.SetManual(h, Area.Production, true); h.Policy.Mobilisation = 2;          // war economy, 0.45
    sim.Warp(1.5);
    SelfTest.Check(h.MilShare > 0.15 && h.MilShare < 0.35, $"halfway after 1.5 of 3 years ({h.MilShare:0.00})");
    SelfTest.Check(h.Efficiency < 0.8, "new lines start inefficient");
    sim.Warp(6);
    SelfTest.Check(Math.Abs(h.MilShare - 0.45) < 0.02 && h.Efficiency > 0.9, "settled and efficient");
    SelfTest.Check(sim.WarGrowth(h) < 1 && sim.WarResearch(h) < 1, "the war costs growth and research");
}),
("manual_override_strains", () => {
    var sim = SelfTest.NewSim(); var h = sim.Humanity; h.Culture.Martial = -0.8f;   // pacifist: comfort level 1
    sim.SetManual(h, Area.Production, true); h.Policy.Mobilisation = 3; sim.Warp(1);
    SelfTest.Check(h.Strain > 5.0 * (1 + 0.4) , "total war on a pacifist people costs more than the base strain");
    SelfTest.Check(h.Mobilisation == 3, "the player's choice is not refused");
}),
("council_mobilises_at_peace", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2);
    SelfTest.Check(sim.CouncilMobilisation(sim.Humanity) == 0 && sim.Humanity.MilShare < 0.05, "peacetime by default");
}),
```

- [ ] **Step 2: Run** `--selftest=mobil` and `--selftest=manual` — expected FAIL.
- [ ] **Step 3: Implement** the fields, `Area.Production`, `MobiliseStep` (called yearly per civ), and the four effect hooks.
- [ ] **Step 4: Run** `--selftest` — expected all PASS.
- [ ] **Step 5: Commit** `git commit -m "War foundation: mobilisation levels, retooling, efficiency and strain"`

---

### Task 5: Materiel, shipyards and the navy's growth

**Files:**
- Modify: `src/Sim/WarEconomy.cs`, `src/Sim/Forces.cs` (assign new hulls)
- Test: `src/Tests/WarTests.cs`

**Interfaces:**
- Consumes: `MilShare`, `Efficiency` (Task 4); `Fleet.Add`, `HullKg`, `BuildYears` (Task 3).
- Produces:
  - `public int Slips(Civ c)` = Σ over colonies: `slipsHome` for the home colony, `slipsPerYard` per colony with `Done("yard")`, `slipsMegaYard` extra when the civ's hull class is ≥ Battleship and the colony has a finished yard.
  - `public class Slip { public int Hull; public double Progress, Paid; }`; `Civ.Yard` : `List<Slip>` (length ≤ `Slips(c)`).
  - `void YardStep(Civ c, double dy)`: `Materiel += Σ colonies IndustryKg * MilShare * Efficiency * dy` (only colonies not occupied; occupation arrives in stage 2, read `Colony.OccupiedBy` if present, else all); pay upkeep `upkeepPerYear * HullKg(hull) * ships` per squadron (if `Materiel` would go negative, remove `deficit / HullKg` ships from the largest squadron instead: mothballing); fill empty slips with the council's hull choice (best hull `c.Hull` 60%, `c.Hull - 2` 40%, never below 0; manual Production uses `Policy.HullMix` if set, an int hull class or −1); advance each slip by `dy / BuildYears(hull)` while paying `HullKg(hull) * dProgress` from `Materiel` (a slip waits when materiel is short); a finished slip adds one ship to the fleet that is furthest below its share of the navy (`Fleet.Add(hull, 1, 0f)`), Home Fleet first on ties.
  - Called yearly per civ after `MobiliseStep`.

- [ ] **Step 1: Write the failing tests**

```csharp
("yards_build_ships", () => {
    var sim = SelfTest.NewSim(); sim.Warp(30);
    var h = sim.Humanity;
    SelfTest.Check(sim.FleetsOf(h).Sum(f => f.Ships) >= 1, "peacetime yards launch something in thirty years");
    SelfTest.Check(h.Yard.Count <= sim.Slips(h), "never more work than slips");
}),
("slips_bound_output", () => {
    var sim = SelfTest.NewSim(); var h = sim.Humanity;
    h.Materiel = 1e30;                                                           // a late-game stockpile
    double before = sim.FleetsOf(h).Sum(f => f.Ships); sim.Warp(10);
    double built = sim.FleetsOf(h).Sum(f => f.Ships) - before;
    SelfTest.Check(built <= sim.Slips(h) * 10 / Simulation.BuildYears(0) + 1, "slips and build time bound the output");
    SelfTest.Check(!double.IsNaN(h.Materiel) && !double.IsInfinity(h.Materiel), "no overflow");
}),
("upkeep_mothballs", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity;
    var f = sim.FleetsOf(h).First(); f.Add(Defs.Hulls.Length - 1, 500, 0f); h.Materiel = 0;   // 500 worldships: far beyond 2060s industry
    sim.Warp(3);
    SelfTest.Check(f.Ships < 500, "a navy the economy cannot pay for is laid up");
}),
```

- [ ] **Step 2: Run** `--selftest=yards`, `slips`, `upkeep` — expected FAIL.
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — expected all PASS.
- [ ] **Step 5: Commit** `git commit -m "War foundation: materiel, shipyard slips, hull production and upkeep"`

---

### Task 6: Barracks, manpower and armies

**Files:**
- Modify: `src/Sim/WarEconomy.cs`
- Test: `src/Tests/WarTests.cs`

**Interfaces:**
- Consumes: `Army` (Task 3), `MilShare`, `Materiel`, `Mobilisation` level (Task 4).
- Produces:
  - `Civ.Manpower` (billions available to recruit) = `Pop * level.manpower * (MandatoryService ? mandatoryManpower : 1) * (1 + 0.3 * Duty)`, recomputed yearly minus troops under arms.
  - `public class Training { public double Troops, Left; public bool Rushed; public int At; }`; `Civ.Barracks` : `List<Training>`.
  - `void BarracksStep(Civ c, double dy)`: the council wants armies ≈ `garrisonPerBillion * Σ Pop` (in units of `troopsPerArmy`) at peace, ×2 at partial, ×4 at war economy and total war; while below that and `Manpower ≥ troopsPerArmy` and `Materiel ≥ kgPerArmy`, start a `Training` at the most populous colony; training takes `trainYears`, or `rushedYears` when the civ is at war (`Rushed = true`); finished training becomes an `Army` with `Quality = trainedQuality` or `rushedQuality` and `Xp = 0`; army upkeep `upkeepPerYear * kgPerArmy` per army from `Materiel` (disband the newest when short).
  - The same quality rule will be reused by special forces (stage 4): peacetime training is better.

- [ ] **Step 1: Write the failing tests**

```csharp
("armies_trained", () => {
    var sim = SelfTest.NewSim(); sim.Warp(20); var h = sim.Humanity;
    SelfTest.Check(sim.ArmiesOf(h).Any(), "a peacetime garrison is raised");
    SelfTest.Check(sim.ArmiesOf(h).All(a => !a.Rushed && Math.Abs(a.Quality - 1f) < 1e-4), "trained in peace, full quality");
}),
("rushed_in_war", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity;
    var foe = sim.Civs.First(c => c.Alien); h.AtWar.Add(foe.Id); foe.AtWar.Add(h.Id);
    h.Materiel = 1e20; sim.Warp(3);
    SelfTest.Check(sim.ArmiesOf(h).Any(a => a.Rushed && a.Quality < 1f), "wartime recruits are rushed and worse");
}),
("dead_civ_skipped", () => {
    var sim = SelfTest.NewSim(); var c = sim.Civs.First(x => x.Alien);
    foreach (var col in c.Colonies.ToList()) sim.DestroyColony(col, "test");
    sim.Warp(3);
    SelfTest.Check(c.Dead && !sim.FleetsOf(c).Any() && !sim.ArmiesOf(c).Any(), "a dead civ keeps no forces and throws nothing");
}),
```

- [ ] **Step 2: Run** `--selftest=armies`, `rushed`, `dead` — expected FAIL.
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — expected all PASS.
- [ ] **Step 5: Commit** `git commit -m "War foundation: manpower, barracks and armies (rushed recruits are worse)"`

---

### Task 7: War reasons and endings model

**Files:**
- Create: `src/Sim/WarModel.cs`
- Test: `src/Tests/WarTests.cs`

**Interfaces:**
- Consumes: `WarData` (`reasons`, `terms`, `stakes`), `Civ.Culture`.
- Produces (pure; stage 2 wires it into wars):
  - `public record Reason(string Id, string Tier, string Name, string[] Terms);` `public record Term(string Id, string Name, double Price, bool Core);`
  - `public static class WarModel { public static Reason ReasonById(string id); public static Term TermById(string id); public static IEnumerable<Reason> Reasons; public static double Concede(Civ loser, string reasonId, double loserScore, double exhaustion); public static List<Term> ChooseTerms(string reasonId, double concede, bool bothExhausted); public static string Ending(List<Term> terms, string reasonId); }`
  - `Concede` (score points the loser will give up) = `stakes[tier].concede * (Math.Max(0, -loserScore) + 0.6 * exhaustion) * (1 - 0.4 * Resolve)`; for `survival` it is 0 until `exhaustion ≥ 100`, then `double.PositiveInfinity` (collapse).
  - `ChooseTerms` walks the reason's priced terms (price > 0) in list order, taking each whose price fits the remaining budget; zero-price terms (`ceasefire`, `status_quo`) are only fallbacks. If nothing priced fits and `bothExhausted` and the tier allows a ceasefire, returns `[ceasefire]`; never returns a term not in the reason's list; for survival returns `[]` unless `concede` is infinite, then a single built-in `Term("collapse", "Collapse", 0, true)`.
  - `Ending`: `"surrender"` if every core term of the reason was taken, `"compromise"` if some priced term was, `"ceasefire"`, `"status quo"`, `"collapse"` (the list is the collapse term), or `"none"` (empty list: fight on).

- [ ] **Step 1: Write the failing tests**

```csharp
("ending_limited_compromise", () => {
    var sim = SelfTest.NewSim(); var h = sim.Humanity; h.Culture.Resolve = 0;
    double give = WarModel.Concede(h, "border_claim", loserScore: -40, exhaustion: 50);
    SelfTest.Check(Math.Abs(give - 70) < 1e-6, "limited stakes concede score + 0.6 × exhaustion");
    var t = WarModel.ChooseTerms("border_claim", give, bothExhausted: false);
    SelfTest.Check(t.Any(x => x.Id == "cede_claims") && WarModel.Ending(t, "border_claim") == "surrender", "the claims change hands");
}),
("ending_survival_no_terms", () => {
    var sim = SelfTest.NewSim(); var h = sim.Humanity;
    SelfTest.Check(WarModel.Concede(h, "extermination", -90, 95) == 0, "no terms before collapse");
    SelfTest.Check(double.IsPositiveInfinity(WarModel.Concede(h, "extermination", -90, 100)), "collapse at full exhaustion");
    SelfTest.Check(WarModel.Ending(WarModel.ChooseTerms("extermination", 0, true), "extermination") == "none", "fight on");
}),
("ending_ceasefire", () => {
    var t = WarModel.ChooseTerms("independence", 5, bothExhausted: true);
    SelfTest.Check(t.Count == 1 && t[0].Id == "ceasefire", "two exhausted sides freeze the front");
    SelfTest.Check(WarModel.ChooseTerms("ideology", 999, false).All(x => WarModel.ReasonById("ideology").Terms.Contains(x.Id)), "only the reason's terms");
}),
("resolve_holds_out", () => {
    var sim = SelfTest.NewSim(); var h = sim.Humanity;
    h.Culture.Resolve = 1; double hard = WarModel.Concede(h, "border_claim", -40, 50);
    h.Culture.Resolve = -1; double soft = WarModel.Concede(h, "border_claim", -40, 50);
    SelfTest.Check(hard < soft, "an unbreakable people concedes less");
}),
```

- [ ] **Step 2: Run** `--selftest=ending` and `resolve` — expected FAIL.
- [ ] **Step 3: Implement** `WarModel.cs`.
- [ ] **Step 4: Run** `--selftest` — expected all PASS.
- [ ] **Step 5: Commit** `git commit -m "War foundation: war reasons, stakes and the endings model"`

---

### Task 8: Saves

**Files:**
- Modify: `src/Sim/SaveGame.cs`
- Test: `src/Tests/WarTests.cs`, plus the existing `--savetest`

**Interfaces:**
- Consumes: every field added in Tasks 2–6.
- Produces: `CivDto` gains `Culture` (float[3]: Martial, Duty, Resolve), `MandatoryService`, `Mobilisation`, `MilShare`, `Efficiency`, `Materiel`, `Manpower`, `Strain`, `Yard` (List<Slip>), `Barracks` (List<Training>); `SaveDto` gains `Fleets`, `Armies`, `NextFleetId`, `NextArmyId`. Missing fields on load: culture from `Cultures.Lore(species)`, `MilShare` from level 0, `Efficiency = 1`, others zero/empty. `Policy.Manual` arrays shorter than the enum are resized on load.

- [ ] **Step 1: Write the failing test**

```csharp
("old_save_defaults", () => {
    var sim = SelfTest.NewSim(); sim.Warp(5);
    SaveGame.Save(sim, 20610, "user://selftest_old.json");
    // strip the war fields to imitate a save from before this stage
    var json = Godot.FileAccess.GetFileAsString("user://selftest_old.json");
    var stripped = System.Text.RegularExpressions.Regex.Replace(json, "\"(Culture|Mobilisation|MilShare|Efficiency|Materiel|Manpower|Strain|Yard|Barracks|Fleets|Armies|MandatoryService)\":(\\[[^\\]]*\\]|[^,\\]}]*),?", "");
    using (var f = Godot.FileAccess.Open("user://selftest_old.json", Godot.FileAccess.ModeFlags.Write)) f.StoreString(stripped);
    var (s2, _) = SaveGame.Load("user://selftest_old.json");
    SelfTest.Check(Math.Abs(s2.Humanity.Culture.Duty - (-0.3f)) < 1e-4, "culture from lore");
    s2.Warp(2);
    SelfTest.Check(s2.FleetsOf(s2.Humanity).Any(), "fleets raised after loading");
}),
```

(If the regex leaves invalid JSON for a nested list, strip with `System.Text.Json.Nodes.JsonNode` instead: parse, remove those property names from every object, serialise.)

- [ ] **Step 2: Run** `--selftest=old_save` — expected FAIL (fields not saved/defaulted).
- [ ] **Step 3: Implement** the DTO fields and defaults.
- [ ] **Step 4: Run** `--selftest` — expected all PASS; then `"$(cat .godotpath)" --headless --path . -- --warp=300 --savetest` — expected `SAVETEST equal=True`.
- [ ] **Step 5: Commit** `git commit -m "War foundation: save culture, economy, fleets and armies"`

---

### Task 9: Placeholder cubes and Empire-screen controls

**Files:**
- Create: `src/View/WorksWar.cs` (`public partial class Works`)
- Modify: `src/View/Works.cs` (build the layer in the constructor/setup alongside `BuildStations()`; call `UpdateWarProps(c, year)` from `Update` next to `UpdateStations`), `src/View/EmpireScreen.cs` (`BuildPolicies`, `RefreshOverview`)

**Interfaces:**
- Consumes: `WarData.Colour`, `Colony.Foundries`, `Sim.Slips`, `Civ.Yard`, `Civ.Barracks`, `Sim.FleetsOf`, `Sim.ArmiesOf`, `Cultures.Describe`, `WarEconomy.Level`.
- Produces:
  - One `MultiMeshInstance3D` of a unit `BoxMesh` with per-instance colour, under `_near`, placed with the same `Drawn(...)`/`PosOfD(...)` pattern as `UpdateStations`, in a shell at 2.2–2.8 seat radii: **factories** (orange) one per foundry up to 12, 3 km cubes; **shipyard slips** (blue) one per slip of this colony, 5 km, with a grey 1–2 km cube in each busy slip for the hull being built; **barracks** (green) one per `Training` at this colony, 2 km; **parked squadrons** (grey cubes sized by `Ships.Size(hull)` × 4 for visibility, one per squadron, up to 24) for fleets whose `At` is this star; **armies** (olive) one per army here, 1.5 km. One face of each cube is tinted with the owner's empire colour via `INSTANCE_CUSTOM` in a tiny spatial shader (`ALBEDO = mix(colour, empire, face_mask)`).
  - Empire screen, Policies tab, new section **PRODUCTION**: an option row "Mobilisation" with the four level names (disabled unless Production is manual; shows the council's current choice when automatic) and a toggle "Mandatory service".
  - Overview tab, two new lines: `War culture: <Cultures.Describe>` and `<level name> · <MilShare×100>% of industry to the war (efficiency <×100>%) · materiel <Hud.Sci(Materiel)> kg · navy <ships> hulls in <fleets> fleets · <armies> armies`.

- [ ] **Step 1: Shoot the current state**: `bash tools/shot.sh war_cubes_before --warp=150 --focus=Earth --dist=6` — expected: the image has no cubes.
- [ ] **Step 2: Implement** the cube layer and the two Empire-screen additions.
- [ ] **Step 3: Verify visually**: `bash tools/shot.sh war_cubes --warp=150 --focus=Earth --dist=6` and `bash tools/shot.sh war_policies --warp=150 --open=policies`; open both. Expected: orange, blue and green cubes around Earth, grey squadron cubes in a group, and the PRODUCTION section with "Mobilisation" and "Mandatory service" on the Policies tab; the Overview shows the war culture and mobilisation lines.
- [ ] **Step 4: Run** `--selftest` (still all PASS) and the 940-year timing run: `time "$(cat .godotpath)" --headless --path . -- --warp=940 --trace > /dev/null` — expected real time under 40 s.
- [ ] **Step 5: Commit** `git commit -m "War foundation: placeholder cubes and mobilisation controls"`

---

## Not in this stage (later stages, per the spec)

Fleet movement, battles, blockades, ground invasion, occupation, war score/exhaustion and peace (stage 2, which also retires `War.cs`'s coin-flip battle and wires `WarModel` into real wars); grievance from shortages and the infighting ladder (stage 3, consuming `Civ.Strain`); special forces (stage 4); the Wars/Military/Interior screens and battle graphics (stage 5); battle command (stage 6).
