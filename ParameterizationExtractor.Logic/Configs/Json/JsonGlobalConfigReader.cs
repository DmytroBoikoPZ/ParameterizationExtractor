using System.IO;
using System.Text.Json;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Quipu.ParameterizationExtractor.Logic.Configs.Json
{
    /// <summary>
    /// Deserialises a <see cref="GlobalExtractConfiguration"/> from JSON. The shape is documented in
    /// <c>docs/methodology/workspace-format.md</c> and locked by ADR-009.
    /// </summary>
    public static class JsonGlobalConfigReader
    {
        public static GlobalExtractConfiguration Read(Stream stream)
            => JsonSerializer.Deserialize<GlobalExtractConfiguration>(stream, JsonOptions.Default)
               ?? throw new JsonException("GlobalExtractConfiguration JSON deserialised to null.");

        public static GlobalExtractConfiguration Read(string path)
        {
            using var stream = File.OpenRead(path);
            return Read(stream);
        }
    }
}
