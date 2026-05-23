using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NarrationAccessibilityToolkit
{
    // Loads narration-specific localization JSON files from Resources and resolves dotted keys such as "nav.menu".
    // The source keeps a small fallback layer so projects can ship a base language and override it per locale.
    [DisallowMultipleComponent]
    public sealed class NarrationI18nSource : MonoBehaviour
    {
        [SerializeField] private string resourcesFolder = "NarrationI18n";
        [SerializeField] private string languageCode = "es";
        [SerializeField] private string fallbackLanguageCode = "en";
        [SerializeField] private bool useSystemLanguageOnStart;
        [SerializeField] private bool loadOnAwake = true;

        private readonly Dictionary<string, string> translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool loaded;

        // Global i18n source used by the narration manager when one is not assigned explicitly.
        public static NarrationI18nSource Instance { get; private set; }

        // Current short language code used for JSON lookup, for example "es" or "en".
        public string CurrentLanguageCode => languageCode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            if (useSystemLanguageOnStart)
            {
                languageCode = NarrationLanguageUtility.NormalizeForJson(NarrationLanguageUtility.ToLanguageCode(Application.systemLanguage));
            }

            if (loadOnAwake)
            {
                Reload();
            }
        }

        // Changes the active narration language and reloads all translation keys.
        // newLanguageCode: A short or long language code. Long codes are normalized when assets are loaded.
        public void SetLanguage(string newLanguageCode)
        {
            if (string.IsNullOrWhiteSpace(newLanguageCode))
            {
                return;
            }

            languageCode = newLanguageCode.Trim();
            Reload();
        }

        // Resolves text according to the selected string mode.
        // text: Literal text or a dotted i18n key.
        // mode: How the text should be interpreted.
        // Returns: Translated text when available; otherwise cleaned input text.
        public string Resolve(string text, NarrationStringMode mode)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (mode == NarrationStringMode.Literal)
            {
                return NarrationTextUtility.Clean(text);
            }

            string cleanText = NarrationTextUtility.Clean(text);
            if (TryTranslate(cleanText, out string translated))
            {
                return translated;
            }

            return cleanText;
        }

        // Resolves a template and formats it with arguments in one call.
        // text: Literal template text or a dotted i18n key.
        // mode: How the template should be interpreted.
        // arguments: Values consumed by string.Format.
        // Returns: The formatted, speakable text.
        public string ResolveFormat(string text, NarrationStringMode mode, params object[] arguments)
        {
            return NarrationTextUtility.Format(Resolve(text, mode), arguments);
        }

        // Tries to find a translation for an exact key.
        // key: The dotted i18n key to resolve.
        // value: The translated value when found.
        // Returns: True when the translation exists.
        public bool TryTranslate(string key, out string value)
        {
            if (!loaded)
            {
                Reload();
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                value = string.Empty;
                return false;
            }

            return translations.TryGetValue(key.Trim(), out value);
        }

        // Clears and reloads fallback and active language JSON files from Resources.
        public void Reload()
        {
            translations.Clear();

            if (!string.IsNullOrWhiteSpace(fallbackLanguageCode) && !string.Equals(fallbackLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                LoadLanguage(fallbackLanguageCode);
            }

            LoadLanguage(languageCode);
            loaded = true;
        }

        private void LoadLanguage(string code)
        {
            string normalizedCode = code.Trim();
            if (string.IsNullOrEmpty(normalizedCode))
            {
                return;
            }

            LoadLanguageAsset(normalizedCode);

            string shortCode = NarrationLanguageUtility.NormalizeForJson(normalizedCode);
            if (!string.Equals(shortCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                LoadLanguageAsset(shortCode);
            }
        }

        private void LoadLanguageAsset(string code)
        {
            string assetPath = string.IsNullOrWhiteSpace(resourcesFolder)
                ? code
                : resourcesFolder.Trim().Trim('/') + "/" + code;

            TextAsset textAsset = Resources.Load<TextAsset>(assetPath);
            if (textAsset == null)
            {
                return;
            }

            try
            {
                object parsedJson = NarrationJsonParser.Parse(textAsset.text);
                FlattenJson(parsedJson, string.Empty, translations);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Narration i18n JSON could not be parsed at Resources/{assetPath}.json: {exception.Message}");
            }
        }

        private static void FlattenJson(object value, string prefix, Dictionary<string, string> target)
        {
            if (value is IDictionary<string, object> dictionary)
            {
                foreach (KeyValuePair<string, object> pair in dictionary)
                {
                    string nextPrefix = string.IsNullOrEmpty(prefix) ? pair.Key : prefix + "." + pair.Key;
                    FlattenJson(pair.Value, nextPrefix, target);
                }

                return;
            }

            if (value is IList list)
            {
                for (int index = 0; index < list.Count; index++)
                {
                    string nextPrefix = string.IsNullOrEmpty(prefix) ? index.ToString(CultureInfo.InvariantCulture) : prefix + "." + index.ToString(CultureInfo.InvariantCulture);
                    FlattenJson(list[index], nextPrefix, target);
                }

                return;
            }

            if (!string.IsNullOrEmpty(prefix) && value != null)
            {
                target[prefix] = Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        // Small JSON reader used to avoid a package dependency for the simple object/string files used by narration.
        private sealed class NarrationJsonParser
        {
            private readonly string json;
            private int position;

            private NarrationJsonParser(string json)
            {
                this.json = json ?? string.Empty;
            }

            public static object Parse(string json)
            {
                NarrationJsonParser parser = new NarrationJsonParser(json);
                object value = parser.ParseValue();
                parser.SkipWhitespace();
                return value;
            }

            private object ParseValue()
            {
                SkipWhitespace();

                if (position >= json.Length)
                {
                    throw new FormatException("Unexpected end of JSON.");
                }

                char current = json[position];
                if (current == '{')
                {
                    return ParseObject();
                }

                if (current == '[')
                {
                    return ParseArray();
                }

                if (current == '"')
                {
                    return ParseString();
                }

                if (current == 't' || current == 'f')
                {
                    return ParseBoolean();
                }

                if (current == 'n')
                {
                    ParseNull();
                    return null;
                }

                return ParseNumber();
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                position++;
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    return result;
                }

                while (position < json.Length)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    object value = ParseValue();
                    result[key] = value;
                    SkipWhitespace();

                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }

                throw new FormatException("Object was not closed.");
            }

            private List<object> ParseArray()
            {
                List<object> result = new List<object>();
                position++;
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    return result;
                }

                while (position < json.Length)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();

                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }

                throw new FormatException("Array was not closed.");
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder builder = new StringBuilder();

                while (position < json.Length)
                {
                    char current = json[position++];
                    if (current == '"')
                    {
                        return builder.ToString();
                    }

                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (position >= json.Length)
                    {
                        throw new FormatException("Invalid string escape.");
                    }

                    char escape = json[position++];
                    switch (escape)
                    {
                        case '"':
                            builder.Append('"');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case '/':
                            builder.Append('/');
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw new FormatException("Unsupported string escape: " + escape);
                    }
                }

                throw new FormatException("String was not closed.");
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > json.Length)
                {
                    throw new FormatException("Invalid unicode escape.");
                }

                string hex = json.Substring(position, 4);
                position += 4;
                return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            private bool ParseBoolean()
            {
                if (TryConsumeLiteral("true"))
                {
                    return true;
                }

                if (TryConsumeLiteral("false"))
                {
                    return false;
                }

                throw new FormatException("Invalid boolean value.");
            }

            private void ParseNull()
            {
                if (!TryConsumeLiteral("null"))
                {
                    throw new FormatException("Invalid null value.");
                }
            }

            private object ParseNumber()
            {
                int start = position;

                if (json[position] == '-')
                {
                    position++;
                }

                while (position < json.Length && char.IsDigit(json[position]))
                {
                    position++;
                }
                if (position < json.Length && json[position] == '.')
                {
                    position++;
                    while (position < json.Length && char.IsDigit(json[position]))
                    {
                        position++;
                    }
                }
                if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
                {
                    position++;
                    if (position < json.Length && (json[position] == '+' || json[position] == '-'))
                    {
                        position++;
                    }
                    while (position < json.Length && char.IsDigit(json[position]))
                    {
                        position++;
                    }
                }

                string number = json.Substring(start, position - start);
                if (number.IndexOf('.') >= 0 || number.IndexOf('e') >= 0 || number.IndexOf('E') >= 0)
                {
                    return double.Parse(number, CultureInfo.InvariantCulture);
                }

                return long.Parse(number, CultureInfo.InvariantCulture);
            }

            private void SkipWhitespace()
            {
                while (position < json.Length && char.IsWhiteSpace(json[position]))
                {
                    position++;
                }
            }

            private void Expect(char expected)
            {
                if (position >= json.Length || json[position] != expected)
                {
                    throw new FormatException($"Expected '{expected}'.");
                }

                position++;
            }

            private bool TryConsume(char expected)
            {
                if (position < json.Length && json[position] == expected)
                {
                    position++;
                    return true;
                }

                return false;
            }

            private bool TryConsumeLiteral(string literal)
            {
                if (position + literal.Length > json.Length)
                {
                    return false;
                }

                for (int index = 0; index < literal.Length; index++)
                {
                    if (json[position + index] != literal[index])
                    {
                        return false;
                    }
                }

                position += literal.Length;
                return true;
            }
        }
    }
}

