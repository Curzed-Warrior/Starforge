# Special Forces (Stage 4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every civ can train a few expensive elite teams, best when trained in peacetime, and send them into systems it
does not control to sabotage a shipyard or a deposit, assassinate an admiral, incite a revolt, steal technology or
prepare a world for invasion; every mission risks capture and a diplomatic incident; counter-intelligence defends; the
new Policy area Intelligence lets the player run it by hand or leave it to the council.

**Architecture:** Teams are units like fleets and armies (`Team` in `src/Sim/Forces.cs`, list `Simulation.Teams`),
trained in a civ's academy (`Civ.Academy`, a `List<Training>` like `Barracks`) by a yearly `AcademyStep(civ, dy)` that
follows the armies' rule: training begun at war is rushed (shorter course, lower quality). A per-tick
`SpecOpsStep(dy)` (`src/Sim/SpecOps.cs`) moves teams, and resolves each mission once its time on site is up: a capture
roll against the target's counter-intelligence, then a success roll. Mission effects are shared with the existing
`CivActs` espionage acts (one code path for "steal tech" and "incite"), and the acts stop firing for a civ that has
teams, so espionage is not counted twice. The council's choice and the manual Decision live in
`src/Sim/Intelligence.cs`.

**Tech Stack:** Godot 4.7.2 .NET, C# (.NET 8), System.Text.Json. Godot binary path in `.godotpath`.

**Spec:** `docs/superpowers/specs/2026-10-18-war-and-strife-design.md` (sections 1, 3, 4.6, 4.8 Intelligence, 5
placeholder assets, 7). Builds on stages 1–3 (`2026-10-18-war-foundation.md`, `2026-10-19-war-in-the-field.md`,
`2026-10-19-infighting.md`; stage 3 merged on `master` at `6103408`).

## Global Constraints

- Same rules for everyone: humanity, splinters and aliens run through the same code. A civ without the `spy` act (tech `Intelligence Agency`, ESP tier 2) has no academy.
- Per-tick work is per team (few: at most `teamsMax` per civ); per-colony work at most yearly. `--warp=940 --trace` stays under ~40 s.
- Saves round-trip byte-identically (`--warp=300 --savetest` and `--warp=700 --savetest` print `SAVETEST equal=True`); saves from before this stage still load (no teams, empty academy).
- Tunable numbers live in `data/war.json` (new section `specops`; the existing `placeholders.specops` colour `#9a4fe0` is the purple cube).
- `Area.Intelligence` is appended after `Area.Interior`. Its `Describe`: "Where special forces go and what they do. Manual: each ready team waits for your orders." Decision key prefix `"intel:"` (closed by `SetManual` like the others).
- Reuse, do not duplicate: `Mods.Spy` (team skill and counter-intelligence), `Mods.Stealth` (harder to catch), the `Caught`/`Spy`-meter exposure of `CivActs`, `CompleteTech`, `Admiral`/`NewAdmiral`, `GarrisonStrength`, `SlipsOf`, `GrievanceTarget`'s causes.
- Copy: plain sentences, British spelling, no jargon. Chronicle: a capture is news; a success is one line for the player's civ and for its victims only; failed-but-uncaught missions are silent.
- Build: `dotnet build -v q` prints `Build succeeded.` Tests: `"$(cat .godotpath)" --headless --path . -- --selftest[=filter]` ends `SELFTEST DONE pass=<n> fail=0`, exit 0 (read Godot's exit code, not grep's).

## Review Focus

