using UnityEngine;
using UnityEngine.AI;

namespace Thelos.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIAgent : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float rotationSpeed = 120f;
        [SerializeField] private float stoppingDistance = 0.5f;
        
        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 10f;
        [SerializeField] private LayerMask targetMask;
        
        [Header("Debug")]
        [SerializeField] private bool drawDebugPath = true;
        
        private NavMeshAgent navAgent;
        private AIAction currentAction;
        private Animator animator;
        private float lastEatTime = -999f;
        
        public Transform CurrentTarget { get; set; }
        public bool HasPath => navAgent != null && navAgent.hasPath;
        public bool IsMoving { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float TimeSinceLastEat => Time.time - lastEatTime;
        
        private static readonly int ANIM_MOVE_X = Animator.StringToHash("MoveX");
        private static readonly int ANIM_MOVE_Y = Animator.StringToHash("MoveY");
        private static readonly int ANIM_IS_MOVING = Animator.StringToHash("IsMoving");
        private static readonly int ANIM_SPEED = Animator.StringToHash("Speed");
        private static readonly int ANIM_EAT_TRIGGER = Animator.StringToHash("Eat");
        private static readonly int ANIM_IS_EATING = Animator.StringToHash("IsEating");
        
        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            
            if (navAgent != null)
            {
                navAgent.speed = moveSpeed;
                navAgent.angularSpeed = rotationSpeed;
                navAgent.stoppingDistance = stoppingDistance;
                navAgent.autoBraking = true;
                navAgent.updateRotation = true;
                navAgent.updateUpAxis = false;
            }
        }
        
        private void Update()
        {
            if (currentAction != null)
            {
                currentAction.OnActionUpdate(this);
                
                if (currentAction.IsComplete(this))
                {
                    EndCurrentAction();
                }
            }
            
            UpdateMovement();
            UpdateAnimator();
        }
        
        public void SetAction(AIAction action)
        {
            if (currentAction != null)
            {
                EndCurrentAction();
            }
            
            currentAction = action;
            currentAction.OnActionStart(this);
        }
        
        private void EndCurrentAction()
        {
            if (currentAction != null)
            {
                currentAction.OnActionEnd(this);
                currentAction = null;
            }
        }
        
        public bool SetDestination(Vector3 target)
        {
            if (navAgent == null || !navAgent.isOnNavMesh)
            {
                return false;
            }
            
            NavMeshPath path = new NavMeshPath();
            if (navAgent.CalculatePath(target, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                navAgent.SetPath(path);
                return true;
            }
            
            return false;
        }
        
        public void StopMovement()
        {
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.ResetPath();
            }
        }
        
        private void UpdateMovement()
        {
            if (navAgent == null) return;
            
            IsMoving = navAgent.velocity.magnitude > 0.1f;
            
            if (IsMoving)
            {
                Vector3 worldVelocity = navAgent.velocity.normalized;
                Velocity = new Vector2(worldVelocity.x, worldVelocity.z);
            }
            else
            {
                Velocity = Vector2.zero;
            }
        }
        
        private void UpdateAnimator()
        {
            if (animator == null) return;
            
            animator.SetBool(ANIM_IS_MOVING, IsMoving);
            
            if (IsMoving && navAgent != null)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
                
                animator.SetFloat(ANIM_MOVE_X, localVelocity.x);
                animator.SetFloat(ANIM_MOVE_Y, localVelocity.z);
                animator.SetFloat(ANIM_SPEED, navAgent.velocity.magnitude);
            }
            else
            {
                animator.SetFloat(ANIM_MOVE_X, 0);
                animator.SetFloat(ANIM_MOVE_Y, 0);
                animator.SetFloat(ANIM_SPEED, 0);
            }
        }
        
        public void OnFinishedEating()
        {
            lastEatTime = Time.time;
        }
        
        public void TriggerEatAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger(ANIM_EAT_TRIGGER);
                animator.SetBool(ANIM_IS_EATING, true);
            }
        }
        
        public void StopEatAnimation()
        {
            if (animator != null)
            {
                animator.SetBool(ANIM_IS_EATING, false);
            }
        }
        
        public Transform FindNearestTarget(string targetTag)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, targetMask);
            Transform nearest = null;
            float nearestDistance = float.MaxValue;
            
            foreach (Collider hit in hits)
            {
                if (hit != null)
                {
                    try
                    {
                        if (hit.CompareTag(targetTag))
                        {
                            float distance = Vector3.Distance(transform.position, hit.transform.position);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearest = hit.transform;
                            }
                        }
                    }
                    catch (UnityException)
                    {
                    }
                }
            }
            
            return nearest;
        }
        
        public Vector3 GetRandomPointInRadius(float radius)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPoint = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                return hit.position;
            }
            
            return transform.position;
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            
            if (drawDebugPath && navAgent != null && navAgent.hasPath)
            {
                Gizmos.color = Color.green;
                Vector3[] corners = navAgent.path.corners;
                
                for (int i = 0; i < corners.Length - 1; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[i + 1]);
                    Gizmos.DrawSphere(corners[i], 0.1f);
                }
                
                if (corners.Length > 0)
                {
                    Gizmos.DrawSphere(corners[corners.Length - 1], 0.2f);
                }
            }
        }
    }
}
