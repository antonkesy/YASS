using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Yass.Core.Tests.Config;

internal static class Yaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static string Serialize<T>(T value) => Serializer.Serialize(value);

    public static T Deserialize<T>(string yaml) => Deserializer.Deserialize<T>(yaml);
}
