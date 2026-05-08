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
public class Vector3SerializeBenchmarks
{
    private Vector3 dto = null!;
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;
    private Dtos.MP.Vector3MP dtoMp = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<Vector3>();
        dto = DtoFactory.Vector3();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;
        dtoMp = new Dtos.MP.Vector3MP { X = dto.X, Y = dto.Y, Z = dto.Z };

        // protobuf-net runtime model — register Vector3 once
        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(Vector3)))
        {
            var meta = rtm.Add(typeof(Vector3), false);
            meta.Add(1, "X");
            meta.Add(2, "Y");
            meta.Add(3, "Z");
        }

        // verify roundtrip
        var bytes = xprpc.Serialize(dto);
        var got = xprpc.Deserialize<Vector3>(bytes);
        if (got!.X != dto.X) throw new System.InvalidOperationException("Roundtrip mismatch");
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
public class Vector3DeserializeBenchmarks
{
    private Vector3 dto = null!;
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
        XPRpc.Touch<Vector3>();
        dto = DtoFactory.Vector3();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(Vector3)))
        {
            var meta = rtm.Add(typeof(Vector3), false);
            meta.Add(1, "X"); meta.Add(2, "Y"); meta.Add(3, "Z");
        }

        payloadXpRpc = xprpc.Serialize(dto);
        payloadMp = MessagePackSerializer.Serialize(dto, mpOpts);
        payloadMemPack = MemoryPackSerializer.Serialize(new Dtos.MP.Vector3MP { X = dto.X, Y = dto.Y, Z = dto.Z });
        payloadJson = JsonSerializer.SerializeToUtf8Bytes(dto);
        using (var ms = new MemoryStream())
        {
            Serializer.Serialize(ms, dto);
            payloadProto = ms.ToArray();
        }
    }

    [Benchmark(Baseline = true)]
    public Vector3? XPacketRpc() => xprpc.Deserialize<Vector3>(payloadXpRpc);

    [Benchmark]
    public Vector3 MessagePackContractless() => MessagePackSerializer.Deserialize<Vector3>(payloadMp, mpOpts);

    [Benchmark]
    public Dtos.MP.Vector3MP? MemoryPack() => MemoryPackSerializer.Deserialize<Dtos.MP.Vector3MP>(payloadMemPack);

    [Benchmark]
    public Vector3? SystemTextJson() => JsonSerializer.Deserialize<Vector3>(payloadJson);

    [Benchmark]
    public Vector3 ProtobufNet()
    {
        using var ms = new MemoryStream(payloadProto);
        return Serializer.Deserialize<Vector3>(ms);
    }
}
