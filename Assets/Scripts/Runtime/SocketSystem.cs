using UnityEngine;
using System.Collections.Generic;

namespace BuildingTools
{
    public enum SocketType
    {
        Point,      // Specific point (corners, ends)
        Surface,    // Flat surface (wall face, floor top)
        Edge,       // Linear edge (wall bottom, floor perimeter)
        Volume      // 3D space (door frame, window opening)
    }
    
    public enum SocketMatchMode
    {
        Any,        // Accept if ANY of the accepted types match (OR logic)
        All         // Accept only if ALL accepted types match (AND logic)
    }
    
    [System.Serializable]
    public class Socket
    {
        [Header("Socket Identity")]
        public string socketName = "Socket";
        public SocketType socketType = SocketType.Point;
        
        [Header("Transform")]
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localRotation = Vector3.zero;
        public Vector3 size = Vector3.one;
        
        [Header("Compatibility")]
        [Tooltip("What piece types can connect to this socket (comma-separated)")]
        public string acceptedTypes = "";
        
        [Tooltip("ANY = accept if any tag matches (floor,wall accepts floor OR wall)\nALL = require all tags (tile,floor requires tile AND floor)")]
        public SocketMatchMode acceptMatchMode = SocketMatchMode.Any;
        
        [Tooltip("What piece types this socket provides when connected")]
        public string providedTypes = "";
        
        [Tooltip("ANY = provides any of the tags (floor,wall means floor OR wall)\nALL = provides all tags together (tile,floor means tile AND floor as a set)")]
        public SocketMatchMode provideMatchMode = SocketMatchMode.Any;
        
        [Header("Behavior")]
        [Tooltip("Auto-align connected piece to this socket's rotation")]
        public bool alignRotation = true;
        
        [Tooltip("Offset to apply to connected piece")]
        public Vector3 connectionOffset = Vector3.zero;
        
        [Header("Visual")]
        public Color gizmoColor = Color.cyan;
        
        public Socket()
        {
            socketName = "Socket";
            socketType = SocketType.Point;
            localPosition = Vector3.zero;
            localRotation = Vector3.zero;
            size = Vector3.one * 0.3f;
        }
        
        public Socket(string name, SocketType type, Vector3 position)
        {
            socketName = name;
            socketType = type;
            localPosition = position;
            localRotation = Vector3.zero;
            size = type == SocketType.Surface ? new Vector3(2, 0.1f, 2) : Vector3.one * 0.3f;
        }
        
