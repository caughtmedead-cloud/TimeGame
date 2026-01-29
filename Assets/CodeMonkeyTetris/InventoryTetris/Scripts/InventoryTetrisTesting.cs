using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryTetrisTesting : MonoBehaviour {

    [SerializeField] private Transform outerInventoryTetrisBackground;
    [SerializeField] private InventoryTetris inventoryTetris;
    [SerializeField] private InventoryTetris outerInventoryTetris;
    [SerializeField] private List<string> addItemTetrisSaveList;

    private int addItemTetrisSaveListIndex;

    private void Start() {
        outerInventoryTetrisBackground.gameObject.SetActive(false);
    }

    private void Update() {
        if (Keyboard.current.spaceKey.wasPressedThisFrame) {
            outerInventoryTetrisBackground.gameObject.SetActive(true);
            outerInventoryTetris.Load(addItemTetrisSaveList[addItemTetrisSaveListIndex]);

            addItemTetrisSaveListIndex = (addItemTetrisSaveListIndex + 1) % addItemTetrisSaveList.Count;
        }

        if (Keyboard.current.pKey.wasPressedThisFrame) {
            Debug.Log(inventoryTetris.Save());
        }
    }

}
