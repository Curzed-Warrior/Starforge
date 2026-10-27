using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// Eleven danger meters (docs/TECH_INTEGRATION.md §8). Risky techs and acts push them up, time lets them settle, and a meter
/// past its threshold sets off its crisis, which can come back after a cooldown. Counter-techs push them down.
/// </summary>
public partial class Simulation
{
    public static readonly string[] MeterNames = { "AI", "Mind", "Goo", "Strange", "Breach", "Veil", "Atrocity", "Elders", "Flux", "Spy", "Egress" };
    public static double Threshold(string m) => TechData.D("meters", "list", m, "threshold");
    public event Action<Civ, string> MeterCrisis;

    void MeterStep(Civ civ, double dy)
    {
        double decay = TechData.D("meters", "decayPerYear") * dy;
        foreach (var m in MeterNames)
        {
            double v = civ.Meters.GetValueOrDefault(m);
            if (v <= 0) continue;
            if (v >= Threshold(m) && Year >= civ.CrisisAt.GetValueOrDefault(m) && !Laws.Contains("nocrisis"))
            {
                civ.CrisisAt[m] = Year + TechData.D("meters", "crisisCooldownYears");
                MeterBreaks(civ, m, v);
                MeterCrisis?.Invoke(civ, m);
            }
            civ.Meters[m] = Math.Max(0, v - decay);
        }
        if (civ.WyrmUntil > Year && _r.NextDouble() < 0.2 * dy) Wyrm(civ);
        if (civ.OvermindUntil > 0 && Year > civ.OvermindUntil)
        {
            civ.OvermindUntil = 0;
            Log(civ.Colonies.FirstOrDefault(), $"The overmind that ruled {Narrative2.Called(civ)} for a decade lets go. It leaves behind a council that remembers everything it was told.", "era", null);
        }
    }

    Colony CrisisSite(Civ civ) => civ.Colonies.Where(c => c.Crisis == null).OrderByDescending(c => (c.Home ? 2 : 0) + (c == Focus ? 3 : 0) + c.Capture).FirstOrDefault();

