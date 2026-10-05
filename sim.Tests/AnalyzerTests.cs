using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SixActual.Analyzers;

namespace SixActual.Sim.Tests;

public class AnalyzerTests
{
    [Fact]
    public async Task Float_in_state_is_an_error()
    {
        var diagnostics = await AnalyzeAsync(
            "/work/sim/State/Bad.cs",
            """
            namespace SixActual.Sim.State;
            public struct Bad
            {
                public float X;
                public double Y;
            }
            """);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == FloatInStateAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task Integer_state_is_clean()
    {
        var diagnostics = await AnalyzeAsync(
            "/work/sim/State/Good.cs",
            """
            namespace SixActual.Sim.State;
            public struct Good
            {
                public long X;
                public int Y;
            }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == FloatInStateAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task Float_literal_in_state_is_an_error()
    {
        var diagnostics = await AnalyzeAsync(
            "/work/sim/State/Literal.cs",
            """
            namespace SixActual.Sim.State;
            public struct LiteralState
            {
                public long X;
                public void Set() { X = (long)1.5; }
            }
            """);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == FloatInStateAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task Float_outside_state_is_ignored()
    {
        var diagnostics = await AnalyzeAsync(
            "/work/sim/View.cs",
            """
            namespace SixActual.View;
            public struct View
            {
                public float X;
            }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == FloatInStateAnalyzer.DiagnosticId);
    }

    static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string path, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: path);
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = trusted.Select(assembly => MetadataReference.CreateFromFile(assembly));
        var compilation = CSharpCompilation.Create(
            "AnalyzerProbe",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FloatInStateAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
