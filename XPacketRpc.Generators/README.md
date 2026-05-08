# XPacketRpc.Generators

A Roslyn incremental source generator that analyses call sites of `XPRpc.Write<T>` / `XPRpc.Read<T>` / `XPRpc.Touch<T>` and emits optimised, allocation-free serializers at compile time.

## Features

- **Incremental generator** — only re-runs when relevant syntax changes, keeping build times fast
- **Call-site discovery** — finds every generic invocation of `XPRpc` methods and collects the concrete type arguments
- **Transitive closure** — walks nested DTO types (records, classes, structs) to emit serializers for all reachable members
- **Constructor binding** — prefers parameterless constructors; falls back to primary/named-parameter constructors
- **Diagnostic errors** — clear error codes (XPRPC001–XPRPC004) pinpoint unsupported patterns at build time
- **Module initializer registration** — generated code auto-registers serializers via `[ModuleInitializer]`; no manual wiring needed

## Target Framework

- .NET Standard 2.0 (required by Roslyn analyzer / generator hosts)

## Usage

Add the generator project as an analyzer reference in your consumer project's `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\XPacketRpc.Generators\XPacketRpc.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Then simply call `XPRpc.Touch<T>()` (or `Write` / `Read`) in your code — the generator picks up every call site automatically.

## Architecture

```
XPacketRpcGenerator (IIncrementalGenerator)
├── Discovery
│   ├── CallSiteCollector   – finds XPRpc generic invocations
│   ├── TypeWalker          – transitive closure of DTO members
│   ├── CtorBinder          – selects & maps constructor parameters
│   ├── DiscoveredType      – model for a resolved DTO type
│   ├── MemberDescriptor    – model for a single serializable member
│   └── WireKind            – enum of supported wire types
├── Emit
│   ├── WriteEmitter        – generates Write<T> method body
│   ├── ReadEmitter         – generates Read<T> method body
│   ├── RegistryEmitter     – generates [ModuleInitializer] registration
│   └── IndentedStringBuilder – helper for indented code generation
└── Diagnostics
    └── Descriptors         – diagnostic descriptor constants
```

## Diagnostics

| Code | Severity | Message |
|---|---|---|
| XPRPC001 | Warning | Open-generic call site — use `XPRpc.Touch<ConcreteType>()` |
| XPRPC002 | Error | Open-generic type in transitive closure |
| XPRPC003 | Error | Cannot construct type (no compatible constructor) |
| XPRPC004 | Error | Unsupported field type |
