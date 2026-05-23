using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace NarrationAccessibilityToolkit
{
    // Low-level platform bridge for speech output.
    // Gameplay code should normally use Narrator or NarrationManager instead.
    public static class SystemNarrator
    {
        private static System.Diagnostics.Process currentProcess;

        // Whether this build target has a speech path implemented by the package.
        public static bool IsSupported
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                return true;
#else
                return false;
#endif
            }
        }

        // Sends text to the current platform speech implementation.
        // text: The final text to speak.
        // languageCode: Optional speech language code, such as "es-ES".
        // interrupt: True to stop current speech before speaking.
        public static void Speak(string text, string languageCode = null, bool interrupt = true)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string cleanText = text.Trim();
            string cleanLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? NarrationLanguageUtility.ToLanguageCode(Application.systemLanguage) : languageCode.Trim();

            if (interrupt)
            {
                Stop();
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            SpeakAndroid(cleanText, cleanLanguageCode, interrupt);
#elif UNITY_IOS && !UNITY_EDITOR
            PN_Speak(cleanText, cleanLanguageCode, interrupt);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            SpeakWindows(cleanText, cleanLanguageCode, interrupt);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            StartProcess("/usr/bin/say", QuoteArgument(cleanText), interrupt);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            SpeakLinux(cleanText, cleanLanguageCode);
#else
            Debug.Log(cleanText);
#endif
        }

        // Stops speech on the current platform when a stop mechanism is available.
        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            StopAndroid();
#elif UNITY_IOS && !UNITY_EDITOR
            PN_Stop();
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            TryStartDetachedProcess("spd-say", "-C");
            StopCurrentProcess();
#else
            StopCurrentProcess();
