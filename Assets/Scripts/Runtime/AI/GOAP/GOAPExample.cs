using UnityEngine;
using System.Collections.Generic;

namespace Thelos.AI.GOAP
{
    public class WorldState
    {
        private Dictionary<string, bool> state = new Dictionary<string, bool>();
        
        public bool GetState(string key, bool defaultValue = false)
        {
            return state.ContainsKey(key) ? state[key] : defaultValue;
        }
        
        public void SetState(string key, bool value)
        {
            state[key] = value;
        }
        
        public Dictionary<string, bool> GetAllStates()
        {
            return new Dictionary<string, bool>(state);
        }
    }
    
    public abstract class GOAPAction : AIAction
    {
        public abstract Dictionary<string, bool> GetPreconditions();
        public abstract Dictionary<string, bool> GetEffects();
        
        public bool CheckPreconditions(WorldState worldState)
        {
            foreach (var condition in GetPreconditions())
            {
                if (worldState.GetState(condition.Key) != condition.Value)
                {
                    return false;
                }
            }
            return true;
        }
        
        public void ApplyEffects(WorldState worldState)
        {
            foreach (var effect in GetEffects())
            {
                worldState.SetState(effect.Key, effect.Value);
            }
        }
    }
    
    public class GOAPGoal
    {
        public string Name { get; set; }
        public Dictionary<string, bool> DesiredState { get; set; }
        public float Priority { get; set; }
        
        public GOAPGoal(string name, float priority)
        {
            Name = name;
            Priority = priority;
            DesiredState = new Dictionary<string, bool>();
        }
        
        public void AddCondition(string key, bool value)
        {
            DesiredState[key] = value;
        }
        
        public bool IsSatisfied(WorldState worldState)
        {
            foreach (var condition in DesiredState)
            {
                if (worldState.GetState(condition.Key) != condition.Value)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
