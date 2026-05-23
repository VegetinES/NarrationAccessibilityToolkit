using System.Collections;
using UnityEngine;

namespace NarrationAccessibilityToolkit
{
    // Scene component for announcements that are not tied to a focused UI element.
    // It is useful for screen titles, tutorials, checkpoints, save messages, and UnityEvent hooks.
    [DisallowMultipleComponent]
    public sealed class NarrationAnnouncement : MonoBehaviour
    {
        [SerializeField] private string message;
        [SerializeField] private NarrationStringMode messageMode = NarrationStringMode.Auto;
        [SerializeField] private NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt;
        [SerializeField] private bool speakOnStart;
        [SerializeField] private bool speakOnEnable;
        [SerializeField] private bool speakOnlyOnce;
        [SerializeField] private float delaySeconds;

        private bool hasSpoken;
        private Coroutine pendingSpeech;

        private void OnEnable()
        {
            if (speakOnEnable)
            {
                SpeakConfigured();
            }
        }

        private void Start()
        {
            if (speakOnStart)
            {
                SpeakConfigured();
            }
        }

        private void OnDisable()
        {
            if (pendingSpeech != null)
            {
                StopCoroutine(pendingSpeech);
                pendingSpeech = null;
            }
        }

        // Speaks the configured message using the Inspector settings.
        public void SpeakConfigured()
        {
            if (speakOnlyOnce && hasSpoken)
            {
                return;
            }

            if (delaySeconds <= 0f)
            {
                hasSpoken = true;
                SpeakMessage(message, messageMode, speechMode);
                return;
            }

            if (pendingSpeech != null)
            {
                StopCoroutine(pendingSpeech);
            }

            pendingSpeech = StartCoroutine(SpeakAfterDelay());
        }

        // Speaks text that may be either a localization key or a literal sentence.
        // textOrKey: Literal text or an i18n key.
        public void Speak(string textOrKey)
        {
            SpeakMessage(textOrKey, NarrationStringMode.Auto, speechMode);
        }

        // Speaks a value intended to be resolved as a localization key.
        // key: The i18n key to speak.
        public void SpeakKey(string key)
        {
            SpeakMessage(key, NarrationStringMode.I18nKey, speechMode);
        }

        // Speaks literal text without localization lookup.
        // text: The literal message to speak.
        public void SpeakLiteral(string text)
        {
            SpeakMessage(text, NarrationStringMode.Literal, speechMode);
        }

        // Queues a message so it does not interrupt current speech.
        // textOrKey: Literal text or an i18n key.
        public void SpeakQueued(string textOrKey)
        {
            SpeakMessage(textOrKey, NarrationStringMode.Auto, NarrationSpeechMode.Queue);
        }

        private IEnumerator SpeakAfterDelay()
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            hasSpoken = true;
            SpeakMessage(message, messageMode, speechMode);
            pendingSpeech = null;
        }

        private static void SpeakMessage(string text, NarrationStringMode mode, NarrationSpeechMode speechMode)
        {
            NarrationManager manager = Narrator.EnsureManager();
            manager.Speak(text, mode, speechMode);
        }
    }
}

