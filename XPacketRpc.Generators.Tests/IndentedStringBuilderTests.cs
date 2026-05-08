using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class IndentedStringBuilderTests
{
    [Test]
    public async Task Append_writes_without_indent_at_root_level()
    {
        var sb = new IndentedStringBuilder();
        sb.AppendLine("hello");

        await Assert.That(sb.ToString()).IsEqualTo("hello\n");
    }

    [Test]
    public async Task Indent_block_adds_4_spaces_per_level()
    {
        var sb = new IndentedStringBuilder();
        sb.AppendLine("namespace Foo");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            sb.AppendLine("class Bar");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                sb.AppendLine("public int X;");
            }
            sb.AppendLine("}");
        }
        sb.AppendLine("}");

        var expected =
            "namespace Foo\n" +
            "{\n" +
            "    class Bar\n" +
            "    {\n" +
            "        public int X;\n" +
            "    }\n" +
            "}\n";
        await Assert.That(sb.ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task Append_without_newline_does_not_add_indent_in_middle()
    {
        var sb = new IndentedStringBuilder();
        using (sb.Indent())
        {
            sb.Append("a");
            sb.Append("b");
            sb.AppendLine();
        }
        await Assert.That(sb.ToString()).IsEqualTo("    ab\n");
    }
}
