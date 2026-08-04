using UnityEngine;

namespace Thelos.AI.Actions
{
    public class EatDeadBodyAction : AIAction
    {
        [SerializeField] private float eatDuration = 3f;
        [SerializeField] private float interactionRange = 1.5f;
        [SerializeField] private float eatCooldown = 10f;
        [SerializeField] private string deadBodyTag = "DeadBody";
        
        private float eatTimer;
        private Transform targetBody;
        private bool isEating;
        
        public override string ActionName => "Eat Dead Body";
        public override float ActionCost => 2f;
        
        public override bool CanPerform(AIAgent agent)
        {
            if (agent == null)
            {
                Debug.LogWarning("EatAction: Agent is null");
                return false;
            }
            
            // Check cooldown
            float timeSinceLastEat = agent.TimeSinceLastEat;
            bool cooldownActive = timeSinceLastEat < eatCooldown;
            
            // ALWAYS log for debugging
            Debug.Log($"EatAction CanPerform: TimeSince={timeSinceLastEat:F1}s, Cooldown={eatCooldown}s, Active={cooldownActive}");
            
            if (cooldownActive)
            {
                return false;
            }
            
            try
            {
                Transform body = agent.FindNearestTarget(deadBodyTag);
                
                if (body != null)
                {
                    float distance = Vector3.Distance(agent.transform.position, body.position);
                    Debug.Log($"EatAction: CAN PERFORM! Body at {distance:F1}m, cooldown expired ({timeSinceLastEat:F1}s)");
                    return true;
                }
                else
                {
                    Debug.Log($"EatAction: Cooldown expired but NO BODY FOUND (detection radius: check AIAgent)");
                    return false;
                }
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"EatAction: Exception - {ex.Message}");
                return false;
            }
        }
        
        public override void OnActionStart(AIAgent agent)
        {
            eatTimer = 0f;
            isEating = false;
            targetBody = agent.FindNearestTarget(deadBodyTag);
            
            if (targetBody != null)
            {
                agent.SetDestination(targetBody.position);
            }
        }
        
        public override void OnActionUpdate(AIAgent agent)
        {
            if (targetBody == null)
            {
                return;
            }
            
            float distanceToBody = Vector3.Distance(agent.transform.position, targetBody.position);
            
            if (distanceToBody <= interactionRange && !isEating)
            {
                isEating = true;
                agent.StopMovement();
                
                // Disable NavMeshAgent to prevent clipping during animation
                UnityEngine.AI.NavMeshAgent navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.enabled = false;
                }
                
                agent.TriggerEatAnimation();
                Debug.Log($"Started eating! Will take {eatDuration} seconds.");
            }
            
            if (isEating)
            {
                eatTimer += Time.deltaTime;
                
                // Debug every second
                if (Mathf.Floor(eatTimer) != Mathf.Floor(eatTimer - Time.deltaTime))
                {
                    Debug.Log($"Eating... {eatTimer:F1}s / {eatDuration}s");
                }
            }
        }
        
        public override void OnActionEnd(AIAgent agent)
        {
            // Stop eating animation
            agent.StopEatAnimation();
            
            // Re-enable NavMeshAgent
            UnityEngine.AI.NavMeshAgent navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.enabled = true;
            }
            
            // If we actually ate (not just cancelled early), start cooldown
            if (isEating && eatTimer >= eatDuration)
            {
                // Mark that eating finished - START COOLDOWN
                agent.OnFinishedEating();
                Debug.Log($"Finished eating after {eatTimer:F1} seconds. Cooldown started.");
                
                // Check if body should be destroyed
                if (targetBody != null)
                {
                    DeadBody deadBodyComponent = targetBody.GetComponent<DeadBody>();
                    if (deadBodyComponent != null && deadBodyComponent.IsInfinite)
                    {
                        Debug.Log("Body is infinite, not destroying.");
                    }
                    else
                    {
                        Debug.Log("Destroying body.");
                        Destroy(targetBody.gameObject);
                    }
                }
            }
            else
            {
                Debug.Log($"Action ended early - eating not complete (isEating: {isEating}, timer: {eatTimer:F1}s / {eatDuration}s)");
            }
            
            eatTimer = 0f;
            isEating = false;
            targetBody = null;
        }
        
        public override bool IsComplete(AIAgent agent)
        {
            if (targetBody == null)
            {
                return true;
            }
            
            return isEating && eatTimer >= eatDuration;
        }
    }
}
