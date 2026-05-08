# TCPClient

A sample TCP client application that demonstrates the `XProtocol` binary packet protocol. Connects to the `TCPServer`, sends a handshake packet, and processes the server's response.

## Target Framework

- .NET 10

## Dependencies

- `XProtocol` (project reference)

## Running

Start `TCPServer` first, then:

```pwsh
dotnet run --project TCPClient
```

The client connects to **127.0.0.1:4910** by default.

## How It Works

1. `XClient` opens a `TcpClient` connection and starts a receive loop on a background thread.
2. A random magic number is generated and sent inside an `XPacketHandshake` packet.
3. On receiving a handshake reply the client verifies the magic number and prints the result.

## Key Types

| Type | Description |
|---|---|
| `XClient` | Wraps `TcpClient`; exposes `OnPacketRecieve` event and `QueuePacketSend` |
| `Program` | Entry point — wires up the client and sends the handshake |
