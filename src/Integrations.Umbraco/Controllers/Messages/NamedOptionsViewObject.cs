namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>
/// Represents a named set of Relewise client options
/// </summary>
public class NamedOptionsViewObject
{
    /// <summary>
    /// Creates a new instance of <see cref="NamedOptionsViewObject"/>
    /// </summary>
    public NamedOptionsViewObject(string name,
        ClientOptionsViewObject tracker,
        ClientOptionsViewObject recommender,
        ClientOptionsViewObject searcher,
        ClientOptionsViewObject searchAdministrator,
        ClientOptionsViewObject analyzer,
        ClientOptionsViewObject dataAccessor)
    {
        Name = name;

        Tracker = tracker;
        Recommender = recommender;
        Searcher = searcher;
        SearchAdministrator = searchAdministrator;
        Analyzer = analyzer;
        DataAccessor = dataAccessor;
    }

    /// <summary>
    /// Unique name for this set of Relewise client options
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Represents the options for the Relewise Tracker client
    /// </summary>
    public ClientOptionsViewObject Tracker { get; }

    /// <summary>
    /// Represents the options for the Relewise Recommender client
    /// </summary>
    public ClientOptionsViewObject Recommender { get; }

    /// <summary>
    /// Represents the options for the Relewise Searcher client
    /// </summary>
    public ClientOptionsViewObject Searcher { get; }

    /// <summary>
    /// Represents the options for the Relewise Search Administrator client
    /// </summary>
    public ClientOptionsViewObject SearchAdministrator { get; }

    /// <summary>
    /// Represents the options for the Relewise Analyzer client
    /// </summary>
    public ClientOptionsViewObject Analyzer { get; }

    /// <summary>
    /// Represents the options for the Relewise Data Access client
    /// </summary>
    public ClientOptionsViewObject DataAccessor { get; }
}