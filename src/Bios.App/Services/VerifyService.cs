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
            {
                // option target "[code]..." — current state must start with that code
                ok = actual.StartsWith(target, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // value target — compare ignoring the delimiter style (<>, quotes, or bare)
                string a = actual.Trim().Trim('<', '>', '"').Trim();
                string t = target.Trim().Trim('<', '>', '"').Trim();
                ok = string.Equals(a, t, StringComparison.OrdinalIgnoreCase);
            }

            row.VerifyStatus = ok ? "ok" : "mismatch";
        }
    }
}
