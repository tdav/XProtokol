; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
XPRPC001 | XPacketRpc | Warning | Open-generic call-site cannot be resolved
XPRPC002 | XPacketRpc | Error | Open-generic type in transitive closure
XPRPC003 | XPacketRpc | Error | Cannot construct type
XPRPC004 | XPacketRpc | Error | Unsupported field type
XPRPC005 | XPacketRpc | Error | Field name collision after FNV-1a hash
XPRPC006 | XPacketRpc | Warning | Type has no serializable members
