using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Bios.App.Services;

/// <summary>
/// Extracts the embedded SCEWIN binaries and drives export (/O /S) and import (/I /S).
/// The app ships as a single .exe; SCEWIN_64.exe + amifldrv64.sys + amigendrv64.sys are
/// embedded resources extracted to %LOCALAPPDATA%\BiosTuner\scewin on first use.
/// </summary>
public sealed class ScewinRunner
{
    private static readonly (string resource, string file)[] Payload =
    {
        ("Bios.App.scewin.SCEWIN_64.exe", "SCEWIN_64.exe"),
        ("Bios.App.scewin.amifldrv64.sys", "amifldrv64.sys"),
        ("Bios.App.scewin.amigendrv64.sys", "amigendrv64.sys"),
    };

    public string ScewinDir { get; }
    public string ScewinExe { get; }

    public ScewinRunner(string baseDir)
    {
        ScewinDir = Path.Combine(baseDir, "scewin");
        ScewinExe = Path.Combine(ScewinDir, "SCEWIN_64.exe");
    }

    /// <summary>Write the embedded SCEWIN payload to disk if missing or size-mismatched.</summary>
    public void EnsureExtracted()
    {
        Directory.CreateDirectory(ScewinDir);
        var asm = Assembly.GetExecutingAssembly();

        foreach (var (resource, file) in Payload)
        {
            using var stream = asm.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resource}");
            string dest = Path.Combine(ScewinDir, file);

            if (File.Exists(dest) && new FileInfo(dest).Length == stream.Length)
                continue;

            using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.CopyTo(fs);
        }
    }

    public sealed record RunResult(int ExitCode, string StdOut, string StdErr);

    private RunResult Run(string args, int timeoutMs = 120_000)
    {
        EnsureExtracted();

        var psi = new ProcessStartInfo
        {
            FileName = ScewinExe,
            Arguments = args,
            WorkingDirectory = ScewinDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = new Process { StartInfo = psi };
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(timeoutMs))
        {
            try { proc.Kill(true); } catch { /* ignore */ }
            throw new TimeoutException("SCEWIN did not finish in time.");
        }
        proc.WaitForExit();

        return new RunResult(proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Export all Setup variables to a text file (read-only operation).</summary>
    public RunResult Export(string outFile)
    {
        var r = Run($"/O /S \"{outFile}\"");
        if (r.ExitCode != 0 || !File.Exists(outFile))
            throw new InvalidOperationException($"SCEWIN export failed (rc={r.ExitCode}).\n{r.StdErr}\n{r.StdOut}");
        return r;
    }

    /// <summary>Import a (possibly edited) export file. THIS WRITES UEFI VARIABLES.</summary>
    public RunResult Import(string inFile)
    {
        var r = Run($"/I /S \"{inFile}\"");
        if (r.ExitCode != 0)
            throw new InvalidOperationException($"SCEWIN import failed (rc={r.ExitCode}).\n{r.StdErr}\n{r.StdOut}");
        return r;
    }
}
