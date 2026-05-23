using System;
using UnityEngine;
using UnityEngine.UI;

namespace NarrationAccessibilityToolkit
{
    // Automatically adds NarrationElement components to common UI objects under this GameObject.
    // This is intended for generated menus and screens where manually tagging every child is impractical.
    [DisallowMultipleComponent]
    public sealed class NarrationAutoTagger : MonoBehaviour
    {
        [SerializeField] private bool scanOnStart = true;
        [SerializeField] private bool continuousScan;
        [SerializeField] private bool includeInactive;
        [SerializeField] private float scanInterval = 0.5f;

        private static readonly Type TmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        private static readonly Type TmpInputFieldType = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro");
        private static readonly Type TmpDropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");

        private float nextScanTime;

        private void Start()
        {
            if (scanOnStart)
            {
                ScanNow();
            }
        }

        private void Update()
        {
            if (!continuousScan || Time.unscaledTime < nextScanTime)
            {
                return;
            }

            ScanNow();
            nextScanTime = Time.unscaledTime + Mathf.Max(0.1f, scanInterval);
        }

        // Scans children and adds missing NarrationElement components to supported UI targets.
        // Returns: The number of GameObjects that were tagged during this scan.
        public int ScanNow()
        {
            int addedCount = 0;
            Transform[] transforms = GetComponentsInChildren<Transform>(includeInactive);
            foreach (Transform child in transforms)
            {
                if (AddNarrationElementIfNeeded(child.gameObject))
                {
                    addedCount++;
                }
            }

            return addedCount;
        }

        private static bool AddNarrationElementIfNeeded(GameObject target)
        {
            if (target.GetComponent<NarrationElement>() != null || !ShouldHaveNarrationElement(target))
            {
                return false;
            }

            target.AddComponent<NarrationElement>();
            return true;
        }

        private static bool ShouldHaveNarrationElement(GameObject target)
        {
            if (target.GetComponent<Selectable>() != null)
            {
                return true;
            }

            bool hasText = target.GetComponent<Text>() != null || HasComponent(target, TmpTextType);
            if (hasText && HasSelectableParent(target))
            {
                return false;
            }

            return hasText || HasComponent(target, TmpInputFieldType) || HasComponent(target, TmpDropdownType);
        }

        private static bool HasSelectableParent(GameObject target)
        {
            Selectable parentSelectable = target.transform.parent == null ? null : target.transform.parent.GetComponentInParent<Selectable>();
            return parentSelectable != null;
        }

        private static bool HasComponent(GameObject target, Type componentType)
        {
            return componentType != null && target.GetComponent(componentType) != null;
        }
    }
}

