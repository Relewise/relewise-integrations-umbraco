using System;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Dashboards;

namespace Relewise.Integrations.Umbraco.Dashboards;

[Weight(40)]
internal class RelewiseDashboard : IDashboard
{
    public string Alias => "relewiseDashboard";
    public string View => "/App_Plugins/Relewise.Dashboard/dashboard.html";
    public string[] Sections => new[] { "Content" };
    public IAccessRule[] AccessRules => Array.Empty<IAccessRule>();
}