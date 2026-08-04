using UnityEngine;
using System.Text;

namespace Thelos.AI
{
    public class AIBehaviorDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool logActionChanges = false;
        [SerializeField] private Color debugTextColor = Color.white;
        [SerializeField] private Vector2 debugTextOffset = new Vector2(0, 2);
        
        private AIAgent agent;
        private SimpleAIController controller;
        private string currentActionName = "None";
        private float actionDuration = 0f;
        
        private void Awake()
        {
            agent = GetComponent<AIAgent>();
            controller = GetComponent<SimpleAIController>();
        }
        
        private void Update()
        {
            actionDuration += Time.deltaTime;
        }
        
        public void OnActionChanged(AIAction newAction)
        {
            if (newAction != null)
            {
                if (logActionChanges)
                {
                    Debug.Log($"[{gameObject.name}] Action: {currentActionName} ({actionDuration:F1}s) → {newAction.ActionName}");
                }
                
                currentActionName = newAction.ActionName;
                actionDuration = 0f;
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo || agent == null) return;
            
            Vector3 worldPos = transform.position + (Vector3)debugTextOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0)
            {
                StringBuilder info = new StringBuilder();
                info.AppendLine($"Action: {currentActionName}");
                info.AppendLine($"Duration: {actionDuration:F1}s");
                info.AppendLine($"Moving: {agent.IsMoving}");
                info.AppendLine($"Velocity: {agent.Velocity}");
                info.AppendLine($"Has Path: {agent.HasPath}");
                
                if (agent.CurrentTarget != null)
                {
                    float distance = Vector2.Distance(transform.position, agent.CurrentTarget.position);
                    info.AppendLine($"Target Dist: {distance:F1}");
                }
                
                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    normal = { textColor = debugTextColor },
                    alignment = TextAnchor.MiddleCenter
                };
                
                Vector2 size = style.CalcSize(new GUIContent(info.ToString()));
                Rect rect = new Rect(
                    screenPos.x - size.x / 2,
                    Screen.height - screenPos.y - size.y / 2,
                    size.x,
                    size.y
                );
                
                GUI.Label(rect, info.ToString(), style);
            }
        }
    }
}
