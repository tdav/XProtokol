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
public class ChunkPayloadSerializeBenchmarks
{
    private ChunkPayload dto = null!;
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;
    private Dtos.MP.ChunkPayloadMP dtoMp = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<ChunkPayload>();
        dto = DtoFactory.ChunkPayload();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;
        dtoMp = DtoFactory.ChunkPayloadMP(dto);

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(ChunkPayload)))
        {
            var mc = rtm.Add(typeof(ChunkPayload), false);
            mc.Add(1, "Id"); mc.Add(2, "Data");
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
public class ChunkPayloadDeserializeBenchmarks
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
        XPRpc.Touch<ChunkPayload>();
        var dto = DtoFactory.ChunkPayload();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(ChunkPayload)))
        {
            var mc = rtm.Add(typeof(ChunkPayload), false);
            mc.Add(1, "Id"); mc.Add(2, "Data");
        }

        payloadXpRpc = xprpc.Serialize(dto);
        payloadMp = MessagePackSerializer.Serialize(dto, mpOpts);
        payloadMemPack = MemoryPackSerializer.Serialize(DtoFactory.ChunkPayloadMP(dto));
        payloadJson = JsonSerializer.SerializeToUtf8Bytes(dto);
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, dto);
        payloadProto = ms.ToArray();
    }

    [Benchmark(Baseline = true)]
    public ChunkPayload? XPacketRpc() => xprpc.Deserialize<ChunkPayload>(payloadXpRpc);

    [Benchmark]
    public ChunkPayload MessagePackContractless() => MessagePackSerializer.Deserialize<ChunkPayload>(payloadMp, mpOpts);

    [Benchmark]
    public Dtos.MP.ChunkPayloadMP? MemoryPack() => MemoryPackSerializer.Deserialize<Dtos.MP.ChunkPayloadMP>(payloadMemPack);

    [Benchmark]
    public ChunkPayload? SystemTextJson() => JsonSerializer.Deserialize<ChunkPayload>(payloadJson);

    [Benchmark]
    public ChunkPayload ProtobufNet()
    {
        using var ms = new MemoryStream(payloadProto);
        return Serializer.Deserialize<ChunkPayload>(ms);
    }
}
