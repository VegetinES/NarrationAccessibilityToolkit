using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NarrationAccessibilityToolkit
{
    // Text cleanup and composition helpers shared by narration components.
    public static class NarrationTextUtility
    {
        // Removes simple markup and collapses whitespace so text sounds natural when spoken.
        // text: The raw text to clean.
        // Returns: Cleaned text, or an empty string for null/blank input.
        public static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string withoutTags = Regex.Replace(text, "<.*?>", string.Empty);
            return Regex.Replace(withoutTags, "\\s+", " ").Trim();
        }

        // Cleans and formats a template, returning the unformatted template if formatting fails.
        // template: A format string that may contain placeholders such as {0}.
        // arguments: Values consumed by string.Format.
        // Returns: The formatted text, or the cleaned template when no arguments are provided or formatting fails.
        public static string Format(string template, params object[] arguments)
        {
            string cleanTemplate = Clean(template);
            if (string.IsNullOrWhiteSpace(cleanTemplate) || arguments == null || arguments.Length == 0)
            {
                return cleanTemplate;
            }

            try
            {
                return string.Format(CultureInfo.CurrentCulture, cleanTemplate, arguments);
            }
            catch (FormatException)
            {
                return cleanTemplate;
            }
        }

        // Cleans, de-duplicates, and joins text fragments into a concise spoken phrase.
        // parts: Fragments to join, usually label, role, state, value, and hint.
        // Returns: A comma-separated phrase suitable for speech.
        public static string Join(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> cleanParts = new List<string>();
            for (int index = 0; index < parts.Length; index++)
            {
                string cleanPart = Clean(parts[index]);
                if (string.IsNullOrWhiteSpace(cleanPart))
                {
                    continue;
                }

                bool duplicate = false;
                for (int existingIndex = 0; existingIndex < cleanParts.Count; existingIndex++)
                {
                    if (string.Equals(cleanParts[existingIndex], cleanPart, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    cleanParts.Add(cleanPart);
                }
            }

            return string.Join(", ", cleanParts);
        }
    }
}

