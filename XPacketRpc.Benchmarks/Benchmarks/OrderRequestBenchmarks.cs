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
public class OrderRequestSerializeBenchmarks
{
    private OrderRequest dto = null!;
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;
    private Dtos.MP.OrderRequestMP dtoMp = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<OrderRequest>();
        dto = DtoFactory.OrderRequest();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;
        dtoMp = DtoFactory.OrderRequestMP(dto);

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(OrderItem)))
        {
            var mi = rtm.Add(typeof(OrderItem), false);
            mi.Add(1, "ProductId"); mi.Add(2, "Quantity"); mi.Add(3, "Price");
        }
        if (!rtm.IsDefined(typeof(OrderRequest)))
        {
            var mr = rtm.Add(typeof(OrderRequest), false);
            mr.Add(1, "Id"); mr.Add(2, "CustomerId"); mr.Add(3, "Items");
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
public class OrderRequestDeserializeBenchmarks
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
        XPRpc.Touch<OrderRequest>();
        var dto = DtoFactory.OrderRequest();
        xprpc = new XPacketRpcSerializer();
        mpOpts = ContractlessStandardResolver.Options;

        var rtm = RuntimeTypeModel.Default;
        if (!rtm.IsDefined(typeof(OrderItem)))
        {
            var mi = rtm.Add(typeof(OrderItem), false);
            mi.Add(1, "ProductId"); mi.Add(2, "Quantity"); mi.Add(3, "Price");
        }
        if (!rtm.IsDefined(typeof(OrderRequest)))
        {
            var mr = rtm.Add(typeof(OrderRequest), false);
            mr.Add(1, "Id"); mr.Add(2, "CustomerId"); mr.Add(3, "Items");
        }

        payloadXpRpc = xprpc.Serialize(dto);
        payloadMp = MessagePackSerializer.Serialize(dto, mpOpts);
        payloadMemPack = MemoryPackSerializer.Serialize(DtoFactory.OrderRequestMP(dto));
        payloadJson = JsonSerializer.SerializeToUtf8Bytes(dto);
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, dto);
        payloadProto = ms.ToArray();
    }

    [Benchmark(Baseline = true)]
    public OrderRequest? XPacketRpc() => xprpc.Deserialize<OrderRequest>(payloadXpRpc);

    [Benchmark]
    public OrderRequest MessagePackContractless() => MessagePackSerializer.Deserialize<OrderRequest>(payloadMp, mpOpts);

    [Benchmark]
    public Dtos.MP.OrderRequestMP? MemoryPack() => MemoryPackSerializer.Deserialize<Dtos.MP.OrderRequestMP>(payloadMemPack);

    [Benchmark]
    public OrderRequest? SystemTextJson() => JsonSerializer.Deserialize<OrderRequest>(payloadJson);

    [Benchmark]
    public OrderRequest ProtobufNet()
    {
        using var ms = new MemoryStream(payloadProto);
        return Serializer.Deserialize<OrderRequest>(ms);
    }
}
