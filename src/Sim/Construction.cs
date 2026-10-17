using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// Great works are built out of a system's economy, not conjured. Each has a real mass (and some a real energy bill),
/// and a floor on how fast it can be assembled however rich the builders (a habitat's soil and air take decades to
/// settle; a ring must be spun up). A system's industry (the kilograms a year its foundries can fabricate, from its
/// power, its foundries and its workforce) is split by its government between the swarm and its great works, and the
/// works budget is shared among whatever is being built at once. Stability, crises, the ruling faction and the
/// government all bend it. Once the gate network joins a civilisation's stars, they pool part of their industry and
/// power into its greatest projects: a ringworld is the work of a galactic civilisation, not a single star.
/// See docs/ECONOMY.md for the sources and arithmetic behind the numbers.
/// </summary>
public partial class Colony
{
    /// <summary>Construction progress per megastructure, 0..1 (absent = not begun).</summary>
    public readonly Dictionary<string, float> Built = new();
    /// <summary>Loaded from a save made before construction existed: what was known then is already standing.</summary>
    public bool LegacyBuilt;
    /// <summary>Progress per year each project made last step (for estimates in the HUD and chronicle).</summary>
    public readonly Dictionary<string, double> BuildRate = new();

    public enum WorkKind { Transit, Industry, War, Mind, Habitat, Stellar }

    /// <summary>
    /// What a great work takes. Mass in kg, the fewest years it can physically be assembled in, an energy bill in
    /// joules for the ones that are bought with power rather than matter (0 = none), and what kind of work it is.
    /// </summary>
    public record Spec(double Kg, double MinYears, double Joules, WorkKind Kind);
    public static readonly Dictionary<string, Spec> Specs = new()
    {
        // Lofstrom's loop: ~2000 km of rotor and sheath, a few million tonnes
        ["launchloop"] = new(5e9, 8, 0, WorkKind.Transit),
        // a phased array of thin mirrors sixty kilometres across
        ["laser"] = new(1e11, 10, 0, WorkKind.Transit),
        // a crucible the size of a city: ten Stanford tori of steel
        ["foundry"] = new(1e12, 12, 0, WorkKind.Industry),
        ["yard"] = new(1e13, 15, 0, WorkKind.War),
        ["shield"] = new(3e13, 20, 0, WorkKind.War),
        // an 1800 km dish of film and truss at ~3 kg/m²
        ["sentry"] = new(3e13, 25, 0, WorkKind.Mind),
        ["relay"] = new(1e14, 15, 0, WorkKind.Transit),
        // a collider ring and the collectors to run it
        ["antimatter"] = new(1e14, 20, 0, WorkKind.Industry),
        ["coord"] = new(1e15, 30, 0, WorkKind.Mind),
        // a black hole made of light: the mass of a mountain, but every gram of it paid for in focused energy (E = mc²)
        ["kugel"] = new(1e15, 40, 1e28, WorkKind.Industry),
        ["cracker"] = new(1e16, 20, 0, WorkKind.Industry),
        ["nexus"] = new(1e16, 40, 0, WorkKind.Mind),
        ["lattice"] = new(1e16, 30, 0, WorkKind.War),
        // 1000 km radius, 500 wide: ~3 million km² of floor under two metres of soil, an atmosphere (~10 t/m²),
        // nanotube hoop and 200 km walls. The soil and air must settle and a biosphere take: decades whatever the budget.
        ["bishop"] = new(5e16, 80, 0, WorkKind.Habitat),
        ["gateway"] = new(1e17, 40, 1e30, WorkKind.Transit),
        ["sanctum"] = new(1e17, 50, 0, WorkKind.War),
        // Criswell's star lifter: rings of current round the star's equator and the collectors over its poles
        ["lifter"] = new(5e18, 60, 0, WorkKind.Industry),
        ["umbralfort"] = new(1e18, 60, 0, WorkKind.War),
        ["gatebastion"] = new(1e18, 60, 0, WorkKind.War),
        ["memory"] = new(1e19, 80, 0, WorkKind.Mind),
        ["throne"] = new(1e19, 100, 0, WorkKind.Mind),
        // a throat held open with exotic matter: the energy of a month of the Sun's whole output
        ["wormhole"] = new(1e19, 100, 1e33, WorkKind.Transit),
        // the swarm already exists: the beam is the lens, the steering and the cooling
        ["beam"] = new(1e20, 60, 0, WorkKind.War),
        ["decompressor"] = new(1e20, 100, 0, WorkKind.Industry),
        ["spire"] = new(1e20, 100, 0, WorkKind.Mind),
        ["coronaforge"] = new(1e21, 100, 0, WorkKind.Industry),
        ["suncitadel"] = new(1e21, 150, 0, WorkKind.War),
        // a 40 000 km shell round a black hole at ~10⁵ kg/m²
        ["penrose"] = new(2e21, 200, 0, WorkKind.Industry),
        // planets fed to a stellar black hole, and the cage that catches what comes back
        ["forge"] = new(1e22, 120, 0, WorkKind.Industry),
        ["aegis"] = new(1e22, 200, 0, WorkKind.War),
        // a machine that gathers a nebula and lights it: the gas is moved, not made, but moving it is a whole star's work
        ["starforge"] = new(1e26, 1000, 1e35, WorkKind.Stellar),
        // Niven's figure: a Jupiter of scrith. And it must spin at 1200 km/s for a gravity: ~1e39 J of rotation,
        // a hundred thousand years of the Sun's output. Only a galactic civilisation's pooled power can pay that.
        ["ringworld"] = new(2e27, 500, 1e39, WorkKind.Habitat),
        // Birch's supramundane shells round a black hole: tens of Jupiters
        ["birch"] = new(1e28, 1000, 0, WorkKind.Habitat),
        // a disk from inside Mercury's orbit to beyond Mars', thousands of km thick: more than half a Sun of matter
        ["alderson"] = new(1e30, 2000, 0, WorkKind.Habitat),
    };
    public static Spec SpecOf(string id) => Specs.TryGetValue(id, out var s) ? s : new Spec(1e16, 40, 0, WorkKind.Industry);
    static bool Instant(string id) => id is "jumps" or "starbase";