        public bool Accepts(string pieceTags)
        {
            if (string.IsNullOrEmpty(acceptedTypes))
                return false;
            
            if (string.IsNullOrEmpty(pieceTags))
                return false;
            
            string[] acceptedArray = acceptedTypes.Split(',');
            string[] pieceTagsArray = pieceTags.Split(',');
            
            if (acceptMatchMode == SocketMatchMode.All)
            {
                // ALL accepted types must be present in the provided tags
                foreach (string acceptedType in acceptedArray)
                {
                    string trimmedAccepted = acceptedType.Trim();
                    bool foundMatch = false;
                    
                    foreach (string pieceTag in pieceTagsArray)
                    {
                        if (trimmedAccepted.Equals(pieceTag.Trim(), System.StringComparison.OrdinalIgnoreCase))
                        {
                            foundMatch = true;
                            break;
                        }
                    }
                    
                    if (!foundMatch)
                        return false;
                }
                
                return true;
            }
            else // SocketMatchMode.Any
            {
                // Accept if ANY of the accepted types match
                foreach (string acceptedType in acceptedArray)
                {
                    string trimmedAccepted = acceptedType.Trim();
                    
                    foreach (string pieceTag in pieceTagsArray)
                    {
                        if (trimmedAccepted.Equals(pieceTag.Trim(), System.StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                
                return false;
            }
        }
        
        public string GetProvidedTypesAsString()
        {
            return providedTypes;
        }
        
        public bool ProvidesCompatibleWith(Socket otherSocket)
        {
            if (string.IsNullOrEmpty(providedTypes))
                return false;
            
            if (string.IsNullOrEmpty(otherSocket.acceptedTypes))
                return false;
            
            string[] providedArray = providedTypes.Split(',');
            string[] acceptedArray = otherSocket.acceptedTypes.Split(',');
            
            if (provideMatchMode == SocketMatchMode.All)
            {
                // When providing ALL: All my provided types must be in the other's accepted list
                // AND the other socket must accept them based on its accept mode
                if (otherSocket.acceptMatchMode == SocketMatchMode.All)
                {
                    // Other wants ALL its accepted types present in what I provide
                    // So I must provide at least all of what it accepts
                    foreach (string acceptedType in acceptedArray)
                    {
                        string trimmedAccepted = acceptedType.Trim();
                        bool foundInProvided = false;
                        
                        foreach (string providedType in providedArray)
                        {
                            if (trimmedAccepted.Equals(providedType.Trim(), System.StringComparison.OrdinalIgnoreCase))
                            {
                                foundInProvided = true;
                                break;
                            }
                        }
                        
                        if (!foundInProvided)
                            return false;
                    }
                    return true;
                }
                else // otherSocket.acceptMatchMode == Any
                {
                    // Other accepts ANY of its types, I provide ALL of mine
                    // Check if ANY of what I provide matches ANY of what it accepts
                    foreach (string providedType in providedArray)
                    {
                        string trimmedProvided = providedType.Trim();
                        
                        foreach (string acceptedType in acceptedArray)
                        {
                            if (trimmedProvided.Equals(acceptedType.Trim(), System.StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                    return false;
                }
            }
            else // provideMatchMode == Any
            {
                // When providing ANY: At least one of my provided types must match what they accept
                return otherSocket.Accepts(providedTypes);
            }
        }
        
        public bool Provides(string pieceType)
        {
            if (string.IsNullOrEmpty(providedTypes))
                return false;
            
            string[] types = providedTypes.Split(',');
            foreach (string type in types)
            {
                if (type.Trim().Equals(pieceType.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
    
    public class SocketConnection
    {
        public BuildingSocket sourceSocket;
        public int sourceSocketIndex;
        public BuildingSocket targetSocket;
        public int targetSocketIndex;
        
        public SocketConnection(BuildingSocket source, int sourceIdx, BuildingSocket target, int targetIdx)
        {
            sourceSocket = source;
            sourceSocketIndex = sourceIdx;
            targetSocket = target;
            targetSocketIndex = targetIdx;
        }
    }
    
    [ExecuteInEditMode]
    [SelectionBase]
    public class BuildingSocket : MonoBehaviour
    {
        [Header("References")]
        public BuildingLibrary library;
        public int pieceIndex = -1;
        
        [Header("Piece Info")]
        public BuildingPieceType pieceType = BuildingPieceType.ConcreteWall;
        public string pieceTypeTags = "wall";
        
        [Header("Sockets")]
        public List<Socket> sockets = new List<Socket>();
        
        [Header("Connections")]
        public List<SocketConnection> connections = new List<SocketConnection>();
        
        [Header("Visual Settings")]
        public bool showSockets = true;
        public float gizmoSize = 0.3f;
        
        private void Awake()
        {
            #if UNITY_EDITOR
            // This component is editor-only, won't be included in builds
            hideFlags = HideFlags.DontSaveInBuild;
            #endif
        }
        
        private void OnEnable()
        {
            #if UNITY_EDITOR
            // Ensure editor-only flag is set
            hideFlags = HideFlags.DontSaveInBuild;
            #endif
        }
        
        public BuildingPieceDefinition Definition
        {
            get
            {
                if (library == null || pieceIndex < 0)
                    return null;
                return library.GetPiece(pieceIndex);
            }
        }
        
        public void InitializeFromDefinition(BuildingLibrary lib, int index)
        {
            library = lib;
            pieceIndex = index;
            
            var def = Definition;
            if (def != null)
            {
                pieceType = def.pieceType;
                pieceTypeTags = def.providedTags;
                
                sockets = new List<Socket>();
                if (def.sockets != null)
                {
                    foreach (var socket in def.sockets)
                    {
                        sockets.Add(new Socket
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
                        });
                    }
                }
            }
        }
        
        public Vector3 GetSocketWorldPosition(int index)
        {
            if (index < 0 || index >= sockets.Count)
                return transform.position;
            
            return transform.TransformPoint(sockets[index].localPosition);
        }
        
        public Quaternion GetSocketWorldRotation(int index)
        {
            if (index < 0 || index >= sockets.Count)
                return transform.rotation;
            
            Quaternion localRot = Quaternion.Euler(sockets[index].localRotation);
            return transform.rotation * localRot;
        }
        
        public bool CanConnectToSocket(int mySocketIndex, BuildingSocket otherPiece, int otherSocketIndex)
        {
            if (mySocketIndex < 0 || mySocketIndex >= sockets.Count)
                return false;
            
            if (otherSocketIndex < 0 || otherSocketIndex >= otherPiece.sockets.Count)
                return false;
            
            Socket mySocket = sockets[mySocketIndex];
            Socket otherSocket = otherPiece.sockets[otherSocketIndex];
            
            // Check if my socket provides what other accepts
            bool iProvideWhatOtherAccepts = mySocket.ProvidesCompatibleWith(otherSocket);
            
            // Check if other provides what I accept
            bool otherProvidesWhatIAccept = otherSocket.ProvidesCompatibleWith(mySocket);
            
            return iProvideWhatOtherAccepts || otherProvidesWhatIAccept;
        }
        
        public void ConnectSocket(int mySocketIndex, BuildingSocket otherPiece, int otherSocketIndex)
        {
            if (!CanConnectToSocket(mySocketIndex, otherPiece, otherSocketIndex))
                return;
            
            SocketConnection connection = new SocketConnection(this, mySocketIndex, otherPiece, otherSocketIndex);
            connections.Add(connection);
            otherPiece.connections.Add(connection);
        }
        
        public void DisconnectSocket(int socketIndex)
        {
            if (socketIndex < 0 || socketIndex >= sockets.Count)
                return;
            
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                var conn = connections[i];
                if (conn.sourceSocketIndex == socketIndex || conn.targetSocketIndex == socketIndex)
                {
                    conn.sourceSocket.connections.Remove(conn);
                    conn.targetSocket.connections.Remove(conn);
                }
            }
        }
        
        public (BuildingSocket piece, int socketIndex) GetConnectedSocket(int mySocketIndex)
        {
            foreach (var conn in connections)
            {
                if (conn.sourceSocketIndex == mySocketIndex && conn.sourceSocket == this)
                    return (conn.targetSocket, conn.targetSocketIndex);
                
                if (conn.targetSocketIndex == mySocketIndex && conn.targetSocket == this)
                    return (conn.sourceSocket, conn.sourceSocketIndex);
            }
            
            return (null, -1);
        }
        
        private void OnDrawGizmos()
        {
            #if UNITY_EDITOR
            if (!showSockets)
                return;
            
            DrawSocketGizmos(false);
            #endif
        }
        
        private void OnDrawGizmosSelected()
        {
            #if UNITY_EDITOR
            if (!showSockets)
                return;
            
            DrawSocketGizmos(true);
            #endif
        }
        
        private void DrawSocketGizmos(bool selected)
        {
            for (int i = 0; i < sockets.Count; i++)
            {
                Socket socket = sockets[i];
                
                Vector3 worldPos = GetSocketWorldPosition(i);
                Quaternion worldRot = GetSocketWorldRotation(i);
                
                Color socketColor = socket.gizmoColor;
                float alpha = selected ? 0.8f : 0.4f;
                socketColor.a = alpha;
                
                Gizmos.color = socketColor;
                
                switch (socket.socketType)
                {
                    case SocketType.Point:
                        Gizmos.DrawWireSphere(worldPos, gizmoSize);
                        if (selected)
                            Gizmos.DrawSphere(worldPos, gizmoSize * 0.5f);
                        break;
                    
                    case SocketType.Surface:
                        Gizmos.matrix = Matrix4x4.TRS(worldPos, worldRot, socket.size);
                        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                        if (selected)
                        {
                            socketColor.a = 0.2f;
                            Gizmos.color = socketColor;
                            Gizmos.DrawCube(Vector3.zero, Vector3.one);
                        }
                        Gizmos.matrix = Matrix4x4.identity;
                        break;
                    
                    case SocketType.Edge:
                        Vector3 edgeEnd = worldPos + worldRot * Vector3.forward * socket.size.z;
                        Gizmos.DrawLine(worldPos, edgeEnd);
                        Gizmos.DrawWireSphere(worldPos, gizmoSize * 0.5f);
                        Gizmos.DrawWireSphere(edgeEnd, gizmoSize * 0.5f);
                        break;
                    
                    case SocketType.Volume:
                        Gizmos.matrix = Matrix4x4.TRS(worldPos, worldRot, socket.size);
                        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                        Gizmos.matrix = Matrix4x4.identity;
                        break;
                }
                
                if (selected && socket.alignRotation)
                {
                    float arrowSize = gizmoSize * 1.5f;
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(worldPos, worldRot * Vector3.forward * arrowSize);
                }
            }
            
            if (selected)
            {
                Gizmos.color = Color.green;
                foreach (var conn in connections)
                {
                    Vector3 start = GetSocketWorldPosition(conn.sourceSocket == this ? conn.sourceSocketIndex : conn.targetSocketIndex);
                    Vector3 end = conn.sourceSocket == this 
                        ? conn.targetSocket.GetSocketWorldPosition(conn.targetSocketIndex)
                        : conn.sourceSocket.GetSocketWorldPosition(conn.sourceSocketIndex);
                    
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }
}
