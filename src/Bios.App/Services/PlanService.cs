using Bios.App.Models;

namespace Bios.App.Services;

/// <summary>
/// Turns the user's selection into a concrete plan (current -> target) and, when applying,
/// rewrites the export lines.
///
/// The selection has two senses, and both write to the BIOS:
///   * a tweak left ON  -> its rules are written (the tweak's own target);
///   * a tweak turned OFF -> the settings it currently holds are restored to their BIOS default.
///
/// A tweak that is off and not currently applied produces nothing: the plan only ever contains
/// real changes.
/// </summary>
public static class PlanService
{
    /// <summary>Build the plan for the selected tweaks only (no revert pass).</summary>
    public static List<PlanRow> BuildPlan(
        IReadOnlyList<ScewinBlock> blocks,
        IList<string> lines,
        IEnumerable<(Tweak tweak, Rule rule)> ruleset,
        bool mutate)
        => BuildPlan(blocks, lines, ruleset, Array.Empty<(Tweak, Rule)>(), mutate);

    /// <summary>
    /// Build the full plan: <paramref name="apply"/> writes each tweak's target,
    /// <paramref name="revert"/> restores the BIOS default of every setting a turned-off tweak
    /// currently holds. Applied rules win: a block claimed by <paramref name="apply"/> is never
    /// reverted, so an enabled tweak and a disabled one can share a setting without fighting.
    /// When <paramref name="mutate"/> is true, <paramref name="lines"/> is rewritten in place.
    /// </summary>
    public static List<PlanRow> BuildPlan(
        IReadOnlyList<ScewinBlock> blocks,
        IList<string> lines,
        IEnumerable<(Tweak tweak, Rule rule)> apply,
        IEnumerable<(Tweak tweak, Rule rule)> revert,
        bool mutate)
    {
        var applyList = apply.ToList();
        var rows = new List<PlanRow>();

        // Blocks an enabled tweak is responsible for. Reverts must not touch them.
        var claimed = new HashSet<int>();
        foreach (var (_, rule) in applyList)
            foreach (var block in ScewinParser.FindMatches(blocks, rule))
                claimed.Add(block.Index);

        foreach (var (tweak, rule) in applyList)
            rows.AddRange(BuildApplyRows(blocks, lines, tweak, rule, mutate));

        var reverted = new HashSet<int>();
        foreach (var (tweak, rule) in revert)
            rows.AddRange(BuildRevertRows(blocks, lines, tweak, rule, claimed, reverted, mutate));

        return rows;
    }

    private static IEnumerable<PlanRow> BuildApplyRows(
        IReadOnlyList<ScewinBlock> blocks,
        IList<string> lines,
        Tweak tweak,
        Rule rule,
        bool mutate)
    {
        var matches = ScewinParser.FindMatches(blocks, rule);
        if (matches.Count == 0)
        {
            yield return new PlanRow
            {
                TweakId = tweak.Id,
                TweakName = tweak.Name,
                Question = rule.Question,
                Token = rule.Token ?? "",
                Offset = rule.Offset ?? "",
                Target = rule.Code is not null ? $"[{ScewinParser.NormalizeCode(rule.Code)}]" : (rule.Value ?? ""),
                Status = "skipped",
                Message = "Paramètre absent de ce BIOS",
                Reason = rule.Reason,
                WillChange = false
            };
            yield break;
        }

        foreach (var block in matches)
        {
            var row = NewRow(tweak, rule, block);

            if (rule.Code is not null)
            {
                string code = ScewinParser.NormalizeCode(rule.Code);
                row.Target = block.Describe(code);
                if (!block.OptionCodes.Contains(code))
                {
                    row.Status = "skipped";
                    row.Message = $"Option [{code}] absente de ce BIOS";
                    row.WillChange = false;
                }
                else
                {
                    row.WillChange = !string.Equals(block.SelectedCode, code, StringComparison.OrdinalIgnoreCase);
                    if (mutate)
                        ScewinParser.SetOption(lines, block, code);
                    row.Status = row.WillChange ? "applied" : "unchanged";
                    row.Message = row.WillChange ? "Option positionnée" : "Déjà à cette valeur";
                }
            }
            else if (rule.Value is not null)
            {
                string rendered = ScewinParser.RenderValue(block, rule.Value);
                row.Target = rendered;
                row.WillChange = !string.Equals(row.Old.Trim(), rendered.Trim(), StringComparison.OrdinalIgnoreCase);
                if (mutate)
                    ScewinParser.SetValue(lines, block, rule.Value);
                row.Status = row.WillChange ? "applied" : "unchanged";
                row.Message = row.WillChange ? "Valeur écrite" : "Déjà à cette valeur";
            }
            else
            {
                row.Status = "skipped";
                row.Message = "Règle sans code ni valeur";
                row.WillChange = false;
            }

            yield return row;
        }
    }

