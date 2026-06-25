using System.Text.RegularExpressions;
using Bios.App.Models;

namespace Bios.App.Services;

/// <summary>
/// Parser/rewriter for AMI SCEWIN exports. Faithful C# port of bios_profile_tool.py
/// (parse_blocks / find_matches / set_option / set_value / render_value).
/// Edits are strictly line-in-place: line count never changes, so block ranges stay valid.
/// </summary>
public static partial class ScewinParser
{
    [GeneratedRegex(@"^Setup Question\s*=\s*(?<value>.*)$")]
    private static partial Regex SetupRe();

    [GeneratedRegex(@"^(?<key>Help String|Token|Offset|Width|BIOS Default|Value)\s*=\s*(?<value>.*)$")]
    private static partial Regex FieldRe();

    [GeneratedRegex(@"^(?<prefix>(?:Options\s*=\s*)?\s*)(?<star>\*)?\[(?<code>[0-9A-Fa-f]+)\](?<label>.*?)(?:\s*//.*)?$")]
    private static partial Regex OptionRe();

    [GeneratedRegex(@"^(?<prefix>Value\s*=\s*)(?<value>.*?)(?<comment>\s*//.*)?$")]
    private static partial Regex ValueRe();

    private static string Clean(string raw)
    {
        raw = raw.Trim();
        int idx = raw.IndexOf("//", StringComparison.Ordinal);
        if (idx >= 0)
            raw = raw[..idx].TrimEnd();
        return raw;
    }

    public static List<ScewinBlock> ParseBlocks(IReadOnlyList<string> lines)
    {
        var blocks = new List<ScewinBlock>();
        var starts = new List<(int idx, string question)>();

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                continue;
            var m = SetupRe().Match(line);
            if (m.Success)
                starts.Add((i, Clean(m.Groups["value"].Value)));
        }

        for (int b = 0; b < starts.Count; b++)
        {
            int start = starts[b].idx;
            int end = (b + 1 < starts.Count) ? starts[b + 1].idx : lines.Count;
            var block = new ScewinBlock
            {
                Index = b,
                Start = start,
                End = end,
                Question = starts[b].question
            };

            for (int i = start; i < end; i++)
            {
                string line = lines[i];

                var field = FieldRe().Match(line);
                if (field.Success)
                {
                    string key = field.Groups["key"].Value;
                    string value = Clean(field.Groups["value"].Value);
                    switch (key)
                    {
                        case "Token": block.Token = value; break;
                        case "Offset": block.Offset = value; break;
                        case "Width": block.Width = value; break;
                        case "Value": block.Value = value; break;
                    }
                    continue;
                }

                var option = OptionRe().Match(line.Trim());
                if (option.Success)
                {
                    string code = option.Groups["code"].Value.ToUpperInvariant();
                    block.OptionCodes.Add(code);
                    if (option.Groups["star"].Success)
                    {
                        block.SelectedCode = code;
                        block.SelectedLabel = Clean(option.Groups["label"].Value);
                    }
                }
            }

            blocks.Add(block);
        }

        return blocks;
    }

    public static string NormalizeCode(string code)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.StartsWith("0X", StringComparison.Ordinal))
            code = code[2..];
        return code;
    }

    public static List<ScewinBlock> FindMatches(IReadOnlyList<ScewinBlock> blocks, Rule rule)
    {
        if (string.IsNullOrEmpty(rule.Question))
            throw new ArgumentException("Rule is missing question.");

        var matches = blocks.Where(x => x.Question == rule.Question).ToList();

        if (!string.IsNullOrEmpty(rule.Token))
            matches = matches.Where(x => string.Equals(x.Token, rule.Token, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrEmpty(rule.Offset))
            matches = matches.Where(x => string.Equals(x.Offset, rule.Offset, StringComparison.OrdinalIgnoreCase)).ToList();

        if (rule.Occurrence is int occ)
            matches = (occ >= 1 && occ <= matches.Count) ? matches.GetRange(occ - 1, 1) : new List<ScewinBlock>();

        return matches;
    }

    public static (bool touched, string message) SetOption(IList<string> lines, ScewinBlock block, string code)
    {
        code = NormalizeCode(code);
        if (!block.OptionCodes.Contains(code))
            return (false, $"option [{code}] not present");

        string esc = Regex.Escape(code);
        bool touched = false;

        for (int idx = block.Start; idx < block.End; idx++)
        {
            string line = lines[idx];
            var option = OptionRe().Match(line.Trim());
            if (!option.Success)
                continue;

            string lineCode = option.Groups["code"].Value.ToUpperInvariant();
            if (lineCode == code)
            {
                if (line.TrimStart().StartsWith("Options", StringComparison.Ordinal))
                    lines[idx] = Regex.Replace(line, @"(Options\s*=\s*)\*?(\[" + esc + @"\])", "$1*$2", RegexOptions.None);
                else
                    lines[idx] = Regex.Replace(line, @"^(\s*)\*?(\[" + esc + @"\])", "$1*$2", RegexOptions.None);
                touched = true;
            }
            else
            {
                lines[idx] = Regex.Replace(line, @"(Options\s*=\s*)\*(\[)", "$1$2", RegexOptions.None);
                lines[idx] = Regex.Replace(lines[idx], @"^(\s*)\*(\[)", "$1$2", RegexOptions.None);
            }
        }

        return (touched, touched ? "option set" : "option line not touched");
    }

    public static string RenderValue(ScewinBlock block, string value)
    {
        // Normalise the incoming value: strip any delimiters the caller may have supplied.
        string raw = value.Trim().Trim('<', '>').Trim('"').Trim();

        // Match the existing field's delimiter convention so the written line stays
        // byte-compatible with what SCEWIN exported. Fields come in three flavours:
        //   Value ="Auto"   (quoted string)
        //   Value =<48>     (angle-bracketed numeric)
        //   Value =0        (bare numeric/hex, e.g. Enable Hibernation, SMART Self Test)
        string current = block.Value.Trim();
        if (current.StartsWith("\"", StringComparison.Ordinal))
            return "\"" + raw + "\"";
        if (current.StartsWith("<", StringComparison.Ordinal))
            return "<" + raw + ">";
        return raw;
    }

    public static (bool touched, string message, string rendered) SetValue(IList<string> lines, ScewinBlock block, string value)
    {
        string rendered = RenderValue(block, value);
        bool touched = false;

        for (int idx = block.Start; idx < block.End; idx++)
        {
            var m = ValueRe().Match(lines[idx]);
            if (!m.Success)
                continue;
            string comment = m.Groups["comment"].Success ? m.Groups["comment"].Value : "";
            lines[idx] = $"{m.Groups["prefix"].Value}{rendered}{comment}";
            touched = true;
        }

        return (touched, touched ? "value set" : "value line not present", rendered);
    }
}
