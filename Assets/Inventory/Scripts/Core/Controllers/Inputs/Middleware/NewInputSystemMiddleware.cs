#pragma warning disable 0067
using System;
using Inventory.Scripts.Core.ScriptableObjects;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Inventory.Scripts.Core.Controllers.Inputs.Middleware
{
    [CreateAssetMenu(menuName = "Inventory/Configuration/Inputs/New Input System Middleware")]
    public class NewInputSystemMiddleware :
#if ENABLE_INPUT_SYSTEM
        InputMiddleware, InventoryInputActions.IInventoryActions
#else
        InputMiddleware
#endif
    {
        [SerializeField] private InventorySettingsAnchorSo inventorySettingsAnchorSo;

        public override event Action OnGenerateItem;
        public override event Action OnPickupItem;
        public override event Action OnReleaseItem;
        public override event Action OnToggleOptions;
        public override event Action OnRotateItem;

#if ENABLE_INPUT_SYSTEM
        private InventoryInputActions _inventoryInputActions;
#endif

        private Vector2 _cursorPosition;
        private Vector2 _cursorPositionOnPickup;
        private Vector2 _gridMovement;

        private InputState _inputState;
        private bool _isPicking;

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_inventoryInputActions != null)
            {
                return;
            }

            _inventoryInputActions = new InventoryInputActions();
            _inventoryInputActions.Inventory.SetCallbacks(this);

            _inventoryInputActions.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            _inventoryInputActions.Inventory.Disable();
#endif
        }

        public override void Process(InputState inputState)
        {
            if (_cursorPosition != default)
            {
                inputState.CursorPosition = _cursorPosition;
            }

            if (_gridMovement != default)
            {
                inputState.GridMovement = _gridMovement;
            }

            HandlePickupMovementThreshold(inputState);
        }

        private void HandlePickupMovementThreshold(InputState inputState)
        {
#if ENABLE_INPUT_SYSTEM
            var inventoryPlacePickup = _inventoryInputActions.Inventory.PlacePickup;

            // Is Pressing
            if (inventoryPlacePickup.ReadValueAsObject() == null) return;

            inputState.CursorPositionWhenPickUp = _cursorPositionOnPickup;

            if (ShouldHoldToDrag() && inventoryPlacePickup.phase == InputActionPhase.Performed)
            {
                OnPickupItem?.Invoke();
                return;
            }

            OnPickupItem?.Invoke();
            _isPicking = true;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        public void OnCursorPosition(InputAction.CallbackContext context)
        {
            _cursorPosition = context.ReadValue<Vector2>();
        }

        public void OnPlacePickup(InputAction.CallbackContext context)
        {
            _cursorPositionOnPickup = _cursorPosition;

            if (ShouldHoldToDrag())
            {
                switch (context.phase)
                {
                    case InputActionPhase.Performed:
                        OnPickupItem?.Invoke();
                        return;
                    case InputActionPhase.Canceled:
                        _cursorPositionOnPickup = default;
                        OnReleaseItem?.Invoke();
                        return;
                    case InputActionPhase.Disabled:
                    case InputActionPhase.Waiting:
                    case InputActionPhase.Started:
                    default:
                        return;
                }
            }

            // Handle the when not need to drag
            if (context.phase != InputActionPhase.Performed) return;

            if (_isPicking)
            {
                OnReleaseItem?.Invoke();
                _cursorPositionOnPickup = default;
                _isPicking = false;
            }
            else
            {
                OnPickupItem?.Invoke();
                _isPicking = true;
            }
        }


        public void OnOpenOptions(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                OnToggleOptions?.Invoke();
            }
        }

        public void OnRotate(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                OnRotateItem?.Invoke();
            }
        }

        public void OnGridMovement(InputAction.CallbackContext context)
        {
            _gridMovement = context.ReadValue<Vector2>();
        }

        void InventoryInputActions.IInventoryActions.OnGenerateItem(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                OnGenerateItem?.Invoke();
            }
        }

        private bool IsGamepadDevice(InputAction.CallbackContext context) =>
            context.control.device.name.Contains("Gamepad");

        private bool ShouldHoldToDrag()
        {
            return inventorySettingsAnchorSo.InventorySettingsSo.HoldToDrag;
        }
#endif
    }
}