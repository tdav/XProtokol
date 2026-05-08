# TCPServer

A sample TCP server application that demonstrates the `XProtocol` binary packet protocol. Accepts multiple clients, performs a magic-number handshake, and echoes the confirmed handshake back to each client.

## Target Framework

- .NET 10

## Dependencies

- `XProtocol` (project reference)

## Running

```pwsh
dotnet run --project TCPServer
```

The server listens on **port 4910** by default.

## How It Works

1. `XServer` starts a `TcpListener` and calls `AcceptClients()` in a loop.
2. Each accepted connection is wrapped in a `ConnectedClient` running on its own thread.
3. On receiving a `Handshake` packet the server validates the magic number and replies with a confirmed handshake packet.
4. Unknown packet types are silently ignored.

## Key Types

| Type | Description |
|---|---|
| `XServer` | Wraps `TcpListener`; spawns `ConnectedClient` per connection |
| `ConnectedClient` | Per-connection handler; reads/writes `XPacket` frames |
| `Program` | Entry point — creates and starts the server |
