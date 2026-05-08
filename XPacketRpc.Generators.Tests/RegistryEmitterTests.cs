using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class RegistryEmitterTests
{
    [Test]
    public async Task Emits_module_initializer_with_per_type_register_calls()
    {
        var src = """
            public class A { public int X; }
            public class B { public int Y; }
            """;
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var comp = CSharpCompilation.Create("MyAsm", new[] { tree }, refs);

        var types = new[] { comp.GetTypeByMetadataName("A")!, comp.GetTypeByMetadataName("B")! };
        var emitter = new RegistryEmitter();
        var code = emitter.Emit(types, assemblyName: "MyAsm");

        await Assert.That(code).Contains("namespace XPacketRpc.Generated.MyAsm");
        await Assert.That(code).Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]");
        await Assert.That(code).Contains("global::XPacketRpc.XPRpc.Register<global::A>");
        await Assert.That(code).Contains("global::XPacketRpc.XPRpc.Register<global::B>");
        await Assert.That(code).Contains("__XPRpcGen_A.Write");
        await Assert.That(code).Contains("__XPRpcGen_A.Read");
    }

    [Test]
    public async Task Sanitizes_assembly_name_for_namespace()
    {
        var src = "public class A {}";
        var tree = CSharpSyntaxTree.ParseText(src);
        var comp = CSharpCompilation.Create("My-Project.X",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var emitter = new RegistryEmitter();
        var code = emitter.Emit(new[] { comp.GetTypeByMetadataName("A")! }, "My-Project.X");

        // hyphen → underscore, dot kept (legal in namespace)
        await Assert.That(code).Contains("namespace XPacketRpc.Generated.My_Project.X");
    }
}
