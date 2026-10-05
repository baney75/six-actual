using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SixActual.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FloatInStateAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SIM01";

    static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Floating point in sim state",
        "Sim state cannot use {0}. Store SixActual.Sim.Fp or an integer instead.",
        "Determinism",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types under sim/State are part of the deterministic simulation and must not contain binary floating-point values.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }

    static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (!IsState(context.Node))
        {
            return;
        }

        foreach (var typeSyntax in context.Node.DescendantNodes().OfType<TypeSyntax>())
        {
            var type = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
            if (type is null)
            {
                continue;
            }

            if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, typeSyntax.GetLocation(), type.ToDisplayString()));
            }
        }

        foreach (var literal in context.Node.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (literal.Token.Value is float or double)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation(), literal.Token.Value.GetType().Name));
            }
        }
    }

    static bool IsState(SyntaxNode node)
    {
        var path = node.SyntaxTree.FilePath.Replace('\\', '/');
        if (path.Contains("/State/", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var ns in node.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>())
        {
            if (ns.Name.ToString().Contains("SixActual.Sim.State", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
