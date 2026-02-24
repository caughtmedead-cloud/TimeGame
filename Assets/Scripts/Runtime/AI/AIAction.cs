using UnityEngine;

namespace Thelos.AI
{
    public abstract class AIAction : MonoBehaviour
    {
        public abstract string ActionName { get; }
        public abstract float ActionCost { get; }
        
        public abstract bool CanPerform(AIAgent agent);
        public abstract void OnActionStart(AIAgent agent);
        public abstract void OnActionUpdate(AIAgent agent);
        public abstract void OnActionEnd(AIAgent agent);
        public abstract bool IsComplete(AIAgent agent);
    }
}