    /// <summary>How far along a structure is (1 = standing, 0 = not begun).</summary>
    public float BuildProgress(string id) => Instant(id) ? 1f : Built.TryGetValue(id, out var p) ? p : 0f;

    // ---------- the economy ----------

    /// <summary>
    /// Kilograms a year the system's foundries can fabricate: the same capacity that raises the swarm, scaled by its
    /// workforce, its stability, its government and the great works that serve industry (foundries, shipyards, the
    /// corona forge). Planet-bound at the start (a few million tonnes a year lifted off the Moon); a hundred thousand
    /// Mercurys' worth a year round a finished star.
    /// </summary>
    public double IndustryKg => Tempo * 0.08 * PFrac * Math.Max(Foundries, 1) * M.Foundry * Boost * Defs.SwarmKg
                                * Workforce * Stability * Gov.Industry * MegaIndustry;

    /// <summary>
    /// What the workforce adds: billions of hands (and, once uploaded, minds that run machines directly). Relative to
    /// Earth's ten billion; a quarter-power law, so a crowded system is busier but never runs only on numbers.
    /// </summary>
    public double Workforce
    {
        get
        {
            double minds = Pop * (1 - Civ.Digital) + Pop * Civ.Digital * 8;
            return Math.Clamp(Math.Pow(Math.Max(minds, 0.01) / 10.0, 0.25), 0.4, 3.0);
        }
    }

    /// <summary>
    /// Order in the system, 0.3..1.2. Crises, wars and a starving population all cost output; a hive has no unrest.
    /// </summary>
    public double Stability
    {
        get
        {
            double s = 1.0;
            s -= Crisis?.Id switch { "plague" => 0.45, "war" => 0.3, "storms" => 0.2, "integration" => 0.1, null => 0, _ => 0.15 };
            s -= 0.06 * Math.Min(Civ.AtWar.Count, 4);
            if (Pop > PopCap * 1.05 && PopCap > 0) s -= 0.1;                  // overcrowded
            if (Splinter.HasValue) s -= 0.15;
            s *= Gov.Unrest;                                                  // how much a government feels any of that
            s += 0.05 * (M.Unity - 1) + 0.05 * (M.Admin - 1);
            if (Civ.Fallen) s -= 0.2;
            return Math.Clamp(s + (1 - Gov.Unrest) + MegaOrder, 0.3, 1.2);
        }
    }

    public Government Gov => Government.Of(Civ);

    /// <summary>The share of industry the government spends on great works (the rest goes to the swarm, ships and homes).</summary>
    public double WorksShare
    {
        get
        {
            if (Civ.Emergency) return 0.95;                                             // everything that can be spared
            if (WorksOverride is double o) return o;                                    // set by hand for this system
            if (Civ.Policy.IsManual(Area.Economy)) return Civ.Policy.WorksShare;
            double s = Gov.Works;
            if (Capture >= CaptureMax * 0.98 || !M.Swarm) s = Math.Min(0.85, s + 0.3);   // nothing else to spend it on
            return s;
        }
    }

