using UnityEngine;
using System.Collections.Generic;

public abstract class BaseZone : MonoBehaviour
{
    [Header("Zone Identity")]
    public string zoneName = "Zone";
    
    [Header("Filtering")]
    [Tooltip("Only GameObjects with this tag can be affected by this zone. Leave empty to affect anything that enters the trigger.")]
    public string requiredTag = "Player";
    
    [Header("Zone Shape")]
    [SerializeField] protected ZoneColliderType _colliderType = ZoneColliderType.Sphere;
    [SerializeField] protected float _editorEffectRadius = 10f;
    
    protected float _effectRadius = 10f;
    
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
        get => Application.isPlaying ? _effectRadius : _editorEffectRadius;
        set
        {
            if (Application.isPlaying)
            {
                _effectRadius = value;
                UpdateColliderSize();
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
    }
    
    protected virtual void OnEnable()
    {
        _effectRadius = _editorEffectRadius;
        UpdateColliderSize();
    }
    
    protected virtual void Start()
    {
        InitializeEffects();
    }
    
    protected virtual void Update()
    {
        UpdateAffectedObjects();
    }
    
    protected virtual void OnTriggerEnter(Collider other)
    {
        NotifyObjectEntered(other.gameObject);
    }
    
    protected virtual void OnTriggerExit(Collider other)
    {
        NotifyObjectExited(other.gameObject);
    }
    
    // Public entry point for external trigger detectors (e.g. a child TriggerDetector
    // collider on the player) that forward enter/exit events directly to this zone.
    public void NotifyObjectEntered(GameObject obj)
    {
        if (!CanAffectObject(obj))
        {
            return;
        }
        
        if (!affectedObjects.Contains(obj))
        {
            affectedObjects.Add(obj);
            Debug.Log($"[BaseZone] {obj.name} ENTERED zone {zoneName}. Total objects in zone: {affectedObjects.Count}");
            OnObjectEnteredZone(obj);
        }
    }
    
    public void NotifyObjectExited(GameObject obj)
    {
        if (affectedObjects.Remove(obj))
        {
            Debug.Log($"[BaseZone] {obj.name} EXITED zone {zoneName}. Remaining objects: {affectedObjects.Count}");
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
    
    // Default: affect any object tagged with requiredTag (or anything at all if
    // requiredTag is left empty). Override in a subclass for more advanced filtering
    // (e.g. by component or layer).
    protected virtual bool CanAffectObject(GameObject obj)
    {
        return string.IsNullOrEmpty(requiredTag) || obj.CompareTag(requiredTag);
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
            
            if (activeCollider == null)
            {
                Debug.LogWarning($"[BaseZone] '{gameObject.name}' is missing a Collider! Expected {_colliderType}.", this);
            }
        }
    }
}
