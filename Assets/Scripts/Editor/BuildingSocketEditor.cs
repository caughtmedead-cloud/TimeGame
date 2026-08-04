using UnityEngine;
using UnityEditor;

namespace BuildingTools
{
    [CustomEditor(typeof(BuildingSocket))]
    public class BuildingSocketEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            BuildingSocket socket = (BuildingSocket)target;
            
            // Editor-only banner
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
            EditorGUILayout.LabelField("⚙ EDITOR-ONLY COMPONENT", labelStyle);
            EditorGUILayout.LabelField("Automatically excluded from builds", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Show default inspector
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            
            // Helper buttons
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Edit Sockets (Live Mode)"))
            {
                SocketEditor window = EditorWindow.GetWindow<SocketEditor>("Socket Editor");
                window.Show();
                // The menu item will handle the actual live edit setup
                EditorApplication.ExecuteMenuItem("Tools/TimeGame/Building Tools/Edit Sockets (Live Mode)");
            }
            
            if (GUILayout.Button("Remove This Component"))
            {
                if (EditorUtility.DisplayDialog(
                    "Remove BuildingSocket?",
                    "Remove the BuildingSocket component from this object?\n\n" +
                    "Note: This component is already excluded from builds automatically.",
                    "Remove",
                    "Cancel"))
                {
                    Undo.DestroyObjectImmediate(socket);
                }
            }
            
            EditorGUILayout.EndVertical();
            
            // Connection info
            if (socket.connections != null && socket.connections.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Connections ({socket.connections.Count})", EditorStyles.boldLabel);
                
                foreach (var conn in socket.connections)
                {
                    if (conn.sourceSocket == socket)
                    {
                        EditorGUILayout.LabelField($"→ {conn.targetSocket.name} [Socket {conn.targetSocketIndex}]");
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"← {conn.sourceSocket.name} [Socket {conn.sourceSocketIndex}]");
                    }
                }
                
                EditorGUILayout.EndVertical();
            }
        }
    }
}
