using System;
using System.Text;

namespace XPacketRpc.Generators.Emit;

internal sealed class IndentedStringBuilder
{
    private readonly StringBuilder sb = new();
    private int level;
    private bool atLineStart = true;

    public IDisposable Indent() => new IndentScope(this);

    public void Append(string text)
    {
        if (this.atLineStart) { WriteIndent(); this.atLineStart = false; }
        this.sb.Append(text);
    }

    public void AppendLine(string text)
    {
        Append(text);
        this.sb.Append('\n');
        this.atLineStart = true;
    }

    public void AppendLine()
    {
        this.sb.Append('\n');
        this.atLineStart = true;
    }

    private void WriteIndent()
    {
        for (int i = 0; i < this.level; i++) this.sb.Append("    ");
    }

    public override string ToString() => this.sb.ToString();

    private sealed class IndentScope : IDisposable
    {
        private readonly IndentedStringBuilder parent;
        public IndentScope(IndentedStringBuilder p) { this.parent = p; this.parent.level++; }
        public void Dispose() => this.parent.level--;
    }
}
