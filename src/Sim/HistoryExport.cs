using System;
using System.Linq;
using System.Text;

namespace Starforge.Sim;

/// <summary>
/// The chronicle as a book: the game's history written out as Markdown, a chapter a century, with the turning points
/// set apart and a closing table of how far humanity got. For sharing a game, or keeping it after the save is gone
/// (a save keeps only the last 800 lines of the chronicle).
/// </summary>
public static class HistoryExport
{
    static string Mark(string kind) => kind switch
    {
        "era" => "**", "omen" => "*", "god" => "*", _ => "",
    };

    public static string Markdown(Simulation sim, int seed)
    {
        var sb = new StringBuilder();
        var hu = sim.Humanity;
        sb.Append($"# A history of {Narrative2.Called(hu)}\n\n");
        sb.Append($"*Starforge, seed {seed}. From {(sim.Chronicle.Count > 0 ? sim.Chronicle[0].Year : sim.Year):0} to {sim.Year:0}.*\n\n");
        int century = -1;
        foreach (var e in sim.Chronicle)
        {
            int c = (int)Math.Floor(e.Year / 100);
            if (c != century)
            {
                century = c;
                sb.Append($"\n## The {Ordinal(c + 1)} century\n\n");
            }
            string who = e.Civ == null || e.Civ.Main ? "" : $"{e.Civ.Name}: ";
            string where = string.IsNullOrEmpty(e.System) ? "" : $"{e.System}. ";
            string m = Mark(e.Kind);
            sb.Append($"- **{e.Year:0}** {where}{who}{m}{e.Text.Trim()}{m}\n");
        }
        sb.Append("\n## Where it stands\n\n| | |\n|---|---|\n");
        sb.Append($"| Year | {sim.Year:0} |\n");
        sb.Append($"| Kardashev (galactic) | {Defs.Kardashev(hu.GalacticPower):0.000} |\n");
        sb.Append($"| People | {hu.FmtPop()} |\n");
        sb.Append($"| Systems | {hu.Colonies.Count} |\n");
        sb.Append($"| Technologies | {hu.Techs.Count} |\n");
        sb.Append($"| Council | the {hu.Faction(hu.Drive)} ({hu.Ethic}) |\n");
        int alive = sim.Civs.Count(c => !c.Dead && c.Colonies.Count > 0);
        sb.Append($"| Polities in the galaxy | {alive} ({sim.Civs.Count(c => c.Human && !c.Dead && c.Colonies.Count > 0)} human) |\n");
        sb.Append($"| Wars fought | {sim.Wars.Count} |\n");
        return sb.ToString();
    }

    static string Ordinal(int n)
    {
        string suf = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n + suf;
    }
}
