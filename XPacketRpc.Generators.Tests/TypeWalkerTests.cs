using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Tests;

public class TypeWalkerTests
{
    private static (Compilation comp, INamedTypeSymbol root) Compile(string source, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs);
        var sym = comp.GetTypeByMetadataName(typeName) ?? throw new InvalidOperationException($"Type {typeName} not found");
        return (comp, sym);
    }

    [Test]
    public async Task Walks_primitive_fields_and_properties()
    {
        var src = """
            public class Foo
            {
                public int Id;
                public string Name { get; init; } = "";
                private int hidden;
                public static int StaticX;
            }
            """;
        var (comp, root) = Compile(src, "Foo");
        var walker = new TypeWalker(comp);
        var members = walker.GetMembers(root).Select(m => m.Name).ToArray();

        await Assert.That(members).IsEquivalentTo(new[] { "Id", "Name" });
    }

    [Test]
    public async Task Closure_includes_nested_DTO()
    {
        var src = """
            public class Inner { public int X; }
            public class Outer { public Inner Child = new(); public int Y; }
            """;
        var (comp, root) = Compile(src, "Outer");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        await Assert.That(closure).Contains("Outer");
        await Assert.That(closure).Contains("Inner");
    }

    [Test]
    public async Task Closure_includes_list_element_type()
    {
        var src = """
            using System.Collections.Generic;
            public class Item { public int X; }
            public class Cart { public List<Item> Items = new(); }
            """;
        var (comp, root) = Compile(src, "Cart");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        await Assert.That(closure).Contains("Cart");
        await Assert.That(closure).Contains("Item");
    }

    [Test]
    public async Task Closure_includes_dictionary_key_and_value()
    {
        var src = """
            using System.Collections.Generic;
            public class K { public int X; }
            public class V { public string Y = ""; }
            public class Map { public Dictionary<K, V> Data = new(); }
            """;
        var (comp, root) = Compile(src, "Map");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        await Assert.That(closure).Contains("Map");
        await Assert.That(closure).Contains("K");
        await Assert.That(closure).Contains("V");
    }

    [Test]
    public async Task Closure_does_not_recurse_into_builtin_types()
    {
        var src = """
            using System;
            public class Foo
            {
                public Guid Id;
                public DateTime When;
                public string Name = "";
            }
            """;
        var (comp, root) = Compile(src, "Foo");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        // only Foo (Guid/DateTime/string are built-in, no recursion)
        await Assert.That(closure).IsEquivalentTo(new[] { "Foo" });
    }
}
