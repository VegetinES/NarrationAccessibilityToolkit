using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NarrationAccessibilityToolkit
{
    // Describes a GameObject in a way that can be spoken when the object receives focus, hover, click, or submit events.
    // Attach this to UI controls, labels, and custom focus targets that need an accessible spoken description.
    [DisallowMultipleComponent]
    public sealed class NarrationElement : MonoBehaviour, ISelectHandler, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
    {
        [Header("Text")]
        [SerializeField] private string textBefore;
        [SerializeField] private NarrationStringMode textBeforeMode = NarrationStringMode.Auto;
        [SerializeField] private bool readVisibleText = true;
        [SerializeField] private NarrationStringMode visibleTextMode = NarrationStringMode.Auto;

        [Header("Role and value")]
        [SerializeField] private bool readControlType = true;
        [SerializeField] private bool readInteractableState = true;
        [SerializeField] private bool readDetectedValue = true;
        [SerializeField] private string valueOverride;
        [SerializeField] private NarrationStringMode valueOverrideMode = NarrationStringMode.Auto;
        [SerializeField] private string hint;
        [SerializeField] private NarrationStringMode hintMode = NarrationStringMode.Auto;
        [SerializeField] private string textAfter;
        [SerializeField] private NarrationStringMode textAfterMode = NarrationStringMode.Auto;

        [Header("Events")]
        [SerializeField] private bool speakOnSelect = true;
        [SerializeField] private bool speakOnPointerEnter;
        [SerializeField] private bool speakOnClickOrSubmit = true;
        [SerializeField] private NarrationSpeechMode speechMode = NarrationSpeechMode.Interrupt;

        // TextMeshPro is detected by reflection so the package can compile in projects that do not import TMP.
        private static readonly Type TmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        private static readonly Type TmpInputFieldType = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro");
        private static readonly Type TmpDropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");

        // Speaks the element when Unity's EventSystem selects this GameObject.
        // eventData: Selection event data provided by the EventSystem.
        public void OnSelect(BaseEventData eventData)
        {
            if (speakOnSelect)
            {
                SpeakNow();
            }
        }

        // Optionally speaks the element when a pointer enters it.
        // eventData: Pointer event data provided by the EventSystem.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (speakOnPointerEnter)
            {
                SpeakNow();
            }
        }

        // Optionally speaks the element when it is clicked.
        // eventData: Pointer event data provided by the EventSystem.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (speakOnClickOrSubmit)
            {
                SpeakNow();
            }
        }

        // Optionally speaks the element when it is submitted from keyboard, gamepad, or accessibility navigation.
        // eventData: Submit event data provided by the EventSystem.
        public void OnSubmit(BaseEventData eventData)
        {
            if (speakOnClickOrSubmit)
            {
                SpeakNow();
            }
        }

        // Speaks the element using the speech mode configured in the Inspector.
        public void SpeakNow()
        {
            SpeakNow(speechMode);
        }

        // Speaks the element without interrupting the current message.
        public void SpeakNowQueued()
        {
            SpeakNow(NarrationSpeechMode.Queue);
        }

        // Speaks the element using a specific speech mode.
        // mode: Whether to interrupt current speech or queue the element description.
        public void SpeakNow(NarrationSpeechMode mode)
        {
            NarrationManager manager = NarrationManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("No NarrationManager found in the scene.");
                return;
            }

            manager.SpeakLiteral(BuildNarration(), mode);
        }

        // Builds the complete spoken description from configured prefixes, visible text, role, state, value, hints, and suffixes.
        // Returns: A clean sentence ready to be sent to the narration manager.
        public string BuildNarration()
        {
            List<string> parts = new List<string>();
            AppendResolved(parts, textBefore, textBeforeMode);

            if (readVisibleText)
            {
                AppendResolved(parts, GetVisibleText(), visibleTextMode);
            }

            if (readControlType)
            {
                AppendLiteral(parts, GetControlTypeText());
            }

            if (readInteractableState)
            {
                AppendLiteral(parts, GetInteractableStateText());
            }

            if (!string.IsNullOrWhiteSpace(valueOverride))
            {
                AppendResolved(parts, valueOverride, valueOverrideMode);
            }
            else if (readDetectedValue)
            {
                AppendLiteral(parts, GetDetectedValueText());
            }

            AppendResolved(parts, hint, hintMode);
            AppendResolved(parts, textAfter, textAfterMode);

            return string.Join(", ", parts);
        }

        // Changes the leading label or context text used by this element.
        // text: Literal text or an i18n key.
        // mode: How the text should be resolved.
        public void SetTextBefore(string text, NarrationStringMode mode = NarrationStringMode.Auto)
        {
            textBefore = text;
            textBeforeMode = mode;
        }

        // Replaces the automatically detected control value with a custom value or key.
        // value: Literal value or an i18n key.
        // mode: How the value should be resolved.
        public void SetValueOverride(string value, NarrationStringMode mode = NarrationStringMode.Auto)
        {
            valueOverride = value;
            valueOverrideMode = mode;
        }

        // Sets an additional hint, such as the action the player can perform on this element.
        // text: Literal hint text or an i18n key.
        // mode: How the hint should be resolved.
        public void SetHint(string text, NarrationStringMode mode = NarrationStringMode.Auto)
        {
            hint = text;
            hintMode = mode;
        }

        private static void AppendResolved(List<string> parts, string text, NarrationStringMode mode)
        {
            string cleanText = CleanText(text);
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                return;
            }

            NarrationManager manager = NarrationManager.Instance;
            string resolved = manager == null ? cleanText : manager.ResolveText(cleanText, mode);
            AppendLiteral(parts, resolved);
        }

        private static void AppendLiteral(List<string> parts, string text)
        {
            string cleanText = CleanText(text);
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                return;
            }

            for (int index = 0; index < parts.Count; index++)
            {
                if (string.Equals(parts[index], cleanText, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            parts.Add(cleanText);
        }

        private string GetVisibleText()
        {
            List<string> values = new List<string>();
            Text[] uiTexts = GetComponentsInChildren<Text>(true);
            foreach (Text uiText in uiTexts)
            {
                if (uiText != null && uiText.gameObject.activeInHierarchy)
                {
                    AppendLiteral(values, uiText.text);
                }
            }

            if (TmpTextType != null)
            {
                Component[] tmpTexts = GetComponentsInChildren(TmpTextType, true);
                foreach (Component tmpText in tmpTexts)
                {
                    if (tmpText != null && tmpText.gameObject.activeInHierarchy)
                    {
                        AppendLiteral(values, ReadStringProperty(tmpText, "text"));
                    }
                }
            }

            return string.Join(", ", values);
        }

        private string GetControlTypeText()
        {
            if (GetComponent<Toggle>() != null)
            {
                return Phrase("narration.role.toggle", "interruptor");
            }
            if (GetComponent<Button>() != null)
            {
                return Phrase("narration.role.button", "boton");
            }
            if (GetComponent<Slider>() != null)
            {
                return Phrase("narration.role.slider", "control deslizante");
            }
            if (GetComponent<InputField>() != null || GetTmpComponent(TmpInputFieldType) != null)
            {
                return Phrase("narration.role.input", "campo de texto");
            }
            if (GetComponent<Dropdown>() != null || GetTmpComponent(TmpDropdownType) != null)
            {
                return Phrase("narration.role.dropdown", "lista desplegable");
            }

            return string.Empty;
        }

        private string GetInteractableStateText()
        {
            Selectable selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.interactable)
            {
                return Phrase("narration.state.unavailable", "no disponible");
            }

            return string.Empty;
        }

        private string GetDetectedValueText()
        {
            Toggle toggle = GetComponent<Toggle>();
            if (toggle != null)
            {
                return toggle.isOn ? Phrase("narration.state.on", "activado") : Phrase("narration.state.off", "desactivado");
            }

            Slider slider = GetComponent<Slider>();
            if (slider != null)
            {
                string value = FormatNumber(slider.value);
                string maximum = FormatNumber(slider.maxValue);
                return Phrase("narration.value.prefix", "valor") + " " + value + " " + Phrase("narration.value.of", "de") + " " + maximum;
            }

            InputField inputField = GetComponent<InputField>();
            if (inputField != null)
            {
                return FormatInputValue(inputField.text);
            }

            Dropdown dropdown = GetComponent<Dropdown>();
            if (dropdown != null && dropdown.options != null && dropdown.options.Count > 0 && dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
            {
                return Phrase("narration.value.selected", "seleccionado") + " " + dropdown.options[dropdown.value].text;
            }

            Component tmpInputField = GetTmpComponent(TmpInputFieldType);
            if (tmpInputField != null)
            {
                return FormatInputValue(ReadStringProperty(tmpInputField, "text"));
            }

            Component tmpDropdown = GetTmpComponent(TmpDropdownType);
            if (tmpDropdown != null)
            {
                string selectedText = ReadTmpDropdownText(tmpDropdown);
                return string.IsNullOrWhiteSpace(selectedText) ? string.Empty : Phrase("narration.value.selected", "seleccionado") + " " + selectedText;
            }

            Scrollbar scrollbar = GetComponent<Scrollbar>();
            if (scrollbar != null)
            {
                int percent = Mathf.RoundToInt(scrollbar.value * 100f);
                return Phrase("narration.value.prefix", "valor") + " " + percent.ToString(CultureInfo.InvariantCulture) + " " + Phrase("narration.value.percent", "por ciento");
            }

            return string.Empty;
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

        private static string FormatInputValue(string value)
        {
            string cleanValue = CleanText(value);
            if (string.IsNullOrWhiteSpace(cleanValue))
            {
                return Phrase("narration.value.empty", "vacio");
            }

            return Phrase("narration.value.prefix", "valor") + " " + cleanValue;
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string withoutTags = Regex.Replace(text, "<.*?>", string.Empty);
            return Regex.Replace(withoutTags, "\\s+", " ").Trim();
        }

        private static string Phrase(string key, string fallback)
        {
            NarrationManager manager = NarrationManager.Instance;
            return manager == null ? fallback : manager.ResolveWithFallback(key, fallback);
        }
    }
}

