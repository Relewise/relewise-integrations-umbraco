namespace Relewise.Integrations.Umbraco;

// Built-ins remain fallbacks regardless of whether application converters are registered before or after AddRelewise.
internal interface IDefaultPropertyValueConverter : IAsyncRelewisePropertyValueConverter
{
}
