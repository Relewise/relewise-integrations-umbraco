namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Integration metadata keys available when selecting or filtering exported entities with the SDK.
/// </summary>
public static class RelewiseDataKeys
{
    internal const string ReservedPrefix = "rwUmbraco";
    internal const string Url = "url";
    internal const string CreatedAt = "createdAt";
    internal const string Version = "UmbracoVersionId";

    /// <summary>
    /// The alias of the Umbraco document type that supplied the entity.
    /// </summary>
    public const string DocumentTypeAlias = "contentTypeAlias";

    /// <summary>
    /// The source Umbraco node's GUID key, independently of its configured Relewise entity ID.
    /// </summary>
    public const string NodeKey = "rwUmbracoNodeKey";

    /// <summary>
    /// Whether the source Umbraco page is published, independently of the entity's enabled state.
    /// </summary>
    public const string Published = "rwUmbracoPublished";

    /// <summary>
    /// Source-page metadata used when filtering search and recommendation results.
    /// </summary>
    public static class Search
    {
        internal const string Access = "rwUmbracoSearchAccess";
        internal const string FieldPrefix = "rwUmbracoField_";

        /// <summary>
        /// The searchable default-segment culture codes, normalized to lowercase; an empty value denotes
        /// invariant content.
        /// </summary>
        public const string Culture = "rwUmbracoSearchCulture";

        /// <summary>
        /// Whether access to the source Umbraco page is protected.
        /// </summary>
        public const string Protected = "rwUmbracoSearchProtected";
    }
}