    /// <summary>This system's own works budget, before any of it is shipped to other systems' works.</summary>
    public double LocalWorks => IndustryKg * WorksShare;

    /// <summary>How many great works can be under way at once: the administration (and the minds) to run them.</summary>
    public int BuildSlots => 1 + Math.Max(Era, 0) / 2 + Gov.Slots + (int)Math.Floor(Math.Log10(1 + Pop / 5)) + (M.Admin > 1.2f ? 1 : 0)
                             + (Done("coord") ? 1 : 0) + (Done("lattice") ? 1 : 0) + (Civ.Emergency ? 3 : 0);

    /// <summary>
    /// Hands and machines on site: however much industry and matter the empire ships in, a work goes up only as fast as
    /// the people (and their machines) where it stands can assemble it. Normally well above the budget; in an
    /// emergency, when the whole empire's industry converges on one system, it is the limit.
    /// </summary>
    public double LabourKg => (3 * IndustryKg / Math.Max(Stability, 0.3) + Pop * 1e15) * (Civ.Emergency ? 2 : 1);

    // ---------- states of emergency ----------

    /// <summary>What counts as critical when the civilisation is under threat: defences, the yards that build fleets, the eyes that see them coming.</summary>
    public static bool Critical(string id) => SpecOf(id).Kind == WorkKind.War || id is "shield" or "aegis" or "sentry" or "yard";

    /// <summary>
    /// Yearly: is the civilisation under threat? At war with someone at least nearly as strong, or a system lost in the
    /// last fifteen years, or war at home. A state of emergency is declared at once and lifted after eight quiet years.
    /// </summary>
    static void EvaluateEmergency(Civ civ, double year)
    {
        var sim = civ.Colonies.FirstOrDefault()?.Sim;
        if (sim == null) return;
        double mine = sim.WarStrength(civ);
        var foe = civ.AtWar.Where(id => id < sim.Civs.Count && !sim.Civs[id].Dead).Select(id => sim.Civs[id])
            .Where(e => sim.WarStrength(e) > 0.6 * mine).OrderByDescending(sim.WarStrength).FirstOrDefault();
        string cause = foe != null ? $"war with {Narrative2.Called(foe)}"
            : year - civ.LastLoss < 15 ? "the loss of a system"
            : civ.Colonies.Any(c => c.Home && c.Crisis?.Id == "war") ? "fighting at home" : null;
        if (Godot.OS.HasEnvironment("SF_EMERGENCY") && civ.Main && year > 2300 && year < 2340) cause = "a test of the emergency powers";
        if (cause != null) civ.ThreatSeen = year;
        var home = civ.Colonies.FirstOrDefault(c => c.Home) ?? civ.Colonies.FirstOrDefault();
        if (!civ.Emergency && cause != null)
        {
            civ.Emergency = true; civ.EmergencySince = year; civ.EmergencyCause = cause;
            if (civ.Main && home != null) sim.Raise("Crisis", "State of emergency", $"Facing {cause}, every system's industry turns to the defences.", home);
            if (civ.Main && home != null)
                sim.Log(home, $"State of emergency. Facing {cause}, the {home.Gov.Name.ToLower()} takes every system's foundries and stockpiles: the swarm stops growing, and every hand turns to the defences.", "council", null);
            if (Godot.OS.HasEnvironment("SF_TRACE") && civ.Main) Godot.GD.Print($"EMERGENCY {year:0000} declared: {cause}");
        }
        else if (civ.Emergency && cause == null && year - civ.ThreatSeen >= 8)
        {
            civ.Emergency = false;
            if (civ.Main && home != null) sim.Log(home, $"The state of emergency is lifted after {year - civ.EmergencySince:0} years. The foundries go back to the swarm, and the work that was set aside resumes.", "council", null);
            if (Godot.OS.HasEnvironment("SF_TRACE") && civ.Main) Godot.GD.Print($"EMERGENCY {year:0000} lifted");
        }
    }

