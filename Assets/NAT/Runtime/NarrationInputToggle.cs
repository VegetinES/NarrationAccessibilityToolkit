using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace NarrationAccessibilityToolkit
{
    // Connects an Input System action to NarrationManager.ToggleNarrator.
    // Projects can bind the toggle to any key, gamepad button, or custom control path they prefer.
    [DisallowMultipleComponent]
    public sealed class NarrationInputToggle : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "UI";
        [SerializeField] private string toggleActionName = "ToggleNarrator";
        [SerializeField, FormerlySerializedAs("createFallbackVKeyAction")] private bool createFallbackToggleAction;
        [SerializeField] private string fallbackBindingPath;

        private InputAction toggleAction;
        private bool ownsToggleAction;

        private void OnEnable()
        {
            BindAction();
        }

        private void OnDisable()
        {
            UnbindAction();
        }

        private void BindAction()
        {
            UnbindAction();

            if (inputActions != null)
            {
                InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
                toggleAction = actionMap == null ? inputActions.FindAction(toggleActionName, false) : actionMap.FindAction(toggleActionName, false);
            }

            if (toggleAction == null && createFallbackToggleAction)
            {
                CreateFallbackAction();
            }

            if (toggleAction == null)
            {
                Debug.LogWarning($"NarrationInputToggle could not find a {toggleActionName} action.");
                return;
            }

            toggleAction.performed += OnTogglePerformed;
            toggleAction.Enable();
        }

        private void CreateFallbackAction()
        {
            if (string.IsNullOrWhiteSpace(fallbackBindingPath))
            {
                Debug.LogWarning("NarrationInputToggle fallback is enabled, but Fallback Binding Path is empty.");
                return;
            }

            string bindingPath = fallbackBindingPath.Trim();

            try
            {
                toggleAction = new InputAction(toggleActionName, InputActionType.Button, bindingPath);
                ownsToggleAction = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"NarrationInputToggle could not create fallback binding '{bindingPath}': {exception.Message}");
            }
        }

        private void UnbindAction()
        {
            if (toggleAction != null)
            {
                toggleAction.performed -= OnTogglePerformed;
                if (ownsToggleAction)
                {
                    toggleAction.Disable();
                    toggleAction.Dispose();
                }
            }

            toggleAction = null;
            ownsToggleAction = false;
        }

        private static void OnTogglePerformed(InputAction.CallbackContext context)
        {
            NarrationManager manager = NarrationManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("No NarrationManager found to toggle narration.");
                return;
            }

            manager.ToggleNarrator();
        }
    }
}

