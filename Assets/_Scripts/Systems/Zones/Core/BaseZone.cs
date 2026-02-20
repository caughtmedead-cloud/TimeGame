using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;

public abstract class BaseZone : NetworkBehaviour
{
    [Header("Zone Identity")]
    public string zoneName = "Zone";
    
    [Header("Zone Shape")]
    [SerializeField] protected ZoneColliderType _colliderType = ZoneColliderType.Sphere;
    [SerializeField] protected float _editorEffectRadius = 10f;
    
    protected readonly SyncVar<float> _effectRadius = new SyncVar<float>(
        10f,
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    
    [Header("Visual Settings")]
    public Color zoneColor = new Color(0f, 1f, 1f, 0.3f);
    public Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    
    [Header("Draw XXL Settings")]
    public bool showRadius = true;
    public bool showCenterPoint = true;
    public float centerPointSize = 0.5f;
    public float infoTextSize = 1.0f;
    public int strutCount = 2;
    public bool showTextLabel = true;
    
    [Tooltip("Vertical offset for text label. 1.0 = top of shape, 0 = center, -1.0 = bottom")]
    [Range(-1f, 2f)]
    public float textAnchorHeight = 1.2f;
    
    [Header("Intensity Gradient")]
    [Tooltip("Enable distance-based intensity falloff for effects")]
    public bool useIntensityGradient = false;
    
    [Tooltip("Intensity curve: X = normalized distance from center (0=center, 1=edge), Y = multiplier (0-1)")]
    public AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
    
    [Tooltip("Visualize gradient rings in editor")]
    public bool showGradientRings = true;
    
    [Tooltip("Number of gradient visualization rings")]
    [Range(2, 10)]
    public int gradientRingCount = 4;
    
    [Header("Debug")]
    [SerializeField] protected bool showGizmos = true;
    [SerializeField] protected Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);
    
    protected readonly List<GameObject> affectedObjects = new List<GameObject>();
    protected Collider activeCollider;
    protected List<ZoneEffect> zoneEffects = new List<ZoneEffect>();
    
    public ZoneColliderType ColliderType => _colliderType;
    public bool ShowGizmos => showGizmos;
    public Color GizmoColor => gizmoColor;
    
    public float effectRadius
    {
        get => Application.isPlaying ? _effectRadius.Value : _editorEffectRadius;
        set
        {
            if (Application.isPlaying && NetworkObject != null && IsServerStarted)
            {
                _effectRadius.Value = value;
            }
            else
            {
                _editorEffectRadius = value;
                UpdateColliderSize();
            }
        }
    }
    
    protected virtual void Awake()
    {
        CacheActiveCollider();
        CacheZoneEffects();
        _effectRadius.OnChange += OnRadiusChanged;
    }
    
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        if (IsServerStarted)
        {
            _effectRadius.Value = _editorEffectRadius;
            UpdateColliderSize();
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateColliderSize();
    }
    
    protected virtual void Start()
    {
        InitializeEffects();
    }
    
    protected virtual void Update()
    {
        if (!IsServerStarted) return;
        
        UpdateAffectedObjects();
    }
    
    protected virtual void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[BaseZone] OnTriggerEnter fired on {(IsServerStarted ? "SERVER" : "CLIENT")} - Object: {other.gameObject.name}, Zone: {zoneName}");
        
        if (!IsServerStarted) return;
        
        GameObject obj = other.gameObject;
        
        if (!CanAffectObject(obj))
        {
            Debug.Log($"[BaseZone] SERVER - Cannot affect object: {obj.name} (no TemporalStability component)");
            return;
        }
        
