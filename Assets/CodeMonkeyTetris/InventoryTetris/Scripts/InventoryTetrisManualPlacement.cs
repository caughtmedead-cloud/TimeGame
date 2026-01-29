using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
using UnityEngine.InputSystem;

public class InventoryTetrisManualPlacement : MonoBehaviour {

    public static InventoryTetrisManualPlacement Instance { get; private set; }

    public event EventHandler OnSelectedChanged;
    public event EventHandler OnObjectPlaced;

    [SerializeField] private Canvas canvas = null;
    [SerializeField] private List<PlacedObjectTypeSO> placedObjectTypeSOList = null;

    private PlacedObjectTypeSO placedObjectTypeSO;
    private PlacedObjectTypeSO.Dir dir;
    private InventoryTetris inventoryTetris;
    private RectTransform canvasRectTransform;
    private RectTransform itemContainer;



    private void Awake() {
        Instance = this;

        inventoryTetris = GetComponent<InventoryTetris>();

        placedObjectTypeSO = null;

        if (canvas == null) {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas != null) {
            canvasRectTransform = canvas.GetComponent<RectTransform>();
        }

        itemContainer = transform.Find("ItemContainer").GetComponent<RectTransform>();
    }

    private void Update() {
        // Try to place
        if (Mouse.current.leftButton.wasPressedThisFrame && placedObjectTypeSO != null) {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(itemContainer, Mouse.current.position.value, null, out Vector2 anchoredPosition);
            
            Vector2Int placedObjectOrigin = inventoryTetris.GetGridPosition(anchoredPosition);

            bool tryPlaceItem = inventoryTetris.TryPlaceItem(placedObjectTypeSO as ItemTetrisSO, placedObjectOrigin, dir);

            if (tryPlaceItem) {
                OnObjectPlaced?.Invoke(this, EventArgs.Empty);
            } else {
                // Cannot build here
                TooltipCanvas.ShowTooltip_Static("Cannot Build Here!");
                FunctionTimer.Create(() => { TooltipCanvas.HideTooltip_Static(); }, 2f, "HideTooltip", true, true);
            }
        }

        if (Keyboard.current.rKey.wasPressedThisFrame) {
            dir = PlacedObjectTypeSO.GetNextDir(dir);
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[0]; RefreshSelectedObjectType(); }
        if (Keyboard.current.digit2Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[1]; RefreshSelectedObjectType(); }
        if (Keyboard.current.digit3Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[2]; RefreshSelectedObjectType(); }
        if (Keyboard.current.digit4Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[3]; RefreshSelectedObjectType(); }
        if (Keyboard.current.digit5Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[4]; RefreshSelectedObjectType(); }
        if (Keyboard.current.digit6Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[5]; RefreshSelectedObjectType(); }
        if (Keyboard.current.digit7Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[6]; RefreshSelectedObjectType(); }
        if (Keyboard.current.digit8Key.wasPressedThisFrame) { placedObjectTypeSO = placedObjectTypeSOList[7]; RefreshSelectedObjectType(); }

        if (Keyboard.current.digit0Key.wasPressedThisFrame) { DeselectObjectType(); }

        // Demolish
        /*
        if (Mouse.current.rightButton.wasPressedThisFrame) {
            Vector3 mousePosition = UtilsClass.GetMouseWorldPosition();
            PlacedObject placedObject = grid.GetGridObject(mousePosition).GetPlacedObject();
            if (placedObject != null) {
                // Demolish
                placedObject.DestroySelf();

                List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
                foreach (Vector2Int gridPosition in gridPositionList) {
                    grid.GetGridObject(gridPosition.x, gridPosition.y).ClearPlacedObject();
                }
            }
        }
        */
    }

    private void DeselectObjectType() {
        placedObjectTypeSO = null; RefreshSelectedObjectType();
    }

    private void RefreshSelectedObjectType() {
        OnSelectedChanged?.Invoke(this, EventArgs.Empty);
    }

    public Vector2 GetCanvasSnappedPosition() {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(itemContainer, Mouse.current.position.value, null, out Vector2 anchoredPosition);
        inventoryTetris.GetGrid().GetXY(anchoredPosition, out int x, out int y);

        if (placedObjectTypeSO != null) {
            Vector2Int rotationOffset = placedObjectTypeSO.GetRotationOffset(dir);
            Vector2 placedObjectCanvas = inventoryTetris.GetGrid().GetWorldPosition(x, y) + new Vector3(rotationOffset.x, rotationOffset.y) * inventoryTetris.GetGrid().GetCellSize();
            return placedObjectCanvas;
        } else {
            return anchoredPosition;
        }
    }

    public Quaternion GetPlacedObjectRotation() {
        if (placedObjectTypeSO != null) {
            return Quaternion.Euler(0, 0, -placedObjectTypeSO.GetRotationAngle(dir));
        } else {
            return Quaternion.identity;
        }
    }

    public PlacedObjectTypeSO GetPlacedObjectTypeSO() {
        return placedObjectTypeSO;
    }



}
