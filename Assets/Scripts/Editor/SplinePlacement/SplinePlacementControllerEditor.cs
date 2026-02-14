using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using SplinePlacement;

namespace SplinePlacementEditor
{
    [CustomEditor(typeof(SplinePlacementController))]
    public class SplinePlacementControllerEditor : Editor
    {
        private SplinePlacementController controller;
        private bool isDrawingMode = false;
        private bool isDragging = false;
        
        private Vector3 dragStartPosition;
        private Vector3 currentDragPosition;
        private Vector3 lastSnappedPosition;
        private Vector3 currentDirection = Vector3.forward;
        
        private List<Vector3> previewPath = new List<Vector3>();
        
        private void OnEnable()
        {
            controller = (SplinePlacementController)target;
        }
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Pipe Drawing Tool", EditorStyles.boldLabel);
            
            GUI.backgroundColor = isDrawingMode ? Color.green : Color.white;
            if (GUILayout.Button(isDrawingMode ? "Drawing Mode Active (ESC to exit)" : "Start Drawing", GUILayout.Height(40)))
            {
                isDrawingMode = !isDrawingMode;
                if (!isDrawingMode)
                {
                    isDragging = false;
                    previewPath.Clear();
                }
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Clear Path", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear Path", 
                    "Are you sure you want to clear the entire path?", "Yes", "No"))
                {
                    Undo.RecordObject(controller, "Clear Path");
                    controller.ClearPath();
                }
            }
            
            if (GUILayout.Button("Commit Path", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Commit Path");
                controller.CommitPath();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Click and drag in the Scene view to draw pipes.\n" +
                "Pipes snap to grid and only place in axis-aligned directions.\n" +
                "Enable 'Snap To Surface' to place on walls, floors, ceilings.\n" +
                "Corners are automatically placed when changing direction.", 
                MessageType.Info);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Path Nodes: {controller.pathNodes.Count}", EditorStyles.helpBox);
        }
        
        private void OnSceneGUI()
        {
            if (controller == null)
                return;
            
            if (isDrawingMode)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                HandleDrawingMode();
                DrawModeGUI();
            }
        }
        
        private void HandleDrawingMode()
        {
            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            Vector3 mouseWorldPos = GetMouseWorldPosition(ray);
            Vector3 snappedPos = controller.SnapToGrid(mouseWorldPos);
            
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                isDragging = true;
                dragStartPosition = snappedPos;
                lastSnappedPosition = snappedPos;
                currentDirection = Vector3.zero;
                previewPath.Clear();
                previewPath.Add(snappedPos);
                
                Undo.RecordObject(controller, "Start Pipe Path");
                controller.AddNode(snappedPos, Vector3.forward, SegmentType.Straight);
                
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && isDragging)
            {
                currentDragPosition = snappedPos;
                UpdatePathFromDrag();
                SceneView.RepaintAll();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && isDragging)
            {
                isDragging = false;
                e.Use();
            }
            else if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                if (isDragging)
                {
                    currentDragPosition = snappedPos;
                    UpdatePathFromDrag();
                }
                SceneView.RepaintAll();
            }
            
            if (isDragging)
            {
                DrawPreviewPath();
            }
            
            DrawGridPreview(snappedPos);
            
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                isDrawingMode = false;
                isDragging = false;
                previewPath.Clear();
                e.Use();
                Repaint();
            }
        }
        
        private Vector3 GetMouseWorldPosition(Ray ray)
        {
            if (controller.snapToSurface && Physics.Raycast(ray, out RaycastHit hit, 1000f, controller.surfaceLayer))
            {
                return hit.point + hit.normal * controller.surfaceOffset;
            }
            else
            {
                Plane plane = new Plane(Vector3.up, controller.transform.position);
                if (plane.Raycast(ray, out float enter))
                {
                    return ray.GetPoint(enter);
                }
            }
            
            return controller.transform.position;
        }
        
        private void UpdatePathFromDrag()
        {
            if (previewPath.Count == 0)
                return;
            
            Vector3 delta = currentDragPosition - lastSnappedPosition;
            
            if (delta.magnitude < controller.gridSize * 0.5f)
                return;
            
            Vector3 dominantDirection = GetDominantAxisDirection(delta);
            
            if (dominantDirection == Vector3.zero)
                return;
            
            if (currentDirection == Vector3.zero)
            {
                currentDirection = dominantDirection;
                SplinePlacementController.GridNode firstNode = controller.pathNodes[0];
                firstNode.direction = dominantDirection;
                controller.pathNodes[0] = firstNode;
            }
            
            bool directionChanged = currentDirection != Vector3.zero && 
                                   Vector3.Dot(dominantDirection, currentDirection) < 0.9f;
            
            if (directionChanged)
            {
                Undo.RecordObject(controller, "Add Corner");
                
                Vector3 cornerPos = lastSnappedPosition + currentDirection * controller.gridSize;
                controller.AddNode(cornerPos, dominantDirection, SegmentType.Corner90);
                
                currentDirection = dominantDirection;
                lastSnappedPosition = cornerPos;
                previewPath.Add(cornerPos);
                return;
            }
            
            Vector3 nextPos = lastSnappedPosition + currentDirection * controller.gridSize;
            
            float distToNext = Vector3.Distance(nextPos, currentDragPosition);
            if (distToNext >= controller.gridSize * 0.5f)
            {
                Undo.RecordObject(controller, "Add Pipe Segment");
                controller.AddNode(nextPos, currentDirection, SegmentType.Straight);
                
                previewPath.Add(nextPos);
                lastSnappedPosition = nextPos;
            }
        }
        
        private Vector3 GetDominantAxisDirection(Vector3 delta)
        {
            float absX = Mathf.Abs(delta.x);
            float absZ = Mathf.Abs(delta.z);
            
            if (absX < 0.1f && absZ < 0.1f)
                return Vector3.zero;
            
            if (absX > absZ)
            {
                return delta.x > 0 ? Vector3.right : Vector3.left;
            }
            else
            {
                return delta.z > 0 ? Vector3.forward : Vector3.back;
            }
        }
        
        private float GetCornerAngle(Vector3 fromDir, Vector3 toDir)
        {
            float angle = Vector3.SignedAngle(fromDir, toDir, Vector3.up);
            return angle;
        }
        
        private void DrawPreviewPath()
        {
            if (previewPath.Count < 2)
                return;
            
            Handles.color = Color.green;
            
            for (int i = 0; i < previewPath.Count - 1; i++)
            {
                Handles.DrawLine(previewPath[i], previewPath[i + 1], 3f);
            }
            
            Handles.DrawLine(previewPath[previewPath.Count - 1], currentDragPosition, 2f);
            
            foreach (var pos in previewPath)
            {
                Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.15f, EventType.Repaint);
            }
        }
        
        private void DrawGridPreview(Vector3 snappedPos)
        {
            Handles.color = new Color(0, 1, 0, 0.3f);
            Handles.DrawWireCube(snappedPos, Vector3.one * controller.gridSize * 0.5f);
        }
        
        private void DrawModeGUI()
        {
            Handles.BeginGUI();
            
            GUI.backgroundColor = new Color(0, 1, 0, 0.8f);
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Box(
                "PIPE DRAWING MODE\n" +
                "Click and drag to draw pipes\n" +
                "Pipes snap to grid\n" +
                "Press ESC to exit", 
                GUILayout.Height(90));
            GUILayout.EndArea();
            
            GUI.backgroundColor = Color.white;
            Handles.EndGUI();
        }
    }
}
