using UnityEngine;
using UnityEditor;

namespace ChainPlacement
{
    [CustomEditor(typeof(ChainSegment))]
    [CanEditMultipleObjects]
    public class ChainSegmentEditor : Editor
    {
        private ChainSegment segment;
        private int selectedSegmentIndex = 0;
        private int selectedSpawnPivotIndex = 0;
        private bool showPlacementTools = true;
        private Vector2 segmentScrollPosition;
        private float placementScale = 1f;
        
        private void OnEnable()
        {
            segment = target as ChainSegment;
            
            if (segment.library != null && selectedSegmentIndex >= 0 && selectedSegmentIndex < segment.library.segments.Length)
            {
                var def = segment.library.segments[selectedSegmentIndex];
                if (def != null)
                {
                    placementScale = def.defaultScale;
                }
            }
            
            if (placementScale == 0f)
                placementScale = 1f;
            
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            DrawHeader();
            
            EditorGUILayout.Space(10f);
            
            DrawLibraryReference();
            
            EditorGUILayout.Space(10f);
            
            DrawSpawnPivotSelector();
            
            EditorGUILayout.Space(10f);
            
            if (segment.library != null && HasAvailableSpawnPoints())
            {
                DrawPlacementUI();
            }
            else if (!HasAvailableSpawnPoints())
            {
                EditorGUILayout.HelpBox("All spawn points are occupied. No more segments can be placed from this segment.", MessageType.Info);
            }
            else if (segment.Definition != null && !segment.Definition.canHaveChildren)
            {
                EditorGUILayout.HelpBox("This segment type cannot have children (e.g., end cap).", MessageType.Info);
            }
            
            EditorGUILayout.Space(10f);
            
            DrawChainHierarchy();
            
            EditorGUILayout.Space(10f);
            
            DrawSpawnPivotControls();
            
            EditorGUILayout.Space(5f);
            
            DrawDefaultInspector();
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private bool HasAvailableSpawnPoints()
        {
            var def = segment.Definition;
            if (def == null || !def.canHaveChildren)
                return false;
            
            for (int i = 0; i < segment.spawnPivots.Count; i++)
            {
                if (segment.CanSpawnChildAt(i))
                    return true;
            }
            
            return false;
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("Chain Placement System", titleStyle);
            
            var def = segment.Definition;
            if (def != null)
            {
                EditorGUILayout.LabelField($"Segment: {def.segmentName}", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLibraryReference()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            SegmentLibrary newLibrary = (SegmentLibrary)EditorGUILayout.ObjectField(
                "Segment Library",
                segment.library,
                typeof(SegmentLibrary),
                false
            );
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(segment, "Change Segment Library");
                segment.library = newLibrary;
                EditorUtility.SetDirty(segment);
            }
            
            if (segment.library == null)
            {
                EditorGUILayout.HelpBox("Assign a Segment Library to enable placement tools.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSpawnPivotSelector()
        {
            if (segment.spawnPivots == null || segment.spawnPivots.Count == 0)
                return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Spawn Point Selection", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active Spawn Point:", GUILayout.Width(120f));
            
            string[] pivotNames = new string[segment.spawnPivots.Count];
            for (int i = 0; i < segment.spawnPivots.Count; i++)
            {
                var pivot = segment.spawnPivots[i];
                bool hasChild = segment.GetChildAt(i) != null;
                string status = hasChild ? " [OCCUPIED]" : " [Available]";
                pivotNames[i] = $"{i}: {pivot.name}{status}";
            }
            
            EditorGUI.BeginChangeCheck();
            selectedSpawnPivotIndex = EditorGUILayout.Popup(selectedSpawnPivotIndex, pivotNames);
            
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.EndHorizontal();
            
            var selectedPivot = segment.GetSpawnPivot(selectedSpawnPivotIndex);
            if (selectedPivot != null)
            {
                EditorGUILayout.LabelField($"Position: {selectedPivot.position}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Rotation: {selectedPivot.rotation}", EditorStyles.miniLabel);
                
                bool hasChild = segment.GetChildAt(selectedSpawnPivotIndex) != null;
                if (hasChild)
                {
                    var child = segment.GetChildAt(selectedSpawnPivotIndex);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Occupied by:", EditorStyles.miniLabel, GUILayout.Width(80f));
                    if (GUILayout.Button(child.name, EditorStyles.miniButton))
                    {
                        Selection.activeGameObject = child.gameObject;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPlacementUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            showPlacementTools = EditorGUILayout.BeginFoldoutHeaderGroup(showPlacementTools, "Placement Tools");
            
            if (showPlacementTools)
            {
                EditorGUILayout.Space(5f);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Placement Scale:", GUILayout.Width(110f));
                placementScale = EditorGUILayout.Slider(placementScale, 0.1f, 5f);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(3f);
                
                EditorGUILayout.LabelField("Place Next Segment:", EditorStyles.boldLabel);
                
                var library = segment.library;
                
                if (library.segments.Length == 0)
                {
                    EditorGUILayout.HelpBox("No segments defined in library.", MessageType.Warning);
                }
                else
                {
                    segmentScrollPosition = EditorGUILayout.BeginScrollView(
                        segmentScrollPosition,
                        GUILayout.Height(Mathf.Min(library.segments.Length * 70f, 300f))
                    );
                    
                    for (int i = 0; i < library.segments.Length; i++)
                    {
                        var segDef = library.segments[i];
                        
                        if (segDef == null || !segDef.IsValid())
                            continue;
                        
                        DrawSegmentButton(segDef, i);
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSegmentButton(SegmentDefinition segDef, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            if (segDef.icon != null)
            {
                GUILayout.Box(segDef.icon, GUILayout.Width(60f), GUILayout.Height(60f));
            }
            else
            {
                GUILayout.Box("No Icon", GUILayout.Width(60f), GUILayout.Height(60f));
            }
            
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.LabelField(segDef.segmentName, EditorStyles.boldLabel);
            
            if (segDef.prefab != null)
            {
                EditorGUILayout.LabelField($"Prefab: {segDef.prefab.name}", EditorStyles.miniLabel);
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Place", GUILayout.Height(25f)))
            {
                if (placementScale == 0f || placementScale != segDef.defaultScale)
                {
                    placementScale = segDef.defaultScale > 0f ? segDef.defaultScale : 1f;
                }
                PlaceSegment(segDef, index);
            }
            
            if (GUILayout.Button("Use Default", GUILayout.Width(90f), GUILayout.Height(25f)))
            {
                placementScale = segDef.defaultScale > 0f ? segDef.defaultScale : 1f;
                Repaint();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5f);
        }
        
        private void DrawChainHierarchy()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Chain Hierarchy", EditorStyles.boldLabel);
            
            if (segment.parentSegment != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parent:", GUILayout.Width(60f));
                EditorGUILayout.ObjectField(segment.parentSegment, typeof(ChainSegment), true);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.LabelField($"(From spawn point: {segment.parentSpawnPivotIndex})", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Parent: None (Root)", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.Space(5f);
            
            if (segment.childSegments != null && segment.childSegments.Count > 0)
            {
                EditorGUILayout.LabelField($"Children: {segment.childSegments.Count}", EditorStyles.boldLabel);
                
                foreach (var child in segment.childSegments)
                {
                    if (child != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Pivot {child.parentSpawnPivotIndex}:", GUILayout.Width(60f));
                        EditorGUILayout.ObjectField(child, typeof(ChainSegment), true);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Children: None", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSpawnPivotControls()
        {
            if (segment.spawnPivots == null || segment.spawnPivots.Count == 0)
                return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Spawn Pivot Editor", EditorStyles.boldLabel);
            
            if (selectedSpawnPivotIndex < 0 || selectedSpawnPivotIndex >= segment.spawnPivots.Count)
            {
                EditorGUILayout.HelpBox("Select a valid spawn pivot above.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }
            
            var pivot = segment.spawnPivots[selectedSpawnPivotIndex];
            
            EditorGUILayout.LabelField($"Editing: {pivot.name}", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            pivot.name = EditorGUILayout.TextField("Name", pivot.name);
            pivot.position = EditorGUILayout.Vector3Field("Position", pivot.position);
            pivot.rotation = EditorGUILayout.Vector3Field("Rotation", pivot.rotation);
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(segment);
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.HelpBox(
                "Rotate this GameObject using the rotation handle in SceneView, then click 'Update from Rotation' to sync the spawn pivot rotation.",
                MessageType.Info
            );
            
            if (GUILayout.Button("Update Spawn Pivot from Current Rotation", GUILayout.Height(30f)))
            {
                UpdateSpawnPivotFromRotation(selectedSpawnPivotIndex);
            }
            
            EditorGUILayout.Space(5f);
            
            if (GUILayout.Button("Reset to Library Defaults"))
            {
                ResetSpawnPivotToDefaults(selectedSpawnPivotIndex);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void UpdateSpawnPivotFromRotation(int pivotIndex)
        {
            if (segment.parentSegment == null)
            {
                EditorUtility.DisplayDialog(
                    "No Parent Segment",
                    "This segment has no parent. The spawn pivot rotation is relative to the parent's rotation.",
                    "OK"
                );
                return;
            }
            
            var pivot = segment.GetSpawnPivot(pivotIndex);
            if (pivot == null)
                return;
            
            Undo.RecordObject(segment, "Update Spawn Pivot Rotation");
            
            Quaternion parentRotation = segment.parentSegment.transform.rotation;
            Quaternion currentRotation = segment.transform.rotation;
            
            Quaternion relativeRotation = Quaternion.Inverse(parentRotation) * currentRotation;
            
            pivot.rotation = relativeRotation.eulerAngles;
            
            EditorUtility.SetDirty(segment);
            
            Debug.Log($"Updated spawn pivot {pivotIndex} ({pivot.name}) rotation to: {pivot.rotation}");
        }
        
        private void ResetSpawnPivotToDefaults(int pivotIndex)
        {
            var def = segment.Definition;
            if (def == null)
            {
                EditorUtility.DisplayDialog("No Definition", "This segment has no definition in the library.", "OK");
                return;
            }
            
            if (pivotIndex < 0 || pivotIndex >= def.spawnPivots.Length)
                return;
            
            Undo.RecordObject(segment, "Reset Spawn Pivot");
            
            var defPivot = def.spawnPivots[pivotIndex];
            var pivot = segment.spawnPivots[pivotIndex];
            
            pivot.name = defPivot.name;
            pivot.position = defPivot.position;
            pivot.rotation = defPivot.rotation;
            
            EditorUtility.SetDirty(segment);
            
            Debug.Log($"Reset spawn pivot {pivotIndex} to library defaults.");
        }
        
        private void PlaceSegment(SegmentDefinition segDef, int index)
        {
            GameObject prefab = segDef.prefab;
            
            if (prefab == null)
            {
                Debug.LogError("Segment prefab is null!");
                return;
            }
            
            if (!segment.CanSpawnChildAt(selectedSpawnPivotIndex))
            {
                EditorUtility.DisplayDialog(
                    "Cannot Place Segment",
                    $"Spawn point {selectedSpawnPivotIndex} is already occupied or invalid.",
                    "OK"
                );
                return;
            }
            
            Vector3 spawnPos = segment.GetSpawnPivotWorldPosition(selectedSpawnPivotIndex);
            Quaternion spawnRot = segment.GetSpawnPivotWorldRotation(selectedSpawnPivotIndex);
            
            GameObject newObj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
            if (newObj == null)
            {
                newObj = Instantiate(prefab);
            }
            
            newObj.transform.position = spawnPos;
            newObj.transform.rotation = spawnRot;
            newObj.transform.localScale = Vector3.one * placementScale;
            newObj.transform.SetParent(segment.transform.parent);
            
            ChainSegment newSegment = newObj.GetComponent<ChainSegment>();
            
            if (newSegment == null)
            {
                newSegment = newObj.AddComponent<ChainSegment>();
            }
            
            newSegment.InitializeFromDefinition(segment.library, index);
            
            segment.SetChildSegment(newSegment, selectedSpawnPivotIndex);
            
            Undo.RegisterCreatedObjectUndo(newObj, "Place Chain Segment");
            Undo.RecordObject(segment, "Set Child Segment");
            
            Selection.activeGameObject = newObj;
            
            EditorUtility.SetDirty(segment);
            EditorUtility.SetDirty(newSegment);
            
            SceneView.RepaintAll();
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (segment == null || segment.library == null)
                return;
            
            DrawAllSpawnPivotHandles();
            DrawSelectedRotationHandle();
        }
        
        private void DrawAllSpawnPivotHandles()
        {
            if (segment.spawnPivots == null)
                return;
            
            for (int i = 0; i < segment.spawnPivots.Count; i++)
            {
                var pivot = segment.spawnPivots[i];
                if (pivot == null)
                    continue;
                
                bool isOccupied = segment.GetChildAt(i) != null;
                bool isSelected = i == selectedSpawnPivotIndex;
                
                Vector3 pivotPos = segment.GetSpawnPivotWorldPosition(i);
                Quaternion pivotRot = segment.GetSpawnPivotWorldRotation(i);
                
                if (isOccupied)
                {
                    Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
                else if (isSelected)
                {
                    Handles.color = new Color(0f, 1f, 0.5f, 1f);
                }
                else
                {
                    Handles.color = segment.library.pivotGizmoColor;
                }
                
                float size = isSelected ? 0.25f : 0.2f;
                Handles.SphereHandleCap(0, pivotPos, Quaternion.identity, size, EventType.Repaint);
                
                if (!isOccupied)
                {
                    Handles.color = Color.red;
                    Handles.DrawLine(pivotPos, pivotPos + pivotRot * Vector3.right * 0.5f);
                    
                    Handles.color = Color.green;
                    Handles.DrawLine(pivotPos, pivotPos + pivotRot * Vector3.up * 0.5f);
                    
                    Handles.color = Color.blue;
                    Handles.DrawLine(pivotPos, pivotPos + pivotRot * Vector3.forward * 0.5f);
                }
                
                GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = isSelected ? 12 : 10
                };
                
                string label = isOccupied ? $"{pivot.name} [OCCUPIED]" : pivot.name;
                Handles.Label(pivotPos + Vector3.up * 0.4f, label, labelStyle);
            }
        }
        
        private void DrawSelectedRotationHandle()
        {
            if (segment.spawnPivots == null || selectedSpawnPivotIndex < 0 || selectedSpawnPivotIndex >= segment.spawnPivots.Count)
                return;
            
            if (segment.GetChildAt(selectedSpawnPivotIndex) != null)
                return;
            
            EditorGUI.BeginChangeCheck();
            
            Vector3 pivotPos = segment.GetSpawnPivotWorldPosition(selectedSpawnPivotIndex);
            Quaternion pivotRot = segment.GetSpawnPivotWorldRotation(selectedSpawnPivotIndex);
            
            Handles.color = new Color(0f, 1f, 0.5f, 0.3f);
            Quaternion newRotation = Handles.RotationHandle(pivotRot, pivotPos);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(segment, "Rotate Spawn Pivot");
                
                var pivot = segment.spawnPivots[selectedSpawnPivotIndex];
                Quaternion localRotation = Quaternion.Inverse(segment.transform.rotation) * newRotation;
                pivot.rotation = localRotation.eulerAngles;
                
                EditorUtility.SetDirty(segment);
                SceneView.RepaintAll();
            }
        }
    }
}
