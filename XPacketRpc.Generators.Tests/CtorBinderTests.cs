using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Tests;

public class CtorBinderTests
{
    private static (Compilation comp, INamedTypeSymbol t) Compile(string src, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        return (comp, comp.GetTypeByMetadataName(typeName)!);
    }

    [Test]
    public async Task Parameterless_ctor_chosen_when_available()
    {
        var src = "public class Foo { public int X { get; set; } }";
        var (comp, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(comp));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.Parameterless);
        await Assert.That(plan.CtorParams.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Record_primary_ctor_chosen_for_immutable()
    {
        var src = """
            public record class Foo(int X, string Name);
            """;
        var (comp, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(comp));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.AllParams);
        var paramNames = plan.CtorParams.Select(p => p.Name).ToArray();
        await Assert.That(paramNames).IsEquivalentTo(new[] { "X", "Name" });
    }

    [Test]
    public async Task Mixed_ctor_plus_init_setters()
    {
        var src = """
            #nullable enable
            public class Foo
            {
                public int X { get; }
                public string? Comment { get; init; }
                public Foo(int x) { X = x; }
            }
            """;
        var (comp, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(comp));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.Mixed);
        var paramNames = plan.CtorParams.Select(p => p.Name).ToArray();
        await Assert.That(paramNames).IsEquivalentTo(new[] { "X" });
        var setterNames = plan.SetterMembers.Select(m => m.Name).ToArray();
        await Assert.That(setterNames).IsEquivalentTo(new[] { "Comment" });
    }

    [Test]
    public async Task Returns_diagnostic_when_no_ctor_covers_immutable_member()
    {
        var src = """
            public class Foo
            {
                public int X { get; }
            }
            """;
        var (comp, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(comp));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.Impossible);
    }
}
