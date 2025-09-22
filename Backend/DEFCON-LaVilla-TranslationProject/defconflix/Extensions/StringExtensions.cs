using System.Text;

namespace defconflix.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Removes null bytes and other problematic characters that PostgreSQL can't handle
        /// </summary>
        public static string SanitizeForDatabase(this string input, ILogger? logger = null, string? fieldName = null)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            bool hasProblematicChars = false;
            var problematicChars = new List<string>();

            // Check for null bytes and other problematic characters
            if (input.Contains('\0'))
            {
                hasProblematicChars = true;
                problematicChars.Add("null byte (\\0)");
            }
            if (input.Contains("\u0000"))
            {
                hasProblematicChars = true;
                problematicChars.Add("unicode null (\\u0000)");
            }

            // Log if problematic characters found
            if (hasProblematicChars && logger != null)
            {
                logger.LogWarning($"Found problematic characters in {fieldName ?? "field"}: {string.Join(", ", problematicChars)}. " +
                    $"Original value: '{input.Replace('\0', '?')}' (length: {input.Length})");
            }

            // Remove null bytes and other control characters
            var sanitized = input
                .Replace("\0", "") // Remove null bytes
                .Replace("\u0000", "") // Remove Unicode null
                .Replace("\x00", ""); // Remove hex null

            // Remove other problematic control characters but keep common ones like \n, \r, \t
            var result = new StringBuilder();
            var removedControlChars = new List<int>();

            foreach (char c in sanitized)
            {
                // Allow printable characters, newlines, carriage returns, and tabs
                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                {
                    removedControlChars.Add((int)c);
                    continue; // Skip control characters
                }
                result.Append(c);
            }

            if (removedControlChars.Any() && logger != null)
            {
                logger.LogWarning($"Removed control characters from {fieldName ?? "field"}: {string.Join(", ", removedControlChars.Distinct().Select(c => $"0x{c:X2}"))}");
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// Safely decodes URL-encoded strings and sanitizes them
        /// </summary>
        public static string SafeUrlDecode(this string input, ILogger? logger = null, string? fieldName = null)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            try
            {
                var decoded = Uri.UnescapeDataString(input);
                return decoded.SanitizeForDatabase(logger, fieldName);
            }
            catch (Exception ex)
            {
                logger?.LogWarning($"URL decoding failed for {fieldName ?? "field"}: '{input}'. Error: {ex.Message}");
                // If URL decoding fails, just sanitize the original string
                return input.SanitizeForDatabase(logger, fieldName);
            }
        }

        /// <summary>
        /// Analyzes a string for problematic characters and returns detailed information
        /// </summary>
        public static string AnalyzeProblematicCharacters(this string input)
        {
            if (string.IsNullOrEmpty(input)) 
            { 
                return "String is null or empty";
            }

            var analysis = new StringBuilder();
            analysis.AppendLine($"String length: {input.Length}");
            analysis.AppendLine($"String preview: '{input.Substring(0, Math.Min(100, input.Length))}{(input.Length > 100 ? "..." : "")}'");

            var problematicChars = new Dictionary<string, List<int>>();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                int charCode = (int)c;

                if (c == '\0')
                {
                    if (!problematicChars.ContainsKey("Null bytes (\\0)"))
                    {
                        problematicChars["Null bytes (\\0)"] = new List<int>();
                    }
                    problematicChars["Null bytes (\\0)"].Add(i);
                }
                else if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                {
                    var key = $"Control char 0x{charCode:X2}";
                    if (!problematicChars.ContainsKey(key))
                    {
                        problematicChars[key] = new List<int>();
                    }
                    problematicChars[key].Add(i);
                }
                else if (charCode > 127 && charCode < 160) // Extended ASCII control chars
                {
                    var key = $"Extended control 0x{charCode:X2}";
                    if (!problematicChars.ContainsKey(key))
                        problematicChars[key] = new List<int>();
                    problematicChars[key].Add(i);
                }
            }

            if (problematicChars.Any())
            {
                analysis.AppendLine("Problematic characters found:");
                foreach (var kvp in problematicChars)
                {
                    var positions = kvp.Value.Take(5).Select(p => p.ToString()).ToList();
                    if (kvp.Value.Count > 5)
                    {
                        positions.Add($"... and {kvp.Value.Count - 5} more");
                    }

                    analysis.AppendLine($"  {kvp.Key}: {kvp.Value.Count} occurrences at positions [{string.Join(", ", positions)}]");
                }
            }
            else
            {
                analysis.AppendLine("No problematic characters detected");
            }

            return analysis.ToString();
        }
    }
}
