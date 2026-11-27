using Relewise.Integrations.Umbraco.Controllers.Messages;

namespace Relewise.Integrations.Umbraco.Controllers;

internal interface IDashboardConfigurationContributor
{
    void Apply(RelewiseUmbracoConfiguration configuration, ConfigurationViewModel snapshot);
}
