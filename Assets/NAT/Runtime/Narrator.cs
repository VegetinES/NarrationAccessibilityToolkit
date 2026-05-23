using System;
using System.Globalization;
using UnityEngine;

namespace NarrationAccessibilityToolkit
{
    // Public entry point for game code that wants to speak text, localized keys, UI elements, values, or states.
    // Use this facade from gameplay scripts instead of talking to platform-specific speech code directly.
    public static class Narrator
    {
        // Returns true when a narration manager exists and narration is currently enabled.
        public static bool IsEnabled => TryGetManager(out NarrationManager manager) && manager.IsEnabled;

        // Gets the active narration manager, creating a default one if the scene does not already contain it.
        public static NarrationManager Manager => EnsureManager();

        // Speaks a string that may be either a localization key or literal text.
        // textOrKey: A plain sentence or an i18n key such as "nav.menu".
        // speechMode: Whether this message should interrupt current speech or be queued.
        public static void Speak(string textOrKey, NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt)
        {
            EnsureManager().SpeakAuto(textOrKey, speechMode);
        }

        // Short alias for Speak(string, NarrationSpeechMode).
        // textOrKey: A plain sentence or an i18n key.
        public static void Say(string textOrKey)
        {
            Speak(textOrKey);
        }

        // Speaks a value that is intended to be resolved as a localization key.
        // key: The localization key to resolve.
        // speechMode: Whether this message should interrupt current speech or be queued.
        public static void SpeakKey(string key, NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt)
        {
            EnsureManager().SpeakKey(key, speechMode);
        }

        // Speaks text exactly as provided, after basic cleanup, without trying to resolve it as a localization key.
        // text: The literal message to speak.
        // speechMode: Whether this message should interrupt current speech or be queued.
        public static void SpeakLiteral(string text, NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt)
        {
            EnsureManager().SpeakLiteral(text, speechMode);
        }

        // Resolves a localized template or literal template and speaks it with formatted arguments.
        // textOrKey: A format string or an i18n key whose value contains placeholders such as {0}.
        // arguments: Values used by string.Format after localization has been resolved.
        public static void SpeakFormat(string textOrKey, params object[] arguments)
        {
            EnsureManager().SpeakFormat(textOrKey, NarrationStringMode.Auto, NarrationSpeechMode.Interrupt, arguments);
        }

        // Queues a formatted message so it does not cut off the current focused UI announcement.
        // textOrKey: A format string or an i18n key whose value contains placeholders.
        // arguments: Values used by string.Format after localization has been resolved.
        public static void SpeakFormatQueued(string textOrKey, params object[] arguments)
        {
            EnsureManager().SpeakFormat(textOrKey, NarrationStringMode.Auto, NarrationSpeechMode.Queue, arguments);
        }

        // Speaks a label and a value as one compact announcement, useful for HUD data such as health or coins.
        // labelOrKey: A literal label or localization key.
        // value: The value to append after the label.
        // speechMode: Whether this message should interrupt current speech or be queued.
        public static void AnnounceValue(string labelOrKey, object value, NarrationSpeechMode speechMode = NarrationSpeechMode.Queue)
        {
            NarrationManager manager = EnsureManager();
            string label = manager.ResolveText(labelOrKey, NarrationStringMode.Auto);
            string valueText = value == null ? string.Empty : Convert.ToString(value, CultureInfo.CurrentCulture);
            manager.SpeakLiteral(NarrationTextUtility.Join(label, valueText), speechMode);
        }

        // Speaks a label followed by an on/off state using the package's localized state words.
        // labelOrKey: A literal label or localization key.
        // isOn: True for the localized "on" phrase, false for the localized "off" phrase.
        // speechMode: Whether this message should interrupt current speech or be queued.
        public static void AnnounceState(string labelOrKey, bool isOn, NarrationSpeechMode speechMode = NarrationSpeechMode.Queue)
        {
            NarrationManager manager = EnsureManager();
            string label = manager.ResolveText(labelOrKey, NarrationStringMode.Auto);
            string state = isOn
                ? manager.ResolveWithFallback("narration.state.on", "activado")
                : manager.ResolveWithFallback("narration.state.off", "desactivado");
            manager.SpeakLiteral(NarrationTextUtility.Join(label, state), speechMode);
        }

        // Speaks the accessible description built by a NarrationElement attached to a GameObject.
        // target: The GameObject that may contain a NarrationElement.
        // speechMode: Whether this message should interrupt current speech or be queued.
        public static void SpeakElement(GameObject target, NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt)
        {
            if (target == null)
            {
                return;
            }

            NarrationElement element = target.GetComponent<NarrationElement>();
            if (element != null)
            {
                element.SpeakNow(speechMode);
            }
        }

        // Toggles narration on or off through the active manager.
        public static void Toggle()
        {
            EnsureManager().ToggleNarrator();
        }

        // Enables or disables narration explicitly.
        // enabled: True to enable narration; false to disable it.
        public static void SetEnabled(bool enabled)
        {
            EnsureManager().SetEnabled(enabled);
        }

        // Changes the spoken language code and asks the i18n source to switch to the matching JSON language.
        // languageCode: A BCP-47-style code such as "es-ES" or "en-US".
        public static void SetLanguage(string languageCode)
        {
            EnsureManager().SetLanguage(languageCode);
        }

        // Stops current speech and clears queued announcements when a manager is available.
        public static void Stop()
        {
            if (TryGetManager(out NarrationManager manager))
            {
                manager.Stop();
            }
            else
            {
                SystemNarrator.Stop();
            }
        }

        // Resolves a literal string or localization key without speaking it.
        // textOrKey: A plain sentence or an i18n key.
        // mode: How the text should be interpreted.
        // Returns: The resolved, cleaned text.
        public static string Resolve(string textOrKey, NarrationStringMode mode = NarrationStringMode.Auto)
        {
            return EnsureManager().ResolveText(textOrKey, mode);
        }

        // Resolves a localization key, returning the supplied fallback if the key is missing.
        // key: The localization key to resolve.
        // fallback: The text to return when no translation is found.
        // Returns: The translated value or fallback text.
        public static string ResolveWithFallback(string key, string fallback)
        {
            return EnsureManager().ResolveWithFallback(key, fallback);
        }

        // Finds the active narration manager without creating a new one.
        // manager: The found manager, or null if none exists.
        // Returns: True when a manager is available.
        public static bool TryGetManager(out NarrationManager manager)
        {
            manager = NarrationManager.Instance;
            if (manager != null)
            {
                return true;
            }

            manager = UnityEngine.Object.FindFirstObjectByType<NarrationManager>();
            return manager != null;
        }

        // Finds the active narration manager or creates a minimal default manager at runtime.
        // Returns: A usable narration manager.
        public static NarrationManager EnsureManager()
        {
            if (TryGetManager(out NarrationManager manager))
            {
                return manager;
            }

            GameObject managerObject = new GameObject("NarrationManager");
            managerObject.AddComponent<NarrationI18nSource>();
            manager = managerObject.AddComponent<NarrationManager>();
            managerObject.AddComponent<NarrationInputToggle>();
            return manager;
        }
    }
}

