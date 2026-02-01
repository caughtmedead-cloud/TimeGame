using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PropScatterTool : EditorWindow
{
    private List<GameObject> prefabs = new List<GameObject>();

    private float radius = 2f;
    private int count = 10;

    private Vector2 scaleRange = new Vector2(1f, 1f);
    private bool randomYaw = true;
    private bool alignToSurface = true;

    private bool placing = false;

    [MenuItem("Tools/Level Design/Prop Scatter Tool")]
    public static void Open()
    {
        GetWindow<PropScatterTool>("Prop Scatter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);

        int removeIndex = -1;
        for (int i = 0; i < prefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false);

            if (GUILayout.Button("X", GUILayout.Width(20)))
                removeIndex = i;

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            prefabs.RemoveAt(removeIndex);

        if (GUILayout.Button("Add Prefab Slot"))
            prefabs.Add(null);

        EditorGUILayout.Space();

        radius = EditorGUILayout.FloatField("Radius", radius);
        count = EditorGUILayout.IntField("Count", count);

        randomYaw = EditorGUILayout.Toggle("Random Y Rotation", randomYaw);
        alignToSurface = EditorGUILayout.Toggle("Align To Surface", alignToSurface);

        scaleRange = EditorGUILayout.Vector2Field("Scale Range", scaleRange);

        EditorGUILayout.Space();

        GUI.color = placing ? Color.green : Color.white;
        if (GUILayout.Button(placing ? "Placing: ON" : "Placing: OFF"))
        {
            placing = !placing;
            SceneView.RepaintAll();
        }
        GUI.color = Color.white;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!placing || prefabs.Count == 0)
            return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Visualize radius
            Handles.color = new Color(0, 1, 0, 0.25f);
            Handles.DrawSolidDisc(hit.point, hit.normal, radius);

            Handles.color = Color.green;
            Handles.DrawWireDisc(hit.point, hit.normal, radius);

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Scatter(hit.point, hit.normal);
                e.Use();
            }
        }
    }

    private void Scatter(Vector3 center, Vector3 surfaceNormal)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
            if (prefab == null)
                continue;

            Vector2 circle = Random.insideUnitCircle * radius;
            Vector3 pos = center + new Vector3(circle.x, 0f, circle.y);

            // Raycast down to surface
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
            {
                Quaternion rotation = Quaternion.identity;

                if (alignToSurface)
                    rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

                if (randomYaw)
                    rotation *= Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Scatter Props");

                instance.transform.position = hit.point;
                instance.transform.rotation = rotation;

                float scale = Random.Range(scaleRange.x, scaleRange.y);
                instance.transform.localScale = Vector3.one * scale;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }
}
