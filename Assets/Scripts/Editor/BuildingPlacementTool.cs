using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace BuildingTools
{
    public class BuildingPlacementTool : EditorWindow
    {
        private const string PREF_LIBRARY_PATH = "BuildingTool_LibraryPath";
        private const float SOCKET_SEARCH_DISTANCE = 5.0f;
        
        private Vector2 scrollPosition;
        private BuildingLibrary currentLibrary;
        private int selectedPieceIndex = -1;
        private GameObject previewObject;
        private int currentRotation = 0;
        private int currentLayer = 0;
        
        private bool snapToGrid = true;
        private bool socketSnapEnabled = true;
        private bool showGridGizmo = true;
        
        private float heightOffset = 0f;
        private float heightAdjustStep = 0.1f;
        
        private Transform buildingParent;
        
        private BuildingSocket nearestSocketTarget;
        private int nearestSocketIndex = -1;
        private int mySocketIndex = -1;
        private Vector3 socketSnapPosition;
        private Quaternion socketSnapRotation;
        
        private Dictionary<BuildingPieceType, bool> categoryFoldouts = new Dictionary<BuildingPieceType, bool>
        {
            { BuildingPieceType.ConcreteWall, true },
            { BuildingPieceType.ConcreteFloor, true },
            { BuildingPieceType.TileWall, true },
            { BuildingPieceType.TileFloor, true },
            { BuildingPieceType.Door, true },
            { BuildingPieceType.Window, true },
            { BuildingPieceType.Decor, true }
        };
        
        [MenuItem("Tools/TimeGame/Building Tools/Building Placement Tool")]
        public static void ShowWindow()
        {
            BuildingPlacementTool window = GetWindow<BuildingPlacementTool>("Building Tool");
            window.minSize = new Vector2(320, 500);
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadLastLibrary();
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyPreview();
            SaveCurrentLibrary();
        }
        
        private void LoadLastLibrary()
        {
            string path = EditorPrefs.GetString(PREF_LIBRARY_PATH, "");
            if (!string.IsNullOrEmpty(path))
            {
                currentLibrary = AssetDatabase.LoadAssetAtPath<BuildingLibrary>(path);
            }
        }
        
        private void SaveCurrentLibrary()
        {
            if (currentLibrary != null)
            {
                string path = AssetDatabase.GetAssetPath(currentLibrary);
                EditorPrefs.SetString(PREF_LIBRARY_PATH, path);
            }
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Building Placement Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUI.BeginChangeCheck();
            currentLibrary = EditorGUILayout.ObjectField("Building Library", currentLibrary, typeof(BuildingLibrary), false) as BuildingLibrary;
            if (EditorGUI.EndChangeCheck())
            {
                selectedPieceIndex = -1;
                DestroyPreview();
                SaveCurrentLibrary();
            }
            
            if (currentLibrary == null)
            {
                EditorGUILayout.HelpBox("Assign a Building Library to begin.\n\nCreate one via: Assets > Create > Building Tools > Building Library", MessageType.Info);
                
                if (GUILayout.Button("Create New Building Library", GUILayout.Height(30)))
                {
                    CreateNewLibrary();
                }
                return;
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Placement Settings", EditorStyles.boldLabel);
            
            snapToGrid = EditorGUILayout.Toggle("Snap to Grid", snapToGrid);
            if (snapToGrid && currentLibrary != null)
            {
                currentLibrary.gridSize = EditorGUILayout.FloatField("Grid Size", currentLibrary.gridSize);
            }
            
            socketSnapEnabled = EditorGUILayout.Toggle("Socket Snap", socketSnapEnabled);
            EditorGUILayout.HelpBox("Socket Snap: Automatically connects pieces at compatible sockets", MessageType.None);
            
            showGridGizmo = EditorGUILayout.Toggle("Show Grid Gizmo", showGridGizmo);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            heightAdjustStep = EditorGUILayout.FloatField("Height Step", heightAdjustStep);
            if (GUILayout.Button("Reset", GUILayout.Width(60)))
            {
                heightOffset = 0f;
            }
            EditorGUILayout.EndHorizontal();
            
            if (heightOffset != 0f)
            {
                EditorGUILayout.HelpBox($"Current Height Offset: {heightOffset:F2} units", MessageType.Info);
            }
            
            if (currentLibrary != null)
            {
                currentLibrary.floorHeight = EditorGUILayout.FloatField("Floor Height", currentLibrary.floorHeight);
            }
            
            currentLayer = EditorGUILayout.IntField("Current Layer", currentLayer);
            
            buildingParent = EditorGUILayout.ObjectField("Building Parent", buildingParent, typeof(Transform), true) as Transform;
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Left Click: Place piece\n" +
                "R: Rotate 90°\n" +
                "Shift+R: Rotate -90°\n" +
                "Mouse Wheel: Adjust height\n" +
                "Shift+Mouse Wheel: Fine adjust (smaller steps)\n" +
                "Delete: Clear selection\n" +
                "Ctrl+Z: Undo\n\n" +
                "Socket Snap: Hover near compatible sockets to auto-align", 
                MessageType.Info);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            if (currentLibrary.pieces == null || currentLibrary.pieces.Length == 0)
            {
                EditorGUILayout.HelpBox("No building pieces in library. Add pieces to the library asset.", MessageType.Warning);
                return;
            }
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawLibraryPieces();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void CreateNewLibrary()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Building Library",
                "New Building Library",
                "asset",
                "Create a new building library"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                BuildingLibrary newLibrary = CreateInstance<BuildingLibrary>();
                newLibrary.libraryName = "New Building Library";
                AssetDatabase.CreateAsset(newLibrary, path);
                AssetDatabase.SaveAssets();
                
                currentLibrary = newLibrary;
                SaveCurrentLibrary();
            }
        }
        
        private void DrawLibraryPieces()
        {
            var groupedPieces = currentLibrary.pieces
                .Select((piece, index) => new { piece, index })
                .Where(x => x.piece != null && x.piece.IsValid())
                .GroupBy(x => x.piece.pieceType);
            
            foreach (var group in groupedPieces.OrderBy(g => g.Key))
            {
                if (!categoryFoldouts.ContainsKey(group.Key))
                    categoryFoldouts[group.Key] = true;
                
                EditorGUILayout.BeginVertical("box");
                
                categoryFoldouts[group.Key] = EditorGUILayout.Foldout(
                    categoryFoldouts[group.Key], 
                    $"{group.Key} ({group.Count()})", 
                    true, 
                    EditorStyles.foldoutHeader
                );
                
                if (categoryFoldouts[group.Key])
                {
                    foreach (var item in group)
                    {
                        DrawPieceButton(item.piece, item.index);
                    }
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }
        
        private void DrawPieceButton(BuildingPieceDefinition piece, int index)
        {
            EditorGUILayout.BeginHorizontal();
            
            bool isSelected = selectedPieceIndex == index;
            GUI.color = isSelected ? Color.green : Color.white;
            
            Texture2D preview = AssetPreview.GetAssetPreview(piece.prefab);
            GUIContent buttonContent = preview != null 
                ? new GUIContent(piece.pieceName, preview)
                : new GUIContent(piece.pieceName);
            
            if (GUILayout.Button(buttonContent, GUILayout.Height(40)))
            {
                SelectPiece(index);
            }
            
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        
        private void SelectPiece(int index)
        {
            selectedPieceIndex = index;
            currentRotation = 0;
            DestroyPreview();
            SceneView.RepaintAll();
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (currentLibrary == null || selectedPieceIndex < 0)
                return;
            
            var selectedPiece = currentLibrary.GetPiece(selectedPieceIndex);
            if (selectedPiece == null || !selectedPiece.IsValid())
                return;
            
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            
            Event e = Event.current;
            
            HandleKeyboardInput(e);
            HandleMouseWheelInput(e);
            
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 targetPosition;
            Quaternion targetRotation;
            
            // Always calculate positions, but store them for click events
            if (socketSnapEnabled)
            {
                FindNearestSocket(ray, selectedPiece, out targetPosition, out targetRotation);
            }
            else
            {
                targetPosition = GetGridPosition(ray);
                targetRotation = Quaternion.Euler(0, currentRotation, 0);
                nearestSocketTarget = null;
                nearestSocketIndex = -1;
                mySocketIndex = -1;
            }
            
            // Apply height offset
            targetPosition.y += heightOffset;
            
            UpdatePreview(targetPosition, targetRotation, selectedPiece);
            
            if (showGridGizmo && snapToGrid)
            {
                DrawGridGizmo(targetPosition);
            }
            
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlacePiece(targetPosition, targetRotation, selectedPiece);
                e.Use();
            }
            
            sceneView.Repaint();
        }
        
        private void HandleKeyboardInput(Event e)
        {
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.R)
                {
                    if (e.shift)
                    {
                        currentRotation -= 90;
                    }
                    else
                    {
                        currentRotation += 90;
                    }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                {
                    selectedPieceIndex = -1;
                    DestroyPreview();
                    e.Use();
                }
            }
        }
        
        private void HandleMouseWheelInput(Event e)
        {
            if (e.type == EventType.ScrollWheel)
            {
                float step = heightAdjustStep;
                
                if (e.shift)
                {
                    step *= 0.1f;
                }
                
                if (e.control)
                {
                    step *= 0.01f;
                }
                
                heightOffset -= e.delta.y * step;
                
                e.Use();
                Repaint();
            }
        }
        
        private void FindNearestSocket(Ray ray, BuildingPieceDefinition selectedPiece, out Vector3 position, out Quaternion rotation)
        {
            float minDistance = SOCKET_SEARCH_DISTANCE;
            
            Vector3 gridPos = GetGridPosition(ray);
            position = gridPos;
            rotation = Quaternion.Euler(0, currentRotation, 0);
            
            if (selectedPiece.sockets == null || selectedPiece.sockets.Length == 0)
            {
                socketSnapPosition = position;
                socketSnapRotation = rotation;
                return;
            }
            
            BuildingSocket[] allSockets = FindObjectsByType<BuildingSocket>(FindObjectsSortMode.None);
            
            foreach (var targetSocket in allSockets)
            {
                if (targetSocket.sockets == null || targetSocket.sockets.Count == 0)
                    continue;
                
                for (int targetIdx = 0; targetIdx < targetSocket.sockets.Count; targetIdx++)
                {
                    Socket targetSocketData = targetSocket.sockets[targetIdx];
                    Vector3 targetWorldPos = targetSocket.GetSocketWorldPosition(targetIdx);
                    
                    Vector3 closestPointOnRay = ClosestPointOnRay(ray, targetWorldPos);
                    float distance = Vector3.Distance(closestPointOnRay, targetWorldPos);
                    
                    if (distance > minDistance)
                        continue;
                    
                    for (int myIdx = 0; myIdx < selectedPiece.sockets.Length; myIdx++)
                    {
                        Socket mySocketData = selectedPiece.sockets[myIdx];
                        
                        // Check if my socket provides what target accepts
                        bool iProvideWhatTargetAccepts = mySocketData.ProvidesCompatibleWith(targetSocketData);
                        
                        // Check if target provides what I accept
                        bool targetProvidesWhatIAccept = targetSocketData.ProvidesCompatibleWith(mySocketData);
                        
                        // At least one direction must be compatible
                        if (!iProvideWhatTargetAccepts && !targetProvidesWhatIAccept)
                            continue;
                        
                        float socketScore = CalculateSocketScore(mySocketData.socketType, targetSocketData.socketType, distance);
                        
                        if (socketScore < minDistance)
                        {
                            minDistance = socketScore;
                            nearestSocketTarget = targetSocket;
                            nearestSocketIndex = targetIdx;
                            mySocketIndex = myIdx;
                            
                            position = CalculateSocketConnectionPosition(
                                targetSocket, targetIdx, targetSocketData,
                                mySocketData, rotation
                            );
                        }
                    }
                }
            }
            
            if (nearestSocketTarget == null)
            {
                nearestSocketTarget = null;
                nearestSocketIndex = -1;
                mySocketIndex = -1;
            }
            
            socketSnapPosition = position;
            socketSnapRotation = rotation;
        }
        
        private Vector3 ClosestPointOnRay(Ray ray, Vector3 point)
        {
            Vector3 rayToPoint = point - ray.origin;
            float dotProduct = Vector3.Dot(rayToPoint, ray.direction);
            
            // Clamp to positive side of ray only
            dotProduct = Mathf.Max(0, dotProduct);
            
            return ray.origin + ray.direction * dotProduct;
        }
        
        private float CalculateSocketScore(SocketType myType, SocketType targetType, float distance)
        {
            float score = distance;
            
            if (myType == SocketType.Point && targetType == SocketType.Point)
            {
                score *= 0.8f;
            }
            else if (myType == SocketType.Surface && targetType == SocketType.Surface)
            {
                score *= 0.9f;
            }
            else if ((myType == SocketType.Point && targetType == SocketType.Edge) ||
                     (myType == SocketType.Edge && targetType == SocketType.Point))
            {
                score *= 0.85f;
            }
            
            return score;
        }
        
        private Vector3 CalculateSocketConnectionPosition(
            BuildingSocket targetSocket, int targetIdx, Socket targetSocketData,
            Socket mySocketData, Quaternion myRotation)
        {
            Vector3 targetWorldPos = targetSocket.GetSocketWorldPosition(targetIdx);
            Quaternion targetWorldRot = targetSocket.GetSocketWorldRotation(targetIdx);
            
            Vector3 mySocketLocalPos = mySocketData.localPosition;
            Vector3 mySocketOffset = myRotation * mySocketLocalPos;
            
            Vector3 connectionPos = targetWorldPos - mySocketOffset;
            
            if (targetSocketData.connectionOffset != Vector3.zero)
            {
                connectionPos += targetWorldRot * targetSocketData.connectionOffset;
            }
            
            if (mySocketData.connectionOffset != Vector3.zero)
            {
                connectionPos += myRotation * mySocketData.connectionOffset;
            }
            
            return connectionPos;
        }
        
        private Vector3 GetGridPosition(Ray ray)
        {
            Vector3 position = Vector3.zero;
            float layerHeight = currentLayer * (currentLibrary != null ? currentLibrary.floorHeight : 3f);
            
            Plane plane = new Plane(Vector3.up, Vector3.up * layerHeight);
            if (plane.Raycast(ray, out float enter))
            {
                position = ray.GetPoint(enter);
            }
            
            if (snapToGrid && currentLibrary != null)
            {
                float gridSize = currentLibrary.gridSize;
                position.x = Mathf.Round(position.x / gridSize) * gridSize;
                position.z = Mathf.Round(position.z / gridSize) * gridSize;
            }
            
            position.y = layerHeight;
            
            return position;
        }
        
        private void UpdatePreview(Vector3 position, Quaternion rotation, BuildingPieceDefinition piece)
        {
            if (previewObject == null && piece.prefab != null)
            {
                previewObject = Instantiate(piece.prefab);
                previewObject.hideFlags = HideFlags.HideAndDontSave;
                
                foreach (Renderer renderer in previewObject.GetComponentsInChildren<Renderer>())
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null)
                        {
                            materials[i] = new Material(materials[i]);
                            Color previewColor = currentLibrary != null ? currentLibrary.previewColor : new Color(0.5f, 1f, 0.5f, 0.5f);
                            materials[i].color = previewColor;
                            
                            materials[i].SetFloat("_Surface", 1);
                            materials[i].SetFloat("_Mode", 2);
                            materials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            materials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            materials[i].SetInt("_ZWrite", 0);
                            materials[i].renderQueue = 3000;
                        }
                    }
                    renderer.sharedMaterials = materials;
                }
                
                foreach (Collider col in previewObject.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }
            
            if (previewObject != null)
            {
                previewObject.transform.position = position;
                previewObject.transform.rotation = rotation;
            }
        }
        
        private void PlacePiece(Vector3 position, Quaternion rotation, BuildingPieceDefinition piece)
        {
            if (piece == null || piece.prefab == null)
                return;
            
            GameObject newObject = PrefabUtility.InstantiatePrefab(piece.prefab) as GameObject;
            newObject.transform.position = position;
            newObject.transform.rotation = rotation;
            
            if (buildingParent != null)
            {
                newObject.transform.SetParent(buildingParent);
            }
            
            BuildingSocket socket = newObject.GetComponent<BuildingSocket>();
            if (socket == null)
            {
                socket = newObject.AddComponent<BuildingSocket>();
            }
            
            socket.InitializeFromDefinition(currentLibrary, selectedPieceIndex);
            
            if (nearestSocketTarget != null && socketSnapEnabled && mySocketIndex >= 0 && nearestSocketIndex >= 0)
            {
                socket.ConnectSocket(mySocketIndex, nearestSocketTarget, nearestSocketIndex);
            }
            
            Undo.RegisterCreatedObjectUndo(newObject, "Place Building Piece");
            Selection.activeGameObject = newObject;
        }
        
        private void DrawGridGizmo(Vector3 center)
        {
            if (currentLibrary == null)
                return;
            
            Handles.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            float gridSize = currentLibrary.gridSize;
            int gridCount = 10;
            
            // Draw grid at base height (without offset)
            Vector3 gridCenter = center;
            gridCenter.y -= heightOffset;
            
            for (int x = -gridCount; x <= gridCount; x++)
            {
                Vector3 start = gridCenter + new Vector3(x * gridSize, 0, -gridCount * gridSize);
                Vector3 end = gridCenter + new Vector3(x * gridSize, 0, gridCount * gridSize);
                Handles.DrawLine(start, end);
            }
            
            for (int z = -gridCount; z <= gridCount; z++)
            {
                Vector3 start = gridCenter + new Vector3(-gridCount * gridSize, 0, z * gridSize);
                Vector3 end = gridCenter + new Vector3(gridCount * gridSize, 0, z * gridSize);
                Handles.DrawLine(start, end);
            }
            
            // Draw height offset indicator
            if (heightOffset != 0f)
            {
                Handles.color = heightOffset > 0 ? Color.green : Color.red;
                
                // Draw vertical line from grid to piece position
                Vector3 basePos = new Vector3(center.x, gridCenter.y, center.z);
                Vector3 offsetPos = new Vector3(center.x, center.y, center.z);
                Handles.DrawLine(basePos, offsetPos);
                
                // Draw arrow at piece position
                float arrowSize = 0.3f;
                Handles.DrawWireCube(offsetPos, Vector3.one * arrowSize);
                
                // Label with offset value
                Handles.Label(offsetPos + Vector3.right * 0.5f, $"{heightOffset:F2}m", EditorStyles.whiteBoldLabel);
            }
            
            if (nearestSocketTarget != null && nearestSocketIndex >= 0)
            {
                Handles.color = Color.yellow;
                Vector3 targetSocketPos = nearestSocketTarget.GetSocketWorldPosition(nearestSocketIndex);
                
                Socket targetSocketData = nearestSocketTarget.sockets[nearestSocketIndex];
                
                switch (targetSocketData.socketType)
                {
                    case SocketType.Point:
                        Handles.DrawWireCube(targetSocketPos, Vector3.one * 0.4f);
                        break;
                    case SocketType.Surface:
                        Quaternion targetRot = nearestSocketTarget.GetSocketWorldRotation(nearestSocketIndex);
                        Handles.matrix = Matrix4x4.TRS(targetSocketPos, targetRot, targetSocketData.size);
                        Handles.DrawWireCube(Vector3.zero, Vector3.one * 1.2f);
                        Handles.matrix = Matrix4x4.identity;
                        break;
                    case SocketType.Edge:
                        Handles.DrawLine(center, targetSocketPos);
                        Handles.DrawWireCube(targetSocketPos, Vector3.one * 0.3f);
                        break;
                }
                
                Handles.color = Color.green;
                Handles.DrawLine(center, targetSocketPos);
            }
        }
        
        private void DestroyPreview()
        {
            if (previewObject != null)
            {
                DestroyImmediate(previewObject);
                previewObject = null;
            }
        }
    }
}
