namespace NarrationAccessibilityToolkit
{
    // Describes how a string should be interpreted before it is spoken.
    public enum NarrationStringMode
    {
        // Try to resolve the string as an i18n key first, then speak it literally if no key exists.
        Auto,

        // Treat the string as final user-facing text and skip localization lookup.
        Literal,

        // Treat the string as an intended i18n key. Missing keys currently fall back to the input text.
        I18nKey
    }
}

