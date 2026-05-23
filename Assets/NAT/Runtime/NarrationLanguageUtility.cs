using UnityEngine;

namespace NarrationAccessibilityToolkit
{
    // Small language helper used to bridge Unity's SystemLanguage enum, platform voice culture codes, and JSON filenames.
    public static class NarrationLanguageUtility
    {
        // Converts Unity's system language enum into a culture-like code suitable for speech engines.
        // language: Unity's detected system language.
        // Returns: A speech language code such as "es-ES" or "en-US".
        public static string ToLanguageCode(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Spanish:
                    return "es-ES";
                case SystemLanguage.English:
                    return "en-US";
                case SystemLanguage.French:
                    return "fr-FR";
                case SystemLanguage.German:
                    return "de-DE";
                case SystemLanguage.Italian:
                    return "it-IT";
                case SystemLanguage.Portuguese:
                    return "pt-PT";
                case SystemLanguage.Japanese:
                    return "ja-JP";
                case SystemLanguage.Korean:
                    return "ko-KR";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return "zh-CN";
                case SystemLanguage.ChineseTraditional:
                    return "zh-TW";
                case SystemLanguage.Russian:
                    return "ru-RU";
                case SystemLanguage.Arabic:
                    return "ar-SA";
                default:
                    return "en-US";
            }
        }

        // Converts a long language code into the short lowercase code used by narration JSON files.
        // languageCode: A code such as "es-ES", "es_ES", or "es".
        // Returns: A short code such as "es".
        public static string NormalizeForJson(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return "en";
            }

            string normalized = languageCode.Trim().Replace('_', '-');
            int separatorIndex = normalized.IndexOf('-');
            return separatorIndex > 0 ? normalized.Substring(0, separatorIndex).ToLowerInvariant() : normalized.ToLowerInvariant();
        }
    }
}

