using System;
using JetBrains.Annotations;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco.Search;

/// <summary>Runtime information for request customization; services belong to the current operation.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class SearchRequestContext
{
    internal SearchRequestContext(IServiceProvider services, User user, string culture)
    {
        Services = services;
        User = user;
        Culture = culture;
    }

    /// <summary>Services in the current request or background-operation scope.</summary>
    public IServiceProvider Services { get; }

    /// <summary>The user resolved by the same locator used for behavioral tracking.</summary>
    public User User { get; }

    /// <summary>The effective Relewise request language.</summary>
    public string Culture { get; }
}
