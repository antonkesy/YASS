namespace Yass.App.Api;

/// <summary>
/// A failure the caller should see in words: a missing session, a state that forbids the call,
/// an export the disk refused. Each host turns it into its own transport's error.
/// </summary>
public sealed class YassException : Exception
{
    public YassException()
    {
    }

    public YassException(string message)
        : base(message)
    {
    }

    public YassException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
