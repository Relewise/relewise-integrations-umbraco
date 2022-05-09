namespace Relewise.Integrations.Umbraco;

public interface IRelewisePropertyValueConverter
{
    bool CanHandle(RelewisePropertyConverterContext context);
    void Convert(RelewisePropertyConverterContext context);
}