    /// <summary>Priority of a work under this council: the ruling faction wants its kind built first.</summary>
    double Want(string id)
    {
        var k = SpecOf(id).Kind;
        double w = 1;
        w *= Civ.Drive switch
        {
            "Industrious" => k is WorkKind.Industry or WorkKind.Stellar ? 1.6 : 1.1,
            "Contemplative" => k == WorkKind.Mind ? 1.8 : k == WorkKind.War ? 0.6 : 0.9,
            "Expansionist" => k == WorkKind.Transit ? 1.8 : k == WorkKind.Habitat ? 1.2 : 0.85,
            _ => 1
        };
        if (k == WorkKind.Habitat && PopCap > 0 && Pop > 0.8 * PopCap) w *= 2;     // people need somewhere to live
        if (k == WorkKind.War && Civ.AtWar.Count > 0) w *= 2;                       // and something to fight with
        w *= Gov.Kind(k);
        w *= Civ.Policy.W(k.ToString());                                          // the player's priorities
        return w;
    }

    // ---------- where each work is built ----------

    /// <summary>Built in every system that qualifies: the infrastructure of a civilisation, not its wonders.</summary>
    public static bool IsEverywhere(string id) => Everywhere.Contains(id);
    static readonly HashSet<string> Everywhere = new() { "foundry", "yard", "shield", "relay", "gateway" };
    /// <summary>Works that live in a star's corona or draw on its body: a star has room for one.</summary>
    static readonly HashSet<string> Corona = new() { "lifter", "coronaforge", "suncitadel" };
    /// <summary>Works that wrap the star itself: they need a quiet, steady, sunlike star, and nothing else feeding on it.</summary>
    static readonly HashSet<string> Wrap = new() { "ringworld", "alderson" };
    /// <summary>Works round a black hole. The forge makes (or captures) the hole; the others work the same one.</summary>
    static readonly HashSet<string> Hole = new() { "forge", "penrose", "decompressor" };
    /// <summary>Works that consume or rework a whole star system and want it to themselves.</summary>
    static readonly HashSet<string> Solitary = new() { "starforge", "birch" };

    bool Has(string id) => Built.ContainsKey(id);
    /// <summary>A finished work stands in this system.</summary>
    public bool Done(string id) => Built.TryGetValue(id, out var p) && p >= 1f;
    bool Sunlike => Star.Lum is > 0.35f and < 2.6f && !Star.Dead;

    /// <summary>
    /// Could this system take this work, given what already stands (or is going up) round its star? A ringworld needs
    /// its star left alone (no lifting, no corona forges, no black hole beside it); Sol keeps its planets, so it gets
    /// no ringworld or disk; one corona work per star; black-hole works share one hole, away from home.
    /// </summary>
    public bool CanHost(string id)
    {
        if (Everywhere.Contains(id))
            return id switch
            {
                "yard" => Home || Era >= 2 || Pop > 1,
                "shield" => Home || Settled.Keys.Any(b => b.Life || b.Greening > 0.3f),
                _ => true
            };
        bool wrapped = Wrap.Any(Has), holed = Hole.Any(Has), alone = Solitary.Any(Has);
        if (alone && !Has(id)) return false;
        if (Corona.Contains(id)) return !wrapped && !Corona.Any(x => x != id && Has(x)) && !(Star.Lum < 0.05f);
        if (Wrap.Contains(id)) return !Home && Sunlike && !Corona.Any(Has) && !holed && !Wrap.Any(x => x != id && Has(x)) && Settled.Count <= 3;
        if (Hole.Contains(id)) return !Home && !wrapped;
        if (Solitary.Contains(id)) return !Home && !wrapped && !holed && !Corona.Any(Has) && (Has(id) || Built.Count(kv => kv.Value > 0) <= 3);
        if (id == "beam") return Capture >= 0.5 && !wrapped;          // the swarm is the beam
        return Home || Has(id);                                         // the rest are the capital's
    }

    /// <summary>How well a system suits a work, among those that can host it (higher is better).</summary>
    double Suits(string id)
    {
        double s = Has(id) ? 1e6 : 0;                                     // never move a work already begun
        // the deposits: build where the matter is, and where the rare thing it needs is found
        if (Needs(id) is string need && Res.Strategic.Contains(need)) s += 4;
        if (SpecOf(id).Kind == WorkKind.Habitat) s += Math.Log10(1 + Res.Metals) + (Res.VolatileRich ? 3 : 0) + Res.LifeWorlds * 0.5;
        if (Wrap.Contains(id)) s += 10 - 4 * Math.Abs(Math.Log10(Math.Max(Star.Lum, 1e-3))) + (Complete ? 3 : 0) + Pop * 0.01;
        else if (Corona.Contains(id)) s += Math.Log10(Math.Max(Star.Lum, 1e-3)) * 3 + (Home ? 2 : 0) + (Complete ? 1 : 0);
        else if (Hole.Contains(id)) s += Math.Log10(Math.Max(Star.Lum, 1e-3)) * 2 + (Hole.Any(Has) ? 20 : 0);   // massive stars; join the existing hole
        else if (Solitary.Contains(id)) s += (Complete ? 5 : 0) - Settled.Count;
        else s += (Home ? 10 : 0) + Math.Log10(1 + IndustryKg) * 0.1;
        return s;
    }

