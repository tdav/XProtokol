using MemoryPack;
using MessagePack;
using MessagePack.Resolvers;
using ProtoBuf;
using ProtoBuf.Meta;
using System.IO;
using System.Text;
using System.Text.Json;
using XPacketRpc;
using XPacketRpc.Benchmarks.Dtos;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Benchmarks;

public static class WireSizeReport
{
    public static string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Wire-size report (bytes)");
        sb.AppendLine();
        sb.AppendLine("| DTO | XPacketRpc | MessagePack | MemoryPack | System.Text.Json | protobuf-net |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");

        var xp = new XPacketRpcSerializer();
        var mpOpts = ContractlessStandardResolver.Options;
        var rtm = RuntimeTypeModel.Default;

        // Register protobuf-net types
        RegisterProto<Vector3>(rtm, m => { m.Add(1, "X"); m.Add(2, "Y"); m.Add(3, "Z"); });
        RegisterProto<LogEntry>(rtm, m => { m.Add(1, "Timestamp"); m.Add(2, "Level"); m.Add(3, "Message"); m.Add(4, "TraceId"); m.Add(5, "SpanId"); });
        RegisterProto<OrderItem>(rtm, m => { m.Add(1, "ProductId"); m.Add(2, "Quantity"); m.Add(3, "Price"); });
        RegisterProto<OrderRequest>(rtm, m => { m.Add(1, "Id"); m.Add(2, "CustomerId"); m.Add(3, "Items"); });
        RegisterProto<Address>(rtm, m => { m.Add(1, "Street"); m.Add(2, "City"); m.Add(3, "Country"); });
        RegisterProto<UserProfile>(rtm, m => { m.Add(1, "Id"); m.Add(2, "Name"); m.Add(3, "Address"); m.Add(4, "Tags"); });
        RegisterProto<ChunkPayload>(rtm, m => { m.Add(1, "Id"); m.Add(2, "Data"); });

        XPRpc.Touch<Vector3>();
        XPRpc.Touch<LogEntry>();
        XPRpc.Touch<OrderRequest>();
        XPRpc.Touch<UserProfile>();
        XPRpc.Touch<ChunkPayload>();

        AppendRow(sb, "Vector3", xp.Serialize(DtoFactory.Vector3()).Length, DtoFactory.Vector3(), DtoFactory.Vector3MP(DtoFactory.Vector3()), mpOpts);

        var logEntry = DtoFactory.LogEntry();
        AppendRow(sb, "LogEntry", xp.Serialize(logEntry).Length, logEntry, DtoFactory.LogEntryMP(logEntry), mpOpts);

        var order = DtoFactory.OrderRequest();
        AppendRow(sb, "OrderRequest", xp.Serialize(order).Length, order, DtoFactory.OrderRequestMP(order), mpOpts);

        var profile = DtoFactory.UserProfile();
        AppendRow(sb, "UserProfile", xp.Serialize(profile).Length, profile, DtoFactory.UserProfileMP(profile), mpOpts);

        var chunk = DtoFactory.ChunkPayload();
        AppendRow(sb, "ChunkPayload", xp.Serialize(chunk).Length, chunk, DtoFactory.ChunkPayloadMP(chunk), mpOpts);

        return sb.ToString();
    }

    private static void RegisterProto<T>(RuntimeTypeModel rtm, Action<MetaType> configure)
    {
        if (!rtm.IsDefined(typeof(T)))
            configure(rtm.Add(typeof(T), false));
    }

    private static void AppendRow<T, TMp>(
        StringBuilder sb, string name,
        int xpSize,
        T dto, TMp dtoMp,
        MessagePackSerializerOptions mpOpts)
        where T : class
        where TMp : class
    {
        var mpSize = MessagePackSerializer.Serialize(dto, mpOpts).Length;
        var memPackSize = MemoryPackSerializer.Serialize(dtoMp).Length;
        var jsonSize = JsonSerializer.SerializeToUtf8Bytes(dto).Length;
        long protoSize;
        using (var ms = new MemoryStream())
        {
            Serializer.Serialize(ms, dto);
            protoSize = ms.Length;
        }
        sb.AppendLine($"| {name} | {xpSize} | {mpSize} | {memPackSize} | {jsonSize} | {protoSize} |");
    }
}
