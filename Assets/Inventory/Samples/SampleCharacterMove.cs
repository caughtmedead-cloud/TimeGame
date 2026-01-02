using Inventory.Scripts.Core.Controllers;
using UnityEngine;

namespace Inventory.Samples
{
    [RequireComponent(typeof(CharacterController))]
    public class SampleCharacterMove : MonoBehaviour
    {
        public float moveSpeed = 2f;
        public float sprintMultiplier = 1.8f;
        public float acceleration = 10.0f;
        public float deceleration = 15.0f;

        public Transform cameraTransform; // Reference to the camera inside the character

        private CharacterController characterController;
        private Vector3 velocity;
        private Vector3 moveDirection;
        public float InputHorizontal { get; private set; }
        public float InputVertical { get; private set; }
        public bool IsWalking { get; private set; }
        public bool IsRunning { get; private set; }
        public float CurrentSpeed { get; private set; }

        private void Start()
        {
            characterController = GetComponent<CharacterController>();

            // Automatically set the camera transform if not assigned
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            GroundCheck();
            HandleMovement();
            ApplyGravity();
        }

        private void GroundCheck()
        {
            // Reset vertical velocity if grounded
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Small value to keep grounded detection stable
            }
        }

        private void HandleMovement()
        {
            // Use Input.GetAxisRaw for instantaneous input response
            InputHorizontal = Input.GetAxisRaw("Horizontal");
            InputVertical = Input.GetAxisRaw("Vertical");
            var inputDir = new Vector3(InputHorizontal, 0, InputVertical).normalized;

            // Determine if the character is walking or running
            IsWalking = inputDir.magnitude > 0;

            if (IsWalking && Input.GetKey(KeyCode.LeftShift))
            {
                IsRunning = true;
            }
            else
            {
                IsRunning = false;
            }

            if (StaticInventoryContext.IsInventoryUIOpened)
            {
                return;
            }
            
            // Calculate camera-relative movement direction
            var cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            var cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

            // Smoothly transition between movement directions for smoother strafing
            moveDirection = Vector3.Lerp(moveDirection, cameraRight * inputDir.x + cameraForward * inputDir.z,
                Time.deltaTime * 10f);

            // Determine the target speed based on input and sprint status
            var targetSpeed = inputDir.magnitude * moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                targetSpeed *= sprintMultiplier;
            }

            if (Mathf.Abs(InputHorizontal) > 0 && InputVertical == 0)
            {
                targetSpeed *= 1.1f; // Slightly increase speed for smoother strafing
            }

            // Smoothly interpolate speed for smoother acceleration/deceleration
            if (inputDir.magnitude > 0)
            {
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, targetSpeed, Time.deltaTime * acceleration);
            }
            else
            {
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, 0, Time.deltaTime * deceleration);
            }

            // Move the character based on the calculated direction and speed
            characterController.Move(moveDirection * CurrentSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (!characterController.isGrounded)
            {
                velocity.y += Physics.gravity.y * Time.deltaTime;
            }

            // Apply vertical velocity for gravity effect
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}