    /// <summary>
    /// Which systems build this work: every qualifying one for infrastructure, otherwise the single best site in the
    /// civilisation (or none yet: a ringworld waits for a colony round a quiet sunlike star).
    /// </summary>
    public static IEnumerable<Colony> SitesFor(Civ civ, string id)
    {
        if (Everywhere.Contains(id)) return civ.Colonies.Where(c => c.Seat != null && c.CanHost(id));
        var started = civ.Colonies.FirstOrDefault(c => c.Has(id));
        if (started != null) return new[] { started };
        var best = civ.Colonies.Where(c => c.Seat != null && c.CanHost(id)).OrderByDescending(c => c.Suits(id)).FirstOrDefault();
        return best == null ? Array.Empty<Colony>() : new[] { best };
    }
    /// <summary>Sites are replanned with the logistics, once a year: ids of the great works each system builds.</summary>
    bool BuildsHere(string id)
    {
        if (Raised.Contains(id)) return true;
        if (Everywhere.Contains(id)) return CanHost(id);
        return Civ.Sites.TryGetValue(id, out var at) && at == this;
    }
    static void PlanSites(Civ civ)
    {
        civ.Sites.Clear();
        foreach (var id in civ.M.Megas)
            if (!Everywhere.Contains(id) && !Instant(id))
                foreach (var c in SitesFor(civ, id)) civ.Sites[id] = c;
    }

    // ---------- logistics between systems ----------

    /// <summary>
    /// What share of one system's works budget can usefully reach another's works. Nothing before interstellar ships;
    /// a trickle by sublight freighter (years in transit, most of it eaten by the journey); more by fold drive, falling
    /// with distance; a steady fifteen percent through a pair of gateways, a quarter once the gates are one network.
    /// A hyper relay at the sending end shortens its lanes. A major shipyard there prefabricates whole sections, so
    /// what it sends counts for a quarter more.
    /// </summary>
    public static double Link(Colony from, Colony to)
    {
        var m = from.Civ.M;
        double d = from.Star.Pos.DistanceTo(to.Star.Pos);                 // galaxy units (~50 light-years)
        double f;
        if (from.Done("gateway") && to.Done("gateway")) f = m.GateNet ? 0.25 : 0.15;
        else if (m.Fold) f = 0.06 / (1 + d / 100.0);
        else if (m.ShipSpeed > 0) f = 0.01 / (1 + d / 4.0);
        else return 0;
        if (from.Done("relay")) f += 0.03;
        return f * (from.Done("yard") ? 1.25 : 1.0);
    }

    /// <summary>
    /// Yearly: route each system's spare works budget to the systems building great works. A system sends at most
    /// half its own works budget away, split across the sites in proportion to how well it can reach each.
    /// </summary>
    static void PlanLevies(Civ civ, double year)
    {
        civ.LevyYear = year;
        civ.Levy.Clear(); civ.LevyOut.Clear();
        var sites = civ.Colonies.Where(c => c.BuildRate.Keys.Any(id => !Everywhere.Contains(id) || civ.Emergency && Critical(id)))
            .Concat(civ.Sites.Where(kv => kv.Value.BuildProgress(kv.Key) < 1f).Select(kv => kv.Value)).Distinct().ToList();
        if (sites.Count == 0) return;
        if (civ.Emergency)
        {
            // everything that can reach a critical site goes there: the whole empire's works budget and stockpile
            var crit = sites.Where(s => s.BuildRate.Keys.Concat(civ.Sites.Where(kv => kv.Value == s).Select(kv => kv.Key)).Any(Critical)).ToList();
            if (crit.Count > 0) sites = crit;
        }
        foreach (var from in civ.Colonies)
        {
            var links = sites.Where(s => s != from).Select(s => (s, f: civ.Emergency ? (Link(from, s) > 0 ? 1.0 : 0) : Link(from, s))).Where(x => x.f > 0).ToList();
            double total = links.Sum(x => x.f);
            if (total <= 0) continue;
            double cap = civ.Emergency ? 1.0 : 0.5;
            double scale = total > cap ? cap / total : 1;
            foreach (var (s, f) in links)
            {
                if (!civ.Levy.TryGetValue(s, out var l)) civ.Levy[s] = l = new();
                l.Add((from, f * scale));
            }
            civ.LevyOut[from] = total * scale;
        }
    }

