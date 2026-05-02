using System;
using System.Text.RegularExpressions;

namespace Tests.CharacterisationTests.Harness
{
    public static class SqlNormalizer
    {
        // Header timestamps are emitted on their own line (e.g., "02.05.2026 15:04:05" inside the
        // /* ... */ banner the engine prepends to every script). Inline data values like
        // "31.05.2021 20:37:31 +03:00" inside `values(...)` are deliberately not matched — they
        // are real row content and must be pinned.
        private static readonly Regex GeneratedTimestamp =
            new(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}$", RegexOptions.Compiled | RegexOptions.Multiline);

        private const string TimestampPlaceholder = "__GENERATED_TIMESTAMP__";

        public static string Normalize(string sql)
        {
            if (sql == null) sql = string.Empty;

            var unified = sql.Replace("\r\n", "\n").Replace("\r", "\n");

            var masked = GeneratedTimestamp.Replace(unified, TimestampPlaceholder);

            var lines = masked.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();

            var joined = string.Join("\n", lines).Trim();

            return joined.Length == 0 ? string.Empty : joined + "\n";
        }
    }
}
