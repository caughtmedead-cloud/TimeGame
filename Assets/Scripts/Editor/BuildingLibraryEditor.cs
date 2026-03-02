using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace BuildingTools
{
    [CustomEditor(typeof(BuildingLibrary))]
    public class BuildingLibraryEditor : Editor
    {
        private SerializedProperty piecesProperty;
        private int duplicateIndex = 0;
        private int copySocketsFromIndex = 0;
        private int pasteSocketsToIndex = 0;
        private Dictionary<int, bool> selectedSockets = new Dictionary<int, bool>();
        private bool showSocketSelection = false;
        
        private static List<Socket> socketClipboard = new List<Socket>();
        
        private void OnEnable()
        {
            piecesProperty = serializedObject.FindProperty("pieces");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Array Tools", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Add Blank Piece", GUILayout.Height(30)))
            {
                AddBlankPiece();
            }
            
            EditorGUILayout.Space(5);
            
            if (piecesProperty.arraySize > 0)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Duplicate Piece", EditorStyles.boldLabel);
                
                duplicateIndex = Mathf.Clamp(duplicateIndex, 0, piecesProperty.arraySize - 1);
                
                string[] pieceNames = new string[piecesProperty.arraySize];
                for (int i = 0; i < piecesProperty.arraySize; i++)
                {
                    SerializedProperty piece = piecesProperty.GetArrayElementAtIndex(i);
                    string pieceName = piece.FindPropertyRelative("pieceName").stringValue;
                    pieceNames[i] = $"[{i}] {pieceName}";
                }
                
                duplicateIndex = EditorGUILayout.Popup("Source Piece", duplicateIndex, pieceNames);
                
                if (GUILayout.Button("Duplicate This Piece", GUILayout.Height(30)))
                {
                    DuplicateSpecificPiece(duplicateIndex);
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "• Add Blank Piece: Adds a new empty piece definition\n" +
                "• Duplicate This Piece: Copies the selected piece including all sockets",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            if (piecesProperty.arraySize > 0)
            {
                DrawSocketClipboard();
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawSocketClipboard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Socket Clipboard", EditorStyles.boldLabel);
            
            if (socketClipboard.Count > 0)
            {
                EditorGUILayout.HelpBox($"Clipboard contains {socketClipboard.Count} socket(s)", MessageType.Info);
                
                foreach (var socket in socketClipboard)
                {
                    EditorGUILayout.LabelField($"  • {socket.socketName} ({socket.socketType})", EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Clipboard is empty", MessageType.None);
            }
            
            EditorGUILayout.Space(5);
            
            copySocketsFromIndex = Mathf.Clamp(copySocketsFromIndex, 0, piecesProperty.arraySize - 1);
            pasteSocketsToIndex = Mathf.Clamp(pasteSocketsToIndex, 0, piecesProperty.arraySize - 1);
            
            string[] pieceNames = new string[piecesProperty.arraySize];
            for (int i = 0; i < piecesProperty.arraySize; i++)
            {
                SerializedProperty piece = piecesProperty.GetArrayElementAtIndex(i);
                string pieceName = piece.FindPropertyRelative("pieceName").stringValue;
                SerializedProperty sockets = piece.FindPropertyRelative("sockets");
                pieceNames[i] = $"[{i}] {pieceName} ({sockets.arraySize} sockets)";
            }
            
            EditorGUI.BeginChangeCheck();
            copySocketsFromIndex = EditorGUILayout.Popup("Copy From", copySocketsFromIndex, pieceNames);
            if (EditorGUI.EndChangeCheck())
            {
                selectedSockets.Clear();
                showSocketSelection = false;
            }
            
            SerializedProperty sourcePiece = piecesProperty.GetArrayElementAtIndex(copySocketsFromIndex);
            SerializedProperty socketsProperty = sourcePiece.FindPropertyRelative("sockets");
            
            if (socketsProperty.arraySize > 0)
            {
                showSocketSelection = EditorGUILayout.Foldout(showSocketSelection, "Select Sockets to Copy", true);
                
                if (showSocketSelection)
                {
                    EditorGUILayout.BeginVertical("box");
                    
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select All", GUILayout.Width(80)))
                    {
                        for (int i = 0; i < socketsProperty.arraySize; i++)
                        {
                            selectedSockets[i] = true;
                        }
                    }
                    if (GUILayout.Button("Select None", GUILayout.Width(80)))
                    {
                        selectedSockets.Clear();
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.Space(3);
                    
                    for (int i = 0; i < socketsProperty.arraySize; i++)
                    {
                        SerializedProperty socketProp = socketsProperty.GetArrayElementAtIndex(i);
                        string socketName = socketProp.FindPropertyRelative("socketName").stringValue;
                        SocketType socketType = (SocketType)socketProp.FindPropertyRelative("socketType").enumValueIndex;
                        
                        if (!selectedSockets.ContainsKey(i))
                            selectedSockets[i] = false;
                        
                        selectedSockets[i] = EditorGUILayout.ToggleLeft(
                            $"[{i}] {socketName} ({socketType})", 
                            selectedSockets[i]
                        );
                    }
                    
                    EditorGUILayout.EndVertical();
                }
                
                int selectedCount = selectedSockets.Values.Count(v => v);
                GUI.enabled = selectedCount > 0;
                
                if (GUILayout.Button($"Copy {selectedCount} Selected Socket(s)", GUILayout.Height(25)))
                {
                    CopySelectedSockets(copySocketsFromIndex);
                }
                
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.HelpBox("This piece has no sockets", MessageType.Warning);
            }
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            pasteSocketsToIndex = EditorGUILayout.Popup("Paste To", pasteSocketsToIndex, pieceNames);
            
            GUI.enabled = socketClipboard.Count > 0;
            if (GUILayout.Button("Replace", GUILayout.Width(80)))
            {
                PasteSocketsToPiece(pasteSocketsToIndex, false);
            }
            if (GUILayout.Button("Append", GUILayout.Width(80)))
            {
                PasteSocketsToPiece(pasteSocketsToIndex, true);
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            if (socketClipboard.Count > 0)
            {
                if (GUILayout.Button("Clear Clipboard"))
                {
                    socketClipboard.Clear();
                    Debug.Log("Socket clipboard cleared");
                }
            }
            
            EditorGUILayout.HelpBox(
                "1. Select source piece and check which sockets to copy\n" +
                "2. Click 'Copy X Selected Socket(s)' to copy to clipboard\n" +
                "3. Select target piece and click Replace or Append",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        private void CopySelectedSockets(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= piecesProperty.arraySize)
                return;
            
            SerializedProperty sourcePiece = piecesProperty.GetArrayElementAtIndex(sourceIndex);
            SerializedProperty socketsProperty = sourcePiece.FindPropertyRelative("sockets");
            
            socketClipboard.Clear();
            
            for (int i = 0; i < socketsProperty.arraySize; i++)
            {
                if (!selectedSockets.ContainsKey(i) || !selectedSockets[i])
                    continue;
                
                SerializedProperty socketProp = socketsProperty.GetArrayElementAtIndex(i);
                
                Socket socket = new Socket
                {
                    socketName = socketProp.FindPropertyRelative("socketName").stringValue,
                    socketType = (SocketType)socketProp.FindPropertyRelative("socketType").enumValueIndex,
                    localPosition = socketProp.FindPropertyRelative("localPosition").vector3Value,
                    localRotation = socketProp.FindPropertyRelative("localRotation").vector3Value,
                    size = socketProp.FindPropertyRelative("size").vector3Value,
                    acceptedTypes = socketProp.FindPropertyRelative("acceptedTypes").stringValue,
                    acceptMatchMode = (SocketMatchMode)socketProp.FindPropertyRelative("acceptMatchMode").enumValueIndex,
                    providedTypes = socketProp.FindPropertyRelative("providedTypes").stringValue,
                    provideMatchMode = (SocketMatchMode)socketProp.FindPropertyRelative("provideMatchMode").enumValueIndex,
                    alignRotation = socketProp.FindPropertyRelative("alignRotation").boolValue,
                    connectionOffset = socketProp.FindPropertyRelative("connectionOffset").vector3Value,
                    gizmoColor = socketProp.FindPropertyRelative("gizmoColor").colorValue
                };
                
                socketClipboard.Add(socket);
            }
            
            string pieceName = sourcePiece.FindPropertyRelative("pieceName").stringValue;
            Debug.Log($"Copied {socketClipboard.Count} selected socket(s) from '{pieceName}'");
            Repaint();
        }
        
        private void PasteSocketsToPiece(int targetIndex, bool append)
        {
            if (socketClipboard.Count == 0)
            {
                EditorUtility.DisplayDialog("Empty Clipboard", "Socket clipboard is empty. Copy sockets first.", "OK");
                return;
            }
            
            if (targetIndex < 0 || targetIndex >= piecesProperty.arraySize)
                return;
            
            SerializedProperty targetPiece = piecesProperty.GetArrayElementAtIndex(targetIndex);
            SerializedProperty socketsProperty = targetPiece.FindPropertyRelative("sockets");
            
            int startIndex = 0;
            
            if (!append)
            {
                socketsProperty.ClearArray();
            }
            else
            {
                startIndex = socketsProperty.arraySize;
            }
            
            for (int i = 0; i < socketClipboard.Count; i++)
            {
                Socket socket = socketClipboard[i];
                
                socketsProperty.InsertArrayElementAtIndex(startIndex + i);
                SerializedProperty newSocketProp = socketsProperty.GetArrayElementAtIndex(startIndex + i);
                
                newSocketProp.FindPropertyRelative("socketName").stringValue = socket.socketName;
                newSocketProp.FindPropertyRelative("socketType").enumValueIndex = (int)socket.socketType;
                newSocketProp.FindPropertyRelative("localPosition").vector3Value = socket.localPosition;
                newSocketProp.FindPropertyRelative("localRotation").vector3Value = socket.localRotation;
                newSocketProp.FindPropertyRelative("size").vector3Value = socket.size;
                newSocketProp.FindPropertyRelative("acceptedTypes").stringValue = socket.acceptedTypes;
                newSocketProp.FindPropertyRelative("acceptMatchMode").enumValueIndex = (int)socket.acceptMatchMode;
                newSocketProp.FindPropertyRelative("providedTypes").stringValue = socket.providedTypes;
                newSocketProp.FindPropertyRelative("provideMatchMode").enumValueIndex = (int)socket.provideMatchMode;
                newSocketProp.FindPropertyRelative("alignRotation").boolValue = socket.alignRotation;
                newSocketProp.FindPropertyRelative("connectionOffset").vector3Value = socket.connectionOffset;
                newSocketProp.FindPropertyRelative("gizmoColor").colorValue = socket.gizmoColor;
            }
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            
            string pieceName = targetPiece.FindPropertyRelative("pieceName").stringValue;
            string action = append ? "appended to" : "replaced in";
            Debug.Log($"{socketClipboard.Count} socket(s) {action} '{pieceName}'");
        }
        
        private void AddBlankPiece()
        {
            int newIndex = piecesProperty.arraySize;
            piecesProperty.InsertArrayElementAtIndex(newIndex);
            
            SerializedProperty newPiece = piecesProperty.GetArrayElementAtIndex(newIndex);
            
            newPiece.FindPropertyRelative("pieceName").stringValue = "New Piece";
            newPiece.FindPropertyRelative("prefab").objectReferenceValue = null;
            newPiece.FindPropertyRelative("pieceType").enumValueIndex = 0;
            newPiece.FindPropertyRelative("gridSize").vector3Value = new Vector3(4, 3, 0.5f);
            newPiece.FindPropertyRelative("canPlaceOnGround").boolValue = true;
            newPiece.FindPropertyRelative("canStack").boolValue = false;
            newPiece.FindPropertyRelative("providedTags").stringValue = "";
            
            SerializedProperty socketsProperty = newPiece.FindPropertyRelative("sockets");
            socketsProperty.ClearArray();
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            
            Debug.Log("Added blank piece at index " + newIndex);
        }
        
        private void DuplicateSpecificPiece(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= piecesProperty.arraySize)
            {
                EditorUtility.DisplayDialog("Invalid Index", "Selected piece index is invalid.", "OK");
                return;
            }
            
            SerializedProperty sourcePiece = piecesProperty.GetArrayElementAtIndex(sourceIndex);
            string originalName = sourcePiece.FindPropertyRelative("pieceName").stringValue;
            
            int newIndex = piecesProperty.arraySize;
            piecesProperty.InsertArrayElementAtIndex(sourceIndex);
            
            SerializedProperty duplicatedPiece = piecesProperty.GetArrayElementAtIndex(sourceIndex + 1);
            duplicatedPiece.FindPropertyRelative("pieceName").stringValue = originalName + " (Copy)";
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            
            Debug.Log($"Duplicated piece '{originalName}' from index {sourceIndex} to index {sourceIndex + 1}");
        }
    }
}