    /// <summary>A share of industry for great works set by hand for this system (null: the empire's policy decides).</summary>
    public double? WorksOverride;

    /// <summary>
    /// Construction in manual: a new great work waits for the player's approval. Infrastructure built everywhere, work
    /// already begun, the voice's own and (in an emergency) the critical defences carry on regardless.
    /// </summary>
    bool Sanctioned(string id)
    {
        var p = Civ.Policy;
        if (!Civ.Main || !p.IsManual(Area.Construction) || Raised.Contains(id) || Everywhere.Contains(id) || Built.ContainsKey(id)
            || p.Approved.Contains(id) || Civ.Emergency && Critical(id)) return true;
        if (p.Declined.Contains(id)) return false;
        var sp = SpecOf(id);
        string name = Simulation.WorkName(id);
        Sim.Raise("Decision", $"Begin the {name}?", $"The council proposes the {name} at {Sys.Name}: {sp.Kg:0.0e0} kg of matter, at least {sp.MinYears:0} years to build.", this, "build:" + id,
            new() { ("Approve", () => p.Approved.Add(id)), ("Turn down", () => p.Declined.Add(id)) });
        return false;
    }

    /// <summary>The works here asked for more assembly than the hands on site could give, last step.</summary>
    public bool LabourBound;

    /// <summary>Industry arriving from the civilisation's other systems for this system's works (kg/yr).</summary>
    public double LevyIn => Civ.Levy.TryGetValue(this, out var l) ? l.Sum(x => x.From.LocalWorks * x.F) : 0;

