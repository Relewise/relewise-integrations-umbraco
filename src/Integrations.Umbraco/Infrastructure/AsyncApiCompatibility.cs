using System;
using System.Reflection;

namespace Relewise.Integrations.Umbraco.Infrastructure;

// Without an override, the two default methods would call each other. Check the actual
// interface map so explicit and inherited implementations count. Only the legacy fallback
// uses reflection; method invocation still uses normal interface dispatch.
internal static class AsyncApiCompatibility
{
    internal static void RequireLegacyImplementation<TContract>(
        TContract implementation,
        string legacyMember,
        string asyncMember)
        where TContract : class
    {
        Type contract = typeof(TContract);
        InterfaceMapping mapping = implementation.GetType().GetInterfaceMap(contract);
        int index = Array.FindIndex(mapping.InterfaceMethods, method => method.Name == legacyMember);

        if (index < 0 || mapping.TargetMethods[index].DeclaringType == contract)
        {
            throw new InvalidOperationException(
                $"Type '{implementation.GetType().FullName}' must implement '{contract.Name}.{asyncMember}' " +
                $"or the legacy '{legacyMember}' member.");
        }
    }
}
