using UnityEngine;

namespace Thelos.AI.Actions
{
    public class FollowAction : AIAction
    {
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private float followDistance = 2f;
        [SerializeField] private float updatePathInterval = 0.5f;
        [SerializeField] private float detectionRange = 15f;
        
        private Transform followTarget;
        private float pathUpdateTimer;
        
        public override string ActionName => "Follow";
        public override float ActionCost => 2f;
        
        public override bool CanPerform(AIAgent agent)
        {
            followTarget = agent.FindNearestTarget(targetTag);
            return followTarget != null;
        }
        
        public override void OnActionStart(AIAgent agent)
        {
            followTarget = agent.FindNearestTarget(targetTag);
            pathUpdateTimer = 0f;
            
            if (followTarget != null)
            {
                agent.SetDestination(followTarget.position);
            }
        }
        
        public override void OnActionUpdate(AIAgent agent)
        {
            if (followTarget == null)
            {
                followTarget = agent.FindNearestTarget(targetTag);
                if (followTarget == null) return;
            }
            
            float distanceToTarget = Vector3.Distance(agent.transform.position, followTarget.position);
            
            if (distanceToTarget > detectionRange)
            {
                followTarget = null;
                return;
            }
            
            pathUpdateTimer += Time.deltaTime;
            
            if (pathUpdateTimer >= updatePathInterval)
            {
                if (distanceToTarget > followDistance)
                {
                    agent.SetDestination(followTarget.position);
                }
                else
                {
                    agent.StopMovement();
                }
                
                pathUpdateTimer = 0f;
            }
        }
        
        public override void OnActionEnd(AIAgent agent)
        {
            agent.StopMovement();
            followTarget = null;
        }
        
        public override bool IsComplete(AIAgent agent)
        {
            return followTarget == null;
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            if (followTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, followTarget.position);
                Gizmos.DrawWireSphere(followTarget.position, followDistance);
            }
        }
    }
}
