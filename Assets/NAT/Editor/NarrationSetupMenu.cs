#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NarrationAccessibilityToolkit.Editor
{
    // Editor menu helpers that create the common narration objects and tag existing scene UI.
    // These commands are intentionally conservative: they add missing components but do not rewrite user UI.
    public static class NarrationSetupMenu
    {
        private static readonly Type TmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        private static readonly Type TmpInputFieldType = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro");
        private static readonly Type TmpDropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");

        // Creates the scene-level narration manager if it does not already exist.
        [MenuItem("Tools/Narration Accessibility Toolkit/Create Narration Manager")]
        public static void CreateNarrationManager()
        {
            NarrationManager existingManager = UnityEngine.Object.FindFirstObjectByType<NarrationManager>();
            if (existingManager != null)
            {
                Selection.activeObject = existingManager.gameObject;
                return;
            }

            GameObject managerObject = new GameObject("NarrationManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Narration Manager");
            managerObject.AddComponent<NarrationI18nSource>();
            managerObject.AddComponent<NarrationManager>();
            NarrationInputToggle inputToggle = managerObject.AddComponent<NarrationInputToggle>();

            InputActionAsset inputActionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputActionAsset != null)
            {
                SerializedObject serializedToggle = new SerializedObject(inputToggle);
                serializedToggle.FindProperty("inputActions").objectReferenceValue = inputActionAsset;
                serializedToggle.ApplyModifiedPropertiesWithoutUndo();
            }

            Selection.activeObject = managerObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // Creates a standalone announcement object for menu titles, tutorials, and UnityEvent-driven speech.
        [MenuItem("Tools/Narration Accessibility Toolkit/Create Announcement Object")]
        public static void CreateAnnouncementObject()
        {
            GameObject announcementObject = new GameObject("NarrationAnnouncement");
            Undo.RegisterCreatedObjectUndo(announcementObject, "Create Narration Announcement");
            announcementObject.AddComponent<NarrationAnnouncement>();
            Selection.activeObject = announcementObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // Creates a standalone live region object for HUD values and dynamic status text.
        [MenuItem("Tools/Narration Accessibility Toolkit/Create Live Region Object")]
        public static void CreateLiveRegionObject()
        {
            GameObject liveRegionObject = new GameObject("NarrationLiveRegion");
            Undo.RegisterCreatedObjectUndo(liveRegionObject, "Create Narration Live Region");
            liveRegionObject.AddComponent<NarrationLiveRegion>();
            Selection.activeObject = liveRegionObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // Adds a runtime auto tagger to each Canvas in the scene.
        [MenuItem("Tools/Narration Accessibility Toolkit/Add Auto Tagger To Scene Canvases")]
        public static void AddAutoTaggerToSceneCanvases()
        {
            int addedCount = 0;
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.GetComponent<NarrationAutoTagger>() != null)
                {
                    continue;
                }

                Undo.AddComponent<NarrationAutoTagger>(canvas.gameObject);
                addedCount++;
            }

            Debug.Log($"Narration Accessibility Toolkit added NarrationAutoTagger to {addedCount} canvases.");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // Adds narration elements to supported UI objects across the active scene.
        [MenuItem("Tools/Narration Accessibility Toolkit/Add NarrationElement To Scene UI")]
        public static void AddNarrationElementsToSceneUi()
        {
            int addedCount = 0;
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform sceneTransform in transforms)
                {
                    if (AddNarrationElementIfNeeded(sceneTransform.gameObject))
                    {
                        addedCount++;
                    }
                }
            }

            Debug.Log($"Narration Accessibility Toolkit added NarrationElement to {addedCount} GameObjects.");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // Adds narration elements to supported UI objects under the current selection.
        [MenuItem("Tools/Narration Accessibility Toolkit/Add NarrationElement To Selection")]
        public static void AddNarrationElementsToSelection()
        {
            int addedCount = 0;
            foreach (GameObject selectedObject in Selection.gameObjects)
            {
                Transform[] transforms = selectedObject.GetComponentsInChildren<Transform>(true);
                foreach (Transform selectedTransform in transforms)
                {
                    if (AddNarrationElementIfNeeded(selectedTransform.gameObject))
                    {
                        addedCount++;
                    }
                }
            }

            Debug.Log($"Narration Accessibility Toolkit added NarrationElement to {addedCount} selected GameObjects.");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static bool AddNarrationElementIfNeeded(GameObject target)
        {
            if (target.GetComponent<NarrationElement>() != null || !ShouldHaveNarrationElement(target))
            {
                return false;
            }

            Undo.AddComponent<NarrationElement>(target);
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
#endif

