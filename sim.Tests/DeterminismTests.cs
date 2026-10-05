namespace SixActual.Sim.Tests;

public class DeterminismTests
{
    [Fact]
    public void Report_is_stable_inside_one_process()
    {
        var first = string.Join('\n', DeterminismReport.Lines());
        var second = string.Join('\n', DeterminismReport.Lines());
        Assert.Equal(first, second);
        Assert.Contains("replay=ok", first, StringComparison.Ordinal);
        Assert.Contains("lockstep_clients=8", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_matches_the_golden_file()
    {
        var path = Path.Combine(FindRepoRoot(), "verify", "golden", "determinism.txt");
        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        var actual = string.Join('\n', DeterminismReport.Lines());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Two_processes_print_the_same_determinism_report()
    {
        var first = RunHeadless();
        var second = RunHeadless();
        Assert.Equal(first, second);
        Assert.Equal(string.Join('\n', DeterminismReport.Lines()) + "\n", first);
    }

    static string RunHeadless()
    {
        var repo = FindRepoRoot();
        var project = Path.Combine(repo, "sim", "Headless", "SixActual.Headless.csproj");
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("Release");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--determinism");
        using var process = System.Diagnostics.Process.Start(start);
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, stderr);
        return stdout;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "verify", "thresholds.toml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
