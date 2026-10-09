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

    /// <summary>
    /// Ratchet (2026-10-09): a static List of wrecks in Combat was shared by the soak's parallel matches and crashed
    /// 391 of 400 with "collection was modified". Sim state and caches live on WorldState; no static mutable
    /// collection fields in sim sources.
    /// </summary>
    [Fact]
    public void Sim_sources_declare_no_static_mutable_collections()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "verify", "thresholds.toml")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var field = new System.Text.RegularExpressions.Regex(@"static\s+(readonly\s+)?(List|Dictionary|HashSet|Queue|Stack|SortedSet)<[^>(]*>\s+\w+\s*(=|;)");
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root!.FullName, "sim"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var match = field.Match(File.ReadAllText(file));
            Assert.False(match.Success, $"{file}: static mutable collection '{match.Value}' (put it on WorldState)");
        }
    }
}
