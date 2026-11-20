using System;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal sealed record DeletedEntity(Type EntityType, string Id, Guid NodeKey, bool Synchronize);
