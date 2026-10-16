// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_metaclass.py, tests/test_decorator_strategy.py, tests/test_pure_python_method_rejection.py

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nooa.Strategies;

namespace Nooa.Generators.Tests;

/// <summary>
/// Tests for <see cref="AgentGenerator"/>. Upstream tests of decorator wrapping, tracing and runtime behaviour do not
/// apply to a source generator (see <c>docs/porting-notes.md</c>). These tests port the cases about declaration
/// rules: generator methods, stacked <c>@strategy</c> and non-LLM methods.
/// </summary>
public class AgentGeneratorTests
{
    private const string _snapshotSource = """
        namespace Shop.Agents;

        /// <summary>You help with {Topic}.</summary>
        [AgentOptions(Llm = "small")]
        public partial class Helper : Base
        {
            public string Topic = "tax";

            /// <summary>Does the work for "{Topic}".</summary>
            /// <param name="query">What to do.</param>
            [Agentic]
            public partial Task<int> RunAsync(string query, CancellationToken cancellationToken);
        }
        """;

    [Fact]
    public void Generate_OneAgent_MatchesSnapshot()
    {
        var result = GeneratorHost.Run(_snapshotSource, assemblyName: "Fixture");

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Equal(["Nooa.ModuleInitializer.g.cs", "global__Shop_Agents_Helper.Nooa.g.cs"], result.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Snapshots.Helper + "\n", result.Generated);
        Assert.Equal(Snapshots.ModuleInitializer + "\n", result.Sources["Nooa.ModuleInitializer.g.cs"]);
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
    }

    [Theory]
    [InlineData(DocumentationMode.None)]
    [InlineData(DocumentationMode.Parse)]
    [InlineData(DocumentationMode.Diagnose)]
    public async Task Generate_AnyDocumentationMode_EmbedsDocComments(DocumentationMode mode)
    {
        var result = GeneratorHost.Run(_snapshotSource, mode);
        var assembly = GeneratorHost.Load(result);

        var agent = Activator.CreateInstance(assembly.GetType("Shop.Agents.Helper")!)!;
        await (Task<int>)agent.GetType().GetMethod("RunAsync")!.Invoke(agent, ["q", CancellationToken.None])!;

        var call = (CurrentCall)assembly.GetType("Base")!.GetField("Last")!.GetValue(null)!;
        Assert.Equal("Does the work for \"tax\".", call.Docstring);
    }

