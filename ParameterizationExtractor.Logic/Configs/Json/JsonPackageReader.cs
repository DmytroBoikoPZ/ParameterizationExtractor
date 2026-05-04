using System.IO;
using System.Text.Json;

namespace Quipu.ParameterizationExtractor.Logic.Configs.Json
{
    /// <summary>
    /// Deserialises a <see cref="Package"/> from JSON. The shape is documented in
    /// <c>docs/methodology/workspace-format.md</c> and locked by ADR-009.
    /// </summary>
    public static class JsonPackageReader
    {
        public static Package Read(Stream stream)
            => JsonSerializer.Deserialize<Package>(stream, JsonOptions.Default)
               ?? throw new JsonException("Package JSON deserialised to null.");

        public static Package Read(string path)
        {
            using var stream = File.OpenRead(path);
            return Read(stream);
        }
    }
}
