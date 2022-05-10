using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;

namespace Relewise.Integrations.Umbraco.Services;

public interface IExportContentService
{
    Task Export(IContent[] contents, long? version = null);
    Task ExportAll();
}