    [Fact]
    public void Generate_WithoutNooaAttributes_EmitsNothing()
    {
        var result = GeneratorHost.Run("public class Plain { public int X; }");

        Assert.Empty(result.Sources);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void Generate_AgentOptionsWithoutMethods_StillRegistersMetadataAndDocs()
    {
        var result = GeneratorHost.Run("""
            /// <summary>Only a prompt.</summary>
            [AgentOptions(Llm = "m")]
            public partial class Quiet : Base
            {
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("AgentMetadata(typeof(Quiet), \"m\"", result.Generated, StringComparison.Ordinal);
        GeneratorHost.Load(result);
    }

    [Fact]
    public void Generate_MethodWithoutDocComment_LeavesDocstringNull()
    {
        var result = GeneratorHost.Run("""
            public partial class A : Base
            {
                [Agentic]
                public partial Task GoAsync();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.DoesNotContain("Docstring", result.Generated, StringComparison.Ordinal);
        GeneratorHost.Load(result);
    }

    [Fact]
    public void Generate_NestedClassInPartialOuter_Compiles()
    {
        var result = GeneratorHost.Run("""
            namespace N;

            public partial class Outer
            {
                /// <summary>Inner.</summary>
                internal partial class Inner : Base
                {
                    /// <summary>Go.</summary>
                    [Agentic]
                    internal virtual partial Task<string> GoAsync(int a, string[] b, System.Collections.Generic.List<int?> c);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("partial class Outer", result.Generated, StringComparison.Ordinal);
        Assert.Contains("internal virtual async partial", result.Generated, StringComparison.Ordinal);
        GeneratorHost.Load(result);
    }

    [Fact]
    public void Generate_OverloadsAndParamsAndKeywordNames_Compile()
    {
        var result = GeneratorHost.Run("""
            public partial class A : Base
            {
                /// <summary>One.</summary>
                [Agentic]
                public partial Task<int> GoAsync(string text);

                /// <summary>Two.</summary>
                [Agentic]
                public partial Task<int> GoAsync(int number, params string[] rest);

                /// <summary>Three.</summary>
                [Agentic]
                public partial ValueTask<bool> Check(string @class, int @int);
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        GeneratorHost.Load(result);
    }

    [Fact]
    public async Task Generate_DocTextWithQuotesAndControlCharacters_IsEscapedNotInjected()
    {
        var text = "Say \"hi\" \\n \\\\ \"\"\" tab\there /* c */ // c {{ }} end.";
        var result = GeneratorHost.Run(
            "public partial class A : Base\n{\n/// <summary>" + text + "</summary>\n[Agentic]\npublic partial Task GoAsync();\n}\n");
        var assembly = GeneratorHost.Load(result);

        var agent = Activator.CreateInstance(assembly.GetType("A")!)!;
        await (Task)agent.GetType().GetMethod("GoAsync")!.Invoke(agent, null)!;

        var call = (CurrentCall)assembly.GetType("Base")!.GetField("Last")!.GetValue(null)!;
        Assert.Equal(text.Replace("{{", "{", StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal), call.Docstring);
    }

    [Fact]
    public void Generate_LlmWithQuotesAndLineSeparators_IsEscapedNotInjected()
    {
        const string llm = "a\"b\\c\u2028d\ne";
        var result = GeneratorHost.Run("[AgentOptions(Llm = \"a\\\"b\\\\c\\u2028d\\ne\")] public partial class A : Base { }");
        var assembly = GeneratorHost.Load(result);

        var type = assembly.GetType("A")!;
        Activator.CreateInstance(type);

        Assert.True(AgentMetadataRegistry.TryGet(type, out var metadata));
        Assert.Equal(llm, metadata!.Llm);
    }

    [Fact]
    public void Generate_DtdInDocComment_IsIgnored()
    {
        var result = GeneratorHost.Run("""
            public partial class A : Base
            {
                /// <!DOCTYPE d [<!ENTITY e "SECRET">]><summary>&e;</summary>
                [Agentic]
                public partial Task GoAsync();
            }
            """);

        Assert.DoesNotContain("SECRET", result.Generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Docstring", result.Generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_PlaceholderPaths_ReadNestedMembersAndTolerateNull()
    {
        var result = GeneratorHost.Run("""
            public class Config { public string Model = "m1"; public Config? Next; }

            public partial class A : Base
            {
                private Config _config = new();
                protected int Count => 3;
                public Config? Missing;

                /// <summary>Model {_config.Model}, next {_config.Next.Model}, missing {Missing.Model}, count {Count}.</summary>
                [Agentic]
                public partial Task GoAsync();
            }
            """);
        var assembly = GeneratorHost.Load(result);

        var agent = Activator.CreateInstance(assembly.GetType("A")!)!;
        await (Task)agent.GetType().GetMethod("GoAsync")!.Invoke(agent, null)!;

        var call = (CurrentCall)assembly.GetType("Base")!.GetField("Last")!.GetValue(null)!;
        Assert.Equal("Model m1, next , missing , count 3.", call.Docstring);
    }

    [Fact]
    public async Task Generate_TrailingCancellationToken_FlowsAndIsNotAnArgument()
    {
        var result = GeneratorHost.Run("""
            public partial class A : Base
            {
                /// <summary>Go.</summary>
                [Agentic]
                public partial Task GoAsync(int a, CancellationToken token = default);
            }
            """);
        var assembly = GeneratorHost.Load(result);

        using var cts = new CancellationTokenSource();
        var agent = Activator.CreateInstance(assembly.GetType("A")!)!;
        await (Task)agent.GetType().GetMethod("GoAsync")!.Invoke(agent, [5, cts.Token])!;

        var call = (CurrentCall)assembly.GetType("Base")!.GetField("Last")!.GetValue(null)!;
        Assert.Equal(["a"], call.Arguments.Select(a => a.Name));
        Assert.Equal(cts.Token, (CancellationToken)assembly.GetType("Base")!.GetField("LastToken")!.GetValue(null)!);
    }

    [Theory]
    [InlineData("NOOA0001", "public class A : Base {\n/// <summary>x</summary>\n [Agentic] public Task GoAsync() => Task.CompletedTask; }")]
    [InlineData("NOOA0001", "public partial class A : Base { [Agentic] public partial Task GoAsync(); }\npublic class B { public partial class C : Base { [Agentic] public partial Task GoAsync(); } }")]
    [InlineData("NOOA0001", "[AgentOptions] public class A : Base { }")]
    [InlineData("NOOA0001", "public partial struct A { [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0002", "public partial class A : Base { [Agentic] public partial int Go(); }")]
    [InlineData("NOOA0002", "public partial class A : Base { [Agentic] public partial System.Collections.Generic.IAsyncEnumerable<int> Go(); }")]
    [InlineData("NOOA0002", "public partial class A : Base { [Agentic] public partial void Go(); }")]
    [InlineData("NOOA0003", "public partial class A : Base { [Agentic] public static partial Task GoAsync(); }")]
    [InlineData("NOOA0004", "public partial class A : Base { [Agentic] public partial Task<T> GoAsync<T>(); }")]
    [InlineData("NOOA0004", "public partial class A<T> : Base { [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0005", "public partial class A : Base { [Agentic] public partial Task GoAsync(ref int x); }")]
    [InlineData("NOOA0005", "public partial class A : Base { [Agentic] public partial Task GoAsync(dynamic x); }")]
    [InlineData("NOOA0006", "public partial class A : Base { [Agentic] public async Task GoAsync() => await Task.Yield(); }")]
    [InlineData("NOOA0006", "public partial class A : Base { [Agentic] public partial Task GoAsync(); public partial Task GoAsync() => Task.CompletedTask; }")]
    [InlineData("NOOA0007", "public partial class A : Base {\n/// <summary>{Nope}</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "public partial class A : Base { public int X;\n/// <summary>{X</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "public partial class A : Base { public int X;\n/// <summary>a } b</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "public partial class A : Base { public int X;\n/// <summary>{this.X}</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "public partial class A : Base { public int X;\n/// <summary>{X.ToString()}</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "public partial class A : Base { public static int X;\n/// <summary>{X}</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "public partial class A : Base { public System.Collections.Generic.List<int> X = new();\n/// <summary>{X.Item}</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "public partial class A : Base { public int X;\n/// <summary>Be {X.}</summary>\n [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0007", "/// <summary>{Nope}</summary>\n[AgentOptions] public partial class A : Base { }")]
    [InlineData("NOOA0008", "public partial class A { [Agentic] public partial Task GoAsync(); }")]
    [InlineData("NOOA0009", "public partial class Outer { private partial class A : Base { [Agentic] public partial Task GoAsync(); } }")]
    public void Generate_Misuse_ReportsDiagnostic(string id, string source)
    {
        var result = GeneratorHost.Run(source);

        Assert.Contains(id, result.GeneratorIds);
        Assert.All(result.GeneratorDiagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public void Generate_Misuse_PointsAtTheMethodName()
    {
        var result = GeneratorHost.Run("public partial class A : Base { [Agentic] public partial int GoAsync(); }");

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "NOOA0002");
        var snippet = result.Output.SyntaxTrees.Single(t => t.FilePath == "Snippet.cs").GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Equal("Snippet.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.Equal(snippet.IndexOf("GoAsync", StringComparison.Ordinal), diagnostic.Location.SourceSpan.Start);
        Assert.Equal(7, diagnostic.Location.SourceSpan.Length);
    }

    [Fact]
    public void Generate_ClassWithTypeErrors_EmitsNoSourceForIt()
    {
        var result = GeneratorHost.Run("public class A : Base { [Agentic] public partial Task GoAsync(); }");

        Assert.Empty(result.Sources);
    }

    [Fact]
    public void Generate_DuplicateDiagnosticsFromManyMethods_AreReportedOnce()
    {
        var result = GeneratorHost.Run("""
            public partial class A
            {
                [Agentic] public partial Task One();
                [Agentic] public partial Task Two();
            }
            """);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "NOOA0008");
    }

    [Fact]
    public void Generate_StackedStrategyAttributes_AreRejectedByTheCompiler()
    {
        var result = GeneratorHost.Run("""
            public partial class A : Base
            {
                [Agentic]
                [Strategy(typeof(string))]
                [Strategy(typeof(int))]
                public partial Task GoAsync();
            }
            """);

        Assert.Contains(result.CompilationDiagnostics, d => d.Id == "CS0579");
    }

    [Fact]
    public void Generate_NonAgenticMethodWithStrategy_NeedsNoBody()
    {
        var result = GeneratorHost.Run("""
            public partial class A : Base
            {
                [Strategy(typeof(string))]
                public Task<int> Plain() => Task.FromResult(1);
            }
            """);

        Assert.Empty(result.Sources);
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generate_SameCompilationTwice_ProducesIdenticalOutput()
    {
        var a = GeneratorHost.Run(_snapshotSource);
        var b = GeneratorHost.Run(_snapshotSource);

        Assert.Equal(a.Generated, b.Generated);
    }

    [Fact]
    public void Generate_UnrelatedEdit_IsCachedAndDoesNotRegenerate()
    {
        var first = GeneratorHost.CreateCompilation(_snapshotSource);
        var driver = GeneratorHost.CreateDriver().RunGenerators(first, TestContext.Current.CancellationToken);

        var edited = first.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { public int Z; }", new CSharpParseOptions(LanguageVersion.Latest), cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);

        var outputs = driver.GetRunResult().Results[0].TrackedOutputSteps.SelectMany(s => s.Value).SelectMany(s => s.Outputs).ToList();
        Assert.NotEmpty(outputs);
        Assert.All(outputs, o => Assert.True(o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, o.Reason.ToString()));
    }

    [Fact]
    public void Generate_EditOfAgentDoc_Regenerates()
    {
        var first = GeneratorHost.CreateCompilation(_snapshotSource);
        var driver = GeneratorHost.CreateDriver().RunGenerators(first, TestContext.Current.CancellationToken);

        var edited = GeneratorHost.CreateCompilation(_snapshotSource.Replace("Does the work", "Does more work", StringComparison.Ordinal));
        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);

        var text = driver.GetRunResult().GeneratedTrees.Single(t => t.FilePath.EndsWith("Helper.Nooa.g.cs", StringComparison.Ordinal)).ToString();
        Assert.Contains("Does more work", text, StringComparison.Ordinal);
    }
}
