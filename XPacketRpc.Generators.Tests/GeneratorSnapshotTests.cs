using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators;

namespace XPacketRpc.Generators.Tests;

public class GeneratorSnapshotTests
{
    private static GeneratorDriverRunResult Run(string userSource)
    {
        var fakeRuntime = """
            #nullable enable
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
                    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> w);
                    public delegate T ReadDelegate<T>(ref XPRpcReader r);
                    public static void Touch<T>() {}
                    public static void Register<T>(WriteDelegate<T> w, ReadDelegate<T> r) {}
                    public static void Write<T>(T value, IBufferWriter<byte> w) {}
                    public static T? Read<T>(ReadOnlySpan<byte> source) => default;
                }
            }
            """;
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(fakeRuntime),
            CSharpSyntaxTree.ParseText(userSource)
        };
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Buffers.IBufferWriter<>).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("UserAsm", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new XPacketRpcGenerator());
        return driver.RunGenerators(comp).GetRunResult();
    }

    [Test]
    public async Task Generator_emits_GenClass_for_discovered_DTO()
    {
        var src = """
            using XPacketRpc;
            public class Foo { public int X; }
            public static class Boot { public static void Init() => XPRpc.Touch<Foo>(); }
            """;
        var result = Run(src);
        var sources = result.GeneratedTrees.Select(t => t.GetText().ToString()).ToArray();

        await Assert.That(sources.Any(s => s.Contains("__XPRpcGen_Foo"))).IsTrue();
        await Assert.That(sources.Any(s => s.Contains("__XPRpcRegistry"))).IsTrue();
        await Assert.That(sources.Any(s => s.Contains("ModuleInitializer"))).IsTrue();
    }

    [Test]
    public async Task Generator_emits_no_DTO_class_when_no_call_sites()
    {
        var src = """
            public class Foo { public int X; }
            """;
        var result = Run(src);
        var sources = result.GeneratedTrees.Select(t => t.GetText().ToString()).ToArray();

        await Assert.That(sources.Any(s => s.Contains("__XPRpcGen_Foo"))).IsFalse();
    }
}
