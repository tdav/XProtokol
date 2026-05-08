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
public class UserProfileSerializeBenchmarks
{
    private UserProfile dto = null!;
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;
    private Dtos.MP.UserProfileMP dtoMp = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<UserProfile>();
        dto = DtoFactory.UserProfile();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;
        dtoMp = DtoFactory.UserProfileMP(dto);

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(Address)))
        {
            var ma = rtm.Add(typeof(Address), false);
            ma.Add(1, "Street"); ma.Add(2, "City"); ma.Add(3, "Country");
        }
        if (!rtm.IsDefined(typeof(UserProfile)))
        {
            var mu = rtm.Add(typeof(UserProfile), false);
            mu.Add(1, "Id"); mu.Add(2, "Name"); mu.Add(3, "Address"); mu.Add(4, "Tags");
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
public class UserProfileDeserializeBenchmarks
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
        XPRpc.Touch<UserProfile>();
        var dto = DtoFactory.UserProfile();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(Address)))
        {
            var ma = rtm.Add(typeof(Address), false);
            ma.Add(1, "Street"); ma.Add(2, "City"); ma.Add(3, "Country");
        }
        if (!rtm.IsDefined(typeof(UserProfile)))
        {
            var mu = rtm.Add(typeof(UserProfile), false);
            mu.Add(1, "Id"); mu.Add(2, "Name"); mu.Add(3, "Address"); mu.Add(4, "Tags");
        }

        payloadXpRpc = xprpc.Serialize(dto);
        payloadMp = MessagePackSerializer.Serialize(dto, mpOpts);
        payloadMemPack = MemoryPackSerializer.Serialize(DtoFactory.UserProfileMP(dto));
        payloadJson = JsonSerializer.SerializeToUtf8Bytes(dto);
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, dto);
        payloadProto = ms.ToArray();
    }

    [Benchmark(Baseline = true)]
    public UserProfile? XPacketRpc() => xprpc.Deserialize<UserProfile>(payloadXpRpc);

    [Benchmark]
    public UserProfile MessagePackContractless() => MessagePackSerializer.Deserialize<UserProfile>(payloadMp, mpOpts);

    [Benchmark]
    public Dtos.MP.UserProfileMP? MemoryPack() => MemoryPackSerializer.Deserialize<Dtos.MP.UserProfileMP>(payloadMemPack);

    [Benchmark]
    public UserProfile? SystemTextJson() => JsonSerializer.Deserialize<UserProfile>(payloadJson);

    [Benchmark]
    public UserProfile ProtobufNet()
    {
        using var ms = new MemoryStream(payloadProto);
        return Serializer.Deserialize<UserProfile>(ms);
    }
}
