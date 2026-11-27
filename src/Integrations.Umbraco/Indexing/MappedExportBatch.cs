using System;
using System.Collections.Generic;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Indexing;

internal sealed record MappedExportBatch(MappedEntity[] Entities, HashSet<Guid> Unmapped);
