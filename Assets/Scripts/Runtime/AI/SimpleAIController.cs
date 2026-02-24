using UnityEngine;
using System.Collections.Generic;

namespace Thelos.AI
{
    public class SimpleAIController : MonoBehaviour
    {
        [Header("Available Actions")]
        [SerializeField] private List<AIAction> availableActions = new List<AIAction>();
        
        [Header("Decision Making")]
        [SerializeField] private float decisionInterval = 1f;
        [SerializeField] private bool allowActionInterruption = false;
        
        private AIAgent agent;
        private float decisionTimer;
        private AIAction currentAction;
        
        private void Awake()
        {
            agent = GetComponent<AIAgent>();
            
            if (availableActions.Count == 0)
            {
                availableActions.AddRange(GetComponents<AIAction>());
            }
        }
        
        private void Update()
        {
            decisionTimer += Time.deltaTime;
            
            // Check if current action is complete
            if (currentAction != null && currentAction.IsComplete(agent))
            {
                currentAction = null;
                MakeDecision();
                decisionTimer = 0f;
            }
            // Only make new decisions if we allow interruption or no action is running
            else if (decisionTimer >= decisionInterval)
            {
                if (allowActionInterruption || currentAction == null)
                {
                    MakeDecision();
                }
                decisionTimer = 0f;
            }
        }
        
        private void MakeDecision()
        {
            AIAction bestAction = null;
            float bestScore = float.MaxValue;
            
            foreach (AIAction action in availableActions)
            {
                if (action.CanPerform(agent))
                {
                    float score = action.ActionCost;
                    
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestAction = action;
                    }
                }
            }
            
            // Only switch if we found a different action
            if (bestAction != null && bestAction != currentAction)
            {
                currentAction = bestAction;
                agent.SetAction(bestAction);
                
                AIBehaviorDebugger debugger = GetComponent<AIBehaviorDebugger>();
                if (debugger != null)
                {
                    debugger.OnActionChanged(bestAction);
                }
            }
        }
    }
}
