#nullable enable
using System;
using System.Text;

namespace Quipu.ParameterizationExtractor.Logic.Helpers
{
    /// <summary>
    /// Extracts the first <c>FROM &lt;schema&gt;.&lt;table&gt;</c> (or bare <c>FROM &lt;table&gt;</c>)
    /// from a SQL string. Pure / stateless / thread-safe; safe to call on every keystroke.
    /// </summary>
    public static class SeedQueryParser
    {
        public static SeedRoot? TryExtract(string? sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return null;

            int n = sql.Length;
            int i = 0;
            while (i < n)
            {
                char c = sql[i];

                if (c == '-' && i + 1 < n && sql[i + 1] == '-')
                {
                    while (i < n && sql[i] != '\n') i++;
                    continue;
                }

                if (c == '/' && i + 1 < n && sql[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                    if (i + 1 < n) i += 2; else i = n;
                    continue;
                }

                if (c == '\'')
                {
                    i++;
                    while (i < n)
                    {
                        if (sql[i] == '\'')
                        {
                            if (i + 1 < n && sql[i + 1] == '\'') { i += 2; continue; }
                            i++;
                            break;
                        }
                        i++;
                    }
                    continue;
                }

                if ((c == 'F' || c == 'f') && i + 4 <= n
                    && (sql[i + 1] == 'R' || sql[i + 1] == 'r')
                    && (sql[i + 2] == 'O' || sql[i + 2] == 'o')
                    && (sql[i + 3] == 'M' || sql[i + 3] == 'm'))
                {
                    bool prevBoundary = i == 0 || !IsIdentChar(sql[i - 1]);
                    bool nextBoundary = i + 4 == n || !IsIdentChar(sql[i + 4]);
                    if (prevBoundary && nextBoundary)
                    {
                        int j = i + 4;
                        while (j < n && char.IsWhiteSpace(sql[j])) j++;
                        if (j >= n) return null;
                        if (sql[j] == '(') return null;

                        var first = ReadIdent(sql, ref j);
                        if (first is null) return null;

                        int k = j;
                        while (k < n && char.IsWhiteSpace(sql[k])) k++;
                        if (k < n && sql[k] == '.')
                        {
                            k++;
                            while (k < n && char.IsWhiteSpace(sql[k])) k++;
                            int kBefore = k;
                            var second = ReadIdent(sql, ref k);
                            if (second is null) return new SeedRoot(string.Empty, first);
                            return new SeedRoot(first, second);
                        }

                        return new SeedRoot(string.Empty, first);
                    }
                }

                i++;
            }

            return null;
        }

        private static string? ReadIdent(string s, ref int j)
        {
            int n = s.Length;
            if (j >= n) return null;
            if (s[j] == '[')
            {
                j++;
                var sb = new StringBuilder();
                while (j < n && s[j] != ']') { sb.Append(s[j]); j++; }
                if (j < n) j++;
                return sb.Length == 0 ? null : sb.ToString();
            }
            int start = j;
            while (j < n && IsIdentChar(s[j])) j++;
            if (j == start) return null;
            return s.Substring(start, j - start);
        }

        private static bool IsIdentChar(char c) =>
            char.IsLetterOrDigit(c) || c == '_' || c == '#' || c == '@' || c == '$';
    }

    public sealed record SeedRoot(string Schema, string Name);
}
