using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yass.Core.Config;
using Yass.Core.Export;

namespace Yass.Naming;

/// <summary>
/// Asks a model for one file name given the text of a document. Two providers, chosen by
/// config: a local Ollama, where the text never leaves the machine, and Anthropic's API,
/// where it does. Off by default, and any failure degrades to no AI suggestion.
/// </summary>
public sealed class AiNamer(HttpClient http, ILogger<AiNamer> logger)
{
    private const string Instruction =
        "You name scanned documents. Reply with ONLY a file name: 2 to 6 words, no extension, " +
        "no path, no quotes, no explanation. Prefer the document's own title, its issuer and its " +
        "date if they appear. Use spaces, not underscores.";

    /// <returns>The name, or the reason there is none. Both null means there was nothing to ask.</returns>
    public async Task<(string? Name, string? Error)> SuggestAsync(string documentText, NamingConfig naming, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(naming);
        if (naming.Provider == NamingProvider.Off || string.IsNullOrWhiteSpace(documentText))
        {
            return (null, null);
        }

        // Enough for a title and a letterhead; more is cost without accuracy.
        string excerpt = documentText.Length > 4000 ? documentText[..4000] : documentText;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(naming.TimeoutSeconds));
            string? name = naming.Provider switch
            {
                NamingProvider.Ollama => await AskOllamaAsync(excerpt, naming, timeout.Token).ConfigureAwait(false),
                NamingProvider.Anthropic => await AskAnthropicAsync(excerpt, naming, timeout.Token).ConfigureAwait(false),
                _ => null,
            };
            return (Clean(name), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException)
        {
            // A naming suggestion is a nicety; never let it take a scan down with it. It is
            // still worth saying why it produced nothing: a bad key and a blank page used to
            // look identical from the UI.
            logger.LogWarning(ex, "Name suggestion from {Provider} failed", naming.Provider);
            return (null, Explain(ex, naming));
        }
    }

    /// <summary>Turns a failure into a sentence someone can act on.</summary>
    private static string Explain(Exception ex, NamingConfig naming) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            $"{naming.Provider} rejected the API key.",
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            $"{naming.Provider} is rate limiting; try again shortly.",
        HttpRequestException { StatusCode: { } status } =>
            $"{naming.Provider} refused the request ({(int)status}).",
        HttpRequestException => $"{naming.Provider} could not be reached.",
        OperationCanceledException => $"{naming.Provider} did not answer within {naming.TimeoutSeconds}s.",
        JsonException => $"{naming.Provider} sent a reply that could not be read.",
        // The key-file checks below throw this, and their messages already name the path.
        _ => ex.Message,
    };

    private async Task<string?> AskOllamaAsync(string text, NamingConfig naming, CancellationToken cancellationToken)
    {
        string endpoint = string.IsNullOrWhiteSpace(naming.Endpoint) ? "http://127.0.0.1:11434" : naming.Endpoint.TrimEnd('/');
        using HttpResponseMessage response = await http.PostAsJsonAsync(
            new Uri($"{endpoint}/api/generate"),
            new
            {
                model = naming.Model,
                prompt = Instruction + "\n\nDocument:\n" + text,
                stream = false,
            },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return body.RootElement.TryGetProperty("response", out JsonElement answer) ? answer.GetString() : null;
    }

    private async Task<string?> AskAnthropicAsync(string text, NamingConfig naming, CancellationToken cancellationToken)
    {
        string key = ReadKey(naming.ApiKeyFile);
        string endpoint = string.IsNullOrWhiteSpace(naming.Endpoint) ? "https://api.anthropic.com" : naming.Endpoint.TrimEnd('/');

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{endpoint}/v1/messages"))
        {
            Content = JsonContent.Create(new
            {
                model = naming.Model,
                max_tokens = 64,
                system = Instruction,
                messages = new[] { new { role = "user", content = "Document:\n" + text } },
            }),
        };
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return body.RootElement.TryGetProperty("content", out JsonElement content) && content.GetArrayLength() > 0
            && content[0].TryGetProperty("text", out JsonElement first)
            ? first.GetString()
            : null;
    }

    /// <summary>The key is read from a file so it never has to appear in yass.yaml or the environment.</summary>
    private static string ReadKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("naming.api_key_file is not set.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"naming.api_key_file \"{path}\" does not exist.");
        }

        string key = File.ReadAllText(path).Trim();
        return key.Length == 0 ? throw new InvalidOperationException($"naming.api_key_file \"{path}\" is empty.") : key;
    }

    /// <summary>Models like to add quotes, prefixes and extensions; strip them back to a bare name.</summary>
    internal static string? Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string name = raw.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        name = name.Trim('"', '\'', '`', '.', ' ');
        foreach (string extension in new[] { ".pdf", ".jpg", ".png" })
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^extension.Length];
            }
        }

        name = FileNames.Strip(name);
        if (name.Length > 120)
        {
            name = name[..120].Trim();
        }

        return name.Length < 2 ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name).Replace("  ", " ", StringComparison.Ordinal);
    }
}