#endif
        }

        private static void StartProcess(string fileName, string arguments, bool replaceCurrent = true)
        {
            try
            {
                if (replaceCurrent)
                {
                    StopCurrentProcess();
                }

                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
                if (replaceCurrent)
                {
                    currentProcess = process;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"System narrator process could not start: {exception.Message}");
            }
        }

        private static bool TryStartDetachedProcess(string fileName, string arguments)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void StopCurrentProcess()
        {
            if (currentProcess == null)
            {
                return;
            }

            try
            {
                if (!currentProcess.HasExited)
                {
                    currentProcess.Kill();
                }
            }
            catch
            {
            }
            finally
            {
                currentProcess.Dispose();
                currentProcess = null;
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // Windows does not expose the Narrator screen reader as a general TTS endpoint for Unity.
        // System.Speech is used as a pragmatic desktop fallback that respects installed voices.
        private static void SpeakWindows(string text, string languageCode, bool interrupt)
        {
            string textBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            string culture = Regex.IsMatch(languageCode ?? string.Empty, "^[a-zA-Z]{2,3}(-[a-zA-Z]{2,4})?$") ? languageCode : string.Empty;
            string command =
                "$bytes=[Convert]::FromBase64String('" + textBase64 + "');" +
                "$text=[Text.Encoding]::UTF8.GetString($bytes);" +
                "Add-Type -AssemblyName System.Speech;" +
                "$speaker=New-Object System.Speech.Synthesis.SpeechSynthesizer;" +
                "try{if('" + culture + "'.Length -gt 0){$speaker.SelectVoiceByHints([System.Speech.Synthesis.VoiceGender]::NotSet,[System.Speech.Synthesis.VoiceAge]::NotSet,0,[Globalization.CultureInfo]'" + culture + "')}}catch{};" +
                "$speaker.Speak($text);";
            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            StartProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand, interrupt);
        }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    // Speech Dispatcher is preferred because it integrates with common desktop accessibility stacks.
    // espeak is kept as a lightweight fallback for development machines.
        private static void SpeakLinux(string text, string languageCode)
        {
            string speechDispatcherLanguage = string.IsNullOrWhiteSpace(languageCode) ? string.Empty : "--language=" + QuoteArgument(languageCode) + " ";
            if (TryStartDetachedProcess("spd-say", speechDispatcherLanguage + QuoteArgument(text)))
            {
                return;
            }

            if (TryStartDetachedProcess("espeak", QuoteArgument(text)))
            {
                return;
            }

            Debug.LogWarning("No Linux speech command found. Install speech-dispatcher or espeak to test narration on this machine.");
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject androidTts;
        private static AndroidTtsInitListener androidListener;
        private static bool androidReady;
        private static string androidPendingText;
        private static string androidPendingLanguage;
        private static bool androidPendingInterrupt;

        private static void SpeakAndroid(string text, string languageCode, bool interrupt)
        {
            EnsureAndroidTts();
            if (!androidReady)
            {
                androidPendingText = text;
                androidPendingLanguage = languageCode;
                androidPendingInterrupt = interrupt;
                return;
            }

            RunOnAndroidUiThread(() =>
            {
                if (androidTts == null)
                {
                    return;
                }

                SetAndroidLanguage(languageCode);
                int queueMode = interrupt ? 0 : 1;
                using (AndroidJavaObject parameters = new AndroidJavaObject("android.os.Bundle"))
                {
                    androidTts.Call<int>("speak", text, queueMode, parameters, "NarrationAccessibilityToolkit");
                }
            });
        }

        private static void StopAndroid()
        {
            if (androidTts == null)
            {
                return;
            }

            RunOnAndroidUiThread(() => androidTts.Call<int>("stop"));
        }

        private static void EnsureAndroidTts()
        {
            if (androidTts != null)
            {
                return;
            }

            RunOnAndroidUiThread(() =>
            {
                if (androidTts != null)
                {
                    return;
                }

                androidReady = false;
                androidListener = new AndroidTtsInitListener();
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        if (activity == null)
                        {
                            return;
                        }

                        using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                        {
                            AndroidJavaObject ttsContext = context ?? activity;
                            androidTts = new AndroidJavaObject("android.speech.tts.TextToSpeech", ttsContext, androidListener);
                        }
                    }
                }
            });
        }

        private static void RunOnAndroidUiThread(Action action)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                    {
                        return;
                    }

                    activity.Call("runOnUiThread", new AndroidJavaRunnable(action));
                }
            }
        }

        private static void SetAndroidLanguage(string languageCode)
        {
            if (androidTts == null || string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            string normalizedLanguageCode = languageCode.Replace('_', '-');
            string[] parts = normalizedLanguageCode.Split('-');
            int languageResult;

            using (AndroidJavaObject locale = parts.Length > 1
                ? new AndroidJavaObject("java.util.Locale", parts[0], parts[1])
                : new AndroidJavaObject("java.util.Locale", parts[0]))
            {
                languageResult = androidTts.Call<int>("setLanguage", locale);
            }

            if (languageResult >= 0 || parts.Length <= 1)
            {
                return;
            }

            using (AndroidJavaObject languageOnlyLocale = new AndroidJavaObject("java.util.Locale", parts[0]))
            {
                languageResult = androidTts.Call<int>("setLanguage", languageOnlyLocale);
            }

            if (languageResult >= 0)
            {
                return;
            }

            using (AndroidJavaClass localeClass = new AndroidJavaClass("java.util.Locale"))
            using (AndroidJavaObject defaultLocale = localeClass.CallStatic<AndroidJavaObject>("getDefault"))
            {
                androidTts.Call<int>("setLanguage", defaultLocale);
            }
        }

        private sealed class AndroidTtsInitListener : AndroidJavaProxy
        {
            public AndroidTtsInitListener() : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
            }

            public void onInit(int status)
            {
                androidReady = status == 0;
                if (!androidReady || string.IsNullOrWhiteSpace(androidPendingText))
                {
                    return;
                }

                string pendingText = androidPendingText;
                string pendingLanguage = androidPendingLanguage;
                bool pendingInterrupt = androidPendingInterrupt;
                androidPendingText = null;
                androidPendingLanguage = null;
                SpeakAndroid(pendingText, pendingLanguage, pendingInterrupt);
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PN_Speak(string text, string languageCode, bool interrupt);

        [DllImport("__Internal")]
        private static extern void PN_Stop();
#endif
    }
}

