using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

/// <summary>Who turns page text into a suggested file name.</summary>
public enum NamingProvider
{
    /// <summary>No model is called; suggestions come from the text and the template only.</summary>
    Off = 0,

    /// <summary>A model served by a local Ollama instance. Page text stays on the machine.</summary>
    Ollama,

    /// <summary>Anthropic's API. Page text leaves the network.</summary>
    Anthropic,
}

/// <summary><c>naming:</c> section. Off by default: this is the only part of YASS that can talk to anything else.</summary>
public sealed record NamingConfig
{
    /// <summary>Read text off the scanned pages. Needs the <c>tesseract</c> binary on PATH.</summary>
    public bool Ocr { get; init; }

    /// <summary>Tesseract language code, e.g. <c>eng</c> or <c>deu</c>.</summary>
    [Required]
    public string OcrLanguage { get; init; } = "eng";

    /// <summary>Pages to read. The first page or two carry the title of almost every document.</summary>
    [Range(1, 10)]
    public int OcrPages { get; init; } = 2;

    public NamingProvider Provider { get; init; } = NamingProvider.Off;

    /// <summary>Base URL of the provider. Defaulted per provider when empty.</summary>
    public string Endpoint { get; init; } = "";

    [Required]
    public string Model { get; init; } = "llama3.2";

    /// <summary>File holding the API key, for providers that need one. Never the key itself.</summary>
    public string ApiKeyFile { get; init; } = "";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 20;
}
