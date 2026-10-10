namespace Yass.App.Api;

/// <summary>A failure the user should see in words. Each host maps it to its transport's error.</summary>
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
