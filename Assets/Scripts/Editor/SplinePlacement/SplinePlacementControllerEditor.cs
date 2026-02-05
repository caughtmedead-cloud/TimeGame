using UnityEngine;
using UnityEditor;
using SplinePlacement;

namespace SplinePlacementEditor
{
    [CustomEditor(typeof(SplinePlacementController))]
    public class SplinePlacementControllerEditor : Editor
    {
        private SplinePlacementController controller;
        private bool isAddingPoints = false;
        
        private void OnEnable()
        {
            controller = (SplinePlacementController)target;
        }
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Spline Controls", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = isAddingPoints ? Color.green : Color.white;
            if (GUILayout.Button(isAddingPoints ? "Adding Points (Click Scene)" : "Add Points Mode", GUILayout.Height(30)))
            {
                isAddingPoints = !isAddingPoints;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Remove Last Point"))
            {
                Undo.RecordObject(controller, "Remove Spline Point");
                controller.RemoveLastPoint();
            }
            
            if (GUILayout.Button("Clear All Points"))
            {
                if (EditorUtility.DisplayDialog("Clear All Points", 
                    "Are you sure you want to clear all spline points?", "Yes", "No"))
                {
                    Undo.RecordObject(controller, "Clear Spline Points");
                    controller.splinePoints.Clear();
                    controller.ClearSegments();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Regenerate Segments", GUILayout.Height(35)))
            {
                controller.RegenerateSegments();
            }
            
            if (GUILayout.Button("Clear Segments"))
            {
                controller.ClearSegments();
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                $"Spline Points: {controller.splinePoints.Count}\n" +
                "Click 'Add Points Mode' then click in the Scene view to add points.", 
                MessageType.Info);
        }
        
        private void OnSceneGUI()
        {
            if (controller == null)
                return;
            
            if (isAddingPoints)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                
                Event e = Event.current;
                
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    
                    Vector3 worldPosition;
                    if (controller.snapToGround && Physics.Raycast(ray, out RaycastHit hit, 1000f, controller.groundLayer))
                    {
                        worldPosition = hit.point + Vector3.up * controller.groundOffset;
                    }
                    else
                    {
                        Plane plane = new Plane(Vector3.up, controller.transform.position);
                        if (plane.Raycast(ray, out float enter))
                        {
                            worldPosition = ray.GetPoint(enter);
                        }
                        else
                        {
                            worldPosition = controller.transform.position;
                        }
                    }
                    
                    Undo.RecordObject(controller, "Add Spline Point");
                    controller.AddSplinePoint(worldPosition);
                    e.Use();
                }
                
                Handles.BeginGUI();
                GUI.backgroundColor = Color.green;
                GUILayout.BeginArea(new Rect(10, 10, 250, 60));
                GUILayout.Box("ADD POINTS MODE\nClick to add points\nPress ESC to exit", GUILayout.Height(50));
                GUILayout.EndArea();
                GUI.backgroundColor = Color.white;
                Handles.EndGUI();
                
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    isAddingPoints = false;
                    e.Use();
                    Repaint();
                }
            }
            
            DrawSplineHandles();
        }
        
        private void DrawSplineHandles()
        {
            if (controller.splinePoints.Count == 0)
                return;
            
            for (int i = 0; i < controller.splinePoints.Count; i++)
            {
                Vector3 worldPos = controller.transform.TransformPoint(controller.splinePoints[i]);
                
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Move Spline Point");
                    controller.splinePoints[i] = controller.transform.InverseTransformPoint(newWorldPos);
                    if (controller.autoUpdate)
                        controller.RegenerateSegments();
                }
                
                Handles.Label(worldPos + Vector3.up * 0.3f, $"Point {i}");
            }
        }
    }
}
