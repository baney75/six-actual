namespace SixActual.Sim.Tests;

public class SourceScanTests
{
    [Fact]
    public void Sim_sources_do_not_name_binary_floating_types()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "verify", "thresholds.toml")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var sim = Path.Combine(root!.FullName, "sim");
        foreach (var file in Directory.EnumerateFiles(sim, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            Assert.DoesNotContain("float", text, StringComparison.Ordinal);
            Assert.DoesNotContain("double", text, StringComparison.Ordinal);
        }
    }
}
