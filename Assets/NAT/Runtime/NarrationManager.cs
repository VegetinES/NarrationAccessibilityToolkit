using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NarrationAccessibilityToolkit
{
    // Central runtime service for narration state, language selection, localization lookup, and speech queueing.
    // A scene normally contains one manager, either created manually or through the setup menu.
    [DisallowMultipleComponent]
    public sealed class NarrationManager : MonoBehaviour
    {
        [SerializeField] private bool startEnabled = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private string speechLanguageCode = "es-ES";
        [SerializeField] private bool useSystemLanguageOnStart;
        [SerializeField] private NarrationI18nSource i18nSource;
        [SerializeField] private bool announceToggle = true;
        [SerializeField] private string enabledAnnouncement = "narration.manager.enabled";
        [SerializeField] private string disabledAnnouncement = "narration.manager.disabled";
        [SerializeField] private NarrationSpeechMode defaultSpeechMode = NarrationSpeechMode.Interrupt;
        [SerializeField] private int maximumQueuedMessages = 8;
        [SerializeField] private float queuedCharactersPerSecond = 18f;
        [SerializeField] private float queuedMinimumSeconds = 1.2f;
        [SerializeField] private float queuedMaximumSeconds = 8f;

        // The queue is intentionally simple: platform speech APIs do not expose a consistent completion event,
        // so queued messages are spaced by a conservative duration estimate instead.
        private readonly Queue<NarrationSpeechRequest> speechQueue = new Queue<NarrationSpeechRequest>();
        private Coroutine queueCoroutine;

        // Global manager instance used by the package facade and UI components.
        public static NarrationManager Instance { get; private set; }

        // Whether narration is currently allowed to speak new messages.
        public bool IsEnabled { get; private set; }

        // Current speech culture used by the platform narrator, for example "es-ES" or "en-US".
        public string SpeechLanguageCode => speechLanguageCode;

        // Default behavior for messages when callers do not choose a queueing strategy explicitly.
        public NarrationSpeechMode DefaultSpeechMode => defaultSpeechMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (useSystemLanguageOnStart)
            {
                speechLanguageCode = NarrationLanguageUtility.ToLanguageCode(Application.systemLanguage);
            }

            if (i18nSource == null)
            {
                i18nSource = GetComponent<NarrationI18nSource>();
            }

            IsEnabled = startEnabled;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Switches narration between enabled and disabled states.
        public void ToggleNarrator()
        {
            SetEnabled(!IsEnabled);
        }

        // Enables or disables narration. Disabling clears queued speech and optionally announces the change first.
        // enabled: True to enable narration, false to disable it.
        public void SetEnabled(bool enabled)
        {
            if (IsEnabled == enabled)
            {
                return;
            }

            if (enabled)
            {
                IsEnabled = true;
                if (announceToggle)
                {
                    Speak(ResolveWithFallback(enabledAnnouncement, "Narrador activado"), true);
                }
                return;
            }

            if (announceToggle)
            {
                SystemNarrator.Speak(ResolveWithFallback(disabledAnnouncement, "Narrador desactivado"), speechLanguageCode, true);
            }

            ClearQueue();
            IsEnabled = false;
        }

        // Changes the platform speech language and asks the i18n source to load the matching short language code.
        // languageCode: A language code such as "es-ES", "en-US", or "ja-JP".
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            speechLanguageCode = languageCode.Trim();

            NarrationI18nSource source = GetI18nSource();
            if (source != null)
            {
                source.SetLanguage(NarrationLanguageUtility.NormalizeForJson(languageCode));
            }
        }

        // Speaks a message using automatic string resolution and a legacy interrupt flag.
        // message: Literal text or an i18n key.
        // interrupt: True to interrupt current speech; false to queue the message.
        public void Speak(string message, bool interrupt = true)
        {
            Speak(message, NarrationStringMode.Auto, interrupt ? NarrationSpeechMode.Interrupt : NarrationSpeechMode.Queue);
        }

        // Speaks a message with explicit string resolution and speech mode.
        // message: The text, key, or template to speak.
        // stringMode: How the message should be interpreted before speaking.
        // speechMode: Whether the message should interrupt or be queued.
        public void Speak(string message, NarrationStringMode stringMode, NarrationSpeechMode speechMode)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string resolvedMessage = ResolveText(message, stringMode);
            SpeakResolved(resolvedMessage, speechMode);
        }

        // Speaks text using automatic localization lookup.
        // textOrKey: Literal text or an i18n key.
        // speechMode: Whether the message should interrupt or be queued.
        public void SpeakAuto(string textOrKey, NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt)
        {
            Speak(textOrKey, NarrationStringMode.Auto, speechMode);
        }

        // Speaks literal text without trying to resolve it as a localization key.
        // text: The literal message to speak.
        // speechMode: Whether the message should interrupt or be queued.
        public void SpeakLiteral(string text, NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt)
        {
            Speak(text, NarrationStringMode.Literal, speechMode);
        }

        // Speaks a value that is intended to be a localization key.
        // key: The localization key to resolve.
        // speechMode: Whether the message should interrupt or be queued.
        public void SpeakKey(string key, NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt)
        {
            Speak(key, NarrationStringMode.I18nKey, speechMode);
        }

        // Resolves a template, formats it with arguments, and speaks the final sentence.
        // textOrKey: Literal template text or a key pointing to a template.
        // stringMode: How the template should be interpreted.
        // speechMode: Whether the message should interrupt or be queued.
        // arguments: Arguments consumed by string.Format.
        public void SpeakFormat(string textOrKey, NarrationStringMode stringMode, NarrationSpeechMode speechMode, params object[] arguments)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(textOrKey))
            {
                return;
            }

            string resolvedMessage = ResolveText(textOrKey, stringMode);
            SpeakResolved(NarrationTextUtility.Format(resolvedMessage, arguments), speechMode);
        }

        // Resolves several text fragments and speaks them as one natural comma-separated message.
        // stringMode: How each fragment should be interpreted.
        // speechMode: Whether the message should interrupt or be queued.
        // parts: Text fragments, keys, labels, or values.
        public void SpeakParts(NarrationStringMode stringMode, NarrationSpeechMode speechMode, params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return;
            }

            List<string> resolvedParts = new List<string>();
            for (int index = 0; index < parts.Length; index++)
            {
                resolvedParts.Add(ResolveText(parts[index], stringMode));
            }

            SpeakResolved(NarrationTextUtility.Join(resolvedParts.ToArray()), speechMode);
        }

        // Stops current speech and clears queued messages.
        public void Stop()
        {
            ClearQueue();
            SystemNarrator.Stop();
        }

        // Resolves text according to the selected mode, using the configured i18n source when available.
        // text: Literal text or a localization key.
        // mode: How the text should be interpreted.
        // Returns: Clean, speakable text.
        public string ResolveText(string text, NarrationStringMode mode = NarrationStringMode.Auto)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            NarrationI18nSource source = GetI18nSource();
            return source == null ? text.Trim() : source.Resolve(text, mode);
        }

        // Resolves a key with an explicit fallback for missing translations.
        // key: Localization key to resolve.
        // fallback: Text returned when the key is missing.
        // Returns: The translated value or fallback text.
        public string ResolveWithFallback(string key, string fallback)
        {
            NarrationI18nSource source = GetI18nSource();
            if (source != null && source.TryTranslate(key, out string translated))
            {
                return translated;
            }

            return fallback;
        }

        private void SpeakResolved(string resolvedMessage, NarrationSpeechMode speechMode)
        {
            string cleanMessage = NarrationTextUtility.Clean(resolvedMessage);
            if (string.IsNullOrWhiteSpace(cleanMessage))
            {
                return;
            }

            NarrationSpeechMode mode = speechMode;
            if (mode != NarrationSpeechMode.Interrupt && mode != NarrationSpeechMode.Queue)
            {
                mode = defaultSpeechMode;
            }

            if (mode == NarrationSpeechMode.Interrupt)
            {
                ClearQueue();
                SystemNarrator.Speak(cleanMessage, speechLanguageCode, true);
                return;
            }

            EnqueueSpeech(cleanMessage);
        }

        private void EnqueueSpeech(string message)
        {
            if (maximumQueuedMessages > 0)
            {
                while (speechQueue.Count >= maximumQueuedMessages)
                {
                    speechQueue.Dequeue();
                }
            }

            speechQueue.Enqueue(new NarrationSpeechRequest(message, speechLanguageCode));
            if (queueCoroutine == null)
            {
                queueCoroutine = StartCoroutine(ProcessSpeechQueue());
            }
        }

        private IEnumerator ProcessSpeechQueue()
        {
            while (speechQueue.Count > 0)
            {
                NarrationSpeechRequest request = speechQueue.Dequeue();
                SystemNarrator.Speak(request.Message, request.LanguageCode, true);
                yield return new WaitForSecondsRealtime(EstimateSpeechSeconds(request.Message));
            }

            queueCoroutine = null;
        }

        private float EstimateSpeechSeconds(string message)
        {
            float charactersPerSecond = Mathf.Max(1f, queuedCharactersPerSecond);
            float estimatedSeconds = NarrationTextUtility.Clean(message).Length / charactersPerSecond;
            return Mathf.Clamp(estimatedSeconds, queuedMinimumSeconds, queuedMaximumSeconds);
        }

        private void ClearQueue()
        {
            speechQueue.Clear();
            if (queueCoroutine != null)
            {
                StopCoroutine(queueCoroutine);
                queueCoroutine = null;
            }
        }

        private NarrationI18nSource GetI18nSource()
        {
            if (i18nSource != null)
            {
                return i18nSource;
            }

            i18nSource = NarrationI18nSource.Instance;
            return i18nSource;
        }

        // Lightweight queue item that captures both message and language at enqueue time.
        private readonly struct NarrationSpeechRequest
        {
            public NarrationSpeechRequest(string message, string languageCode)
            {
                Message = message;
                LanguageCode = languageCode;
            }

            public string Message { get; }

            public string LanguageCode { get; }
        }
    }
}

