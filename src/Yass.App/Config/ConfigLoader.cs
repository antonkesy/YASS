using System.Collections;
using Yass.Core.Config;

namespace Yass.App.Config;

/// <summary>
/// Reads <c>yass.yaml</c>, applies <c>YASS__section__key</c> environment overrides, types and validates.
/// Errors name the offending key.
/// </summary>
public static class ConfigLoader
{
    public const string EnvPrefix = "YASS__";

    public static YassConfig Load(string path, IDictionary? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariables();

        string yaml = File.Exists(path) ? File.ReadAllText(path) : "";
        return Parse(yaml, environment, path);
    }

    /// <exception cref="ConfigValidationException">Malformed YAML or invalid values; the message names the key.</exception>
    public static YassConfig Parse(string yaml, IDictionary environment, string sourceName = "yass.yaml")
    {
        ArgumentNullException.ThrowIfNull(environment);

        Dictionary<object, object?> tree;
        try
        {
            tree = YassYaml.DeserializeTree(yaml);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigValidationException($"{sourceName}: {ex.Message}", ex);
        }

        foreach (DictionaryEntry entry in environment)
        {
            if (entry.Key is string key && key.StartsWith(EnvPrefix, StringComparison.Ordinal) && entry.Value is string value)
            {
                string[] segments = key[EnvPrefix.Length..].ToLowerInvariant().Split("__", StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    Set(tree, segments, value);
                }
            }
        }

        YassConfig config;
        try
        {
            config = YassYaml.Deserialize(YassYaml.SerializeTree(tree));
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigValidationException($"{sourceName}: {Describe(ex)}", ex);
        }

        return ConfigValidator.ValidateOrThrow(config);
    }

    private static void Set(Dictionary<object, object?> tree, string[] segments, string value)
    {
        Dictionary<object, object?> node = tree;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (node.TryGetValue(segments[i], out object? child) && child is Dictionary<object, object?> dict)
            {
                node = dict;
            }
            else
            {
                var created = new Dictionary<object, object?>();
                node[segments[i]] = created;
                node = created;
            }
        }

        node[segments[^1]] = value;
    }

    /// <summary>YamlDotNet nests the useful part ("Property 'foo' not found on type ...") in inner exceptions.</summary>
    private static string Describe(Exception ex)
    {
        Exception inner = ex;
        while (inner.InnerException is not null)
        {
            inner = inner.InnerException;
        }

        return inner.Message;
    }
}
