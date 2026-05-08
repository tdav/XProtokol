using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal enum CtorStrategy
{
    Parameterless,
    AllParams,
    Mixed,
    Impossible,
}

internal sealed record CtorPlan(
    CtorStrategy Strategy,
    IMethodSymbol? Ctor,
    MemberDescriptor[] CtorParams,
    MemberDescriptor[] SetterMembers,
    string? Reason = null);

internal sealed class CtorBinder
{
    private readonly TypeWalker walker;

    public CtorBinder(TypeWalker walker) { this.walker = walker; }

    public CtorPlan Bind(INamedTypeSymbol type)
    {
        var members = walker.GetMembers(type);

        // 1. Parameterless ctor — only if ALL members are settable (no read-only strays).
        var parameterless = type.InstanceConstructors
            .FirstOrDefault(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
        if (parameterless is not null && members.All(m => CanSet(type, m)))
        {
            return new CtorPlan(CtorStrategy.Parameterless, parameterless,
                CtorParams: Array.Empty<MemberDescriptor>(),
                SetterMembers: members.ToArray());
        }

        // 2. Find public ctor with most parameters whose names are subset of members.
        var memberByName = members.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var candidate = type.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .Where(c => c.Parameters.All(p => memberByName.ContainsKey(p.Name)))
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (candidate is null)
        {
            return new CtorPlan(CtorStrategy.Impossible, null,
                Array.Empty<MemberDescriptor>(), Array.Empty<MemberDescriptor>(),
                $"No public constructor whose parameters match property/field names of {type.Name}.");
        }

        var ctorParams = candidate.Parameters
            .Select(p => memberByName[p.Name])
            .ToArray();
        var ctorSet = new HashSet<string>(ctorParams.Select(p => p.Name), StringComparer.Ordinal);
        var remaining = members.Where(m => !ctorSet.Contains(m.Name)).ToArray();

        // All remaining must be settable.
        var notSettable = remaining.Where(m => !CanSet(type, m)).ToArray();
        if (notSettable.Length > 0)
        {
            return new CtorPlan(CtorStrategy.Impossible, null,
                Array.Empty<MemberDescriptor>(), Array.Empty<MemberDescriptor>(),
                $"Members [{string.Join(",", notSettable.Select(m => m.Name))}] cannot be set " +
                $"(no setter/init) and are not in any constructor.");
        }

        var strategy = remaining.Length == 0 ? CtorStrategy.AllParams : CtorStrategy.Mixed;
        return new CtorPlan(strategy, candidate, ctorParams, remaining);
    }

    private static bool CanSet(INamedTypeSymbol type, MemberDescriptor m)
    {
        var field = type.GetMembers(m.Name).OfType<IFieldSymbol>().FirstOrDefault();
        if (field is not null) return !field.IsReadOnly;

        var prop = type.GetMembers(m.Name).OfType<IPropertySymbol>().FirstOrDefault();
        if (prop is not null) return prop.SetMethod is not null;

        return false;
    }
}
