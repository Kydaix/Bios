using Bios.App.Models;

namespace Bios.App.Services;

/// <summary>Re-reads an export after an import and confirms each applied row reached its target.</summary>
public static class VerifyService
{
    public static void Verify(IReadOnlyList<string> afterLines, IReadOnlyList<PlanRow> appliedRows)
    {
        var blocks = ScewinParser.ParseBlocks(afterLines);
        var byKey = new Dictionary<(string q, string t, string o), List<ScewinBlock>>();
        foreach (var block in blocks)
        {
            var key = (block.Question, block.Token, block.Offset);
            if (!byKey.TryGetValue(key, out var list))
                byKey[key] = list = new List<ScewinBlock>();
            list.Add(block);
        }

        foreach (var row in appliedRows)
        {
            if (row.Status != "applied" && row.Status != "unchanged")
                continue;

            var key = (row.Question, row.Token, row.Offset);
            string actual = byKey.TryGetValue(key, out var matches) && matches.Count > 0 ? matches[0].Selected : "";
            row.Actual = actual;

            bool ok;
            string target = row.Target;
            if (target.StartsWith("[", StringComparison.Ordinal))
                ok = actual.StartsWith(target, StringComparison.OrdinalIgnoreCase);
            else if (target.StartsWith("<", StringComparison.Ordinal) || target.StartsWith("\"", StringComparison.Ordinal))
                ok = string.Equals(actual.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase);
            else
                ok = false;

            row.VerifyStatus = ok ? "ok" : "mismatch";
        }
    }
}
