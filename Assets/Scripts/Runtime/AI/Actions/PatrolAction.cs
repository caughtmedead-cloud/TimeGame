using UnityEngine;
using System.Collections.Generic;

namespace Thelos.AI.Actions
{
    public class PatrolAction : AIAction
    {
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float waypointReachedDistance = 0.5f;
        [SerializeField] private float waitTimeAtWaypoint = 1f;
        [SerializeField] private bool loopPatrol = true;
        
        private int currentWaypointIndex = 0;
        private float waitTimer = 0f;
        private bool isWaiting = false;
        
        public override string ActionName => "Patrol";
        public override float ActionCost => 1.5f;
        
        public override bool CanPerform(AIAgent agent)
        {
            return patrolPoints != null && patrolPoints.Length > 0;
        }
        
        public override void OnActionStart(AIAgent agent)
        {
            if (patrolPoints.Length > 0)
            {
                currentWaypointIndex = 0;
                isWaiting = false;
                waitTimer = 0f;
                MoveToCurrentWaypoint(agent);
            }
        }
        
        public override void OnActionUpdate(AIAgent agent)
        {
            if (patrolPoints.Length == 0) return;
            
            if (isWaiting)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= waitTimeAtWaypoint)
                {
                    isWaiting = false;
                    waitTimer = 0f;
                    AdvanceToNextWaypoint(agent);
                }
                return;
            }
            
            if (!agent.HasPath)
            {
                float distanceToWaypoint = Vector2.Distance(
                    agent.transform.position,
                    patrolPoints[currentWaypointIndex].position
                );
                
                if (distanceToWaypoint <= waypointReachedDistance)
                {
                    isWaiting = true;
                    agent.StopMovement();
                }
            }
        }
        
        public override void OnActionEnd(AIAgent agent)
        {
            agent.StopMovement();
        }
        
        public override bool IsComplete(AIAgent agent)
        {
            return false;
        }
        
        private void MoveToCurrentWaypoint(AIAgent agent)
        {
            if (currentWaypointIndex >= 0 && currentWaypointIndex < patrolPoints.Length)
            {
                agent.SetDestination(patrolPoints[currentWaypointIndex].position);
            }
        }
        
        private void AdvanceToNextWaypoint(AIAgent agent)
        {
            currentWaypointIndex++;
            
            if (currentWaypointIndex >= patrolPoints.Length)
            {
                if (loopPatrol)
                {
                    currentWaypointIndex = 0;
                }
                else
                {
                    currentWaypointIndex = patrolPoints.Length - 1;
                    return;
                }
            }
            
            MoveToCurrentWaypoint(agent);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            
            Gizmos.color = Color.cyan;
            
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                
                Gizmos.DrawWireSphere(patrolPoints[i].position, waypointReachedDistance);
                
                if (i < patrolPoints.Length - 1 && patrolPoints[i + 1] != null)
                {
                    Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
                }
                else if (loopPatrol && i == patrolPoints.Length - 1 && patrolPoints[0] != null)
                {
                    Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[0].position);
                }
            }
        }
    }
}
