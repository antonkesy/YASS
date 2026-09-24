using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Yawss.Core.Config;

namespace Yawss.Server.Config;

/// <summary>YAML mapping for <see cref="YawssConfig"/>: snake_case keys and snake_case enum values, as in SPEC.md §5.</summary>
public static class YawssYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer TreeDeserializer = new DeserializerBuilder().Build();

    public static string Serialize(YawssConfig config) => Serializer.Serialize(config);

    public static YawssConfig Deserialize(string yaml) => Deserializer.Deserialize<YawssConfig>(yaml) ?? new YawssConfig();

    /// <summary>Untyped tree (nested dictionaries with string keys) so overrides can be merged before typing.</summary>
    public static Dictionary<object, object?> DeserializeTree(string yaml) =>
        TreeDeserializer.Deserialize<Dictionary<object, object?>>(yaml) ?? [];

    public static string SerializeTree(Dictionary<object, object?> tree) => Serializer.Serialize(tree);
}
