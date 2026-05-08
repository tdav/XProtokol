using BenchmarkDotNet.Attributes;
using MemoryPack;
using MessagePack;
using MessagePack.Resolvers;
using ProtoBuf;
using ProtoBuf.Meta;
using System.IO;
using System.Text.Json;
using XPacketRpc;
using XPacketRpc.Benchmarks.Dtos;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class LogEntrySerializeBenchmarks
{
    private LogEntry dto = null!;
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;
    private Dtos.MP.LogEntryMP dtoMp = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<LogEntry>();
        dto = DtoFactory.LogEntry();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;
        dtoMp = DtoFactory.LogEntryMP(dto);

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(LogEntry)))
        {
            var meta = rtm.Add(typeof(LogEntry), false);
            meta.Add(1, "Timestamp");
            meta.Add(2, "Level");
            meta.Add(3, "Message");
            meta.Add(4, "TraceId");
            meta.Add(5, "SpanId");
        }
    }

    [Benchmark(Baseline = true)]
    public byte[] XPacketRpc() => xprpc.Serialize(dto);

    [Benchmark]
    public byte[] MessagePackContractless() => MessagePackSerializer.Serialize(dto, mpOpts);

    [Benchmark]
    public byte[] MemoryPack() => MemoryPackSerializer.Serialize(dtoMp);

    [Benchmark]
    public byte[] SystemTextJson() => JsonSerializer.SerializeToUtf8Bytes(dto);

    [Benchmark]
    public byte[] ProtobufNet()
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, dto);
        return ms.ToArray();
    }
}

[MemoryDiagnoser]
public class LogEntryDeserializeBenchmarks
{
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;

    private byte[] payloadXpRpc = null!;
    private byte[] payloadMp = null!;
    private byte[] payloadMemPack = null!;
    private byte[] payloadJson = null!;
    private byte[] payloadProto = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<LogEntry>();
        var dto = DtoFactory.LogEntry();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(LogEntry)))
        {
            var meta = rtm.Add(typeof(LogEntry), false);
            meta.Add(1, "Timestamp");
            meta.Add(2, "Level");
            meta.Add(3, "Message");
            meta.Add(4, "TraceId");
            meta.Add(5, "SpanId");
        }

        payloadXpRpc = xprpc.Serialize(dto);
        payloadMp = MessagePackSerializer.Serialize(dto, mpOpts);
        payloadMemPack = MemoryPackSerializer.Serialize(DtoFactory.LogEntryMP(dto));
        payloadJson = JsonSerializer.SerializeToUtf8Bytes(dto);
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, dto);
        payloadProto = ms.ToArray();
    }

    [Benchmark(Baseline = true)]
    public LogEntry? XPacketRpc() => xprpc.Deserialize<LogEntry>(payloadXpRpc);

    [Benchmark]
    public LogEntry MessagePackContractless() => MessagePackSerializer.Deserialize<LogEntry>(payloadMp, mpOpts);

    [Benchmark]
    public Dtos.MP.LogEntryMP? MemoryPack() => MemoryPackSerializer.Deserialize<Dtos.MP.LogEntryMP>(payloadMemPack);

    [Benchmark]
    public LogEntry? SystemTextJson() => JsonSerializer.Deserialize<LogEntry>(payloadJson);

    [Benchmark]
    public LogEntry ProtobufNet()
    {
        using var ms = new MemoryStream(payloadProto);
        return Serializer.Deserialize<LogEntry>(ms);
    }
}
