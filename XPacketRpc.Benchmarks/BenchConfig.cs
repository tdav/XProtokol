using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;

namespace XPacketRpc.Benchmarks;

public sealed class BenchConfig : ManualConfig
{
    public BenchConfig()
    {
        // CoreRuntime.Core100 is not available in BDN 0.14.0 — runtime auto-detected from TFM
        AddJob(Job.Default
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithWarmupCount(3)
            .WithIterationCount(10));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}
