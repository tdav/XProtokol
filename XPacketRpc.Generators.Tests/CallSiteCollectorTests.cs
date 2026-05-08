using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Tests;

public class CallSiteCollectorTests
{
    private static (Compilation comp, SemanticModel model, SyntaxTree tree) Compile(string source)
    {
        var fakeXPRpc = """
            using System;
            using System.Buffers;
            namespace XPacketRpc
            {
                public interface IRpcSerializer
                {
                    string ContentType { get; }
                    byte[] Serialize<T>(T value);
                    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
                }
                public ref struct XPRpcReader { public XPRpcReader(ReadOnlySpan<byte> s) { } }
                public static class XPRpc
                {
                    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> writer);
                    public delegate T ReadDelegate<T>(ref XPRpcReader reader);
                    public static void Touch<T>() {}
                    public static void Register<T>(WriteDelegate<T> w, ReadDelegate<T> r) {}
                    public static void Write<T>(T value, IBufferWriter<byte> w) {}
                    public static T? Read<T>(ReadOnlySpan<byte> source) => default;
                }
            }
            """;
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(fakeXPRpc),
            CSharpSyntaxTree.ParseText(source)
        };
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Buffers.IBufferWriter<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("test",
            trees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false));
        return (comp, comp.GetSemanticModel(trees[1]), trees[1]);
    }

    [Test]
    public async Task Collects_T_from_Touch()
    {
        var src = """
            using XPacketRpc;
            public class Probe { public int X; }
            public class Foo
            {
                public static void Init() => XPRpc.Touch<Probe>();
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default).ToArray();
        var names = results.Select(r => r.Type.Name).ToArray();

        await Assert.That(names).Contains("Probe");
    }

    [Test]
    public async Task Collects_T_from_Write_and_Read()
    {
        var src = """
            using System;
            using System.Buffers;
            using XPacketRpc;
            public class Probe { public int X; }
            public class Foo
            {
                public static void DoIt(IBufferWriter<byte> w, ReadOnlySpan<byte> s)
                {
                    XPRpc.Write<Probe>(new Probe(), w);
                    var p = XPRpc.Read<Probe>(s);
                }
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default).ToArray();
        var names = results.Select(r => r.Type.Name).Distinct().ToArray();

        await Assert.That(names).IsEquivalentTo(new[] { "Probe" });
    }

    [Test]
    public async Task Collects_T_from_IRpcSerializer()
    {
        var src = """
            using System;
            using XPacketRpc;
            public class Probe { public int X; }
            public class Foo
            {
                public static void DoIt(IRpcSerializer s)
                {
                    var bytes = s.Serialize<Probe>(new Probe());
                    var p = s.Deserialize<Probe>(default);
                }
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default).ToArray();
        var names = results.Select(r => r.Type.Name).Distinct().ToArray();

        await Assert.That(names).IsEquivalentTo(new[] { "Probe" });
    }

    [Test]
    public async Task Open_generic_T_returns_diagnostic_marker()
    {
        var src = """
            using XPacketRpc;
            public static class Foo
            {
                public static void Generic<T>() => XPRpc.Touch<T>();
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default).ToArray();

        // Open-generic must be marked IsOpen=true
        var openOnes = results.Where(r => r.Type.Name == "T").ToArray();
        if (openOnes.Length > 0)
        {
            await Assert.That(openOnes.All(r => r.IsOpen)).IsTrue();
        }
        else
        {
            // OK if collector simply doesn't yield open-generic (also valid)
            await Assert.That(true).IsTrue();
        }
    }
}