1. **The target changes under a team** (the colony is ceded, destroyed, splintered, or the team's own civ takes it, or the target civ dies, or peace makes a truce): the team aborts and comes home; nothing throws, no effect lands on the new owner. (Test: `target_changes_hands`, Task 2.)
2. **The team's own civ dies or is absorbed** (splinter reunion, extermination): its teams and academy are removed; a splinter's birth takes no teams (they stay loyal to the parent). (Test: `dead_civ_teams_go`, Task 1.)
3. **A war breaks out or ends mid-course**: a course already begun keeps the rule it began under (peacetime course stays full, rushed course stays rushed); only new courses switch. (Test: `course_keeps_its_rule`, Task 1.)
4. **Assassination with no admiral to kill** (no fleet parked at the target star, or the target civ has no fleets): the mission is not offered; if the fleet leaves while the team is on site, the team aborts. (Test: `no_admiral_no_mission`, Task 2.)
5. **Switching the player to a splinter** (stage 3's `SwitchPlayer`) with teams in the field and `intel:` decisions open: the old civ's teams stay with it on the council, its decisions are closed, the new civ starts with its own (empty) academy. (Test: `switch_player_teams`, Task 3.)

---

## File Structure

| File | Responsibility |
|---|---|
| `data/war.json` (modify) | New `specops` section (values below); `strife.causes.incited` |
| `src/Sim/Forces.cs` (modify) | `Team` class; `Simulation.Teams`, `TeamsOf`, `NextTeamId` |
| `src/Sim/SpecOps.cs` (create) | `AcademyStep` (training, upkeep, cap), `Dispatch`, `SpecOpsStep` (travel, resolve), capture and incidents, counter-intelligence, mission effects |
| `src/Sim/Intelligence.cs` (create) | `Area.Intelligence` council choice of missions and targets; manual Decisions |
| `src/Sim/CivActs.cs` (modify) | Espionage acts call the shared effects; skipped for civs with teams |
| `src/Sim/Civ.cs`, `Colony.cs` (modify) | `Civ.Academy`; `Colony.YardDownUntil`, `DepositsDownUntil`, `IncitedUntil`, `PreparedBy`, `PreparedUntil` |
| `src/Sim/WarEconomy.cs`, `Construction.cs`, `Ground.cs`, `Strife.cs` (modify) | Effects take hold: slips, strategic deposits, garrison, grievance |
| `src/Sim/Policy.cs` (modify) | `Area.Intelligence`, its `Describe` and `"intel:"` prefix |
| `src/Sim/Simulation.cs` (modify) | Yearly `AcademyStep` after `StrifeStep`; per-tick `SpecOpsStep` beside fleet movement; teams removed with a dead civ |
| `src/Sim/SaveGame.cs` (modify) | `Teams`, `NextTeamId`, `CivDto.Academy`, the new colony fields |
| `src/View/WorksWar.cs`, `src/View/EmpireScreen.cs` (modify) | Purple cubes; Overview "Special forces" line; placeholder legend |
| `src/Main.cs` (modify) | `SPECOPSSTATS` trace line |
| `src/Tests/SpecOpsTests.cs` (create) | Stage 4 tests, run beside `WarTests`, `FieldTests`, `StrifeTests` |

### `data/war.json` additions (builder's calls; tune in Task 5)

```json
"specops": {
  "teamsBase": 1, "teamsMax": 8, "kgPerTeam": 4.0e8, "manpowerPerTeam": 0.00002, "upkeepPerYear": 0.05,
  "trainYears": 6.0, "rushedYears": 1.5, "trainedQuality": 1.0, "rushedQuality": 0.5, "xpPerMission": 0.15,
  "onSiteYears": 1.0, "travelYearsMax": 15, "captureMin": 0.02, "captureMax": 0.9, "guardPerTeam": 0.4,
  "incidentRelation": -25, "spyMeterOnCapture": 10,
  "missions": {
    "sabotage_yard":     { "act": "sabotage",   "risk": 0.25, "success": 0.7,  "years": 6 },
    "sabotage_deposit":  { "act": "sabotage",   "risk": 0.20, "success": 0.7,  "years": 10 },
    "assassinate":       { "act": "spy",        "risk": 0.45, "success": 0.5 },
    "incite":            { "act": "incite",     "risk": 0.25, "success": 0.65, "years": 12, "grievance": 30 },
    "steal_tech":        { "act": "steal_tech", "risk": 0.35, "success": 0.55 },
    "prepare_invasion":  { "act": "spy",        "risk": 0.30, "success": 0.7,  "years": 4,  "garrison": 0.5 }
  }
}
```

and in `strife.causes`: `"incited": 30` (the grievance target a full incitement adds while `IncitedUntil > Year`).

Rules the numbers feed (decided here; the formulas are the builder's call recorded for the implementer):
- **Team cap** = `min(teamsMax, teamsBase + floor(log2(1 + colonies)))`.
- **Quality** is fixed when the course *starts*: at war → rushed (`rushedYears`, `rushedQuality`), at peace → full (`trainYears`, `trainedQuality`). Same rule as `BarracksStep`.
- **Counter-intelligence** `CounterIntel(Civ d) = d.M.Spy × (1 + guardPerTeam × Σ Quality of d's teams on mission "guard")`.
- **Capture chance** `clamp(risk × CounterIntel(def) / (team.Quality × (1 + team.Xp) × att.M.Spy × att.M.Stealth), captureMin, captureMax)`.
- **Success chance** (when not captured) `min(0.95, success × team.Quality × (1 + team.Xp) × att.M.Spy / sqrt(CounterIntel(def)))`.

---

### Task 1: Teams and the academy

**Files:** Create `src/Sim/SpecOps.cs`, `src/Tests/SpecOpsTests.cs`; modify `data/war.json`, `src/Sim/Forces.cs`, `src/Sim/Civ.cs`, `src/Sim/Simulation.cs`, `src/Tests/SelfTest.cs` (run `SpecOpsTests.All`).

**Interfaces:**
- Consumes: `Training` (WarEconomy.cs), `c.Materiel`, `c.Manpower`, `c.AtWar`, `c.M.Acts`, `Free(colony)`, `WarData.D`.
- Produces:
  - `public class Team { public int Id, Civ, At, Target = -1, To = -1; public string Name, Mission = "guard"; public float Quality, Xp; public bool Rushed; public double Arrive, Due; }` — `At` the star it is at, `To` the star it is flying to (-1 when there), `Target` the colony's star id, `Due` when its time on site ends.
  - `Simulation`: `public readonly List<Team> Teams = new(); public int NextTeamId = 1; public IEnumerable<Team> TeamsOf(Civ c);`
  - `Civ`: `public List<Training> Academy = new();` (each entry is one team: `Troops = 1`).
  - `public int TeamCap(Civ c)` (rule above; 0 without act `spy`).
  - `void AcademyStep(Civ c, double dy)` yearly: finished courses become `Team`s at home on `"guard"` named `"{Ordinal} Special Group"`; upkeep `upkeepPerYear × kgPerTeam × dy` each from `Materiel`, disbanding the newest when it cannot pay; start courses while `teams + Academy < TeamCap`, paying `kgPerTeam` materiel and `manpowerPerTeam × pop` manpower, rushed when `c.AtWar.Count > 0`.
  - `Extinct(civ)` removes its teams and academy; `Splinter()` gives the new civ no teams.

- [ ] **Step 1: Write the failing tests** in `SpecOpsTests.All`:

```csharp
("peace_trains_best", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Acts.Add("spy"); h.Materiel = 1e30; h.Manpower = 1e9;
    sim.Warp(8);
    var t = sim.TeamsOf(h).First();
    SelfTest.Check(!t.Rushed && t.Quality == 1.0f, "a peacetime course is the full course");
}),
("war_rushes_teams", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Acts.Add("spy"); h.Materiel = 1e30; h.Manpower = 1e9;
    sim.DeclareWar(h, sim.Civs.First(c => c.Alien && !c.Dead));
    sim.Warp(2);
    var t = sim.TeamsOf(h).First();
    SelfTest.Check(t.Rushed && t.Quality == 0.5f, "a wartime team is rushed out and worse");
}),
("course_keeps_its_rule", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Acts.Add("spy"); h.Materiel = 1e30; h.Manpower = 1e9;
    sim.Warp(1); int n = h.Academy.Count;
    sim.DeclareWar(h, sim.Civs.First(c => c.Alien && !c.Dead));
    sim.Warp(2);
    SelfTest.Check(h.Academy.Take(n).All(t => !t.Rushed) && sim.TeamsOf(h).Count() == 0, "a peacetime course is not cut short by war");
}),
("few_and_expensive", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Acts.Add("spy"); h.Materiel = 1e30; h.Manpower = 1e9;
    sim.Warp(20);
    SelfTest.Check(sim.TeamsOf(h).Count() + h.Academy.Count <= sim.TeamCap(h) && sim.TeamCap(h) <= 8, "never more than the cap");
    h.M.Acts.Remove("spy"); SelfTest.Check(sim.TeamCap(h) == 0, "no agency, no academy");
}),
("dead_civ_teams_go", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Acts.Add("spy"); h.Materiel = 1e30; h.Manpower = 1e9;
    sim.Warp(8);
    var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
    SelfTest.Check(!sim.TeamsOf(s).Any(), "a splinter is born with no teams");
    var o = sim.Civs.First(c => c.Alien && !c.Dead); o.M.Acts.Add("spy"); o.Materiel = 1e30; o.Manpower = 1e9; sim.Warp(8);
    sim.Extinct(o);
    SelfTest.Check(!sim.Teams.Any(t => t.Civ == o.Id), "a dead civ's teams are gone");
}),
```

(`Extinct(Civ)` in `Simulation.cs` is where a civ dies; make it `internal` and remove its teams and academy there.)

- [ ] **Step 2: Run** `--selftest=peace_trains` etc. — expected FAIL (build: types missing).
- [ ] **Step 3: Implement** as above; call `AcademyStep(civ, yd)` after `StrifeStep`.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Special forces: teams and the academy"`

---

### Task 2: Missions, capture and counter-intelligence

**Files:** Modify `src/Sim/SpecOps.cs`, `src/Sim/CivActs.cs`, `src/Sim/Colony.cs`, `src/Sim/WarEconomy.cs` (`SlipsOf` is 0 while `YardDownUntil > Year`), `src/Sim/Construction.cs` (line ~398: a colony's `Res.Strategic` counts only when `DepositsDownUntil <= Year`), `src/Sim/Ground.cs` (`GarrisonStrength` × `missions.prepare_invasion.garrison` against `PreparedBy` while `PreparedUntil > Year`; `Land` gains no other change), `src/Sim/Strife.cs` (cause `incited` while `IncitedUntil > Year`, phrase "agitators from abroad are stirring it up"), `src/Sim/Simulation.cs` (per-tick `SpecOpsStep(dt)`); test in `SpecOpsTests.cs`.

**Interfaces:**
- Consumes: `Team`, `TravelYears(civ, a, b)`, `Holder(colony)`, `ColonyAtAny(starId)`, `Fleets`, `NewAdmiral`, `CompleteTech`, `Available`, `Raise`, `Log`, `Civ.Truce`, `AddMeter("Spy", …)`.
- Produces:
  - `Colony`: `public double YardDownUntil, DepositsDownUntil, IncitedUntil, PreparedUntil; public int PreparedBy = -1;` (reset by `TransferColony`).
  - `public static readonly string[] Missions = { "sabotage_yard", "sabotage_deposit", "assassinate", "incite", "steal_tech", "prepare_invasion" };`
  - `public bool CanTarget(Civ att, Colony c, string mission)`: `Holder(c) != att`, the holder is met and not dead, `att.M.Acts` holds the mission's `act`, travel from att's capital ≤ `travelYearsMax`, and the mission fits: yard → `SlipsOf(c) > 0`; deposit → `c.Res.Strategic.Count > 0`; assassinate → a fleet of the holder parked at `c.Star.Id` with an admiral; incite → `c.Civ == Holder(c)` and not a hive; steal_tech → the holder has a tech `Available` to att; prepare_invasion → att at war with the holder.
  - `public void Dispatch(Team t, Colony target, string mission)`: sets `Mission`, `Target`, `To`, `Arrive = Year + TravelYears`; `"guard"` sends it home.
  - `public double CounterIntel(Civ d)`, `public double CaptureChance(Team t, Civ def, string mission)`, `public double SuccessChance(Team t, Civ def, string mission)` (rules above).
  - `void SpecOpsStep(double dt)`: arrivals set `Due = Year + onSiteYears`; at `Due`, if `!CanTarget` any more → abort home (silent); else roll capture → `Captured`; else roll success → `Succeed`; either way the team gains `xpPerMission` (capped 1) and heads home on `"guard"`.
  - `void Captured(Team t, Civ def)`: team removed; relations `incidentRelation` both ways; attacker `AddMeter("Spy", spyMeterOnCapture)`; one chronicle line (`"loss"` for the attacker): "A {att} special group is caught at {sys}. {def} parade them before the cameras."; `Raise("Incident", …)` for `Main` on either side.
  - `void Succeed(Team t, Colony c)` per mission: yard → `YardDownUntil = Year + years`, its slips' `Progress` zeroed; deposit → `DepositsDownUntil = Year + years`; assassinate → that fleet's admiral replaced by `NewAdmiral(holder)` and its `Morale *= 0.7`; incite → `IncitedUntil = Year + years`; steal_tech → shared `StealTech(att, holder)`; prepare_invasion → `PreparedBy = att.Id`, `PreparedUntil = Year + years`.
  - `CivActs`: `StealTech(Civ c, Civ o) -> Tech` and `Incite(Civ c, Colony target)` extracted and used by both the acts and `Succeed`; `ActStep` skips acts `steal_tech`, `sabotage`, `incite` for a civ with any team (teams do that work now); `spy`, `dreamwalk`, `block_research`, `memetic` are untouched.

- [ ] **Step 1: Write the failing tests:**

```csharp
("sabotage_yard_lands", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var c = o.Colonies.First(x => sim.SlipsOf(x) > 0);
    sim.Succeed(t, c, "sabotage_yard");
    SelfTest.Check(sim.SlipsOf(c) == 0 && c.YardDownUntil > sim.Year, "a sabotaged yard builds nothing for years");
}),
("sabotage_deposit_lands", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var c = o.Colonies.First(x => x.Res.Strategic.Count > 0);
    var only = c.Res.Strategic.Where(x => !o.Colonies.Where(y => y != c).Any(y => y.Res.Strategic.Contains(x))).ToList();
    sim.Succeed(t, c, "sabotage_deposit"); sim.Warp(1.1);
    SelfTest.Check(c.DepositsDownUntil > sim.Year && only.All(x => !o.Strategic.Contains(x)), "deposits held nowhere else stop feeding the empire");
}),
("assassinate_replaces_admiral", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var f = sim.FleetsOf(o).First(); var c = o.Colonies.First(); f.At = c.Star.Id; f.To = -1;
    string before = f.Admiral.Name; f.Admiral.Wins = 7;
    sim.Succeed(t, c, "assassinate");
    SelfTest.Check(f.Admiral.Name != before && f.Admiral.Wins == 0, "the admiral is dead; a green one takes over");
}),
("incite_feeds_unrest", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var c = o.Colonies.First(x => !x.Home);
    double t0 = sim.GrievanceTarget(c, out _); sim.Succeed(t, c, "incite");
    SelfTest.Check(sim.GrievanceTarget(c, out var cause) >= t0 + 25 && cause.Contains("agitators"), "incitement feeds stage 3's grievance");
}),
("prepare_weakens_garrison", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var c = o.Colonies.First(x => !x.Home); sim.DeclareWar(h, o);
    double g0 = sim.GarrisonStrength(c, h); sim.Succeed(t, c, "prepare_invasion");
    SelfTest.Check(sim.GarrisonStrength(c, h) < g0 * 0.6 && sim.GarrisonStrength(c, null) == sim.GarrisonStrength(c), "weaker only against the civ that prepared it");
}),
("capture_is_an_incident", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); float r0 = o.Relations.GetValueOrDefault(h.Id);
    sim.Captured(t, o);
    SelfTest.Check(!sim.Teams.Contains(t) && o.Relations[h.Id] <= r0 - 24 && sim.Alerts.Any(a => a.Cat == "Incident"), "a captured team is a diplomatic incident");
}),
("counter_intel_defends", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); double p0 = sim.CaptureChance(t, o, "steal_tech");
    o.M.Acts.Add("spy"); for (int i = 0; i < 3; i++) sim.Teams.Add(new Team { Id = sim.NextTeamId++, Civ = o.Id, At = o.Colonies[0].Star.Id, Quality = 1 });
    SelfTest.Check(sim.CaptureChance(t, o, "steal_tech") > p0 * 1.5, "guarding teams catch more spies");
    t.Quality = 0.5f; SelfTest.Check(sim.CaptureChance(t, o, "steal_tech") > p0, "rushed teams are caught more often");
}),
("target_changes_hands", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var c = o.Colonies.First(x => !x.Home);
    sim.Dispatch(t, c, "incite"); sim.TransferColony(c, h);
    for (int i = 0; i < 40 && t.Mission != "guard"; i++) sim.Warp(1);
    SelfTest.Check(t.Mission == "guard" && c.IncitedUntil <= sim.Year, "the team aborts and nothing lands on the new owner");
}),
("no_admiral_no_mission", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var c = o.Colonies.First(x => !x.Home);
    foreach (var f in sim.FleetsOf(o)) if (f.At == c.Star.Id) f.At = o.Colonies.First(x => x.Home).Star.Id;
    SelfTest.Check(!sim.CanTarget(h, c, "assassinate"), "nobody to kill, no mission");
}),
("acts_not_doubled", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); h.M.Acts.Add("steal_tech");
    SelfTest.Check(!sim.ActOptions(h).Contains("steal_tech"), "with teams, the act gives way to the missions");
}),
```

`SpecOpsTests.Ready()` (helper in the test file): `NewSim`, `Warp(400)`, humanity gets every mission's act, `Materiel`/`Manpower` huge, one trained `Team` (Quality 1) added by hand at the capital; returns `(sim, humanity, nearest met alien civ with a colony, team)`. `GarrisonStrength(Colony c, Civ against = null)` gains the optional parameter; `Succeed(Team, Colony, string mission)` takes the mission explicitly for the tests; `ActOptions(Civ)` is `ActStep`'s option list pulled out (ids only).

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS (earlier tests included: the espionage acts still behave for civs without teams).
- [ ] **Step 5: Commit** `git commit -m "Special forces: missions, capture and counter-intelligence"`

---

### Task 3: The council's choices, and the Intelligence area

**Files:** Create `src/Sim/Intelligence.cs`; modify `src/Sim/Policy.cs`, `src/Sim/SpecOps.cs` (a team home on `"guard"` for ≥ 1 year is offered to `ChooseMission`), `src/Sim/Simulation.cs` (`SwitchPlayer` leaves teams with the old civ); test in `SpecOpsTests.cs`.

**Interfaces:**
- Consumes: `CanTarget`, `Dispatch`, `SuccessChance`, `CaptureChance`, `Fleet.Target`, `Colony.Grievance`, `c.Ethic`, `Policy.IsManual`, `Raise`, `AskAfter`.
- Produces:
  - `public (Colony Target, string Mission)? ChooseMission(Civ c, Team t)` — scores every `(colony, mission)` that `CanTarget` as `value × SuccessChance × (1 − CaptureChance × caution)`, best wins, `null` keeps the team home. Values (builder's call): at war with the holder → `prepare_invasion` 3 when one of `c`'s fleets has that star as `Target`, `assassinate` 2 × the admiral's `Wins + 1`, `sabotage_yard` 2; otherwise → `steal_tech` 1.5, `incite` 1 + `Grievance/50` (rivals only: relations < 0), `sabotage_deposit` 1 (rivals only). `caution` = 2 for Reverent (who also never assassinate), 1 Pragmatic, 0.5 Ruthless. At least one team stays home on guard when the civ has ≥ 3 teams.
  - Manual (`c.Main && IsManual(Area.Intelligence)`): instead of dispatching, `Raise("Decision", "{team} is ready", …, key "intel:" + t.Id)` with the council's top three `(target, mission)` as choices ("Sabotage the yards at {sys}", "Assassinate Admiral {name} at {sys}", "Stir up {sys}", "Steal {civ}'s secrets at {sys}", "Prepare {sys} for invasion", "Sabotage the deposits at {sys}"), plus "Keep them home (counter-intelligence)"; `AskAfter` 3 years.
  - `SwitchPlayer` closes open `intel:` decisions (it already closes all alerts) and does not move teams.

- [ ] **Step 1: Write the failing tests:**

```csharp
("war_aims_at_the_front", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); sim.DeclareWar(h, o);
    var c = o.Colonies.First(x => !x.Home); var f = sim.FleetsOf(h).First(); f.Target = c.Star.Id;
    var pick = sim.ChooseMission(h, t);
    SelfTest.Check(pick?.Mission is "prepare_invasion" or "assassinate" or "sabotage_yard", "at war, teams serve the war");
}),
("reverent_never_assassinate", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); h.SetDrive(h.Drive, "Reverent"); sim.DeclareWar(h, o);
    for (int i = 0; i < 20; i++) SelfTest.Check(sim.ChooseMission(h, t)?.Mission != "assassinate", "a Reverent council will not order a killing");
}),
("manual_intel_asks", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); sim.SetManual(h, Area.Intelligence, true);
    sim.OfferTeam(h, t);
    SelfTest.Check(sim.Alerts.Any(a => a.Cat == "Decision" && !a.Done && a.Key == "intel:" + t.Id) && t.Mission == "guard", "the player chooses");
}),
("switch_player_teams", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); sim.SetManual(h, Area.Intelligence, true); sim.OfferTeam(h, t);
    var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
    sim.SwitchPlayer(s);
    SelfTest.Check(t.Civ == h.Id && !sim.TeamsOf(s).Any() && sim.Alerts.Where(a => a.Key?.StartsWith("intel:") == true).All(a => a.Done), "teams stay with the old civ");
}),
```

(`public void OfferTeam(Civ c, Team t)` is the entry point `SpecOpsStep` calls: Decision when manual, else `ChooseMission` and `Dispatch`.)

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Special forces: the council's choices and the Intelligence area"`

---

### Task 4: Purple cubes, the overview line, saves

**Files:** Modify `src/View/WorksWar.cs`, `src/View/EmpireScreen.cs`, `src/Sim/SaveGame.cs`; test in `SpecOpsTests.cs`.

**Interfaces:**
- Consumes: `WarData.Colour("specops")`, `Put(slot, of, lane, km, col)` in `UpdateWarProps`, `Teams`, `Civ.Academy`.
- Produces:
  - `UpdateWarProps`: purple cubes (`km = 1.5`, lane `3.6f`) for each course in `civ.Academy` at the capital, and for each of **the player's** teams at this star (own, guarding, or on site in an enemy system, tinted by the player's empire colour as usual). Enemy teams are not drawn (they are secret until caught).
  - Overview line on the empire screen: `Special forces: {n} teams ({trained} fully trained, {rushed} rushed) · {k} in training · {m} on missions · counter-intelligence {CounterIntel:0.0}` (hidden when `TeamCap == 0`).
  - Placeholder legend on the empire screen's Military/Overview tab (one line, the six kinds with their colours from `placeholders`), if stage 1–3 did not already add one.
  - Saves: `SaveDto.Teams`, `SaveDto.NextTeamId`, `CivDto.Academy`, `ColonyDto` gains `YardDownUntil, DepositsDownUntil, IncitedUntil, PreparedUntil, PreparedBy` (old saves: `PreparedBy` defaults to -1 when the field is missing).

- [ ] **Step 1: Write the failing test:**

```csharp
("specops_saved", () => {
    var (sim, h, o, t) = SpecOpsTests.Ready(); var c = o.Colonies.First(x => !x.Home);
    sim.Dispatch(t, c, "incite"); t.Xp = 0.3f; h.Academy.Add(new Training { Troops = 1, Left = 2, Rushed = true, At = h.Colonies[0].Star.Id });
    c.PreparedBy = h.Id; c.PreparedUntil = 2500; c.YardDownUntil = 2490;
    SaveGame.Save(sim, 20610, "user://selftest_specops.json");
    var (s2, _) = SaveGame.Load("user://selftest_specops.json");
    var t2 = s2.Teams.First(x => x.Id == t.Id); var c2 = s2.Civs[o.Id].Colonies.First(x => x.Star.Id == c.Star.Id);
    SelfTest.Check(t2.Mission == "incite" && t2.Target == c.Star.Id && t2.Xp == 0.3f && s2.Humanity.Academy.Count == h.Academy.Count
        && c2.PreparedBy == h.Id && c2.PreparedUntil == 2500 && c2.YardDownUntil == 2490, "special forces round-trip");
}),
```

- [ ] **Step 2: Run** — expected FAIL.
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest`, `--warp=300 --savetest`, `--warp=700 --savetest` — all PASS, `SAVETEST equal=True` twice. Take `tools/shot.sh specops --focus=Earth --year=2500` (give humanity the `spy` act in a debug path or use a year past `Intelligence Agency`) and check that purple cubes show beside the green barracks cubes once the player has the `spy` act.
- [ ] **Step 5: Commit** `git commit -m "Special forces: placeholder cubes, overview line, saves"`

---

### Task 5: Acceptance runs and tuning

**Files:** Modify `src/Main.cs` (trace summary), `data/war.json` (tuning).

**Interfaces:**
- Produces: at the end of a `--trace` warp, one line
  `SPECOPSSTATS teams=<peak living teams> trained=<n> rushed=<n> missions=<n> success=<n> captured=<n> aborted=<n> incidents=<n> bymission=<id:n,...> warsAfterIncident=<n>` (counters on `Simulation`, like `StrifeStats`; `warsAfterIncident` counts wars declared within 10 years of an incident between the same two civs).

- [ ] **Step 1: Add** `SPECOPSSTATS` (the `WARSTATS` and `STRIFESTATS` lines stay).
- [ ] **Step 2: Run** `"$(cat .godotpath)" --headless --path . -- --warp=940 --trace --seed=<s>` for seeds 20610, 7, 4242, 1, 99, 2026 and record all three lines per seed. Acceptance: every seed has missions of all six kinds somewhere; captures are 15–40 % of missions; success rate of fully trained teams is visibly higher than of rushed teams (log both in the counters); teams per civ never exceed the cap; stage 2 and 3 bars still hold (hundreds of battles, a mix of endings, fracture on some seeds and not on others); chronicle at most ~300 lines a century; real time under ~40 s per seed.
- [ ] **Step 3: Tune** `data/war.json` `specops` values until Step 2 holds. Record each change and its effect in the ledger. If a code defect blocks acceptance, fix it with a failing test first and ledger it.
- [ ] **Step 4: Run** `--selftest`, `--warp=300 --savetest`, `--warp=700 --savetest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Special forces: trace statistics and tuning"`

---

## Not in this stage

The Military screen listing every team with its course, mission and odds, and map markers for teams (stage 5);
captured teams traded back at peace talks; enemy teams shown once caught; real Blender models for special forces
(the purple cubes stay until then).
