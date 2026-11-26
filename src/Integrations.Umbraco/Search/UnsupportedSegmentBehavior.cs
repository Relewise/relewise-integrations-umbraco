namespace Relewise.Integrations.Umbraco.Search;

/// <summary>
/// Controls queries requesting an unsupported Umbraco segment.
/// </summary>
public sealed class UnsupportedSegmentBehavior
{
    internal bool IgnoreSegment { get; }

    private UnsupportedSegmentBehavior(bool ignoreSegment)
    {
        IgnoreSegment = ignoreSegment;
    }

    /// <summary>
    /// Reject segment-specific queries. This is the default.
    /// </summary>
    public static UnsupportedSegmentBehavior Throw()
    {
        return new UnsupportedSegmentBehavior(false);
    }

    /// <summary>
    /// Ignore the segment argument and execute the ordinary query.
    /// </summary>
    public static UnsupportedSegmentBehavior Ignore()
    {
        return new UnsupportedSegmentBehavior(true);
    }
}
