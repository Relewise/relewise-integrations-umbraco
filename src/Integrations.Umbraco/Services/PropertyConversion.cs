#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.

using System.Collections.Generic;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco.Services;

internal sealed record PropertyConversion(IReadOnlyDictionary<string, DataValue?> Data, bool Handled);
