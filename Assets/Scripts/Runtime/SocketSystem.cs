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
        
        [Tooltip("What piece types this socket provides when connected")]
        public string providedTypes = "";
        
        [Tooltip("Is this socket occupied?")]
        public bool isOccupied = false;
        
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
            if (isOccupied)
                return false;
            
            if (string.IsNullOrEmpty(acceptedTypes))
                return true;
            
            if (string.IsNullOrEmpty(pieceTags))
                return false;
            
            string[] acceptedArray = acceptedTypes.Split(',');
            string[] pieceTagsArray = pieceTags.Split(',');
            
            foreach (string acceptedType in acceptedArray)
            {
                foreach (string pieceTag in pieceTagsArray)
                {
                    if (acceptedType.Trim().Equals(pieceTag.Trim(), System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
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
        public bool showOccupiedSockets = false;
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
                            providedTypes = socket.providedTypes,
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
            
            if (mySocket.isOccupied || otherSocket.isOccupied)
                return false;
            
            bool myAcceptsOther = mySocket.Accepts(otherPiece.pieceTypeTags);
            bool otherAcceptsMe = otherSocket.Accepts(pieceTypeTags);
            
            return myAcceptsOther || otherAcceptsMe;
        }
        
        public void ConnectSocket(int mySocketIndex, BuildingSocket otherPiece, int otherSocketIndex)
        {
            if (!CanConnectToSocket(mySocketIndex, otherPiece, otherSocketIndex))
                return;
            
            sockets[mySocketIndex].isOccupied = true;
            otherPiece.sockets[otherSocketIndex].isOccupied = true;
            
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
                    conn.sourceSocket.sockets[conn.sourceSocketIndex].isOccupied = false;
                    conn.targetSocket.sockets[conn.targetSocketIndex].isOccupied = false;
                    
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
                
                if (!showOccupiedSockets && socket.isOccupied)
                    continue;
                
                Vector3 worldPos = GetSocketWorldPosition(i);
                Quaternion worldRot = GetSocketWorldRotation(i);
                
                Color socketColor = socket.isOccupied ? Color.gray : socket.gizmoColor;
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
