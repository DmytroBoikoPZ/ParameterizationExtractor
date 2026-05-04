using System.Text.Json;

namespace Quipu.ParameterizationExtractor.Logic.Configs.Json
{
    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> for the engine's workspace JSON readers.
    /// Conventions are documented in <c>docs/methodology/workspace-format.md</c> and locked by ADR-009.
    /// </summary>
    public static class JsonOptions
    {
        public static JsonSerializerOptions Default { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }
}
