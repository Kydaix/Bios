using Bios.App.Models;

namespace Bios.App.Services;

/// <summary>Turns selected tweaks into a concrete plan (current -> target) and, when applying, rewrites the export lines.</summary>
public static class PlanService
{
    /// <summary>
    /// Build the plan for a set of (tweak, rule) pairs against parsed blocks.
    /// When <paramref name="mutate"/> is true, <paramref name="lines"/> is rewritten in place.
    /// </summary>
    public static List<PlanRow> BuildPlan(
        IReadOnlyList<ScewinBlock> blocks,
        IList<string> lines,
        IEnumerable<(Tweak tweak, Rule rule)> ruleset,
        bool mutate)
    {
        var rows = new List<PlanRow>();

        foreach (var (tweak, rule) in ruleset)
        {
            var matches = ScewinParser.FindMatches(blocks, rule);
            if (matches.Count == 0)
            {
                rows.Add(new PlanRow
                {
                    TweakId = tweak.Id,
                    TweakName = tweak.Name,
                    Question = rule.Question,
                    Token = rule.Token ?? "",
                    Offset = rule.Offset ?? "",
                    Target = rule.Code is not null ? $"[{ScewinParser.NormalizeCode(rule.Code)}]" : (rule.Value ?? ""),
                    Status = "skipped",
                    Message = "no matching block",
                    Reason = rule.Reason,
                    WillChange = false
                });
                continue;
            }

            foreach (var block in matches)
            {
                string old = block.Selected;
                var row = new PlanRow
                {
                    TweakId = tweak.Id,
                    TweakName = tweak.Name,
                    Question = block.Question,
                    Token = block.Token,
                    Offset = block.Offset,
                    Line = block.Start + 1,
                    Old = old,
                    Reason = rule.Reason
                };

                if (rule.Code is not null)
                {
                    string code = ScewinParser.NormalizeCode(rule.Code);
                    row.Target = $"[{code}]";
                    bool present = block.OptionCodes.Contains(code);
                    if (!present)
                    {
                        row.Status = "skipped";
                        row.Message = $"option [{code}] not present";
                        row.WillChange = false;
                    }
                    else
                    {
                        row.WillChange = !old.StartsWith($"[{code}]", StringComparison.OrdinalIgnoreCase);
                        if (mutate)
                            ScewinParser.SetOption(lines, block, code);
                        row.Status = row.WillChange ? "applied" : "unchanged";
                        row.Message = row.WillChange ? "option set" : "already set";
                    }
                }
                else if (rule.Value is not null)
                {
                    string rendered = ScewinParser.RenderValue(block, rule.Value);
                    row.Target = rendered;
                    row.WillChange = !string.Equals(old.Trim(), rendered.Trim(), StringComparison.OrdinalIgnoreCase);
                    if (mutate)
                        ScewinParser.SetValue(lines, block, rule.Value);
                    row.Status = row.WillChange ? "applied" : "unchanged";
                    row.Message = row.WillChange ? "value set" : "already set";
                }
                else
                {
                    row.Status = "skipped";
                    row.Message = "rule has neither code nor value";
                    row.WillChange = false;
                }

                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>Flatten selected tweaks into (tweak, rule) pairs in catalog order.</summary>
    public static IEnumerable<(Tweak, Rule)> Flatten(IEnumerable<Tweak> tweaks)
    {
        foreach (var t in tweaks)
            foreach (var r in t.Rules)
                yield return (t, r);
    }
}
