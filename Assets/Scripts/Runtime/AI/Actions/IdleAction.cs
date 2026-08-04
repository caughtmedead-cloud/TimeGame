using UnityEngine;

namespace Thelos.AI.Actions
{
    public class IdleAction : AIAction
    {
        [SerializeField] private float idleDuration = 2f;
        [SerializeField] private bool onlyAsFallback = true;
        
        private float idleTimer;
        
        public override string ActionName => "Idle";
        public override float ActionCost => onlyAsFallback ? 100f : 0f;
        
        public override bool CanPerform(AIAgent agent)
        {
            return true;
        }
        
        public override void OnActionStart(AIAgent agent)
        {
            idleTimer = 0f;
            agent.StopMovement();
        }
        
        public override void OnActionUpdate(AIAgent agent)
        {
            idleTimer += Time.deltaTime;
        }
        
        public override void OnActionEnd(AIAgent agent)
        {
            idleTimer = 0f;
        }
        
        public override bool IsComplete(AIAgent agent)
        {
            return idleTimer >= idleDuration;
        }
    }
}
