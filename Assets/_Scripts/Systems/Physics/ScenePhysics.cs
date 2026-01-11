using UnityEngine;

public class ScenePhysics : MonoBehaviour
{
    private PhysicsScene _currentPhysicsScene;
    private Transform _transform;

    private void Awake()
    {
        _transform = transform;
        UpdatePhysicsScene();
    }

    private void Update()
    {
        UpdatePhysicsScene();
    }

    private void UpdatePhysicsScene()
    {
        _currentPhysicsScene = gameObject.scene.GetPhysicsScene();
    }

    public bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        if (!_currentPhysicsScene.IsValid())
        {
            return Physics.SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }

        return _currentPhysicsScene.SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hitInfo, Quaternion orientation, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        if (!_currentPhysicsScene.IsValid())
        {
            return Physics.BoxCast(center, halfExtents, direction, out hitInfo, orientation, maxDistance, layerMask, queryTriggerInteraction);
        }

        return _currentPhysicsScene.BoxCast(center, halfExtents, direction, out hitInfo, orientation, maxDistance, layerMask, queryTriggerInteraction);
    }

    public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        if (!_currentPhysicsScene.IsValid())
        {
            return Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }

        return _currentPhysicsScene.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }
}
