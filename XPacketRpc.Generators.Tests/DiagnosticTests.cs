using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators;

namespace XPacketRpc.Generators.Tests;

public class DiagnosticTests
{
    private static (Compilation comp, IEnumerable<Diagnostic> diags) Run(string userSource)
    {
        var fakeRuntime = """
            #nullable enable
            using System;
            using System.Buffers;
            namespace XPacketRpc
            {
                public interface IRpcSerializer { string ContentType { get; } byte[] Serialize<T>(T v); T? Deserialize<T>(ReadOnlyMemory<byte> p); }
                public ref struct XPRpcReader { public XPRpcReader(ReadOnlySpan<byte> s) {} }
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
        var run = driver.RunGenerators(comp).GetRunResult();
        return (comp, run.Diagnostics);
    }

    [Test]
    public async Task XPRPC001_open_generic_call_site()
    {
        var src = """
            using XPacketRpc;
            public static class Foo { public static void Generic<T>() => XPRpc.Touch<T>(); }
            """;
        var (_, diags) = Run(src);

        await Assert.That(diags.Any(d => d.Id == "XPRPC001")).IsTrue();
    }
}