        if (!affectedObjects.Contains(obj))
        {
            affectedObjects.Add(obj);
            Debug.Log($"[BaseZone] SERVER - Player {obj.name} ENTERED zone {zoneName}. Total objects in zone: {affectedObjects.Count}");
            OnObjectEnteredZone(obj);
        }
        else
        {
            Debug.Log($"[BaseZone] SERVER - Player {obj.name} already in list");
        }
    }
    
    protected virtual void OnTriggerExit(Collider other)
    {
        Debug.Log($"[BaseZone] OnTriggerExit fired on {(IsServerStarted ? "SERVER" : "CLIENT")} - Object: {other.gameObject.name}, Zone: {zoneName}");
        
        if (!IsServerStarted) return;
        
        GameObject obj = other.gameObject;
        
        if (affectedObjects.Remove(obj))
        {
            Debug.Log($"[BaseZone] SERVER - Player {obj.name} EXITED zone {zoneName}. Remaining objects: {affectedObjects.Count}");
            OnObjectExitedZone(obj);
        }
    }
    
    // Called by PlayerZoneTriggerHandler via ServerRpc (for client-predicted players)
    public void OnNetworkTriggerEnter(GameObject obj)
    {
        if (!IsServerStarted) return;
        
        Debug.Log($"[BaseZone] OnNetworkTriggerEnter (ServerRpc) - Object: {obj.name}, Zone: {zoneName}");
        
        if (!CanAffectObject(obj))
        {
            Debug.Log($"[BaseZone] SERVER - Cannot affect object: {obj.name} (no TemporalStability component)");
            return;
        }
        
        if (!affectedObjects.Contains(obj))
        {
            affectedObjects.Add(obj);
            Debug.Log($"[BaseZone] SERVER - Player {obj.name} ENTERED zone {zoneName} via NetworkTrigger. Total objects: {affectedObjects.Count}");
            OnObjectEnteredZone(obj);
        }
    }
    
    // Called by PlayerZoneTriggerHandler via ServerRpc (for client-predicted players)
    public void OnNetworkTriggerExit(GameObject obj)
    {
        if (!IsServerStarted) return;
        
        Debug.Log($"[BaseZone] OnNetworkTriggerExit (ServerRpc) - Object: {obj.name}, Zone: {zoneName}");
        
        if (affectedObjects.Remove(obj))
        {
            Debug.Log($"[BaseZone] SERVER - Player {obj.name} EXITED zone {zoneName} via NetworkTrigger. Remaining objects: {affectedObjects.Count}");
            OnObjectExitedZone(obj);
        }
    }
    
    protected virtual void UpdateAffectedObjects()
    {
        for (int i = affectedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = affectedObjects[i];
            
            if (obj == null)
            {
                affectedObjects.RemoveAt(i);
                continue;
            }
            
            if (obj.scene != gameObject.scene)
            {
                affectedObjects.RemoveAt(i);
                OnObjectExitedZone(obj);
                continue;
            }
            
            float intensity = CalculateIntensityAtPosition(obj.transform.position);
            OnObjectStayInZone(obj, Time.deltaTime, intensity);
        }
    }
    
    protected void CacheZoneEffects()
    {
        zoneEffects.Clear();
        GetComponents(zoneEffects);
    }
    
    protected void InitializeEffects()
    {
        foreach (ZoneEffect effect in zoneEffects)
        {
            if (effect != null)
            {
                effect.Initialize(this);
            }
        }
    }
    
    protected virtual void OnObjectEnteredZone(GameObject obj)
    {
        foreach (ZoneEffect effect in zoneEffects)
        {
            if (effect != null && effect.isEnabled)
            {
                effect.OnPlayerEnter(obj);
            }
        }
    }
    
    protected virtual void OnObjectStayInZone(GameObject obj, float deltaTime, float intensity)
    {
        foreach (ZoneEffect effect in zoneEffects)
        {
            if (effect != null && effect.isEnabled)
            {
                effect.OnPlayerStay(obj, deltaTime, intensity);
            }
        }
    }
    
    protected virtual void OnObjectExitedZone(GameObject obj)
    {
        foreach (ZoneEffect effect in zoneEffects)
        {
            if (effect != null && effect.isEnabled)
            {
                effect.OnPlayerExit(obj);
            }
        }
    }
    
    protected virtual bool CanAffectObject(GameObject obj)
    {
        return obj.GetComponent<TemporalStability>() != null;
    }
    
    public float CalculateIntensityAtPosition(Vector3 position)
    {
        if (!useIntensityGradient)
            return 1f;
        
        Vector3 localPos = transform.InverseTransformPoint(position);
        float normalizedDistance = 0f;
        
        if (activeCollider is SphereCollider sphereCol)
        {
            float distanceFromCenter = Vector3.Distance(transform.position, position);
            normalizedDistance = Mathf.Clamp01(distanceFromCenter / sphereCol.radius);
        }
        else if (activeCollider is BoxCollider boxCol)
        {
            Vector3 halfSize = boxCol.size * 0.5f;
            Vector3 absLocal = new Vector3(Mathf.Abs(localPos.x), Mathf.Abs(localPos.y), Mathf.Abs(localPos.z));
            Vector3 normalizedPos = new Vector3(
                absLocal.x / halfSize.x,
                absLocal.y / halfSize.y,
                absLocal.z / halfSize.z
            );
            normalizedDistance = Mathf.Max(normalizedPos.x, normalizedPos.y, normalizedPos.z);
        }
        else if (activeCollider is CapsuleCollider capCol)
        {
            float distanceFromCenter = Vector3.Distance(transform.position, position);
            normalizedDistance = Mathf.Clamp01(distanceFromCenter / capCol.radius);
        }
        
        return intensityCurve.Evaluate(normalizedDistance);
    }
    
    public abstract string GetZoneTypeDescription();
    
    protected void CacheActiveCollider()
    {
        switch (_colliderType)
        {
            case ZoneColliderType.Sphere:
                activeCollider = GetComponent<SphereCollider>();
                break;
            case ZoneColliderType.Box:
                activeCollider = GetComponent<BoxCollider>();
                break;
            case ZoneColliderType.Capsule:
                activeCollider = GetComponent<CapsuleCollider>();
                break;
        }
        
        if (activeCollider == null)
        {
            Debug.LogWarning($"[BaseZone] '{gameObject.name}' is missing a {_colliderType} collider!", this);
        }
    }
    
    public Collider GetActiveCollider()
    {
        if (activeCollider == null)
            CacheActiveCollider();
        return activeCollider;
    }
    
    protected void OnRadiusChanged(float previousValue, float newValue, bool asServer)
    {
        if (!asServer) // Only update visually on clients
        {
            UpdateColliderSize();
        }
    }
    
    protected void UpdateColliderSize()
    {
        float radius = effectRadius;
        
        if (activeCollider == null)
            CacheActiveCollider();
        
        if (activeCollider is SphereCollider sphereCol)
        {
            sphereCol.radius = radius;
        }
        else if (activeCollider is BoxCollider boxCol)
        {
            boxCol.size = new Vector3(radius * 2f, radius * 2f, radius * 2f);
        }
        else if (activeCollider is CapsuleCollider capCol)
        {
            capCol.radius = radius / 2f;
            capCol.height = radius * 2f;
        }
    }
    
    protected virtual void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheActiveCollider();
            UpdateColliderSize();
            
            if (GetComponent<NetworkObject>() == null)
            {
                Debug.LogWarning($"[BaseZone] '{gameObject.name}' is missing a NetworkObject component!", this);
            }
            
            if (activeCollider == null)
            {
                Debug.LogWarning($"[BaseZone] '{gameObject.name}' is missing a Collider! Expected {_colliderType}.", this);
            }
        }
    }
}