    /// <summary>Each step: begin what the civilisation builds here (as many as it can administer), and advance it.</summary>
    void BuildStep(double dy)
    {
        if (Seat == null || dy <= 0) return;
        if (LegacyBuilt) { foreach (var id in Civ.M.Megas) Built[id] = 1f; foreach (var id in Raised) Built[id] = 1f; LegacyBuilt = false; }
        if (Year - Civ.LevyYear >= 1)
        {
            Civ.Strategic.Clear();
            foreach (var c in Civ.Colonies) foreach (var x in c.Res.Strategic) Civ.Strategic.Add(x);
            EvaluateEmergency(Civ, Year);
            PlanSites(Civ); PlanLevies(Civ, Year);
        }
        BuildRate.Clear();
        // the voice's raised structures always proceed; then what is sited here, most wanted (and smallest) first
        var want = Raised.Concat(Civ.M.Megas.Where(id => !Raised.Contains(id))).Where(id => !Instant(id)).Distinct()
            .Where(id => BuildProgress(id) < 1f && BuildsHere(id) && (!Civ.Emergency || Critical(id) || Raised.Contains(id)) && Sanctioned(id))
            .OrderBy(id => Raised.Contains(id) ? 0 : 1).ThenBy(id => Built.ContainsKey(id) ? 0 : 1).ThenByDescending(Want).ThenBy(id => SpecOf(id).Kg)
            .ToList();
        if (want.Count == 0) return;
        var active = new List<string>();
        int slots = BuildSlots;
        foreach (var id in want) if (Raised.Contains(id) || active.Count(a => !Raised.Contains(a)) < slots) active.Add(id);

        // the budget: what is left of this system's own, plus what the other systems ship in
        var levy = Civ.Levy.TryGetValue(this, out var lv) ? lv : null;
        double inflow = levy?.Sum(x => x.From.LocalWorks * x.F) ?? 0;
        double budget = LocalWorks * (1 - Civ.LevyOut.GetValueOrDefault(this)) + inflow;
        double powerIn = levy?.Sum(x => x.From.GalacticPower * x.F) ?? 0;
        double power = (Power + powerIn) * 0.1 * 3.156e7;                // a tenth of the power, in joules a year
        double wsum = active.Where(a => !Raised.Contains(a)).Sum(Want);
        // what each project could do this step with its share of industry and power...
        var plan = new List<(string Id, double Rate, double Units)>();
        foreach (var id in active)
        {
            var sp = SpecOf(id);
            if (Raised.Contains(id)) { plan.Add((id, 1.0 / (sp.MinYears * 0.2), 0)); continue; }   // the voice assembles it out of the dark
            double share = Want(id) / Math.Max(wsum, 1e-9);
            double floor = sp.MinYears * (Civ.Emergency ? 0.4 : 1);                   // a crash programme works round the clock
            double rate = Math.Min(budget * share / sp.Kg, 1.0 / floor);                // matter, and how fast it can be put together
            if (sp.Joules > 0) rate = Math.Min(rate, power * share / sp.Joules);      // and the power bill
            rate *= M.Build * MaterialPace(id);
            plan.Add((id, rate, rate * dy * sp.Kg / Defs.SwarmKg));
        }
        // the hands on site: scale everything down together if the plan asks for more assembly than they can do
        double asked = plan.Where(x => !Raised.Contains(x.Id)).Sum(x => x.Rate * SpecOf(x.Id).Kg);
        if (asked > LabourKg)
        {
            double k = LabourKg / asked;
            for (int i = 0; i < plan.Count; i++) if (!Raised.Contains(plan[i].Id)) plan[i] = (plan[i].Id, plan[i].Rate * k, plan[i].Units * k);
        }
        LabourBound = asked > LabourKg;
        // ...then matter: this system's stockpile, plus what the senders ship with their industry (up to half of each
        // sender's stock). Shared smallest need first, so one Jupiter-sized project cannot starve every small one.
        double shipped = 0;
        var offers = new List<(Colony From, double Units)>();
        if (levy != null)
            foreach (var (from, f) in levy)
            {
                double u = Civ.Emergency ? from.Stock * f : Math.Min(from.Stock * 0.5, from.LocalWorks * f * dy / Defs.SwarmKg);
                if (u > 0) { offers.Add((from, u)); shipped += u; }
            }
        double pool = Stock + shipped, left = pool;
        var order = plan.Select((x, i) => (x, i)).OrderBy(t => t.x.Units).ToList();
        int remaining = order.Count(t => t.x.Units > 0);
        var got = new double[plan.Count];
        foreach (var (x, i) in order)
        {
            if (x.Units <= 0) { got[i] = 1; continue; }
            double give = Math.Min(x.Units, left / Math.Max(remaining, 1));
            got[i] = give / x.Units;
            left -= give; remaining--;
        }
        // the local stockpile is spent first, then the shipments, each sender in proportion
        double used = pool - left, fromHere = Math.Min(Stock, used), fromAway = used - fromHere;
        Stock -= fromHere;
        if (shipped > 0) foreach (var (from, u) in offers) from.Stock = Math.Max(0, from.Stock - fromAway * u / shipped);
        for (int i = 0; i < plan.Count; i++)
        {
            var (id, rate0, _) = plan[i];
            double rate = rate0 * got[i];
            float p = Built.TryGetValue(id, out var v) ? v : 0f;
            BuildRate[id] = rate;
            if (p == 0f) Sim.MegaBegun(this, id);
            float next = (float)Math.Min(1.0, p + dy * rate);
            Built[id] = Math.Max(next, 1e-4f);
            if (next >= 1f) Sim.MegaBuilt(this, id);
        }
    }

    // ---------- what finished works give back ----------

    /// <summary>
    /// Industry multiplier from the works standing here: foundries and launch loops cheapen every kilogram; a major
    /// shipyard's slips and cranes are the system's biggest factory; the corona forge works metal in the star itself.
    /// </summary>
    public double MegaIndustry => (Done("foundry") ? 1.15 : 1) * (Done("yard") ? 1.2 : 1) * (Done("launchloop") ? 1.1 : 1)
                                  * (Done("antimatter") ? 1.1 : 1) * (Done("coronaforge") ? 1.5 : 1) * (Done("starforge") ? 1.3 : 1);
    /// <summary>Power the works add (watts): a kugelblitz's Hawking glow fed with matter, a black hole's accretion, its spin.</summary>
    public double MegaPower => (Done("kugel") ? 0.005 * L : 0) + (Done("forge") ? 0.3 * Defs.LSun : 0) + (Done("penrose") ? 1.0 * Defs.LSun : 0)
                               + (Done("decompressor") ? 0.1 * Defs.LSun : 0);
    /// <summary>Mining multiplier: the arc furnace drinks worlds whole.</summary>
    public double MegaMine => Done("cracker") ? 1.3 : 1;
    /// <summary>Star lifting: the tech alone skims a little; the lifter itself is the real flow.</summary>
    public double LiftShare => Done("lifter") ? 1.5 : 0.35;
    /// <summary>Research multiplier from thinking works.</summary>
    public double MegaResearch => 1 + (Done("sentry") ? 0.1 : 0) + (Done("nexus") ? 0.25 : 0) + (Done("memory") ? 0.3 : 0) + (Done("spire") ? 0.5 : 0) + (Done("coord") ? 0.1 : 0);
    /// <summary>Homes (billions): a Bishop ring is an Argentina of floor; a ringworld three million Earths.</summary>
    public double MegaHousing => (Done("bishop") ? 3 : 0) + (Done("ringworld") ? 1e5 : 0) + (Done("birch") ? 4e5 : 0) + (Done("alderson") ? 1e6 : 0);
    /// <summary>Stability: shields, sentinels and a seat of light make people feel safe.</summary>
    double MegaOrder => (Done("shield") ? 0.04 : 0) + (Done("aegis") ? 0.08 : 0) + (Done("sentry") ? 0.02 : 0) + (Done("coord") ? 0.06 : 0) + (Done("throne") ? 0.08 : 0)
                        + (Done("sanctum") ? 0.04 : 0) + (Done("suncitadel") ? 0.04 : 0) + (Done("umbralfort") ? 0.03 : 0) + (Done("gatebastion") ? 0.03 : 0);

