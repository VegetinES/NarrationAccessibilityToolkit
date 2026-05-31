using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NarrationAccessibilityToolkit.Examples
{
    [DisallowMultipleComponent]
    public sealed class NarrationExampleFocusNavigator : MonoBehaviour
    {
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private RectTransform searchRoot;
        [SerializeField] private RectTransform focusFrame;
        [SerializeField] private TMP_Text focusReadout;
        [SerializeField] private Selectable firstSelection;
        [SerializeField] private float frameHorizontalPadding = 22f;
        [SerializeField] private float frameVerticalPadding = 18f;
        [SerializeField] private float minimumFrameWidth = 64f;
        [SerializeField] private float minimumFrameHeight = 50f;

        private readonly List<Selectable> navigationItems = new List<Selectable>();
        private readonly List<Selectable> scanBuffer = new List<Selectable>();
        private readonly List<Slider> sliderBuffer = new List<Slider>();
        private readonly List<Scrollbar> scrollbarBuffer = new List<Scrollbar>();
        private readonly List<TMP_Text> textBuffer = new List<TMP_Text>();
        private GameObject lastFocusedObject;
        private bool navigationDirty = true;

        private void Awake()
        {
            if (eventSystem == null)
            {
                eventSystem = EventSystem.current;
            }

            RefreshNavigationItems();
            EnsureSelection();
            UpdateFocusFrame(true);
        }

        private void OnEnable()
        {
            navigationDirty = true;
        }

        private void LateUpdate()
        {
            RefreshNavigationItems();
            EnsureSelection();
            UpdateFocusFrame(false);
            SyncRangeReadouts();
        }

        private void RefreshNavigationItems()
        {
            RectTransform root = searchRoot;
            if (root == null)
            {
                return;
            }

            scanBuffer.Clear();
            root.GetComponentsInChildren(false, scanBuffer);

            bool changed = navigationDirty;
            int navigableIndex = 0;
            for (int index = 0; index < scanBuffer.Count; index++)
            {
                Selectable item = scanBuffer[index];
                if (!IsNavigable(item))
                {
                    continue;
                }

                if (!changed && (navigableIndex >= navigationItems.Count || navigationItems[navigableIndex] != item))
                {
                    changed = true;
                }

                navigableIndex++;
            }

            if (!changed && navigableIndex != navigationItems.Count)
            {
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            navigationItems.Clear();
            for (int index = 0; index < scanBuffer.Count; index++)
            {
                Selectable item = scanBuffer[index];
                if (IsNavigable(item))
                {
                    navigationItems.Add(item);
                }
            }

            LinkNavigation();
            navigationDirty = false;
        }

        private void LinkNavigation()
        {
            for (int index = 0; index < navigationItems.Count; index++)
            {
                Selectable item = navigationItems[index];
                Selectable previous = index > 0 ? navigationItems[index - 1] : null;
                Selectable next = index + 1 < navigationItems.Count ? navigationItems[index + 1] : null;
                bool usesHorizontalValueInput = UsesHorizontalValueInput(item);
                bool usesVerticalValueInput = UsesVerticalValueInput(item);

                // Keep the value axis free for sliders and scrollbars; Unity changes their value only when that side has no target.
                Navigation navigation = item.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = usesVerticalValueInput ? null : previous;
                navigation.selectOnDown = usesVerticalValueInput ? null : next;
                navigation.selectOnLeft = usesHorizontalValueInput ? null : previous;
                navigation.selectOnRight = usesHorizontalValueInput ? null : next;
                item.navigation = navigation;
            }
        }

        private void EnsureSelection()
        {
            if (eventSystem == null || GetCurrentSelectedControl() != null)
            {
                return;
            }

            Selectable target = IsNavigable(firstSelection) ? firstSelection : GetFirstNavigableItem();
            if (target != null)
            {
                eventSystem.SetSelectedGameObject(target.gameObject);
            }
        }

        private Selectable GetCurrentSelectedControl()
        {
            if (eventSystem == null)
            {
                return null;
            }

            GameObject selectedObject = eventSystem.currentSelectedGameObject;
            Selectable selected = selectedObject == null ? null : selectedObject.GetComponent<Selectable>();
            if (selected == null && selectedObject != null)
            {
                selected = selectedObject.GetComponentInParent<Selectable>();
            }

            return IsNavigable(selected) ? selected : null;
        }

        private Selectable GetFirstNavigableItem()
        {
            for (int index = 0; index < navigationItems.Count; index++)
            {
                Selectable item = navigationItems[index];
                if (IsNavigable(item))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool IsNavigable(Selectable item)
        {
            return item != null && item.gameObject.activeInHierarchy && item.IsInteractable();
        }

        private void UpdateFocusFrame(bool force)
        {
            if (focusFrame == null || searchRoot == null)
            {
                return;
            }

            Selectable selected = GetCurrentSelectedControl();
            if (selected == null)
            {
                focusFrame.gameObject.SetActive(false);
                if (focusReadout != null)
                {
                    focusReadout.text = "FOCUS --";
                }

                return;
            }

            RectTransform selectedRect = selected.transform as RectTransform;
            if (selectedRect == null)
            {
                focusFrame.gameObject.SetActive(false);
                return;
            }

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(searchRoot, selectedRect);
            focusFrame.gameObject.SetActive(true);
            focusFrame.anchorMin = new Vector2(0.5f, 0.5f);
            focusFrame.anchorMax = new Vector2(0.5f, 0.5f);
            focusFrame.pivot = new Vector2(0.5f, 0.5f);
            focusFrame.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);
            focusFrame.sizeDelta = new Vector2(
                Mathf.Max(minimumFrameWidth, bounds.size.x + frameHorizontalPadding),
                Mathf.Max(minimumFrameHeight, bounds.size.y + frameVerticalPadding));
            focusFrame.SetAsLastSibling();

            if (force || selected.gameObject != lastFocusedObject || selected is Slider || selected is Scrollbar)
            {
                lastFocusedObject = selected.gameObject;
                UpdateFocusReadout(selected);
            }
        }

        private void UpdateFocusReadout(Selectable selected)
        {
            if (focusReadout == null)
            {
                return;
            }

            int index = navigationItems.IndexOf(selected);
            string focusIndex = index < 0 ? "--" : (index + 1).ToString("00");
            string focusCount = navigationItems.Count.ToString("00");
            focusReadout.text = "FOCUS " + focusIndex + "/" + focusCount + "  " + GetFocusLabel(selected.gameObject);
        }

        private void SyncRangeReadouts()
        {
            if (searchRoot == null)
            {
                return;
            }

            sliderBuffer.Clear();
            scrollbarBuffer.Clear();
            textBuffer.Clear();
            searchRoot.GetComponentsInChildren(false, sliderBuffer);
            searchRoot.GetComponentsInChildren(false, scrollbarBuffer);
            searchRoot.GetComponentsInChildren(false, textBuffer);

            for (int index = 0; index < sliderBuffer.Count; index++)
            {
                Slider slider = sliderBuffer[index];
                TMP_Text valueText = FindValueText(GetSliderDisplayName(slider));
                if (valueText != null)
                {
                    valueText.text = FormatSliderValue(slider);
                }
            }

            for (int index = 0; index < scrollbarBuffer.Count; index++)
            {
                Scrollbar scrollbar = scrollbarBuffer[index];
                TMP_Text valueText = FindValueText(GetScrollbarDisplayName(scrollbar));
                if (valueText != null)
                {
                    valueText.text = FormatScrollbarValue(scrollbar);
                }
            }
        }

        private TMP_Text FindValueText(string controlName)
        {
            if (string.IsNullOrWhiteSpace(controlName))
            {
                return null;
            }

            string expectedName = controlName + " Value";
            for (int index = 0; index < textBuffer.Count; index++)
            {
                TMP_Text text = textBuffer[index];
                if (text != null && text.gameObject.name == expectedName)
                {
                    return text;
                }
            }

            return null;
        }

        private static string GetSliderDisplayName(Slider slider)
        {
            return slider.gameObject.name.Replace(" Slider", string.Empty).Trim();
        }

        private static string GetScrollbarDisplayName(Scrollbar scrollbar)
        {
            return scrollbar.gameObject.name.Replace(" Scrollbar", string.Empty).Trim();
        }

        private static string FormatSliderValue(Slider slider)
        {
            int roundedValue = Mathf.RoundToInt(slider.value);
            string sliderName = GetSliderDisplayName(slider).ToLowerInvariant();
            if (sliderName == "volume" || sliderName == "brightness")
            {
                return roundedValue + "%";
            }

            return roundedValue.ToString();
        }

        private static string FormatScrollbarValue(Scrollbar scrollbar)
        {
            return Mathf.RoundToInt(scrollbar.value * 100f) + "%";
        }

        private static bool UsesHorizontalValueInput(Selectable item)
        {
            Slider slider = item as Slider;
            if (slider != null)
            {
                return slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft;
            }

            Scrollbar scrollbar = item as Scrollbar;
            return scrollbar != null && (scrollbar.direction == Scrollbar.Direction.LeftToRight || scrollbar.direction == Scrollbar.Direction.RightToLeft);
        }

        private static bool UsesVerticalValueInput(Selectable item)
        {
            Slider slider = item as Slider;
            if (slider != null)
            {
                return slider.direction == Slider.Direction.BottomToTop || slider.direction == Slider.Direction.TopToBottom;
            }

            Scrollbar scrollbar = item as Scrollbar;
            return scrollbar != null && (scrollbar.direction == Scrollbar.Direction.BottomToTop || scrollbar.direction == Scrollbar.Direction.TopToBottom);
        }

        private static string GetFocusLabel(GameObject target)
        {
            Slider slider = target.GetComponent<Slider>();
            if (slider != null)
            {
                return GetSliderDisplayName(slider) + " " + FormatSliderValue(slider);
            }

            Scrollbar scrollbar = target.GetComponent<Scrollbar>();
            if (scrollbar != null)
            {
                return GetScrollbarDisplayName(scrollbar) + " " + FormatScrollbarValue(scrollbar);
            }

            TMP_Dropdown dropdown = target.GetComponent<TMP_Dropdown>();
            if (dropdown != null && dropdown.captionText != null && !string.IsNullOrWhiteSpace(dropdown.captionText.text))
            {
                return dropdown.captionText.text.Trim();
            }

            TMP_InputField inputField = target.GetComponent<TMP_InputField>();
            if (inputField != null && !string.IsNullOrWhiteSpace(inputField.text))
            {
                return inputField.text.Trim();
            }

            TMP_Text[] texts = target.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                TMP_Text text = texts[index];
                if (text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                {
                    return text.text.Trim();
                }
            }

            return target.name.Replace(" Button", string.Empty).Replace("_", " ").Trim();
        }
    }
}