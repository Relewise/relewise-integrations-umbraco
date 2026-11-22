using System;
using System.Reflection;

namespace Relewise.Integrations.Umbraco.Infrastructure;

// Inspect the actual interface implementation, including explicit and inherited members.
// This runs only on the legacy fallback path; never invoke methods through reflection.
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