    /// <summary>Years left on a work at its current pace (infinity if stalled or not begun).</summary>
    public double YearsLeft(string id) => BuildRate.TryGetValue(id, out var r) && r > 0 ? (1 - BuildProgress(id)) / r : double.PositiveInfinity;
}

/// <summary>
/// How a polity governs itself, after Stellaris's authorities, read from what it has become rather than chosen:
/// an elected Assembly to begin with; a Directorate when it turns ruthless; a Synthetic Commonwealth once most of it
/// is software; a Hive Consensus once disagreement is engineered out.
/// </summary>
public record Government(string Name, double Industry, double Works, int Slots, double Unrest, string About)
{
    /// <summary>Priority multiplier this government gives a kind of work.</summary>
    public double Kind(Colony.WorkKind k) => Name switch
    {
        "Directorate" => k == Colony.WorkKind.War ? 1.5 : 1,
        "Synthetic Commonwealth" => k == Colony.WorkKind.Mind ? 1.5 : k == Colony.WorkKind.Habitat ? 0.6 : 1,
        "Hive Consensus" => k == Colony.WorkKind.Stellar ? 1.4 : 1,
        _ => k == Colony.WorkKind.Habitat ? 1.3 : 1,
    };

    public static readonly Government Assembly = new("Assembly", 1.0, 0.22, 0, 1.0,
        "An elected council. Slow to agree, but what it agrees to, people build willingly.");
    public static readonly Government Directorate = new("Directorate", 1.15, 0.32, 1, 1.25,
        "Rule by decree. More is built, and faster, but every hardship lands harder.");
    public static readonly Government Synthetic = new("Synthetic Commonwealth", 1.35, 0.35, 1, 0.7,
        "Most citizens are software. Industry runs at the speed of thought; flesh-and-blood concerns come second.");
    public static readonly Government Hive = new("Hive Consensus", 1.5, 0.45, 2, 0.0,
        "One will in many bodies. No dissent, no waste, no unrest.");

    public static Government Of(Civ c) =>
        c.M.Hive ? Hive : c.Digital > 0.6 || c.Knows("S7") ? Synthetic : c.Ethic == "Ruthless" || c.Imperial ? Directorate : Assembly;
}

public partial class Simulation
{
    /// <summary>Work begins on a megastructure in a colony (id).</summary>
    public event Action<Colony, string> MegaStarted;
    /// <summary>A megastructure is finished in a colony (id).</summary>
    public event Action<Colony, string> MegaFinished;
    public void MegaBegun(Colony c, string id)
    {
        if (c.Civ.Main && (c.Home || !Colony.IsEverywhere(id)) && Godot.OS.HasEnvironment("SF_TRACE"))
            Godot.GD.Print($"MEGA {Year:0000.0} begin {id} at {c.Sys.Name}{(c.Home ? "" : " (colony)")}: industry {c.IndustryKg:0.0e0} kg/yr ×{c.WorksShare:0.00}, levy in {c.LevyIn:0.0e0}, {c.Gov.Name}, slots {c.BuildSlots}, est {c.YearsLeft(id):0} yr");
        MegaStarted?.Invoke(c, id);
    }
    public void MegaBuilt(Colony c, string id)
    {
        if (c.Civ.Main && (c.Home || !Colony.IsEverywhere(id)) && Godot.OS.HasEnvironment("SF_TRACE")) Godot.GD.Print($"MEGA {Year:0000.0} done  {id} at {c.Sys.Name}");
        MegaFinished?.Invoke(c, id);
    }
}
