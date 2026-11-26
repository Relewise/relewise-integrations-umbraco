using System;

namespace Relewise.Integrations.Umbraco.Indexing;

internal sealed record ExportChange(Guid Id, ExportChangeImpact Impact, bool Published);
