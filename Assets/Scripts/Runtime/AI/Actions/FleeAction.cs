using UnityEngine;

namespace Thelos.AI.Actions
{
    public class FleeAction : AIAction
    {
        [SerializeField] private string threatTag = "Player";
        [SerializeField] private float fleeDistance = 10f;
        [SerializeField] private float minFleeDistance = 5f;
        [SerializeField] private float detectionRange = 8f;
        
        private Transform threat;
        private Vector2 fleeTarget;
        
        public override string ActionName => "Flee";
        public override float ActionCost => 3f;
        
        public override bool CanPerform(AIAgent agent)
        {
            threat = FindNearestThreat(agent);
            return threat != null;
        }
        
        public override void OnActionStart(AIAgent agent)
        {
            threat = FindNearestThreat(agent);
            if (threat != null)
            {
                CalculateFleeTarget(agent);
                
                Vector3 fleePos3D = new Vector3(fleeTarget.x, agent.transform.position.y, fleeTarget.y);
                agent.SetDestination(fleePos3D);
            }
        }
        
        public override void OnActionUpdate(AIAgent agent)
        {
            if (threat == null)
            {
                threat = FindNearestThreat(agent);
                if (threat == null) return;
            }
            
            float distanceToThreat = Vector3.Distance(agent.transform.position, threat.position);
            
            if (distanceToThreat < minFleeDistance && !agent.HasPath)
            {
                CalculateFleeTarget(agent);
                Vector3 fleePos3D = new Vector3(fleeTarget.x, agent.transform.position.y, fleeTarget.y);
                agent.SetDestination(fleePos3D);
            }
        }
        
        public override void OnActionEnd(AIAgent agent)
        {
            agent.StopMovement();
            threat = null;
        }
        
        public override bool IsComplete(AIAgent agent)
        {
            if (threat == null) return true;
            
            float distanceToThreat = Vector3.Distance(agent.transform.position, threat.position);
            return distanceToThreat >= fleeDistance;
        }
        
        private Transform FindNearestThreat(AIAgent agent)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(agent.transform.position, detectionRange);
            Transform nearest = null;
            float nearestDistance = float.MaxValue;
            
            foreach (Collider2D hit in hits)
            {
                if (hit.CompareTag(threatTag))
                {
                    float distance = Vector2.Distance(agent.transform.position, hit.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = hit.transform;
                    }
                }
            }
            
            return nearest;
        }
        
        private void CalculateFleeTarget(AIAgent agent)
        {
            Vector3 agentPos = agent.transform.position;
            Vector3 threatPos = threat.position;
            
            Vector3 fleeDirection = (agentPos - threatPos).normalized;
            Vector3 fleePos = agentPos + fleeDirection * fleeDistance;
            
            // Find valid point on NavMesh
            fleeTarget = agent.GetRandomPointInRadius(0.5f); // Will clamp to navmesh
            
            // Try to move away from threat
            if (UnityEngine.AI.NavMesh.SamplePosition(fleePos, out UnityEngine.AI.NavMeshHit hit, fleeDistance, UnityEngine.AI.NavMesh.AllAreas))
            {
                fleeTarget = new Vector2(hit.position.x, hit.position.z);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            if (threat != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, threat.position);
                
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(fleeTarget, 0.5f);
            }
        }
    }
}
