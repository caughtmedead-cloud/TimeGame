using UnityEngine;

namespace Thelos.AI.Actions
{
    public class WanderAction : AIAction
    {
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float minWaitTime = 1f;
        [SerializeField] private float maxWaitTime = 3f;
        [SerializeField] private bool completeAfterEachPoint = true;
        
        private float wanderTimer;
        private float currentWaitTime;
        private bool hasDestination;
        
        public override string ActionName => "Wander";
        public override float ActionCost => 10f;
        
        public override bool CanPerform(AIAgent agent)
        {
            return true;
        }
        
        public override void OnActionStart(AIAgent agent)
        {
            wanderTimer = 0f;
            hasDestination = false;
            currentWaitTime = Random.Range(minWaitTime, maxWaitTime);
            PickNewWanderPoint(agent);
        }
        
        public override void OnActionUpdate(AIAgent agent)
        {
            wanderTimer += Time.deltaTime;
            
            // Check if reached destination or no path
            bool reachedDestination = !agent.IsMoving && hasDestination;
            
            if (reachedDestination && wanderTimer >= currentWaitTime)
            {
                currentWaitTime = Random.Range(minWaitTime, maxWaitTime);
                PickNewWanderPoint(agent);
                wanderTimer = 0f;
            }
        }
        
        public override void OnActionEnd(AIAgent agent)
        {
            agent.StopMovement();
        }
        
        public override bool IsComplete(AIAgent agent)
        {
            // If set to complete after each point, return true after waiting
            if (completeAfterEachPoint)
            {
                bool reachedDestination = !agent.IsMoving && hasDestination;
                return reachedDestination && wanderTimer >= currentWaitTime;
            }
            
            // Otherwise, never complete (wander forever)
            return false;
        }
        
        private void PickNewWanderPoint(AIAgent agent)
        {
            Vector3 wanderPoint = agent.GetRandomPointInRadius(wanderRadius);
            hasDestination = agent.SetDestination(wanderPoint);
        }
    }
}
