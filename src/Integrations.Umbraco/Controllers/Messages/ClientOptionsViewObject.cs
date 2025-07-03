using System;
using Relewise.Client.Extensions;

namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>
/// Represents the options for a Relewise client
/// </summary>
public class ClientOptionsViewObject
{
    /// <summary>
    /// Creates a new instance of <see cref="ClientOptionsViewObject"/> from the provided <see cref="RelewiseClientOptions"/>
    /// </summary>
    /// <param name="options"></param>
    public ClientOptionsViewObject(RelewiseClientOptions options)
    {
        DatasetId = options.DatasetId;
        ServerUrl = options.ServerUrl?.ToString();
        Timeout = options.Timeout.TotalSeconds;
    }

    /// <summary>
    /// Unique identifier for the dataset this client is configured to use
    /// </summary>
    public Guid DatasetId { get; }
    /// <summary>
    ///     URL of the Relewise server this client connects to
    /// </summary>
    public string? ServerUrl { get; }

    /// <summary>
    ///     Timeout in seconds for requests made by this client
    /// </summary>
    public double Timeout { get; }
}