    /// <summary>
    /// Rows that undo one rule of a turned-off tweak. Only settings the BIOS currently holds at
    /// the tweak's value are touched — anything else was never applied, so there is nothing to undo.
    /// </summary>
    private static IEnumerable<PlanRow> BuildRevertRows(
        IReadOnlyList<ScewinBlock> blocks,
        IList<string> lines,
        Tweak tweak,
        Rule rule,
        HashSet<int> claimed,
        HashSet<int> reverted,
        bool mutate)
    {
        foreach (var block in ScewinParser.FindMatches(blocks, rule))
        {
            // An enabled tweak owns this setting, or another disabled tweak already restored it.
            if (claimed.Contains(block.Index) || reverted.Contains(block.Index))
                continue;

            // Not currently applied -> nothing to undo.
            if (!ScewinParser.Matches(block, rule))
                continue;

            var (source, code, value) = ScewinParser.ResolveDefault(block, rule);

            var row = NewRow(tweak, rule, block);
            row.IsRevert = true;
            row.DefaultSource = source switch
            {
                ScewinParser.DefaultSource.Catalog => "catalogue",
                ScewinParser.DefaultSource.Bios => "BIOS",
                ScewinParser.DefaultSource.Auto => "Auto",
                _ => "",
            };

            if (source == ScewinParser.DefaultSource.None)
            {
                // Never guess: leave the setting alone and say so.
                row.Target = "—";
                row.Status = "skipped";
                row.Message = "Ce BIOS n'expose aucune valeur par défaut";
                row.WillChange = false;
                yield return row;
                continue;
            }

            if (code is not null)
            {
                row.Target = block.Describe(code);
                row.WillChange = !string.Equals(block.SelectedCode, code, StringComparison.OrdinalIgnoreCase);
                if (row.WillChange && mutate)
                    ScewinParser.SetOption(lines, block, code);
            }
            else
            {
                string rendered = ScewinParser.RenderValue(block, value!);
                row.Target = rendered;
                row.WillChange = !string.Equals(row.Old.Trim(), rendered.Trim(), StringComparison.OrdinalIgnoreCase);
                if (row.WillChange && mutate)
                    ScewinParser.SetValue(lines, block, value!);
            }

            row.Status = row.WillChange ? "applied" : "unchanged";
            row.Message = row.WillChange
                ? $"Valeur par défaut rétablie (source : {row.DefaultSource})"
                : "Déjà à la valeur par défaut";

            if (row.WillChange)
                reverted.Add(block.Index);

            yield return row;
        }
    }

    private static PlanRow NewRow(Tweak tweak, Rule rule, ScewinBlock block) => new()
    {
        TweakId = tweak.Id,
        TweakName = tweak.Name,
        Question = block.Question,
        Token = block.Token,
        Offset = block.Offset,
        Line = block.Start + 1,
        Old = block.Selected,
        Reason = rule.Reason
    };

    /// <summary>Flatten tweaks into (tweak, rule) pairs in catalog order.</summary>
    public static IEnumerable<(Tweak, Rule)> Flatten(IEnumerable<Tweak> tweaks)
    {
        foreach (var t in tweaks)
            foreach (var r in t.Rules)
                yield return (t, r);
    }
}
