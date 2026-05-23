using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NarrationAccessibilityToolkit
{
    // Watches a changing value and announces it without requiring UI focus to move.
    // Use this for HUD text, health, coins, score, objectives, and other live game state.
    [DisallowMultipleComponent]
    public sealed class NarrationLiveRegion : MonoBehaviour
    {
        [SerializeField] private string label;
        [SerializeField] private NarrationStringMode labelMode = NarrationStringMode.Auto;
        [SerializeField] private string manualValue;
        [SerializeField] private NarrationStringMode valueMode = NarrationStringMode.Auto;
        [SerializeField] private bool watchDetectedComponent = true;
        [SerializeField] private bool announceOnEnable;
        [SerializeField] private bool announceWhenChanged = true;
        [SerializeField] private float minimumSecondsBetweenAnnouncements = 0.35f;
        [SerializeField] private NarrationSpeechMode speechMode = NarrationSpeechMode.Queue;

        private static readonly Type TmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        private static readonly Type TmpInputFieldType = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro");
        private static readonly Type TmpDropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");

        private string lastValue;
        private float lastAnnouncementTime = -999f;

        private void OnEnable()
        {
            lastValue = ReadCurrentValue();

            if (announceOnEnable)
            {
                AnnounceCurrentValue();
            }
        }

        private void Update()
        {
            if (!announceWhenChanged || !watchDetectedComponent)
            {
                return;
            }

            string currentValue = ReadCurrentValue();
            if (string.Equals(currentValue, lastValue, StringComparison.Ordinal))
            {
                return;
            }

            if (Time.unscaledTime - lastAnnouncementTime < minimumSecondsBetweenAnnouncements)
            {
                return;
            }

            lastValue = currentValue;
            AnnounceValue(currentValue);
        }

        // Sets a manual value and immediately announces the current live region text.
        // value: The new value to store and announce.
        public void SetValue(string value)
        {
            manualValue = value;
            AnnounceCurrentValue();
        }

        // Reads the current detected or manual value and announces it with the configured label.
        public void AnnounceCurrentValue()
        {
            AnnounceValue(ReadCurrentValue());
        }

        // Announces a supplied value with the configured label and speech mode.
        // value: The value to speak after the label.
        public void AnnounceValue(string value)
        {
            NarrationManager manager = Narrator.EnsureManager();
            string resolvedLabel = manager.ResolveText(label, labelMode);
            string resolvedValue = manager.ResolveText(value, valueMode);
            string message = NarrationTextUtility.Join(resolvedLabel, resolvedValue);

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lastAnnouncementTime = Time.unscaledTime;
            manager.SpeakLiteral(message, speechMode);
        }

        // Reads the best available value from known UI components, falling back to the manual value.
        // Returns: The current value as text.
        private string ReadCurrentValue()
        {
            if (!watchDetectedComponent)
            {
                return manualValue;
            }

            Text text = GetComponent<Text>();
            if (text != null)
            {
                return text.text;
            }

            Component tmpText = GetTmpComponent(TmpTextType);
            if (tmpText != null)
            {
                return ReadStringProperty(tmpText, "text");
            }

            Toggle toggle = GetComponent<Toggle>();
            if (toggle != null)
            {
                return toggle.isOn
                    ? Narrator.ResolveWithFallback("narration.state.on", "activado")
                    : Narrator.ResolveWithFallback("narration.state.off", "desactivado");
            }

            Slider slider = GetComponent<Slider>();
            if (slider != null)
            {
                return slider.value.ToString("0.##", CultureInfo.InvariantCulture);
            }

            Scrollbar scrollbar = GetComponent<Scrollbar>();
            if (scrollbar != null)
            {
                int percent = Mathf.RoundToInt(scrollbar.value * 100f);
                return percent.ToString(CultureInfo.InvariantCulture) + " " + Narrator.ResolveWithFallback("narration.value.percent", "por ciento");
            }

            InputField inputField = GetComponent<InputField>();
            if (inputField != null)
            {
                return inputField.text;
            }

            Dropdown dropdown = GetComponent<Dropdown>();
            if (dropdown != null && dropdown.options != null && dropdown.options.Count > 0 && dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
            {
                return dropdown.options[dropdown.value].text;
            }

            Component tmpInputField = GetTmpComponent(TmpInputFieldType);
            if (tmpInputField != null)
            {
                return ReadStringProperty(tmpInputField, "text");
            }

            Component tmpDropdown = GetTmpComponent(TmpDropdownType);
            if (tmpDropdown != null)
            {
                return ReadTmpDropdownText(tmpDropdown);
            }

            return manualValue;
        }

        private Component GetTmpComponent(Type componentType)
        {
            return componentType == null ? null : GetComponent(componentType);
        }

        private static string ReadTmpDropdownText(Component tmpDropdown)
        {
            PropertyInfo valueProperty = tmpDropdown.GetType().GetProperty("value");
            PropertyInfo optionsProperty = tmpDropdown.GetType().GetProperty("options");
            if (valueProperty == null || optionsProperty == null)
            {
                return string.Empty;
            }

            object valueObject = valueProperty.GetValue(tmpDropdown, null);
            object optionsObject = optionsProperty.GetValue(tmpDropdown, null);
            if (!(valueObject is int selectedIndex) || !(optionsObject is IList options) || selectedIndex < 0 || selectedIndex >= options.Count)
            {
                return string.Empty;
            }

            object option = options[selectedIndex];
            return option == null ? string.Empty : ReadStringProperty(option, "text");
        }

        private static string ReadStringProperty(object target, string propertyName)
        {
            if (target == null)
            {
                return string.Empty;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName);
            object value = property == null ? null : property.GetValue(target, null);
            return value == null ? string.Empty : value.ToString();
        }
    }
}