    void MeterBreaks(Civ civ, string m, double v)
    {
        var home = civ.Colonies.FirstOrDefault();
        var site = CrisisSite(civ);
        if (home == null) return;
        string Called() => Narrative2.Called(civ);
        switch (m)
        {
            case "Goo":
                if (site != null) Narrative.StartCrisis(site, "runaway");
                break;
            case "AI":
                // the machines break away: with an interstellar presence they take colonies with them
                var mine = civ.Colonies.Where(c => !c.Home).OrderByDescending(c => c.Capture).Take(Math.Max(0, civ.Colonies.Count / 5)).ToList();
                if (mine.Count >= 2)
                {
                    // a rising of machines, not a movement: always a war
                    var split = Separate(civ, mine, civ.Drive, peaceful: false, name: "the Machine Concord of " + mine[0].Sys.Name);
                    split.Digital = 1;
                    Log(home, $"Rogue minds in {Called()}'s networks stop answering and start governing. {mine.Count} colonies wake up as {split.Name}.", "loss", null);
                }
                else if (site != null) Narrative.StartCrisis(site, "secession");
                break;
            case "Flux":
                if (site != null) Narrative.StartCrisis(site, "storms");
                if (v >= 80 && civ.WyrmUntil < Year)
                {
                    civ.WyrmUntil = Year + 30;
                    Log(home, $"Something is born in the flux of {Called()}'s stars: a coil of living plasma the length of an orbit. It feeds on swarms. They call it a Star-Wyrm.", "loss", null);
                    if (civ.Main) Milestone(home, "The Star-Wyrm", $"Year {Year:0}. A living thing made of stellar fire is loose among humanity's stars.", "star");
                }
                break;
            case "Mind":
                civ.OvermindUntil = Year + 10;
                Log(home, $"A mind grown in {Called()}'s archives wakes up larger than the council that made it, and quietly takes the council's place.", "loss", null);
                if (civ.Drive != "Contemplative") { civ.Drive = "Contemplative"; civ.Leader = civ.NewLeader("Contemplative", Year); civ.Leader.Name = "the Overmind"; }
                break;
            case "Strange":
                var w = home.Sys.All().Where(b => b != home.Seat && b.Kind != BodyKind.Belt && b.Mined < 0.9f).OrderBy(_ => _r.Next()).FirstOrDefault();
                if (w != null)
                {
                    bool lived = home.Settled.Remove(w);
                    w.Mined = 1f; home.Hubs.Remove(w);
                    if (lived) home.Pop *= 0.9;
                    Log(home, $"A strangelet gets loose on {w.Name}. In nine days the whole world has turned into something denser and quieter. {(lived ? "Nobody on it got out." : "Nobody lived there, thankfully.")}", "loss", w);
                }
                break;
            case "Breach":
                if (site != null)
                {
                    var target = site.Sys.All().Where(b => b != site.Seat && b.Kind != BodyKind.Belt).OrderBy(_ => _r.Next()).FirstOrDefault() ?? site.Seat;
                    site.Rival = new Rival { Name = "Rift-Host", Pop = 0, Ships = 8 + _r.Next(6), Planet = target, T0 = Year, Stance = "war" };
                    Log(site, $"A rift opens beside {target.Name} and something comes through it, armed, and not interested in talking.", "loss", target);
                    Narrative.StartCrisis(site, "war");
                }
                break;
            case "Veil":
                var drives = Defs.Drives.Keys.Where(d => d != civ.Drive).ToList();
                string nd = drives[_r.Next(drives.Count)], od = civ.Drive;
                civ.Drive = nd; civ.Leader = civ.NewLeader(nd, Year);
                foreach (var a in Areas) civ.Slots[a].Id = null;
                Log(home, $"The whole council of {Called()} dreams the same dream on the same night. In the morning the {civ.Faction(nd)} rule, and the {civ.Faction(od)} cannot remember why they ever did.", "omen", null);
                break;
            case "Elders":
                var elders = Civs.Where(a => a.Ancient && a != civ && a.Met.Contains(civ.Id)).ToList();
                if (elders.Count == 0) break;
                bool pay = civ.Ethic != "Ruthless" && home.Stock > 0.3 * home.PFrac;
                if (pay)
                {
                    home.Stock *= 0.7;
                    Log(home, $"{Narrative2.Called(elders[0], true)} demand tribute for meddling with what is theirs. {Called().First().ToString().ToUpper() + Called()[1..]} pay.", "council", null);
                    civ.Meters["Elders"] = Threshold("Elders") * 0.5;
                }
                else foreach (var a in elders) Blame(a, civ, 70, home, "will not tolerate younger races meddling with the deep places");
                break;
            case "Atrocity":
                foreach (var o in Civs.Where(o => !o.Dead && o != civ && o.Met.Contains(civ.Id) && (o.Ancient || o.Exalted)).ToList())
                    Blame(o, civ, 80, home, "pronounce judgement on what has been done");
                break;
            case "Spy":
                foreach (var id in civ.Met.ToList())
                {
                    if (id >= Civs.Count) continue;
                    var o = Civs[id];
                    o.Relations[civ.Id] = o.Relations.GetValueOrDefault(civ.Id) - 30; civ.Relations[o.Id] = o.Relations[civ.Id];
                }
                // humanity's own scandal is a loss; a foreign one is foreign news (Log gives those one shared slot)
                Log(home, $"{Called().First().ToString().ToUpper() + Called()[1..]}'s spy networks are exposed all at once. Every embassy in the galaxy closes its doors to them.", civ.Main ? "loss" : "era", null);
                break;
            case "Egress":
                HorizonShock(civ);
                break;
        }
    }

    /// <summary>When a civilisation leaves the universe, everyone else feels it go.</summary>
    void HorizonShock(Civ leaver)
    {
        foreach (var c in Colonies) c.Capture *= 0.95;
        Log(HomeColony, $"Space itself shudders as {Narrative2.Called(leaver)} {(leaver.Dead ? "leave" : "open a door out of")} the universe. Every swarm in the galaxy loses a twentieth of its collectors to the wake.", "omen", null);
    }

    void Wyrm(Civ civ)
    {
        var c = civ.Colonies.Where(x => x.Capture > 0.05).OrderBy(_ => _r.Next()).FirstOrDefault();
        if (c == null) return;
        c.Capture *= 0.9;
        if (civ.Main || c == Focus) Log(c, $"The Star-Wyrm coils through {c.Sys.Name}'s swarm and feeds. A tenth of it is simply gone.", "loss", null);
        Fx(c, "flare", null);
    }
}
