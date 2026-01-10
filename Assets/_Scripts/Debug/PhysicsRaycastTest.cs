using UnityEngine;
using FishNet.Object;

/// <summary>
/// Test script to verify iStep raycasts work with PhysicsMode.Unity + local physics scenes.
/// Attach to your player prefab temporarily for testing.
/// </summary>
public class PhysicsRaycastTest : NetworkBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private LayerMask groundLayer = -1;
    [SerializeField] private float raycastDistance = 2f;
    [SerializeField] private bool showDebugRays = true;
    
    private void FixedUpdate()
    {
        if (!IsOwner) return;
        
        TestRaycast();
    }
    
    private void TestRaycast()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = Vector3.down;
        
        if (Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, groundLayer))
        {
            if (showDebugRays)
            {
                Debug.DrawRay(origin, direction * hit.distance, Color.green);
                Debug.Log($"[PhysicsTest] ✅ Raycast HIT: {hit.collider.name} at distance {hit.distance:F2}");
                Debug.Log($"[PhysicsTest]    Player scene: {gameObject.scene.name}");
                Debug.Log($"[PhysicsTest]    Hit object scene: {hit.collider.gameObject.scene.name}");
            }
        }
        else
        {
            if (showDebugRays)
            {
                Debug.DrawRay(origin, direction * raycastDistance, Color.red);
                Debug.LogWarning($"[PhysicsTest] ❌ Raycast MISS - no ground detected!");
            }
        }
    }
}
