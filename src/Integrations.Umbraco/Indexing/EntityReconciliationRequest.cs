using System;
using Relewise.Client.Requests.Filters;

namespace Relewise.Integrations.Umbraco.Indexing;

internal sealed record EntityReconciliationRequest(
    Type EntityType,
    FilterCollection Filters,
    bool Synchronize,
    bool Delete);
