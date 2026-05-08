using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class WriteEmitterTests
{
    private static (TypeWalker walker, INamedTypeSymbol type) Compile(string src, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var sym = comp.GetTypeByMetadataName(typeName)!;
        return (new TypeWalker(comp), sym);
    }

    [Test]
    public async Task Emits_primitive_writes_in_hash_sorted_order()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id; public string Name = ""; public bool Flag; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("WriteInt32LE(value.Id");
        await Assert.That(code).Contains("WriteString(value.Name");
        await Assert.That(code).Contains("WriteByte((byte)(value.Flag ? 1 : 0)");
    }

    [Test]
    public async Task Emits_nullability_bitmap_for_nullable_fields()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id; public string? Comment; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("byte bitmap = 0;");
        await Assert.That(code).Contains("value.Comment is null");
        await Assert.That(code).Contains("bitmap |=");
    }

    [Test]
    public async Task Emits_throw_for_required_reference_field()
    {
        var src = """
            #nullable enable
            public class Foo { public string Name = ""; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("ThrowNullRequired(\"Name\")");
    }

    [Test]
    public async Task Emits_list_loop()
    {
        var src = """
            #nullable enable
            using System.Collections.Generic;
            public class Foo { public List<int> Scores = new(); }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("WriteVarUInt32((uint)value.Scores.Count");
        await Assert.That(code).Contains("for (int i = 0;");
    }

    [Test]
    public async Task Emits_nested_DTO_call()
    {
        var src = """
            #nullable enable
            public class Inner { public int X; }
            public class Outer { public Inner Child = new(); }
            """;
        var (walker, type) = Compile(src, "Outer");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("__XPRpcGen_Inner.Write(value.Child");
    }
}
