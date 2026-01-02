using Inventory.Scripts.Core.Controllers;
using UnityEngine;

namespace Inventory.Samples
{
    public class SampleCameraHeadBobbing : MonoBehaviour
    {
        private const float BlendSpeed = 8f;

        private const float SmoothInputThreshold = 0.1f; // Threshold for smoothing small movements

        // Smooth transition duration for low input magnitude
        private const float SmoothReturnSpeed = 0.825f;

        [Header("Bob Configuration")] [SerializeField]
        private float walkBobMagnitude = 0.05f;

        [SerializeField] private float runBobMagnitude = 0.10f;

        [Header("Bob Animation")] [SerializeField]
        private AnimationCurve bobCurve = new(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.50f, 0f),
            new Keyframe(0.75f, -1f),
            new Keyframe(1.00f, 0f)
        );

        [Header("Character Movement Settings")] [SerializeField]
        private float moveSpeed = 2f;

        [SerializeField] private float sprintMultiplier = 1.8f;

        [Header("Character Movement Settings")] [SerializeField]
        private SampleCharacterMove characterMove;

        private Vector3 _initialCameraPosition;
        private float _distance;

        private float _magnitudeBlendFactor;
        private float _frequencyBlendFactor;

        // Track the current bobbing value to smooth the transition when stopping
        private float _bobPosition;

        private void Start()
        {
            _initialCameraPosition = transform.localPosition;
        }

        private void Update()
        {
            HandleHeadBobbing();
        }

        private Vector3 HandleMovementInput()
        {
            // Get movement values from the character movement script
            var horizontal = characterMove.InputHorizontal; // Assuming this is how you access horizontal input
            var vertical = characterMove.InputVertical; // Assuming this is how you access vertical input

            // Normalize input to avoid faster diagonal movement
            var inputDir = new Vector3(horizontal, 0, vertical).normalized;

            return inputDir;
        }

        private void HandleHeadBobbing()
        {
            var inputDir = HandleMovementInput();

            // When the input magnitude is low, transition smoothly back to a default state
            if (inputDir.magnitude < SmoothInputThreshold)
            {
                // Smoothly reset the bob position to the idle state
                _bobPosition = Mathf.Lerp(_bobPosition, 0f, Time.deltaTime * SmoothReturnSpeed);
            }
            else
            {
                if (!characterMove.IsWalking || StaticInventoryContext.IsInventoryUIOpened)
                {
                    // Smoothly reset the position if not walking/running
                    _bobPosition = Mathf.Lerp(_bobPosition, 0f, Time.deltaTime * BlendSpeed);
                }
                else
                {
                    // Smoothly transition magnitude and frequency between walking and running
                    var targetMagnitude = characterMove.IsRunning ? runBobMagnitude : walkBobMagnitude;
                    var targetFrequency = characterMove.IsRunning ? 2.5f : 1.5f;

                    // Attenuate the magnitude if moving diagonally while running
                    if (characterMove.IsRunning && inputDir.magnitude > 0.7f) // When moving diagonally
                    {
                        targetMagnitude *=
                            0.6f; // Reduce magnitude to smooth out the bobbing during sprinting diagonally
                    }

                    // Attenuate for sideways movement (side-to-side)
                    if (characterMove.IsRunning &&
                        Mathf.Abs(inputDir.x) > Mathf.Abs(inputDir.z)) // More side-to-side movement
                    {
                        targetFrequency *= 0.7f; // Reduce frequency for sideways sprinting to make it feel slower
                        targetMagnitude *= 0.7f; // Also reduce magnitude for smoother lateral movement
                    }

                    _magnitudeBlendFactor =
                        Mathf.Lerp(_magnitudeBlendFactor, targetMagnitude, Time.deltaTime * BlendSpeed);
                    _frequencyBlendFactor =
                        Mathf.Lerp(_frequencyBlendFactor, targetFrequency, Time.deltaTime * BlendSpeed);

                    // Calculate the movement speed
                    var currentMoveSpeed = moveSpeed * (characterMove.IsRunning ? sprintMultiplier : 1f);
                    _distance += inputDir.magnitude * Time.deltaTime * currentMoveSpeed * _frequencyBlendFactor;
                    _distance %= currentMoveSpeed;

                    // Evaluate the current bobbing curve based on distance
                    _bobPosition =
                        bobCurve.Evaluate(_distance / currentMoveSpeed); // Get the current position in the curve
                }
            }

            // Smooth the curve transition back to 0 when stopping movement
            var deltaPosition = _magnitudeBlendFactor * _bobPosition * Vector3.up;

            // Apply the bobbing effect to the camera's position
            transform.localPosition = _initialCameraPosition + deltaPosition;
        }
    }
}