using System.Globalization;
using System.IO;
using System.Text;
using Bios.App.Models;

namespace Bios.App.Services;

/// <summary>Owns the app data layout under %LOCALAPPDATA%\BiosTuner and writes per-run artifacts/backups.</summary>
public sealed class RunStore
{
    public string BaseDir { get; }
    public string RunsDir { get; }

    public RunStore()
    {
        BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BiosTuner");
        RunsDir = Path.Combine(BaseDir, "runs");
        Directory.CreateDirectory(RunsDir);
    }

    public string NewRunDir(string label)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string dir = Path.Combine(RunsDir, $"{stamp}_{label}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Path of the most recent backup recorded by an Apply (for one-click restore).</summary>
    public string LastBackupPointer => Path.Combine(BaseDir, "last_backup.txt");

    public void RecordLastBackup(string backupFilePath)
        => File.WriteAllText(LastBackupPointer, backupFilePath);

    public string? GetLastBackup()
    {
        if (!File.Exists(LastBackupPointer)) return null;
        string p = File.ReadAllText(LastBackupPointer).Trim();
        return File.Exists(p) ? p : null;
    }

    public static void WritePlanCsv(string path, IEnumerable<PlanRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("tweak,question,token,offset,line,old,target,status,will_change,reason,actual,verify_status");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Csv(r.TweakName), Csv(r.Question), Csv(r.Token), Csv(r.Offset),
                r.Line.ToString(CultureInfo.InvariantCulture),
                Csv(r.Old), Csv(r.Target), Csv(r.Status), r.WillChange ? "1" : "0",
                Csv(r.Reason), Csv(r.Actual), Csv(r.VerifyStatus)
            }));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Csv(string s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
