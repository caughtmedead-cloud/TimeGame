using UnityEngine;

namespace Inventory.Samples
{
    public class SampleCameraMove : MonoBehaviour
    {
        [Range(0.1f, 10f)] public float sensitivity = 0.78f; // Mouse sensitivity
        public float smoothSpeed = 0.05f; // Smoothness of camera rotation
        public bool enableSmoothing = true; // Toggle smoothing on or off

        private Vector2 targetRotation; // Target rotation from mouse input
        private Vector2 currentRotation; // Smoothed rotation value
        private bool _isFreeze;

        private void Start()
        {
            // Lock the cursor to the center of the screen
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            if (_isFreeze) return;

            // Get raw mouse input
            var mouseX = Input.GetAxis("Mouse X") * sensitivity;
            var mouseY = Input.GetAxis("Mouse Y") * sensitivity;

            // Update target rotation directly with mouse input
            targetRotation.x += mouseX;
            targetRotation.y -= mouseY;

            // Clamp the up and down rotation to avoid flipping the camera
            targetRotation.y = Mathf.Clamp(targetRotation.y, -90f, 90f);

            // Apply smoothing if enabled
            if (enableSmoothing)
            {
                // Smoothly interpolate between current and target rotation for gradual movement
                currentRotation = Vector2.Lerp(currentRotation, targetRotation, smoothSpeed);
            }
            else
            {
                // Directly set rotation to target for immediate response
                currentRotation = targetRotation;
            }

            // Apply rotation to the camera
            transform.localRotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0f);
        }

        public void Freeze()
        {
            _isFreeze = true;
        }

        public void Unfreeze()
        {
            _isFreeze = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}