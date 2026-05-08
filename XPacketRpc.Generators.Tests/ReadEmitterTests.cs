using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class ReadEmitterTests
{
    private static (TypeWalker walker, INamedTypeSymbol type) Compile(string src, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        return (new TypeWalker(comp), comp.GetTypeByMetadataName(typeName)!);
    }

    [Test]
    public async Task Emits_parameterless_ctor_and_setters()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id { get; set; } public string Name { get; set; } = ""; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("new global::Foo()");
        await Assert.That(code).Contains(".Id =");
        await Assert.That(code).Contains(".Name =");
    }

    [Test]
    public async Task Emits_bitmap_read_for_nullable_fields()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id { get; set; } public string? Comment { get; set; } }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("byte bitmap = r.ReadByte();");
        await Assert.That(code).Contains("commentIsNull");
    }

    [Test]
    public async Task Emits_list_read()
    {
        var src = """
            #nullable enable
            using System.Collections.Generic;
            public class Foo { public List<int> Scores { get; set; } = new(); }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("ReadList<");
    }

    [Test]
    public async Task Emits_nested_DTO_call()
    {
        var src = """
            #nullable enable
            public class Inner { public int X { get; set; } }
            public class Outer { public Inner Child { get; set; } = new(); }
            """;
        var (walker, type) = Compile(src, "Outer");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("__XPRpcGen_Inner.Read(ref r)");
    }
}
