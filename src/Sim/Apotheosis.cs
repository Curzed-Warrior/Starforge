using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// Civilisations can come close to godhood but never reach it (progression.json apotheosis). Capstone techs build an
/// apotheosis score; every decade a civ with enough of it rolls to become Exalted: a lesser power that seeds, ignites,
/// uplifts, gifts and smites inside its own reach, and has feelings about the voice. Failing high can make it Fallen.
/// </summary>
public partial class Simulation
{
    public event Action<Civ> CivExalted, CivFell;
    int MaxExalted => (int)TechData.D("apotheosis", "maxExaltedCivs");

    void ApoStep(Civ civ, double dy)
    {
        civ.Strain = Math.Max(0, civ.Strain - dy);
        if (civ.Exalted) { ExaltedStep(civ, dy); return; }
        if (civ.Fallen) return;
        int score = civ.ApoScore;
        if (score < TechData.D("apotheosis", "minScore")) return;
        if (Year < civ.NextApoRoll) return;
        civ.NextApoRoll = Year + TechData.D("apotheosis", "rollEveryYears");
        double ethic = civ.Ethic switch { "Reverent" => 1.2, "Ruthless" => 0.8, _ => 1.0 };
        double luck = (0.5 + civ.Faith) * ethic * Math.Max(0, 1 - civ.Meter("Atrocity") / 100.0);
        double chance = Math.Min(0.25, 0.004 * score * luck);
        if (_r.NextDouble() < chance && Civs.Count(c => c.Exalted) < MaxExalted) { Exalt(civ, null); return; }
        // reaching too high without the faith (or with the blood) to carry it: the fall chance is spread over a century of rolls
        if (score >= TechData.D("apotheosis", "onFailAtHighScore", "minScore") && luck < 1.0
            && _r.NextDouble() < TechData.D("apotheosis", "onFailAtHighScore", "fallChance") * 0.25 * (1.0 - luck))
            Fall(civ, "They reached for something too large, and it did not hold them.");
    }

    /// <summary>A civ becomes Exalted: by its own apotheosis, or because the voice lifted it.</summary>
    public bool Exalt(Civ civ, string byVoice)
    {
        if (civ.Exalted || civ.Dead) return false;
        civ.Exalted = true; civ.Fallen = false;
        civ.NextExaltedAct = Year + 5;
        civ.VoiceRelation = byVoice != null ? 40 : 10;
        civ.ComputeMods();
        var h = civ.Colonies.FirstOrDefault();
        string text = byVoice ?? $"{Narrative2.Called(civ, true)} step halfway out of the world. Their worlds become sacred ground; their ships carry a light that was not built into them. They are not gods. They are the nearest thing.";
        Log(h, text, "omen", null);
        Milestone(civ.Main ? HomeColony : h, civ.Main ? "Exaltation" : $"{Cap(Narrative2.Called(civ))} Exalted", $"Year {Year:0}. {text}", civ.Main ? "seat" : "none");
        CivExalted?.Invoke(civ);
        return true;
    }

    /// <summary>Strip an Exalted civ back to mortal, or make it Fallen.</summary>
    public void CastDown(Civ civ, bool fallen)
    {
        if (fallen) { Fall(civ, "The voice takes back what it never gave. They will be a long time recovering."); Witness(civ.Colonies.FirstOrDefault(), true, 1.5f); return; }
        if (!civ.Exalted) return;
        civ.Exalted = false;
        civ.ComputeMods();
        Log(civ.Colonies.FirstOrDefault(), $"The light goes out of {Narrative2.Called(civ)}'s ships, all at once. Their worlds are only worlds again.", "god", null);
        Witness(civ.Colonies.FirstOrDefault(), true, 1f);
    }

    void Fall(Civ civ, string why)
    {
        if (civ.Fallen) return;
        civ.Exalted = false; civ.Fallen = true;
        civ.Vigour *= 0.3f;
        civ.ComputeMods();
        var h = civ.Colonies.FirstOrDefault();
        Log(h, $"{Narrative2.Called(civ, true)} fall. {why} They stop sending seed ships. Their research slows to a crawl. They will be like the Vessh now: old, proud, and finished growing.", "loss", null);
        if (civ.Main) Milestone(HomeColony, "The Fall", $"Year {Year:0}. {why}", "seat");
        CivFell?.Invoke(civ);
    }

