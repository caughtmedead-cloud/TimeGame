using UnityEngine;

/// <summary>
/// Extension methods for performing physics queries in local physics scenes.
/// 
/// CRITICAL FOR TIMELINE PHYSICS ISOLATION:
/// Standard Physics.* methods use the DEFAULT physics scene, which means they interact
/// with objects from ALL timelines. These extension methods ensure physics queries
/// use the LOCAL physics scene of the GameObject's current scene.
/// 
/// USAGE EXAMPLES:
/// 
/// // OLD (broken - uses default physics scene):
/// bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo, distance, layer);
/// 
/// // NEW (correct - uses local physics scene):
/// bool hit = gameObject.PhysicsRaycast(origin, direction, out RaycastHit hitInfo, distance, layer);
/// 
/// // Or if you already have the PhysicsScene:
/// PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
/// bool hit = physicsScene.Raycast(origin, direction, out RaycastHit hitInfo, distance, layer, QueryTriggerInteraction.Ignore);
/// </summary>
public static class LocalPhysicsExtensions
{
    /// <summary>
    /// Performs a raycast using the GameObject's local physics scene.
    /// This ensures the raycast only interacts with objects in the same scene.
    /// </summary>
    public static bool PhysicsRaycast(this GameObject gameObject, Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, LayerMask layerMask, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
        
        if (!physicsScene.IsValid())
        {
            Debug.LogWarning($"[LocalPhysicsExtensions] Physics scene not valid for '{gameObject.scene.name}'. Falling back to default physics.");
            return Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }
        
        return physicsScene.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }
    
    /// <summary>
    /// Performs a spherecast using the GameObject's local physics scene.
    /// </summary>
    public static bool PhysicsSphereCast(this GameObject gameObject, Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, LayerMask layerMask, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
        
        if (!physicsScene.IsValid())
        {
            Debug.LogWarning($"[LocalPhysicsExtensions] Physics scene not valid for '{gameObject.scene.name}'. Falling back to default physics.");
            return Physics.SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }
        
        return physicsScene.SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }
    
    /// <summary>
    /// Performs an overlap sphere check using the GameObject's local physics scene.
    /// </summary>
    public static Collider[] PhysicsOverlapSphere(this GameObject gameObject, Vector3 position, float radius, LayerMask layerMask, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
        
        if (!physicsScene.IsValid())
        {
            Debug.LogWarning($"[LocalPhysicsExtensions] Physics scene not valid for '{gameObject.scene.name}'. Falling back to default physics.");
            return Physics.OverlapSphere(position, radius, layerMask, queryTriggerInteraction);
        }
        
        // PhysicsScene.OverlapSphere requires a Collider array to be pre-allocated
        Collider[] colliders = new Collider[32]; // Reasonable max for most queries
        int count = physicsScene.OverlapSphere(position, radius, colliders, layerMask, queryTriggerInteraction);
        
        // Resize array to actual hit count
        Collider[] results = new Collider[count];
        System.Array.Copy(colliders, results, count);
        
        return results;
    }
    
    /// <summary>
    /// Gets the local PhysicsScene for a GameObject.
    /// Returns default physics scene if local scene is not valid.
    /// </summary>
    public static PhysicsScene GetLocalPhysicsScene(this GameObject gameObject)
    {
        PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
        
        if (!physicsScene.IsValid())
        {
            Debug.LogWarning($"[LocalPhysicsExtensions] Physics scene not valid for '{gameObject.scene.name}'. Using default physics scene.");
            return Physics.defaultPhysicsScene;
        }
        
        return physicsScene;
    }
}