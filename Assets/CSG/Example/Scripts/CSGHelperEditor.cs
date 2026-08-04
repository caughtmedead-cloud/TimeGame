using UnityEngine;
using UnityEditor;
using CSGOperations;

[CustomEditor(typeof(CSGHelper))]
public class CSGHelperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CSGHelper helper = (CSGHelper)target;

        EditorGUILayout.HelpBox(
            "CSG Helper with stack overflow protection.\n\n" +
            "⚠️ For large terrain meshes, increase epsilon to 0.1 or higher.\n" +
            "⚠️ Lower maxVertices to prevent stack overflow on high-poly meshes.", 
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (helper.lhs != null && helper.rhs != null)
        {
            MeshFilter lhsFilter = helper.lhs.GetComponent<MeshFilter>();
            MeshFilter rhsFilter = helper.rhs.GetComponent<MeshFilter>();
            
            if (lhsFilter != null && rhsFilter != null && 
                lhsFilter.sharedMesh != null && rhsFilter.sharedMesh != null)
            {
                int lhsVerts = lhsFilter.sharedMesh.vertexCount;
                int rhsVerts = rhsFilter.sharedMesh.vertexCount;
                
                EditorGUILayout.LabelField("Mesh Info:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"LHS Vertices: {lhsVerts:N0}");
                EditorGUILayout.LabelField($"RHS Vertices: {rhsVerts:N0}");
                
                if (lhsVerts > helper.maxVertices || rhsVerts > helper.maxVertices)
                {
                    EditorGUILayout.HelpBox(
                        $"⚠️ Mesh exceeds max vertices ({helper.maxVertices})!\n" +
                        "This will likely cause stack overflow. Simplify your mesh first.", 
                        MessageType.Warning);
                }
                else if (lhsVerts > 1000 || rhsVerts > 1000)
                {
                    EditorGUILayout.HelpBox(
                        "Large mesh detected. CSG may take a while or fail.\n" +
                        "Consider simplifying the mesh for better performance.", 
                        MessageType.Warning);
                }
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Perform CSG Operation", GUILayout.Height(40)))
                {
                    helper.Perform();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Both objects must have MeshFilter components with valid meshes.", MessageType.Error);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign both LHS and RHS GameObjects to proceed.", MessageType.Warning);
        }
    }
}
