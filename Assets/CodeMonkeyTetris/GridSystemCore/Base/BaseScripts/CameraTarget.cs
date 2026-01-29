using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
using UnityEngine.InputSystem;

public class CameraTarget : MonoBehaviour {

    public enum Axis {
        XZ,
        XY,
    }

    [SerializeField] private Axis axis = Axis.XZ;
    [SerializeField] private float moveSpeed = 50f;



    private void Update() {
        float moveX = 0f;
        float moveY = 0f;

        if (Keyboard.current.wKey.isPressed) {
            moveY = +1f;
        }
        if (Keyboard.current.sKey.isPressed) {
            moveY = -1f;
        }
        if (Keyboard.current.aKey.isPressed) {
            moveX = -1f;
        }
        if (Keyboard.current.dKey.isPressed) {
            moveX = +1f;
        }

        Vector3 moveDir;

        switch (axis) {
            default:
            case Axis.XZ:
                moveDir = new Vector3(moveX, 0, moveY).normalized;
                break;
            case Axis.XY:
                moveDir = new Vector3(moveX, moveY).normalized;
                break;
        }

        if (moveX != 0 || moveY != 0) {
            // Not idle
        }

        if (axis == Axis.XZ) {
            moveDir = UtilsClass.ApplyRotationToVectorXZ(moveDir, 30f);
        }

        transform.position += ((transform.forward * moveY) + (transform.right * moveX)) * moveSpeed * Time.deltaTime;



        float rotateY = 0f;
        if (Keyboard.current.qKey.isPressed) {
            rotateY = +1f;
        }
        if (Keyboard.current.eKey.isPressed) {
            rotateY = -1f;
        }

        float rotateSpeed = 180f;
        transform.eulerAngles += new Vector3(0, rotateY, 0) * rotateSpeed * Time.deltaTime;
    }

}