    /// <summary>An Exalted civ's lesser acts, every 15-40 years, within its own reach.</summary>
    void ExaltedStep(Civ civ, double dy)
    {
        // it answers the voice if the voice has hurt it, against whoever the voice seems to favour
        if (civ.VoiceRelation < -50 && _r.NextDouble() < 0.05 * dy)
        {
            var fav = Civs.Where(c => c != civ && !c.Dead && c.Colonies.Count > 0 && !c.Exalted).OrderByDescending(c => c.Faith).FirstOrDefault();
            if (fav != null)
            {
                var t = fav.Colonies.OrderBy(c => c.Star.Pos.DistanceTo(civ.Colonies[0].Star.Pos)).First();
                t.Capture *= 0.8;
                civ.VoiceRelation += 20;
                Log(t, $"{Narrative2.Called(civ, true)} answer the voice the only way they can reach it: through those it favours. A fifth of {t.Sys.Name}'s swarm burns.", "loss", null);
            }
        }
        if (Year < civ.NextExaltedAct) return;
        civ.NextExaltedAct = Year + 15 + _r.Next(26);
        var h = civ.Colonies.FirstOrDefault();
        if (h == null) return;
        var reach = Colonies.Where(c => c.Civ == civ || civ.Colonies.Any(o => o.Star.Pos.DistanceTo(c.Star.Pos) < 40)).ToList();
        var choices = new List<string> { "seed", "ignite", "orbit", "uplift" };
        if (civ.Ethic != "Ruthless") choices.Add("gift");
        if (civ.Ethic != "Reverent") choices.Add("smite");
        string act = choices[_r.Next(choices.Count)];
        bool done = false;
        switch (act)
        {
            case "seed":
                var sc = reach.FirstOrDefault(c => c.Sys.All().Any(Seedable));
                if (sc != null) done = SeedLife(sc, sc.Sys, sc.Sys.All().First(Seedable), civ);
                break;
            case "ignite":
                var ic = reach.FirstOrDefault(c => c.Sys.Bodies.Any(Ignitable));
                if (ic != null) done = Ignite(ic, ic.Sys, ic.Sys.Bodies.First(Ignitable), civ);
                break;
            case "orbit":
                var oc = civ.Colonies.FirstOrDefault(c => c.Sys.Bodies.Any(b => b != c.Seat && Movable(c.Sys, b)));
                if (oc != null) done = ShiftOrbit(oc, oc.Sys, oc.Sys.Bodies.First(b => b != oc.Seat && Movable(oc.Sys, b)), civ);
                break;
            case "uplift":
                var uc = reach.FirstOrDefault(c => c.Natives.Values.Any(n => n.Contact != "cleared" && n.Level < 3));
                if (uc != null)
                {
                    var (b, n) = uc.Natives.First(kv => kv.Value.Contact != "cleared" && kv.Value.Level < 3);
                    n.Level = 3; uc.Pop += 0.5;
                    Log(uc, $"{Narrative2.Called(civ, true)} touch the {n.Name} of {b.Name}, and in a single night they understand fire, writing and orbit.", "omen", b);
                    done = true;
                }
                break;
            case "gift":
                var friend = Known(civ).Where(o => civ.Relations.GetValueOrDefault(o.Id) > 30).OrderByDescending(o => civ.Relations.GetValueOrDefault(o.Id)).FirstOrDefault();
                var gt = friend == null ? null : civ.Techs.Select(Defs.TechById).Where(t => Available(friend, t)).OrderByDescending(t => t.Tier).FirstOrDefault();
                if (gt != null)
                {
                    CompleteTech(friend, gt, true);
                    Log(friend.Colonies[0], $"{Narrative2.Called(civ, true)} give {Narrative2.Called(friend)} {gt.Name}, the way a parent gives a child a lamp.", "omen", null);
                    done = true;
                }
                break;
            case "smite":
                var foe = Known(civ).Where(o => civ.Relations.GetValueOrDefault(o.Id) < -30).OrderBy(o => civ.Relations.GetValueOrDefault(o.Id)).FirstOrDefault();
                var fc = foe?.Colonies.OrderBy(c => c.Star.Pos.DistanceTo(h.Star.Pos)).FirstOrDefault(c => civ.Colonies.Any(o => o.Star.Pos.DistanceTo(c.Star.Pos) < 40));
                if (fc != null)
                {
                    fc.Capture *= 0.75;
                    Log(fc, $"{Narrative2.Called(civ, true)} turn their light on {fc.Sys.Name}. A quarter of its swarm is gone.", "loss", null);
                    done = true;
                }
                break;
        }
        if (!done) return;
        civ.Strain += 5;
        if (civ.Strain > 40 && _r.NextDouble() < 0.15) Fall(civ, "They acted like gods too often, and it hollowed them out.");
    }
}
