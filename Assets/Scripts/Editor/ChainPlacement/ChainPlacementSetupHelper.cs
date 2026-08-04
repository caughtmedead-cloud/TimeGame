using UnityEngine;
using UnityEditor;

namespace ChainPlacement
{
    public class ChainPlacementSetupHelper : EditorWindow
    {
        [MenuItem("Tools/Chain Placement/Setup Helper")]
        public static void ShowWindow()
        {
            var window = GetWindow<ChainPlacementSetupHelper>("Setup Helper");
            window.minSize = new Vector2(400f, 600f);
            window.Show();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Chain Placement Setup Helper", titleStyle);
            
            EditorGUILayout.Space(10f);
            
            DrawQuickStart();
            
            EditorGUILayout.Space(10f);
            
            DrawCommonSpawnPivots();
            
            EditorGUILayout.Space(10f);
            
            DrawDocumentation();
        }
        
        private void DrawQuickStart()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Start Guide", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("1. Create Segment Library:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("   Right-click > Create > Chain Placement > Segment Library", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.LabelField("2. Configure Your Segments:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("   • Assign prefabs", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("   • Set spawn pivot positions/rotations", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("   • Add optional icons", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.LabelField("3. Place First Segment:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("   Tools > Chain Placement > Placement Tool", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.LabelField("4. Build Chain:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("   Click segment buttons in Inspector to continue", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(10f);
            
            if (GUILayout.Button("Create New Segment Library", GUILayout.Height(30f)))
            {
                CreateNewLibrary();
            }
            
            if (GUILayout.Button("Open Placement Tool", GUILayout.Height(30f)))
            {
                ChainPlacementTool.ShowWindow();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCommonSpawnPivots()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Common Spawn Pivot Values", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5f);
            
            DrawPivotExample("Straight Pipe (1m)", "(0, 0, 1)", "(0, 0, 0)", "Continue straight");
            DrawPivotExample("Straight Pipe (2m)", "(0, 0, 2)", "(0, 0, 0)", "Continue straight");
            DrawPivotExample("Corner Left 90°", "(0, 0, 1)", "(0, -90, 0)", "Turn left");
            DrawPivotExample("Corner Right 90°", "(0, 0, 1)", "(0, 90, 0)", "Turn right");
            DrawPivotExample("Corner Up 90°", "(0, 0, 1)", "(90, 0, 0)", "Turn up");
            DrawPivotExample("Corner Down 90°", "(0, 0, 1)", "(-90, 0, 0)", "Turn down");
            DrawPivotExample("T-Junction Center", "(0, 0, 1)", "(0, 0, 0)", "Main path continues");
            DrawPivotExample("End Cap", "(0, 0, 0)", "(0, 0, 0)", "Terminates chain");
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.HelpBox(
                "Tip: These values assume your pipe prefabs face forward (+Z) in their local space. " +
                "Adjust based on your actual prefab orientations.",
                MessageType.Info
            );
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPivotExample(string name, string pos, string rot, string description)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(name, EditorStyles.boldLabel, GUILayout.Width(150f));
            EditorGUILayout.LabelField($"Pos: {pos}", GUILayout.Width(100f));
            EditorGUILayout.LabelField($"Rot: {rot}", GUILayout.Width(100f));
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawDocumentation()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Documentation", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("System Components:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• SegmentLibrary: ScriptableObject catalog", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• SegmentDefinition: Individual segment config", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• ChainSegment: Component on placed objects", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.HelpBox(
                "This system is inspired by the Airduct BMT tool. " +
                "It uses manual piece-by-piece placement with spawn pivots to determine where the next segment connects.",
                MessageType.Info
            );
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.LabelField("Script Locations:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Runtime: /Assets/Scripts/Runtime/ChainPlacement/", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Editor: /Assets/Scripts/Editor/ChainPlacement/", EditorStyles.miniLabel);
            
            EditorGUILayout.EndVertical();
        }
        
        private void CreateNewLibrary()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Segment Library",
                "New Segment Library",
                "asset",
                "Choose where to save the new Segment Library"
            );
            
            if (string.IsNullOrEmpty(path))
                return;
            
            SegmentLibrary newLibrary = CreateInstance<SegmentLibrary>();
            newLibrary.libraryName = "New Library";
            newLibrary.description = "Configure segments for chain placement.";
            newLibrary.segments = new SegmentDefinition[3];
            
            for (int i = 0; i < newLibrary.segments.Length; i++)
            {
                newLibrary.segments[i] = new SegmentDefinition
                {
                    segmentName = $"Segment {i + 1}",
                    spawnPivotPosition = new Vector3(0f, 0f, 1f),
                    spawnPivotRotation = Vector3.zero,
                    canBeStartSegment = true,
                    canHaveChildren = true
                };
            }
            
            AssetDatabase.CreateAsset(newLibrary, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = newLibrary;
            EditorGUIUtility.PingObject(newLibrary);
            
            Debug.Log($"Created Segment Library at: {path}");
        }
    }
}
