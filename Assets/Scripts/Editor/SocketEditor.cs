using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace BuildingTools
{
    public class SocketEditor : EditorWindow
    {
        private enum EditMode
        {
            PrefabMode,
            LiveSceneMode
        }
        
        private EditMode currentMode = EditMode.PrefabMode;
        
        private BuildingLibrary targetLibrary;
        private int selectedPieceIndex = -1;
        private GameObject previewInstance;
        private List<SocketGizmo> socketGizmos = new List<SocketGizmo>();
        
        private BuildingSocket liveEditTarget;
        
        private int selectedSocketIndex = -1;
        private Vector2 scrollPosition;
        private bool showHandles = true;
        private bool autoSave = true;
        
        private class SocketGizmo
        {
            public GameObject gizmoObject;
            public Socket data;
        }
        
        [MenuItem("Tools/TimeGame/Building Tools/Socket Editor")]
        public static void ShowWindow()
        {
            SocketEditor window = GetWindow<SocketEditor>("Socket Editor");
            window.minSize = new Vector2(450, 700);
            window.Show();
        }
        
        [MenuItem("Tools/TimeGame/Building Tools/Edit Sockets (Live Mode)", true)]
        private static bool ValidateEditLive()
        {
            return Selection.activeGameObject != null && 
                   Selection.activeGameObject.GetComponent<BuildingSocket>() != null;
        }
        
        [MenuItem("Tools/TimeGame/Building Tools/Edit Sockets (Live Mode)")]
        public static void EditLive()
        {
            SocketEditor window = GetWindow<SocketEditor>("Socket Editor");
            window.minSize = new Vector2(450, 700);
            window.LoadLiveMode();
            window.Show();
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CleanupPreview();
        }
        
        private void LoadLiveMode()
        {
            if (Selection.activeGameObject != null)
            {
                liveEditTarget = Selection.activeGameObject.GetComponent<BuildingSocket>();
                if (liveEditTarget != null)
                {
                    currentMode = EditMode.LiveSceneMode;
                    LoadLiveSockets();
                }
            }
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            DrawTitle();
            DrawModeSelector();
            
            EditorGUILayout.Space(10);
            
            DrawInfo();
            EditorGUILayout.Space(10);
            
            if (currentMode == EditMode.PrefabMode)
            {
                DrawPrefabMode();
            }
            else
            {
                DrawLiveSceneMode();
            }
        }
        
        private void DrawTitle()
        {
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Socket Editor", titleStyle);
        }
        
        private void DrawModeSelector()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = currentMode == EditMode.PrefabMode ? Color.green : Color.white;
            if (GUILayout.Button("Prefab Mode", GUILayout.Height(30)))
            {
                SwitchMode(EditMode.PrefabMode);
            }
            
            GUI.backgroundColor = currentMode == EditMode.LiveSceneMode ? Color.green : Color.white;
            if (GUILayout.Button("Live Scene Mode", GUILayout.Height(30)))
            {
                SwitchMode(EditMode.LiveSceneMode);
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Socket System", EditorStyles.boldLabel);
            
            if (currentMode == EditMode.PrefabMode)
            {
                EditorGUILayout.HelpBox(
                    "Prefab Mode: Design sockets on prefabs before placing.\n" +
                    "• Edit socket definitions\n" +
                    "• Save to library\n" +
                    "• Preview in isolation",
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Live Scene Mode: Edit sockets on placed pieces in real-time!\n" +
                    "• See connections update live\n" +
                    "• Test socket positions while building\n" +
                    "• Iterative workflow",
                    MessageType.Info
                );
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPrefabMode()
        {
            DrawPrefabSelection();
            EditorGUILayout.Space(10);
            
            DrawSettings();
            EditorGUILayout.Space(10);
            
            DrawPreviewControls();
            EditorGUILayout.Space(10);
            
            if (previewInstance != null)
            {
                DrawSocketList();
                EditorGUILayout.Space(10);
                
                DrawSocketPresets();
                EditorGUILayout.Space(10);
                
                DrawExportButtons();
            }
        }
        
        private void DrawLiveSceneMode()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Live Edit Target", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            liveEditTarget = (BuildingSocket)EditorGUILayout.ObjectField(
                "Target Piece",
                liveEditTarget,
                typeof(BuildingSocket),
                true
            );
            
            if (EditorGUI.EndChangeCheck())
            {
                if (liveEditTarget != null)
                {
                    LoadLiveSockets();
                    Selection.activeGameObject = liveEditTarget.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
            
            if (liveEditTarget == null)
            {
                EditorGUILayout.HelpBox("Select a placed BuildingSocket in the scene to edit live.", MessageType.Warning);
                
                if (GUILayout.Button("Select from Scene"))
                {
                    if (Selection.activeGameObject != null)
                    {
                        liveEditTarget = Selection.activeGameObject.GetComponent<BuildingSocket>();
                        if (liveEditTarget != null)
                        {
                            LoadLiveSockets();
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Piece Type:", liveEditTarget.pieceType.ToString());
                EditorGUILayout.LabelField("Sockets:", liveEditTarget.sockets.Count.ToString());
                EditorGUILayout.LabelField("Connections:", liveEditTarget.connections.Count.ToString());
                
                if (GUILayout.Button("Refresh from Piece"))
                {
                    LoadLiveSockets();
                }
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            if (liveEditTarget != null)
            {
                DrawLiveSocketList();
                EditorGUILayout.Space(10);
                
                DrawSocketPresets();
                EditorGUILayout.Space(10);
                
                DrawLiveSaveButtons();
            }
        }
        
        private void DrawPrefabSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Library & Piece Selection", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            targetLibrary = (BuildingLibrary)EditorGUILayout.ObjectField(
                "Building Library",
                targetLibrary,
                typeof(BuildingLibrary),
                false
            );
            bool libraryChanged = EditorGUI.EndChangeCheck();
            
            if (targetLibrary == null)
            {
                EditorGUILayout.HelpBox("Assign a Building Library to begin", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            if (targetLibrary.pieces == null || targetLibrary.pieces.Length == 0)
            {
                EditorGUILayout.HelpBox("Library has no pieces. Add pieces to the library first.", MessageType.Warning);
                
                if (GUILayout.Button("Open Library Asset"))
                {
                    Selection.activeObject = targetLibrary;
                }
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            if (libraryChanged)
            {
                selectedPieceIndex = -1;
                CleanupPreview();
            }
            
            EditorGUILayout.Space();
            
            string[] pieceNames = targetLibrary.pieces
                .Select((p, i) => p != null && p.IsValid() ? $"{i}: {p.pieceName}" : $"{i}: [Invalid]")
                .ToArray();
            
            EditorGUI.BeginChangeCheck();
            selectedPieceIndex = EditorGUILayout.Popup("Select Piece", selectedPieceIndex, pieceNames);
            bool pieceChanged = EditorGUI.EndChangeCheck();
            
            if (selectedPieceIndex >= 0 && selectedPieceIndex < targetLibrary.pieces.Length)
            {
                var piece = targetLibrary.pieces[selectedPieceIndex];
                
                if (piece != null && piece.IsValid())
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Prefab:", GUILayout.Width(50));
                    EditorGUILayout.ObjectField(piece.prefab, typeof(GameObject), false);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.LabelField($"Type: {piece.pieceType}");
                    EditorGUILayout.LabelField($"Sockets: {(piece.sockets != null ? piece.sockets.Length : 0)}");
                    
                    if (pieceChanged && previewInstance != null)
                    {
                        CleanupPreview();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Selected piece is invalid", MessageType.Error);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        
        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Editor Settings", EditorStyles.boldLabel);
            
            showHandles = EditorGUILayout.Toggle("Show Position Handles", showHandles);
            autoSave = EditorGUILayout.Toggle("Auto-Save to Library", autoSave);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPreviewControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            
            if (selectedPieceIndex >= 0 && previewInstance == null)
            {
                if (GUILayout.Button("Create Preview", GUILayout.Height(30)))
                {
                    CreatePreview();
                }
            }
            else if (previewInstance != null)
            {
                EditorGUILayout.HelpBox($"{socketGizmos.Count} sockets", MessageType.Info);
                
                if (GUILayout.Button("Clear Preview", GUILayout.Height(30)))
                {
                    CleanupPreview();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a piece from the library above", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSocketList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Sockets ({socketGizmos.Count})", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Add Socket", GUILayout.Width(100)))
            {
                AddSocket();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));
            
            for (int i = 0; i < socketGizmos.Count; i++)
            {
                DrawSocketEntry(i);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLiveSocketList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Sockets ({liveEditTarget.sockets.Count})", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Add Socket", GUILayout.Width(100)))
            {
                Undo.RecordObject(liveEditTarget, "Add Socket");
                liveEditTarget.sockets.Add(new Socket($"Socket_{liveEditTarget.sockets.Count}", SocketType.Point, Vector3.zero));
                EditorUtility.SetDirty(liveEditTarget);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));
            
            for (int i = 0; i < liveEditTarget.sockets.Count; i++)
            {
                DrawLiveSocketEntry(i);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSocketEntry(int index)
        {
            var gizmo = socketGizmos[index];
            bool isSelected = selectedSocketIndex == index;
            
            GUI.backgroundColor = isSelected ? new Color(0.5f, 1f, 0.5f) : Color.white;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(isSelected ? "●" : "○", GUILayout.Width(25)))
            {
                selectedSocketIndex = isSelected ? -1 : index;
                SceneView.RepaintAll();
            }
            
            EditorGUI.BeginChangeCheck();
            gizmo.data.socketName = EditorGUILayout.TextField(gizmo.data.socketName);
            if (EditorGUI.EndChangeCheck())
            {
                SaveToLibraryIfAuto();
            }
            
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                RemoveSocket(index);
                return;
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (isSelected)
            {
                DrawSocketDetails(gizmo.data, true);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLiveSocketEntry(int index)
        {
            Socket socket = liveEditTarget.sockets[index];
            bool isSelected = selectedSocketIndex == index;
            
            GUI.backgroundColor = isSelected ? new Color(0.5f, 1f, 0.5f) : Color.white;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(isSelected ? "●" : "○", GUILayout.Width(25)))
            {
                selectedSocketIndex = isSelected ? -1 : index;
                SceneView.RepaintAll();
            }
            
            EditorGUI.BeginChangeCheck();
            socket.socketName = EditorGUILayout.TextField(socket.socketName);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(liveEditTarget);
            }
            
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                
                Undo.RecordObject(liveEditTarget, "Remove Socket");
                liveEditTarget.DisconnectSocket(index);
                liveEditTarget.sockets.RemoveAt(index);
                EditorUtility.SetDirty(liveEditTarget);
                if (selectedSocketIndex == index) selectedSocketIndex = -1;
                return;
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (isSelected)
            {
                DrawSocketDetails(socket, false);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSocketDetails(Socket socket, bool isPrefabMode)
        {
            EditorGUI.indentLevel++;
            
            EditorGUI.BeginChangeCheck();
            
            socket.socketType = (SocketType)EditorGUILayout.EnumPopup("Type", socket.socketType);
            
            EditorGUILayout.LabelField("Position:", EditorStyles.miniLabel);
            socket.localPosition = EditorGUILayout.Vector3Field("", socket.localPosition);
            
            EditorGUILayout.LabelField("Rotation:", EditorStyles.miniLabel);
            socket.localRotation = EditorGUILayout.Vector3Field("", socket.localRotation);
            
            if (socket.socketType == SocketType.Surface || socket.socketType == SocketType.Volume)
            {
                EditorGUILayout.LabelField("Size:", EditorStyles.miniLabel);
                socket.size = EditorGUILayout.Vector3Field("", socket.size);
            }
            
            socket.acceptedTypes = EditorGUILayout.TextField("Accepts Types", socket.acceptedTypes);
            socket.providedTypes = EditorGUILayout.TextField("Provides Types", socket.providedTypes);
            
            socket.alignRotation = EditorGUILayout.Toggle("Align Rotation", socket.alignRotation);
            socket.connectionOffset = EditorGUILayout.Vector3Field("Connection Offset", socket.connectionOffset);
            
            socket.gizmoColor = EditorGUILayout.ColorField("Gizmo Color", socket.gizmoColor);
            
            if (EditorGUI.EndChangeCheck())
            {
                if (isPrefabMode)
                {
                    SaveToLibraryIfAuto();
                }
                else
                {
                    EditorUtility.SetDirty(liveEditTarget);
                }
                SceneView.RepaintAll();
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawSocketPresets()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Socket Presets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Wall Edges"))
            {
                AddWallSockets();
            }
            if (GUILayout.Button("Wall Center (Tile)"))
            {
                AddWallCenterSocket();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Floor Top"))
            {
                AddFloorTopSocket();
            }
            if (GUILayout.Button("Floor Edges"))
            {
                AddFloorEdgeSockets();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawExportButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            
            GUI.enabled = targetLibrary != null && selectedPieceIndex >= 0;
            if (GUILayout.Button("Save to Library", GUILayout.Height(35)))
            {
                SaveToLibrary();
            }
            GUI.enabled = true;
            
            if (targetLibrary == null)
            {
                EditorGUILayout.HelpBox("Assign a library above", MessageType.Info);
            }
            else if (selectedPieceIndex < 0)
            {
                EditorGUILayout.HelpBox("Select a piece above", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLiveSaveButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Save to Prefab & Library", GUILayout.Height(35)))
            {
                SaveLiveToLibrary();
            }
            
            EditorGUILayout.HelpBox("Changes are already saved to the instance. This button updates the library definition.", MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        private void SwitchMode(EditMode newMode)
        {
            if (currentMode == newMode)
                return;
            
            CleanupPreview();
            currentMode = newMode;
            selectedSocketIndex = -1;
            
            if (newMode == EditMode.LiveSceneMode)
            {
                if (Selection.activeGameObject != null)
                {
                    liveEditTarget = Selection.activeGameObject.GetComponent<BuildingSocket>();
                    if (liveEditTarget != null)
                    {
                        LoadLiveSockets();
                    }
                }
            }
        }
        
        private void CreatePreview()
        {
            if (targetLibrary == null || selectedPieceIndex < 0 || selectedPieceIndex >= targetLibrary.pieces.Length)
                return;
            
            var piece = targetLibrary.pieces[selectedPieceIndex];
            if (piece == null || !piece.IsValid())
                return;
            
            CleanupPreview();
            
            previewInstance = Instantiate(piece.prefab);
            previewInstance.name = "[Preview] " + piece.pieceName;
            previewInstance.transform.position = Vector3.zero;
            previewInstance.transform.rotation = Quaternion.identity;
            
            LoadExistingSockets();
            
            Selection.activeGameObject = previewInstance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        
        private void LoadExistingSockets()
        {
            socketGizmos.Clear();
            
            if (targetLibrary != null && selectedPieceIndex >= 0)
            {
                var piece = targetLibrary.GetPiece(selectedPieceIndex);
                if (piece != null && piece.sockets != null)
                {
                    foreach (var socket in piece.sockets)
                    {
                        AddSocketFromData(socket);
                    }
                }
            }
        }
        
        private void LoadLiveSockets()
        {
            if (liveEditTarget == null)
                return;
            
            selectedSocketIndex = -1;
            SceneView.RepaintAll();
            Repaint();
        }
        
        private void AddSocket()
        {
            if (previewInstance == null)
                return;
            
            int index = socketGizmos.Count;
            Socket newSocket = new Socket($"Socket_{index}", SocketType.Point, Vector3.zero);
            AddSocketFromData(newSocket);
            
            selectedSocketIndex = index;
            SaveToLibraryIfAuto();
        }
        
        private void AddSocketFromData(Socket socket)
        {
            SocketGizmo gizmo = new SocketGizmo
            {
                data = new Socket
                {
                    socketName = socket.socketName,
                    socketType = socket.socketType,
                    localPosition = socket.localPosition,
                    localRotation = socket.localRotation,
                    size = socket.size,
                    acceptedTypes = socket.acceptedTypes,
                    acceptMatchMode = socket.acceptMatchMode,
                    providedTypes = socket.providedTypes,
                    provideMatchMode = socket.provideMatchMode,
                    alignRotation = socket.alignRotation,
                    connectionOffset = socket.connectionOffset,
                    gizmoColor = socket.gizmoColor
                }
            };
            
            socketGizmos.Add(gizmo);
        }
        
        private void RemoveSocket(int index)
        {
            if (index < 0 || index >= socketGizmos.Count)
                return;
            
            socketGizmos.RemoveAt(index);
            
            if (selectedSocketIndex == index)
                selectedSocketIndex = -1;
            else if (selectedSocketIndex > index)
                selectedSocketIndex--;
            
            SaveToLibraryIfAuto();
        }
        
        private void AddWallSockets()
        {
            if (currentMode == EditMode.PrefabMode)
            {
                socketGizmos.Clear();
                AddSocketFromData(new Socket("Left", SocketType.Point, new Vector3(-2, 0, 0)) 
                { 
                    acceptedTypes = "wall", 
                    gizmoColor = Color.cyan 
                });
                AddSocketFromData(new Socket("Right", SocketType.Point, new Vector3(2, 0, 0)) 
                { 
                    acceptedTypes = "wall",
                    gizmoColor = Color.cyan 
                });
                SaveToLibraryIfAuto();
            }
            else if (liveEditTarget != null)
            {
                Undo.RecordObject(liveEditTarget, "Add Wall Sockets");
                liveEditTarget.sockets.Clear();
                liveEditTarget.sockets.Add(new Socket("Left", SocketType.Point, new Vector3(-2, 0, 0)) 
                { 
                    acceptedTypes = "wall",
                    gizmoColor = Color.cyan 
                });
                liveEditTarget.sockets.Add(new Socket("Right", SocketType.Point, new Vector3(2, 0, 0)) 
                { 
                    acceptedTypes = "wall",
                    gizmoColor = Color.cyan 
                });
                EditorUtility.SetDirty(liveEditTarget);
            }
        }
        
        private void AddWallCenterSocket()
        {
            Socket centerSocket = new Socket("Center", SocketType.Surface, Vector3.zero)
            {
                acceptedTypes = "tile,decor",
                size = new Vector3(4, 3, 0.1f),
                gizmoColor = Color.magenta
            };
            
            if (currentMode == EditMode.PrefabMode)
            {
                AddSocketFromData(centerSocket);
                SaveToLibraryIfAuto();
            }
            else if (liveEditTarget != null)
            {
                Undo.RecordObject(liveEditTarget, "Add Center Socket");
                liveEditTarget.sockets.Add(centerSocket);
                EditorUtility.SetDirty(liveEditTarget);
            }
        }
        
        private void AddFloorTopSocket()
        {
            Socket topSocket = new Socket("Top", SocketType.Surface, new Vector3(0, 0.05f, 0))
            {
                acceptedTypes = "wall,tile,decor",
                size = new Vector3(4, 0.1f, 4),
                gizmoColor = Color.green
            };
            
            if (currentMode == EditMode.PrefabMode)
            {
                AddSocketFromData(topSocket);
                SaveToLibraryIfAuto();
            }
            else if (liveEditTarget != null)
            {
                Undo.RecordObject(liveEditTarget, "Add Floor Top Socket");
                liveEditTarget.sockets.Add(topSocket);
                EditorUtility.SetDirty(liveEditTarget);
            }
        }
        
        private void AddFloorEdgeSockets()
        {
            if (currentMode == EditMode.PrefabMode)
            {
                AddSocketFromData(new Socket("Edge_North", SocketType.Edge, new Vector3(0, 0, 2)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow });
                AddSocketFromData(new Socket("Edge_South", SocketType.Edge, new Vector3(0, 0, -2)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow });
                AddSocketFromData(new Socket("Edge_East", SocketType.Edge, new Vector3(2, 0, 0)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow, localRotation = new Vector3(0, 90, 0) });
                AddSocketFromData(new Socket("Edge_West", SocketType.Edge, new Vector3(-2, 0, 0)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow, localRotation = new Vector3(0, 90, 0) });
                SaveToLibraryIfAuto();
            }
            else if (liveEditTarget != null)
            {
                Undo.RecordObject(liveEditTarget, "Add Floor Edge Sockets");
                liveEditTarget.sockets.Add(new Socket("Edge_North", SocketType.Edge, new Vector3(0, 0, 2)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow });
                liveEditTarget.sockets.Add(new Socket("Edge_South", SocketType.Edge, new Vector3(0, 0, -2)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow });
                liveEditTarget.sockets.Add(new Socket("Edge_East", SocketType.Edge, new Vector3(2, 0, 0)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow, localRotation = new Vector3(0, 90, 0) });
                liveEditTarget.sockets.Add(new Socket("Edge_West", SocketType.Edge, new Vector3(-2, 0, 0)) { acceptedTypes = "wall", size = new Vector3(0.2f, 0.2f, 4), gizmoColor = Color.yellow, localRotation = new Vector3(0, 90, 0) });
                EditorUtility.SetDirty(liveEditTarget);
            }
        }
        
        private void SaveToLibraryIfAuto()
        {
            if (autoSave)
            {
                SaveToLibrary();
            }
        }
        
        private void SaveToLibrary()
        {
            if (targetLibrary == null || selectedPieceIndex < 0)
                return;
            
            var piece = targetLibrary.GetPiece(selectedPieceIndex);
            if (piece == null)
                return;
            
            Undo.RecordObject(targetLibrary, "Update Sockets");
            
            piece.sockets = new Socket[socketGizmos.Count];
            for (int i = 0; i < socketGizmos.Count; i++)
            {
                piece.sockets[i] = socketGizmos[i].data;
            }
            
            EditorUtility.SetDirty(targetLibrary);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Saved {socketGizmos.Count} sockets to '{piece.pieceName}' in library.");
        }
        
        private void SaveLiveToLibrary()
        {
            if (liveEditTarget == null || liveEditTarget.library == null || liveEditTarget.pieceIndex < 0)
            {
                EditorUtility.DisplayDialog("Cannot Save", "Live target doesn't have library reference.", "OK");
                return;
            }
            
            var library = liveEditTarget.library;
            var piece = library.GetPiece(liveEditTarget.pieceIndex);
            
            if (piece == null)
                return;
            
            Undo.RecordObject(library, "Update Sockets from Live");
            
            piece.sockets = liveEditTarget.sockets.ToArray();
            
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Saved {liveEditTarget.sockets.Count} sockets from live instance to library.");
        }
        
        private void CleanupPreview()
        {
            if (previewInstance != null)
            {
                DestroyImmediate(previewInstance);
                previewInstance = null;
            }
            
            socketGizmos.Clear();
            selectedSocketIndex = -1;
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (currentMode == EditMode.PrefabMode)
            {
                DrawPrefabModeGizmos();
            }
            else
            {
                DrawLiveModeGizmos();
            }
        }
        
        private void DrawPrefabModeGizmos()
        {
            if (previewInstance == null)
                return;
            
            Transform transform = previewInstance.transform;
            
            for (int i = 0; i < socketGizmos.Count; i++)
            {
                Socket socket = socketGizmos[i].data;
                bool isSelected = selectedSocketIndex == i;
                
                DrawSocketGizmo(socket, transform, isSelected, i);
            }
        }
        
        private void DrawLiveModeGizmos()
        {
            if (liveEditTarget == null)
                return;
            
            Transform transform = liveEditTarget.transform;
            
            for (int i = 0; i < liveEditTarget.sockets.Count; i++)
            {
                Socket socket = liveEditTarget.sockets[i];
                bool isSelected = selectedSocketIndex == i;
                
                DrawSocketGizmo(socket, transform, isSelected, i);
                
                // Allow editing occupied sockets in Live Mode!
                if (showHandles && isSelected)
                {
                    EditorGUI.BeginChangeCheck();
                    
                    Vector3 worldPos = transform.TransformPoint(socket.localPosition);
                    Quaternion worldRot = transform.rotation * Quaternion.Euler(socket.localRotation);
                    
                    Vector3 newPos = Handles.PositionHandle(worldPos, worldRot);
                    Quaternion newRot = Handles.RotationHandle(worldRot, worldPos);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(liveEditTarget, "Move Socket");
                        
                        socket.localPosition = transform.InverseTransformPoint(newPos);
                        socket.localRotation = (Quaternion.Inverse(transform.rotation) * newRot).eulerAngles;
                        EditorUtility.SetDirty(liveEditTarget);
                        
                        Repaint();
                    }
                }
            }
        }
        
        private void DrawSocketGizmo(Socket socket, Transform transform, bool isSelected, int index)
        {
            Vector3 worldPos = transform.TransformPoint(socket.localPosition);
            Quaternion worldRot = transform.rotation * Quaternion.Euler(socket.localRotation);
            
            Color color = socket.gizmoColor;
            color.a = isSelected ? 1f : 0.6f;
            Handles.color = color;
            
            switch (socket.socketType)
            {
                case SocketType.Point:
                    Handles.SphereHandleCap(0, worldPos, Quaternion.identity, 0.3f, EventType.Repaint);
                    break;
                
                case SocketType.Surface:
                    Handles.matrix = Matrix4x4.TRS(worldPos, worldRot, socket.size);
                    Handles.DrawWireCube(Vector3.zero, Vector3.one);
                    if (isSelected)
                    {
                        color.a = 0.2f;
                        Handles.color = color;
                        Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
                    }
                    Handles.matrix = Matrix4x4.identity;
                    break;
                
                case SocketType.Edge:
                    Vector3 edgeEnd = worldPos + worldRot * Vector3.forward * socket.size.z;
                    Handles.DrawLine(worldPos, edgeEnd);
                    Handles.SphereHandleCap(0, worldPos, Quaternion.identity, 0.2f, EventType.Repaint);
                    Handles.SphereHandleCap(0, edgeEnd, Quaternion.identity, 0.2f, EventType.Repaint);
                    break;
                
                case SocketType.Volume:
                    Handles.matrix = Matrix4x4.TRS(worldPos, worldRot, socket.size);
                    Handles.DrawWireCube(Vector3.zero, Vector3.one);
                    Handles.matrix = Matrix4x4.identity;
                    break;
            }
            
            if (socket.alignRotation)
            {
                Handles.color = Color.blue;
                Handles.ArrowHandleCap(0, worldPos, worldRot, 0.5f, EventType.Repaint);
            }
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
            {
                fontSize = isSelected ? 12 : 10,
                normal = { textColor = color }
            };
            Handles.Label(worldPos + Vector3.up * 0.4f, socket.socketName, labelStyle);
        }
    }
}
