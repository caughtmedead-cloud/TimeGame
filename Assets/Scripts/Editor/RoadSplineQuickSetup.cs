using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;

namespace RoadSystem
{
    public class RoadSplineQuickSetup : EditorWindow
    {
        private enum RoadType
        {
            PrefabBased,
            ProceduralMesh
        }

        private RoadType roadType = RoadType.PrefabBased;
        private GameObject roadPrefab;
        private float roadWidth = 4f;
        private float segmentLength = 5f;
        private Material roadMaterial;

        [MenuItem("Tools/Road System/Quick Setup Wizard")]
        public static void ShowWindow()
        {
            RoadSplineQuickSetup window = GetWindow<RoadSplineQuickSetup>("Road Spline Setup");
            window.minSize = new Vector2(400, 450);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity Splines Road Quick Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This wizard helps you quickly set up road splines in your scene.\n\n" +
                "Choose between:\n" +
                "• Prefab-Based: Use road segment prefabs (easier)\n" +
                "• Procedural: Generate road mesh from profile (advanced)",
                MessageType.Info
            );

            GUILayout.Space(10);

            roadType = (RoadType)EditorGUILayout.EnumPopup("Road Type:", roadType);

            GUILayout.Space(10);

            if (roadType == RoadType.PrefabBased)
            {
                DrawPrefabBasedUI();
            }
            else
            {
                DrawProceduralUI();
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Create Road Spline in Scene", GUILayout.Height(40)))
            {
                CreateRoadSpline();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Create Quick Test Road Prefab (ProBuilder)", GUILayout.Height(30)))
            {
                CreateTestRoadPrefab();
            }

            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "After creating the spline:\n" +
                "1. Select the created GameObject in Hierarchy\n" +
                "2. Use Scene view tools to edit spline points\n" +
                "3. Move/add/delete control points to shape your road\n" +
                "4. Adjust spacing in the Inspector",
                MessageType.None
            );
        }

        private void DrawPrefabBasedUI()
        {
            GUILayout.Label("Prefab Settings", EditorStyles.boldLabel);

            roadPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Road Segment Prefab:",
                roadPrefab,
                typeof(GameObject),
                false
            );

            if (roadPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "No prefab assigned. Click 'Create Quick Test Road Prefab' below to generate one.",
                    MessageType.Warning
                );
            }

            segmentLength = EditorGUILayout.FloatField("Segment Length (m):", segmentLength);

            EditorGUILayout.HelpBox(
                "The spacing between road segments. Should match your prefab's length.",
                MessageType.None
            );
        }

        private void DrawProceduralUI()
        {
            GUILayout.Label("Procedural Road Settings", EditorStyles.boldLabel);

            roadWidth = EditorGUILayout.FloatField("Road Width (m):", roadWidth);
            roadMaterial = (Material)EditorGUILayout.ObjectField(
                "Road Material:",
                roadMaterial,
                typeof(Material),
                false
            );

            EditorGUILayout.HelpBox(
                "Procedural roads generate mesh geometry along the spline path.\n" +
                "This creates seamless roads without prefab segments.",
                MessageType.Info
            );

            EditorGUILayout.HelpBox(
                "Note: Spline Extrude requires manual setup. This wizard creates the base spline.\n" +
                "See documentation for full procedural setup.",
                MessageType.Warning
            );
        }

        private void CreateRoadSpline()
        {
            GameObject roadSplineObj = new GameObject("Road Spline");
            
            Undo.RegisterCreatedObjectUndo(roadSplineObj, "Create Road Spline");

            SplineContainer splineContainer = roadSplineObj.AddComponent<SplineContainer>();

            var spline = splineContainer.AddSpline();

            Vector3 startPos = Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
            {
                startPos = SceneView.lastActiveSceneView.camera.transform.position + 
                           SceneView.lastActiveSceneView.camera.transform.forward * 10f;
                startPos.y = 0;
            }

            spline.Add(new BezierKnot(startPos));
            spline.Add(new BezierKnot(startPos + Vector3.forward * 10f));
            spline.Add(new BezierKnot(startPos + Vector3.forward * 20f));

            if (roadType == RoadType.PrefabBased && roadPrefab != null)
            {
                SplineInstantiate splineInstantiate = roadSplineObj.AddComponent<SplineInstantiate>();
                
                splineInstantiate.Container = splineContainer;
                
                var items = new SplineInstantiate.InstantiableItem[1];
                items[0] = new SplineInstantiate.InstantiableItem();
                items[0].Prefab = roadPrefab;
                
                SerializedObject so = new SerializedObject(splineInstantiate);
                SerializedProperty itemsProp = so.FindProperty("m_ItemsToInstantiate");
                itemsProp.arraySize = 1;
                itemsProp.GetArrayElementAtIndex(0).FindPropertyRelative("Prefab").objectReferenceValue = roadPrefab;
                so.ApplyModifiedProperties();

                Debug.Log($"Created road spline with Spline Instantiate. Assigned prefab: {roadPrefab.name}");
            }
            else
            {
                Debug.Log("Created basic road spline. Add 'Spline Instantiate' or 'Spline Extrude' component manually.");
            }

            Selection.activeGameObject = roadSplineObj;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorUtility.DisplayDialog(
                "Road Spline Created!",
                "Road spline has been created in the scene.\n\n" +
                "Next steps:\n" +
                "1. The GameObject is now selected\n" +
                "2. Use Scene view to edit control points\n" +
                "3. Adjust spacing in Inspector\n" +
                "4. Shape your road path!",
                "OK"
            );
        }

        private void CreateTestRoadPrefab()
        {
#if UNITY_PROBUILDER
            GameObject roadSegment = new GameObject("Road_Segment_Test");
            
            var pbMesh = roadSegment.AddComponent<UnityEngine.ProBuilder.ProBuilderMesh>();
            pbMesh.CreateShapeFromPolygon(
                UnityEngine.ProBuilder.Shapes.Shape.CreatePolygon(
                    new Vector3[] {
                        new Vector3(-roadWidth/2, 0, 0),
                        new Vector3(roadWidth/2, 0, 0),
                        new Vector3(roadWidth/2, 0, segmentLength),
                        new Vector3(-roadWidth/2, 0, segmentLength)
                    },
                    0.1f,
                    false
                ),
                0.1f,
                false
            );

            if (roadMaterial != null)
            {
                var renderer = roadSegment.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = roadMaterial;
                }
            }

            string prefabPath = "Assets/_Prefabs/WorldObjects/Props/Road/Road_Segment_Test.prefab";
            
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(roadSegment, prefabPath);
            DestroyImmediate(roadSegment);

            roadPrefab = prefab;

            EditorUtility.DisplayDialog(
                "Test Prefab Created!",
                $"Road segment prefab created at:\n{prefabPath}\n\n" +
                $"Dimensions: {roadWidth}m wide x {segmentLength}m long\n\n" +
                "It has been automatically assigned to the prefab field above.",
                "OK"
            );

            EditorGUIUtility.PingObject(prefab);
#else
            EditorUtility.DisplayDialog(
                "ProBuilder Required",
                "ProBuilder package is required to create test prefabs automatically.\n\n" +
                "Alternative: Create a simple cube or plane manually:\n" +
                "1. Create 3D Object → Cube or Plane\n" +
                "2. Scale to road dimensions (e.g., 4 x 0.1 x 5)\n" +
                "3. Apply material\n" +
                "4. Drag to Project to create prefab",
                "OK"
            );
#endif
        }
    }
}
