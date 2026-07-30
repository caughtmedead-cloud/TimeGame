using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Full-screen inspect panel — shows a rotating 3D preview of the item
    /// plus all its stats, Tarkov-style.
    ///
    /// Usage:
    ///   ItemInspectPanel.Instance.Show(itemDef, placedItem);
    ///   ItemInspectPanel.Instance.Hide();
    ///
    /// The panel builds its entire UI hierarchy in code on first use and sits
    /// above the inventory canvas (high sort order).  A dedicated render-texture
    /// camera renders the 3D model into a RawImage so it composites cleanly over
    /// the UI without any depth-sorting issues.
    /// </summary>
    public class ItemInspectPanel : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static ItemInspectPanel Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Config (optional)")]
        [Tooltip("ScriptableObject with all panel settings. " +
                 "If left empty the panel auto-loads 'ItemInspectPanelConfig' from any Resources folder. " +
                 "All settings fall back to code defaults when the asset is absent.")]
        [SerializeField] private ItemInspectPanelConfig config;

        // ── Runtime settings (populated from config in Awake, then never change) ──
        // Keeping these as plain private fields means the rest of the class doesn't
        // need to be touched — it still reads e.g. `uiFont`, `closeButtonSprite`, etc.
        private TMP_FontAsset uiFont;
        private Sprite        closeButtonSprite;
        private Sprite        resizeHandleSprite;
        private float         windowWidthFraction  = 0.72f;
        private float         windowHeightFraction = 0.78f;
        private Vector2       minWindowSize        = new Vector2(500f, 380f);
        private Vector2       maxWindowSize        = new Vector2(1600f, 1000f);
        private int           previewResolution    = 512;
        private float         cameraDistance       = 2.5f;
        private float         cameraFOV            = 40f;
        private float         cameraElevation      = 15f;
        private float         dragSensitivity      = 0.4f;
        private float         inertiaDamping       = 8f;
        private float         autoSpinSpeed        = 20f;

        // ── Runtime state ────────────────────────────────────────────────────
        private bool        isVisible        = false;
        private bool        isDraggingModel  = false;
        private Vector2     lastDragPos;
        private float       yawVelocity;   // deg/s spin carried by inertia
        private float       currentYaw;
        private float       currentPitch;
        private const float MaxPitch = 80f; // limit vertical rotation

        // Player control state tracking
        private bool        shouldRestorePlayerControls = false;
        private bool        cursorWasLockedBeforeInspect = false;

        // Resize state — proportional: both axes scale together
        private bool        isResizing       = false;
        private Vector2     resizeStartMouse;
        private Vector2     resizeStartSize;
        private RectTransform windowRT;      // kept as field for resize + drag

        // Window drag state
        private bool        isDraggingWindow = false;
        private Vector2     windowDragOffset;
        private RectTransform topBarRT;      // used for drag hit-test

        // 3-D preview
        private Camera      previewCamera;
        private RenderTexture previewRT;
        private GameObject  modelRoot;     // parent that we rotate
        private GameObject  spawnedModel; // the actual prefab instance

        // UI roots
        private Canvas      panelCanvas;
        private CanvasGroup panelCanvasGroup;
        private RawImage    previewImage;  // shows the render texture
        private GameObject  statsScrollContent; // parent for stat rows
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI descriptionText;
        private TextMeshProUGUI rarityText;

        // Rarity colours matching InventoryItemVisual
        private static readonly Dictionary<ItemRarity, Color> RarityColors = new()
        {
            { ItemRarity.Common,    new Color(0.75f, 0.75f, 0.75f) },
            { ItemRarity.Uncommon,  new Color(0.3f,  0.8f,  0.3f)  },
            { ItemRarity.Rare,      new Color(0.3f,  0.5f,  1f)    },
            { ItemRarity.Epic,      new Color(0.8f,  0.3f,  0.8f)  },
            { ItemRarity.Legendary, new Color(1f,    0.6f,  0f)    },
        };

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadConfig();
            BuildUI();
            SetupPreviewCamera();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Populates runtime settings from an <see cref="ItemInspectPanelConfig"/> asset.
        ///
        /// Resolution order:
        ///   1. Inspector slot on this component (drag-and-drop, fastest iteration)
        ///   2. Resources.Load("ItemInspectPanelConfig")  (zero scene dependency)
        ///   3. Code defaults already set on the private fields (always safe)
        /// </summary>
        private void LoadConfig()
        {
            // Try the inspector slot first; fall back to Resources.
            if (config == null)
                config = Resources.Load<ItemInspectPanelConfig>("ItemInspectPanelConfig");

            if (config == null)
            {
                Debug.Log("[ItemInspectPanel] No config asset found — using code defaults. " +
                          "Create one via Assets ▶ Create ▶ TimeGame ▶ Inventory ▶ Item Inspect Panel Config " +
                          "and save it to Assets/Resources/.");
                return;
            }

            // Apply every field that was set in the asset; skip nulls/zeros so the
            // code defaults stay in effect when the designer leaves a field blank.
            if (config.uiFont              != null)  uiFont               = config.uiFont;
            if (config.closeButtonSprite   != null)  closeButtonSprite    = config.closeButtonSprite;
            if (config.resizeHandleSprite  != null)  resizeHandleSprite   = config.resizeHandleSprite;
            if (config.windowWidthFraction  > 0f)    windowWidthFraction  = config.windowWidthFraction;
            if (config.windowHeightFraction > 0f)    windowHeightFraction = config.windowHeightFraction;
            if (config.minWindowSize.x      > 0f)    minWindowSize        = config.minWindowSize;
            if (config.maxWindowSize.x      > 0f)    maxWindowSize        = config.maxWindowSize;
            if (config.previewResolution    > 0)     previewResolution    = config.previewResolution;
            if (config.cameraDistance       > 0f)    cameraDistance       = config.cameraDistance;
            if (config.cameraFOV            > 0f)    cameraFOV            = config.cameraFOV;
            if (config.cameraElevation      > 0f)    cameraElevation      = config.cameraElevation;
            if (config.dragSensitivity      > 0f)    dragSensitivity      = config.dragSensitivity;
            if (config.inertiaDamping       > 0f)    inertiaDamping       = config.inertiaDamping;
            if (config.autoSpinSpeed        > 0f)    autoSpinSpeed        = config.autoSpinSpeed;
        }

        private void Update()
        {
            if (!isVisible) return;

            // ESC always closes
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            // Tab closes only when opened from world (to prevent interfering with inventory toggle)
            if (shouldRestorePlayerControls && Input.GetKeyDown(KeyCode.Tab))
            {
                Hide();
                return;
            }

            TickModelRotation();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupPreviewRT();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show the panel for an inventory item (optionally with instance data).</summary>
        public void Show(InventoryItemSO itemDef, GridPlacement.PlacedItem placedItem = null)
        {
            Show(itemDef, placedItem, false);
        }

        /// <summary>
        /// Show the panel for an inventory item with optional player control management.
        /// </summary>
        /// <param name="itemDef">The item definition to display</param>
        /// <param name="placedItem">Optional instance data</param>
        /// <param name="disablePlayerControls">If true, disables player controls and tracks cursor state</param>
        public void Show(InventoryItemSO itemDef, GridPlacement.PlacedItem placedItem, bool disablePlayerControls)
        {
            if (itemDef == null) return;

            // Lazy camera init — guard against BuildUI exceptions or hot-reload leaving camera null
            if (previewCamera == null)
                SetupPreviewCamera();

            // Always ensure the RawImage is showing the current RT (handles hot-reload / re-init)
            if (previewImage != null && previewRT != null)
                previewImage.texture = previewRT;

            gameObject.SetActive(true);
            isVisible = true;

            // Track whether we should restore controls on close
            shouldRestorePlayerControls = disablePlayerControls;
            if (disablePlayerControls)
            {
                cursorWasLockedBeforeInspect = Cursor.lockState == CursorLockMode.Locked;
                
                // Actually disable player controls and unlock cursor
                var inventoryController = FindObjectOfType<TimeGame.Inventory.InventoryUIController>();
                if (inventoryController != null && inventoryController.PlayerController != null)
                {
                    inventoryController.PlayerController.enabled = false;
                }
                
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Enable interaction now that the panel is open
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.interactable   = true;

            PopulateStats(itemDef, placedItem);
            SpawnPreviewModel(itemDef);

            // Reset rotation
            currentYaw   = 0f;
            currentPitch = 0f;
            yawVelocity  = 0f;
            if (modelRoot != null)
                modelRoot.transform.localRotation = Quaternion.identity;

            // Fade in
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.DOFade(1f, 0.18f);
        }

        /// <summary>Hide and clean up the panel.</summary>
        public void Hide()
        {
            if (!isVisible) return;

            // Stop blocking input IMMEDIATELY — don't wait for the fade to complete.
            // Without this the full-screen backdrop eats all inventory clicks during the tween.
            isVisible = false;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable   = false;

            // Restore player controls if we disabled them
            if (shouldRestorePlayerControls)
            {
                RestorePlayerControls();
                shouldRestorePlayerControls = false;
            }

            panelCanvasGroup.DOFade(0f, 0.15f).OnComplete(() =>
            {
                DestroyPreviewModel();
                gameObject.SetActive(false);
            });
        }

        public bool IsVisible => isVisible;

        /// <summary>
        /// Restore player controls and cursor state after inspecting from world.
        /// </summary>
        private void RestorePlayerControls()
        {
            // Find the local player's inventory UI controller
            var inventoryController = FindObjectOfType<TimeGame.Inventory.InventoryUIController>();
            if (inventoryController != null && inventoryController.PlayerController != null)
            {
                inventoryController.PlayerController.enabled = true;
            }

            // Restore cursor state
            if (cursorWasLockedBeforeInspect)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Apply a font to all TMP components on the panel.
        /// Called by ItemUsageHandler after spawning, passing the font from InventoryGridFactory.
        /// No-op if a font was already loaded from the config asset (config takes priority).
        /// </summary>
        public void SetFont(TMP_FontAsset font)
        {
            if (font == null) return;
            if (uiFont != null) return; // config / previous assignment takes priority

            uiFont = font;

            // Apply to every TMP component already created (BuildUI has already run by this point)
            foreach (TextMeshProUGUI tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.font = uiFont;
        }

        // ── 3-D preview ───────────────────────────────────────────────────────

        private void SetupPreviewCamera()
        {
            // Spawn a hidden camera that only renders to the RT
            GameObject camObj = new GameObject("InspectPreviewCamera");
            camObj.transform.SetParent(transform, false);

            previewCamera         = camObj.AddComponent<Camera>();
            previewCamera.enabled = false; // we call Render() manually each frame
            previewCamera.clearFlags      = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0f);
            previewCamera.fieldOfView     = cameraFOV;
            previewCamera.nearClipPlane   = 0.05f;
            previewCamera.farClipPlane    = 100f;
            previewCamera.cullingMask     = LayerMask.GetMask("InspectPreview");

            // Move the camera well off-screen so world objects don't interfere
            camObj.transform.position = new Vector3(0f, -1000f, 0f);

            BuildPreviewRT();
        }

        private void BuildPreviewRT()
        {
            CleanupPreviewRT();
            previewRT = new RenderTexture(previewResolution, previewResolution, 16, RenderTextureFormat.ARGB32);
            previewRT.Create();
            previewCamera.targetTexture = previewRT;
            if (previewImage != null)
                previewImage.texture = previewRT;
        }

        private void CleanupPreviewRT()
        {
            if (previewRT != null)
            {
                previewRT.Release();
                Destroy(previewRT);
                previewRT = null;
            }
        }

        private void SpawnPreviewModel(InventoryItemSO itemDef)
        {
            DestroyPreviewModel();

            if (itemDef.WorldItemPrefab == null)
            {
                // No prefab — show a coloured cube as fallback
                SpawnFallbackCube(itemDef);
                return;
            }

            // Spawn preview root parented to camera so it moves with it
            modelRoot = new GameObject("InspectModelRoot");
            modelRoot.transform.SetParent(previewCamera.transform, false);

            // Place model in front of camera
            modelRoot.transform.localPosition = new Vector3(0f, 0f, cameraDistance);

            // Elevate camera angle
            previewCamera.transform.localRotation =
                Quaternion.Euler(-cameraElevation, 0f, 0f);

            // Instantiate the world prefab
            spawnedModel = Instantiate(itemDef.WorldItemPrefab, modelRoot.transform);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;

            // Move all renderers to InspectPreview layer; disable physics/colliders
            int layer = LayerMask.NameToLayer("InspectPreview");
            if (layer < 0) layer = 0; // fallback if layer isn't configured yet

            foreach (Transform t in spawnedModel.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            foreach (Collider c in spawnedModel.GetComponentsInChildren<Collider>(true))
                c.enabled = false;

            foreach (Rigidbody rb in spawnedModel.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;

            // Disable any scripts that might fight us (WorldItem, etc.)
            foreach (MonoBehaviour mb in spawnedModel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is not ItemInspectPanel)
                    mb.enabled = false;
            }

            // Auto-fit: scale model so it fills roughly 60 % of the preview frame
            AutoFitModel(spawnedModel);

            // Render one frame immediately so the RT isn't black on first show
            previewCamera.Render();
        }

        private void SpawnFallbackCube(InventoryItemSO itemDef)
        {
            modelRoot = new GameObject("InspectModelRoot");
            modelRoot.transform.SetParent(previewCamera.transform, false);
            modelRoot.transform.localPosition = new Vector3(0f, 0f, cameraDistance);

            previewCamera.transform.localRotation =
                Quaternion.Euler(-cameraElevation, 0f, 0f);

            spawnedModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawnedModel.transform.SetParent(modelRoot.transform, false);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localScale    = Vector3.one * 0.6f;

            int layer = LayerMask.NameToLayer("InspectPreview");
            if (layer >= 0) spawnedModel.layer = layer;

            // Tint by rarity
            Renderer r = spawnedModel.GetComponent<Renderer>();
            if (r != null && RarityColors.TryGetValue(itemDef.Rarity, out Color col))
                r.material.color = col;

            Destroy(spawnedModel.GetComponent<Collider>());
            previewCamera.Render();
        }

        private void DestroyPreviewModel()
        {
            if (spawnedModel != null) { Destroy(spawnedModel); spawnedModel = null; }
            if (modelRoot    != null) { Destroy(modelRoot);    modelRoot    = null; }
        }

        /// <summary>
        /// Scale the model so its longest axis fits inside a unit sphere,
        /// giving a consistent-looking preview regardless of prefab scale.
        /// </summary>
        private void AutoFitModel(GameObject model)
        {
            // Collect all renderers and compute combined bounds
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim <= 0f) return;

            float targetSize = 0.6f; // world units to fill in front of camera
            float scaleFactor = targetSize / maxDim;

            model.transform.localScale = Vector3.one * scaleFactor;

            // Re-centre so the model pivot is at the camera-facing origin
            Bounds scaledBounds = new Bounds();
            bool first = true;
            foreach (Renderer rend in model.GetComponentsInChildren<Renderer>())
            {
                if (first) { scaledBounds = rend.bounds; first = false; }
                else scaledBounds.Encapsulate(rend.bounds);
            }
            // Offset model so bounds centre is at modelRoot origin
            Vector3 offset = model.transform.position - scaledBounds.center;
            model.transform.position += offset;
        }

        // ── Rotation logic ────────────────────────────────────────────────────

        private void TickModelRotation()
        {
            if (modelRoot == null || previewCamera == null) return;

            if (!isDraggingModel)
            {
                // Bleed off inertia
                yawVelocity = Mathf.Lerp(yawVelocity, 0f, Time.deltaTime * inertiaDamping);

                // Auto-spin when almost stopped
                float effectiveSpin = Mathf.Abs(yawVelocity) < 2f ? autoSpinSpeed : yawVelocity;
                currentYaw += effectiveSpin * Time.deltaTime;
            }

            modelRoot.transform.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

            // Manually render to the RT each frame (camera.enabled = false intentionally)
            previewCamera.Render();
        }

        // Called by the RawImage drag handler (set up in BuildUI)
        private void OnPreviewBeginDrag(PointerEventData eventData)
        {
            isDraggingModel = true;
            lastDragPos     = eventData.position;
            yawVelocity     = 0f;
        }

        private void OnPreviewDrag(PointerEventData eventData)
        {
            if (!isDraggingModel) return;

            // Horizontal movement rotates around Y axis (yaw)
            // Negate to make dragging right rotate the model right (feels natural)
            float deltaX = -(eventData.position.x - lastDragPos.x) * dragSensitivity;
            currentYaw += deltaX;
            yawVelocity = deltaX / Mathf.Max(Time.deltaTime, 0.0001f);

            // Vertical movement rotates around X axis (pitch)
            // Negate so dragging up tilts the model up (feels natural)
            float deltaY = -(eventData.position.y - lastDragPos.y) * dragSensitivity;
            currentPitch += deltaY;
            currentPitch = Mathf.Clamp(currentPitch, -MaxPitch, MaxPitch);

            lastDragPos = eventData.position;
        }

        private void OnPreviewEndDrag(PointerEventData eventData)
        {
            isDraggingModel = false;
        }

        // ── Resize handle logic ───────────────────────────────────────────────

        private void OnResizeBeginDrag(PointerEventData eventData)
        {
            isResizing       = true;
            resizeStartMouse = eventData.position;
            resizeStartSize  = windowRT.sizeDelta;
        }

        private void OnResizeDrag(PointerEventData eventData)
        {
            if (!isResizing) return;

            // Use the diagonal drag distance as a single scalar — both axes grow together
            // so the window scales proportionally rather than stretching.
            Vector2 delta = eventData.position - resizeStartMouse;
            // Positive diagonal (right+down in screen space) = bigger
            float scalar = (delta.x - delta.y) * 0.5f; // average of rightward and downward motion

            Vector2 newSize = resizeStartSize + new Vector2(scalar, scalar * (resizeStartSize.y / Mathf.Max(resizeStartSize.x, 1f)));

            newSize.x = Mathf.Clamp(newSize.x, minWindowSize.x, maxWindowSize.x);
            newSize.y = Mathf.Clamp(newSize.y, minWindowSize.y, maxWindowSize.y);

            windowRT.sizeDelta = newSize;
        }

        private void OnResizeEndDrag(PointerEventData eventData)
        {
            isResizing = false;
        }

        // ── Window drag logic ─────────────────────────────────────────────────

        private void OnWindowDragBegin(PointerEventData eventData)
        {
            // Only start drag if the pointer is actually over the top bar
            if (topBarRT != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(topBarRT, eventData.position, eventData.pressEventCamera))
                return;

            isDraggingWindow = true;

            // Convert screen point to canvas local space to get a stable offset
            RectTransform canvasRT = panelCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, eventData.position, eventData.pressEventCamera, out Vector2 localMouse);
            windowDragOffset = windowRT.anchoredPosition - localMouse;
        }

        private void OnWindowDrag(PointerEventData eventData)
        {
            if (!isDraggingWindow) return;

            RectTransform canvasRT = panelCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, eventData.position, eventData.pressEventCamera, out Vector2 localMouse);
            windowRT.anchoredPosition = localMouse + windowDragOffset;
        }

        private void OnWindowDragEnd(PointerEventData eventData)
        {
            isDraggingWindow = false;
        }

        // ── Font scaling ──────────────────────────────────────────────────────

        // ── Stats population ──────────────────────────────────────────────────

        private void PopulateStats(InventoryItemSO itemDef, GridPlacement.PlacedItem placedItem)
        {
            // Title
            titleText.text = itemDef.ItemName;

            // Rarity label + colour
            rarityText.text = itemDef.Rarity.ToString().ToUpper();
            rarityText.color = RarityColors.TryGetValue(itemDef.Rarity, out Color rc) ? rc : Color.white;

            // Description
            descriptionText.text = string.IsNullOrEmpty(itemDef.Description)
                ? "No description available."
                : itemDef.Description;

            // Clear old stat rows
            foreach (Transform child in statsScrollContent.transform)
                Destroy(child.gameObject);

            // ── Core stats ──────────────────────────────────────────────────
            AddDivider("PROPERTIES");
            AddStatRow("Weight",    $"{itemDef.Weight} kg");
            AddStatRow("Size",      $"{itemDef.Width}×{itemDef.Height}");
            AddStatRow("Category",  itemDef.Category.ToString());
            AddStatRow("Value",     $"{itemDef.Value}");

            if (itemDef.EquipmentType != ItemType.None)
                AddStatRow("Slot", itemDef.EquipmentType.ToString());

            // ── Stack info ──────────────────────────────────────────────────
            if (itemDef.IsStackable)
            {
                AddDivider("STACK");
                int currentStack = placedItem?.StackCount ?? 1;
                AddStatRow("In Stack",  currentStack.ToString());
                AddStatRow("Max Stack", itemDef.MaxStackSize.ToString());
            }

            // ── Instance / durability ───────────────────────────────────────
            if (placedItem != null && itemDef.HasLimitedUses)
            {
                ItemInstance useInstance = null;

                // Check for fully-tracked items
                if (placedItem.IsFullyTracked &&
                    placedItem.ItemInstances != null && placedItem.ItemInstances.Count > 0)
                {
                    useInstance = placedItem.ItemInstances[0];
                }
                // Check for non-tracked items with uses representative
                else if (placedItem.HasUsesRepresentative &&
                         placedItem.ItemInstances != null && placedItem.ItemInstances.Count > 0)
                {
                    useInstance = placedItem.ItemInstances[0];
                }

                if (useInstance != null)
                {
                    AddDivider("CONDITION");
                    AddStatRow("Uses Remaining", $"{useInstance.UsesRemaining} / {itemDef.MaxUses}");

                    // Durability as a bar + number (only for tracked items)
                    if (placedItem.IsFullyTracked)
                    {
                        AddDurabilityRow(useInstance.Durability);
                    }
                }
            }
            // ── Durability-only (no uses) ────────────────────────────────────
            else if (placedItem != null && placedItem.IsFullyTracked &&
                placedItem.ItemInstances != null && placedItem.ItemInstances.Count > 0)
            {
                ItemInstance inst = placedItem.ItemInstances[0];
                AddDivider("CONDITION");
                AddDurabilityRow(inst.Durability);
            }

            // ── Storage ─────────────────────────────────────────────────────
            if (itemDef.ProvidesStorage)
            {
                AddDivider("STORAGE");
                AddStatRow("Grid Size",   $"{itemDef.StorageGridSize.x}×{itemDef.StorageGridSize.y}");
                AddStatRow("Capacity",    $"{itemDef.StorageMaxWeight} kg");

                if (placedItem?.ContainerInventory != null)
                    AddStatRow("Current Load",
                        $"{placedItem.ContainerInventory.GetCurrentWeight():0.#} kg");
            }
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private void AddDivider(string label)
        {
            GameObject row = new GameObject("Divider_" + label);
            row.transform.SetParent(statsScrollContent.transform, false);

            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            le.flexibleWidth   = 1f;

            // Faint separator line
            Image bg = row.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);

            TextMeshProUGUI txt = CreateChildTMP(row, "DividerText", 18f);
            txt.text      = label;
            txt.fontStyle = FontStyles.Bold;
            txt.color     = new Color(0.65f, 0.65f, 0.65f);
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            StretchRectTransform(txt.rectTransform, new Vector2(8, 0), Vector2.zero);
        }

        private void AddStatRow(string label, string value)
        {
            GameObject row = new GameObject("Stat_" + label);
            row.transform.SetParent(statsScrollContent.transform, false);

            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;
            le.flexibleWidth   = 1f;

            // Label (left)
            TextMeshProUGUI labelTMP = CreateChildTMP(row, "Label", 19f);
            labelTMP.text      = label;
            labelTMP.color     = new Color(0.65f, 0.65f, 0.65f);
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform lRT  = labelTMP.rectTransform;
            lRT.anchorMin      = new Vector2(0f, 0f);
            lRT.anchorMax      = new Vector2(0.55f, 1f);
            lRT.offsetMin      = new Vector2(8f, 0f);
            lRT.offsetMax      = Vector2.zero;

            // Value (right)
            TextMeshProUGUI valueTMP = CreateChildTMP(row, "Value", 19f);
            valueTMP.text      = value;
            valueTMP.color     = Color.white;
            valueTMP.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform vRT  = valueTMP.rectTransform;
            vRT.anchorMin      = new Vector2(0.55f, 0f);
            vRT.anchorMax      = new Vector2(1f, 1f);
            vRT.offsetMin      = Vector2.zero;
            vRT.offsetMax      = new Vector2(-8f, 0f);
        }

        private void AddDurabilityRow(float durability)
        {
            // Clamp 0-100
            float pct = Mathf.Clamp01(durability / 100f);

            GameObject row = new GameObject("Stat_Durability");
            row.transform.SetParent(statsScrollContent.transform, false);

            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            le.flexibleWidth   = 1f;

            // Label
            TextMeshProUGUI labelTMP = CreateChildTMP(row, "Label", 19f);
            labelTMP.text      = "Durability";
            labelTMP.color     = new Color(0.65f, 0.65f, 0.65f);
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform lRT  = labelTMP.rectTransform;
            lRT.anchorMin      = new Vector2(0f, 0f);
            lRT.anchorMax      = new Vector2(0.45f, 1f);
            lRT.offsetMin      = new Vector2(8f, 0f);
            lRT.offsetMax      = Vector2.zero;

            // Bar background
            GameObject barBg = new GameObject("DurabilityBarBg");
            barBg.transform.SetParent(row.transform, false);
            Image bgImg    = barBg.AddComponent<Image>();
            bgImg.color    = new Color(0.15f, 0.15f, 0.15f);
            RectTransform bgRT = barBg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.45f, 0.2f);
            bgRT.anchorMax = new Vector2(0.88f, 0.8f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Bar fill
            GameObject barFill = new GameObject("DurabilityBarFill");
            barFill.transform.SetParent(barBg.transform, false);
            Image fillImg    = barFill.AddComponent<Image>();
            // Colour: green→yellow→red based on durability
            fillImg.color    = Color.Lerp(
                pct > 0.5f ? Color.Lerp(new Color(1f, 0.8f, 0f), new Color(0.3f, 0.9f, 0.3f), (pct - 0.5f) * 2f)
                           : Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.8f, 0f), pct * 2f),
                Color.white, 0f);
            RectTransform fillRT = barFill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(pct, 1f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            // Numeric value
            TextMeshProUGUI valueTMP = CreateChildTMP(row, "Value", 19f);
            valueTMP.text      = $"{durability:0}%";
            valueTMP.color     = Color.white;
            valueTMP.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform vRT  = valueTMP.rectTransform;
            vRT.anchorMin      = new Vector2(0.88f, 0f);
            vRT.anchorMax      = new Vector2(1f, 1f);
            vRT.offsetMin      = Vector2.zero;
            vRT.offsetMax      = new Vector2(-8f, 0f);
        }

        /// <summary>
        /// Creates a child TMP component, applies the shared font, and registers it for live font scaling.
        /// Pass baseSize = the desired font size at the reference window width (900px canvas units).
        /// All dynamic text in this panel goes through this helper.
        /// </summary>
        private TextMeshProUGUI CreateChildTMP(GameObject parent, string objName, float fontSize = 13f)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(parent.transform, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget      = false;
            tmp.enableWordWrapping = false;
            tmp.fontSize           = fontSize;

            if (uiFont != null)
                tmp.font = uiFont;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return tmp;
        }

        private void StretchRectTransform(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // ── UI construction ───────────────────────────────────────────────────

        /// <summary>
        /// Builds the entire panel hierarchy in code — no prefab required.
        ///
        /// Layout (top to bottom, inside a centred window):
        ///   ┌─────────────────────────────────┐
        ///   │  [Title]              [Rarity][X]│  ← top bar
        ///   ├──────────────┬──────────────────┤
        ///   │              │  Description      │
        ///   │  3-D Preview │  ─────────────── │  ← middle
        ///   │   (RawImage) │  PROPERTIES       │
        ///   │              │  Weight  1 kg     │
        ///   │              │  ...              │
        ///   └──────────────┴──────────────────┘
        ///                                    [⇲]  ← resize handle
        /// </summary>
        private void BuildUI()
        {
            // ── Root canvas ──────────────────────────────────────────────────
            panelCanvas                  = gameObject.AddComponent<Canvas>();
            panelCanvas.renderMode       = RenderMode.ScreenSpaceOverlay;
            panelCanvas.sortingOrder     = 200; // above inventory (typically 100)

            gameObject.AddComponent<GraphicRaycaster>();

            // CanvasScaler ensures font sizes / layout are correct regardless of screen resolution
            CanvasScaler scaler             = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode              = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution      = new Vector2(1920f, 1080f);
            scaler.screenMatchMode          = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight       = 0.5f; // blend between width and height matching

            panelCanvasGroup              = gameObject.AddComponent<CanvasGroup>();
            panelCanvasGroup.alpha        = 0f;
            panelCanvasGroup.blocksRaycasts = false;  // don't eat clicks while invisible
            panelCanvasGroup.interactable   = false;

            // ── Dim backdrop ─────────────────────────────────────────────────
            GameObject backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(transform, false);
            Image backdropImg  = backdrop.AddComponent<Image>();
            backdropImg.color  = new Color(0f, 0f, 0f, 0.72f);
            backdropImg.raycastTarget = true; // block clicks to inventory behind
            StretchRectTransform(backdropImg.rectTransform, Vector2.zero, Vector2.zero);

            // Clicking the backdrop closes the panel
            EventTrigger backdropTrigger = backdrop.AddComponent<EventTrigger>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(_ => Hide());
            backdropTrigger.triggers.Add(pointerDown);

            // ── Window ───────────────────────────────────────────────────────
            GameObject window = new GameObject("Window");
            window.transform.SetParent(transform, false);

            windowRT = window.AddComponent<RectTransform>();
            windowRT.anchorMin       = new Vector2(0.5f, 0.5f);
            windowRT.anchorMax       = new Vector2(0.5f, 0.5f);
            windowRT.pivot           = new Vector2(0.5f, 0.5f);
            windowRT.anchoredPosition = Vector2.zero;

            // Screen-percentage initial size, clamped to min/max
            float initW = Mathf.Clamp(Screen.width  * windowWidthFraction,  minWindowSize.x, maxWindowSize.x);
            float initH = Mathf.Clamp(Screen.height * windowHeightFraction, minWindowSize.y, maxWindowSize.y);
            windowRT.sizeDelta = new Vector2(initW, initH);

            Image windowBg = window.AddComponent<Image>();
            windowBg.color = new Color(0.09f, 0.09f, 0.09f, 0.97f);
            windowBg.raycastTarget = true;

            // ── Top bar (draggable) ───────────────────────────────────────────
            const float topBarH = 48f;
            GameObject topBar = new GameObject("TopBar");
            topBar.transform.SetParent(window.transform, false);

            topBarRT = topBar.AddComponent<RectTransform>();
            topBarRT.anchorMin = new Vector2(0f, 1f);
            topBarRT.anchorMax = new Vector2(1f, 1f);
            topBarRT.pivot     = new Vector2(0f, 1f);
            topBarRT.sizeDelta = new Vector2(0f, topBarH);
            topBarRT.anchoredPosition = Vector2.zero;

            Image topBg  = topBar.AddComponent<Image>();
            topBg.color  = new Color(0.06f, 0.06f, 0.06f, 1f);
            topBg.raycastTarget = true; // must receive events for drag

            // Wire window drag through EventTrigger on the top bar background
            EventTrigger topDragET = topBar.AddComponent<EventTrigger>();
            var topBeginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            topBeginDrag.callback.AddListener(e => OnWindowDragBegin((PointerEventData)e));
            topDragET.triggers.Add(topBeginDrag);
            var topDrag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            topDrag.callback.AddListener(e => OnWindowDrag((PointerEventData)e));
            topDragET.triggers.Add(topDrag);
            var topEndDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            topEndDrag.callback.AddListener(e => OnWindowDragEnd((PointerEventData)e));
            topDragET.triggers.Add(topEndDrag);

            // Title text — fixed size 20pt
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(topBar.transform, false);
            titleText           = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.fontStyle = FontStyles.Bold;
            titleText.fontSize  = 20f;
            titleText.color     = Color.white;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.raycastTarget = false;
            if (uiFont != null) titleText.font = uiFont;
            RectTransform titleRT = titleText.rectTransform;
            titleRT.anchorMin = new Vector2(0f, 0f);
            titleRT.anchorMax = new Vector2(0.65f, 1f);
            titleRT.offsetMin = new Vector2(14f, 0f);
            titleRT.offsetMax = Vector2.zero;

            // Rarity badge — fixed size 13pt
            GameObject rarityGO = new GameObject("Rarity");
            rarityGO.transform.SetParent(topBar.transform, false);
            rarityText           = rarityGO.AddComponent<TextMeshProUGUI>();
            rarityText.fontStyle = FontStyles.Bold;
            rarityText.fontSize  = 13f;
            rarityText.alignment = TextAlignmentOptions.MidlineRight;
            rarityText.raycastTarget = false;
            if (uiFont != null) rarityText.font = uiFont;
            RectTransform rarityRT = rarityText.rectTransform;
            rarityRT.anchorMin = new Vector2(0.65f, 0f);
            rarityRT.anchorMax = new Vector2(0.88f, 1f);
            rarityRT.offsetMin = Vector2.zero;
            rarityRT.offsetMax = Vector2.zero;

            // Close button — square, right-anchored
            const float closeBtnW = 36f;
            GameObject closeGO  = new GameObject("CloseButton");
            closeGO.transform.SetParent(topBar.transform, false);
            Button closeBtn     = closeGO.AddComponent<Button>();
            Image closeBg       = closeGO.AddComponent<Image>();
            RectTransform closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin   = new Vector2(1f, 0f);
            closeRT.anchorMax   = new Vector2(1f, 1f);
            closeRT.pivot       = new Vector2(1f, 0.5f);
            closeRT.sizeDelta   = new Vector2(closeBtnW, 0f);
            closeRT.anchoredPosition = Vector2.zero;

            if (closeButtonSprite != null)
            {
                // Custom icon — display sprite, no tint background
                closeBg.sprite = closeButtonSprite;
                closeBg.color  = Color.white;
                closeBg.preserveAspect = true;
            }
            else
            {
                // Fallback: dark red button with ✕ text (#401717)
                closeBg.color = new Color(0x40 / 255f, 0x17 / 255f, 0x17 / 255f, 1f);
                TextMeshProUGUI closeTxt = CreateChildTMP(closeGO, "X", 16f);
                closeTxt.text      = "✕";
                closeTxt.color     = Color.white;
                closeTxt.alignment = TextAlignmentOptions.Center;
            }

            closeBtn.onClick.AddListener(Hide);
            ColorBlock cb      = closeBtn.colors;
            cb.normalColor     = Color.white;
            cb.highlightedColor = new Color(1.2f, 0.5f, 0.5f);
            cb.pressedColor    = new Color(0.6f, 0.1f, 0.1f);
            closeBtn.colors    = cb;
            closeBtn.targetGraphic = closeBg;

            // ── Content area (below top bar) ─────────────────────────────────
            GameObject content = new GameObject("Content");
            content.transform.SetParent(window.transform, false);

            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin     = new Vector2(0f, 0f);
            contentRT.anchorMax     = new Vector2(1f, 1f);
            contentRT.offsetMin     = new Vector2(0f, 0f);
            contentRT.offsetMax     = new Vector2(0f, -topBarH); // leave room for top bar

            // HorizontalLayoutGroup splits into left (preview) and right (stats)
            HorizontalLayoutGroup hLayout   = content.AddComponent<HorizontalLayoutGroup>();
            hLayout.childControlWidth       = true;
            hLayout.childControlHeight      = true;
            hLayout.childForceExpandWidth   = true;
            hLayout.childForceExpandHeight  = true;
            hLayout.spacing                 = 0f;

            // ── Left: 3-D preview panel ──────────────────────────────────────
            GameObject leftPanel = new GameObject("PreviewPanel");
            leftPanel.transform.SetParent(content.transform, false);

            LayoutElement leftLE    = leftPanel.AddComponent<LayoutElement>();
            leftLE.flexibleWidth    = 1f;  // 50 % of width
            leftLE.flexibleHeight   = 1f;

            Image leftBg = leftPanel.AddComponent<Image>();
            leftBg.color = new Color(0.05f, 0.05f, 0.05f);

            // RawImage to display the render texture
            GameObject rawImgGO = new GameObject("PreviewRawImage");
            rawImgGO.transform.SetParent(leftPanel.transform, false);

            previewImage         = rawImgGO.AddComponent<RawImage>();
            previewImage.texture = previewRT;
            previewImage.color   = Color.white;
            previewImage.raycastTarget = true;

            RectTransform rawRT  = previewImage.rectTransform;
            rawRT.anchorMin      = new Vector2(0.05f, 0.05f);
            rawRT.anchorMax      = new Vector2(0.95f, 0.95f);
            rawRT.offsetMin      = Vector2.zero;
            rawRT.offsetMax      = Vector2.zero;

            // Drag events on the RawImage to rotate the model
            EventTrigger et = rawImgGO.AddComponent<EventTrigger>();

            var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginDrag.callback.AddListener(e => OnPreviewBeginDrag((PointerEventData)e));
            et.triggers.Add(beginDrag);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(e => OnPreviewDrag((PointerEventData)e));
            et.triggers.Add(drag);

            var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endDrag.callback.AddListener(e => OnPreviewEndDrag((PointerEventData)e));
            et.triggers.Add(endDrag);

            // "Drag to rotate" hint label
            GameObject hintGO = new GameObject("DragHint");
            hintGO.transform.SetParent(leftPanel.transform, false);
            TextMeshProUGUI hint = hintGO.AddComponent<TextMeshProUGUI>();
            hint.text      = "drag to rotate";
            hint.fontSize  = 11f;
            hint.color     = new Color(1f, 1f, 1f, 0.28f);
            hint.alignment = TextAlignmentOptions.BottomRight;
            hint.raycastTarget = false;
            if (uiFont != null) hint.font = uiFont;
            RectTransform hintRT = hint.rectTransform;
            hintRT.anchorMin = new Vector2(0f, 0f);
            hintRT.anchorMax = new Vector2(1f, 0f);
            hintRT.pivot     = new Vector2(1f, 0f);
            hintRT.sizeDelta = new Vector2(0f, 22f);
            hintRT.anchoredPosition = new Vector2(-8f, 6f);

            // ── Right: stats panel ────────────────────────────────────────────
            GameObject rightPanel = new GameObject("StatsPanel");
            rightPanel.transform.SetParent(content.transform, false);

            LayoutElement rightLE   = rightPanel.AddComponent<LayoutElement>();
            rightLE.flexibleWidth   = 1f;
            rightLE.flexibleHeight  = 1f;

            Image rightBg = rightPanel.AddComponent<Image>();
            rightBg.color = new Color(0.08f, 0.08f, 0.08f);

            // Description (fixed height at top)
            GameObject descGO   = new GameObject("Description");
            descGO.transform.SetParent(rightPanel.transform, false);
            descriptionText     = descGO.AddComponent<TextMeshProUGUI>();
            descriptionText.fontSize       = 18f;
            descriptionText.color          = new Color(0.75f, 0.75f, 0.75f);
            descriptionText.alignment      = TextAlignmentOptions.TopLeft;
            descriptionText.enableWordWrapping = true;
            descriptionText.raycastTarget  = false;
            if (uiFont != null) descriptionText.font = uiFont;
            RectTransform descRT           = descriptionText.rectTransform;
            descRT.anchorMin               = new Vector2(0f, 1f);
            descRT.anchorMax               = new Vector2(1f, 1f);
            descRT.pivot                   = new Vector2(0f, 1f);
            descRT.sizeDelta               = new Vector2(0f, 80f);
            descRT.anchoredPosition        = new Vector2(0f, 0f);
            descRT.offsetMin               = new Vector2(12f, 0f);
            descRT.offsetMax               = new Vector2(-12f, 0f);

            // Thin separator line
            GameObject sepGO = new GameObject("Separator");
            sepGO.transform.SetParent(rightPanel.transform, false);
            Image sepImg   = sepGO.AddComponent<Image>();
            sepImg.color   = new Color(1f, 1f, 1f, 0.08f);
            RectTransform sepRT = sepGO.GetComponent<RectTransform>();
            sepRT.anchorMin = new Vector2(0f, 1f);
            sepRT.anchorMax = new Vector2(1f, 1f);
            sepRT.pivot     = new Vector2(0f, 1f);
            sepRT.sizeDelta = new Vector2(0f, 1f);
            sepRT.anchoredPosition = new Vector2(0f, -80f);

            // Scroll view for stat rows
            GameObject scrollGO  = new GameObject("StatsScrollView");
            scrollGO.transform.SetParent(rightPanel.transform, false);

            ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
            RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin     = new Vector2(0f, 0f);
            scrollRT.anchorMax     = new Vector2(1f, 1f);
            scrollRT.offsetMin     = new Vector2(0f, 0f);
            scrollRT.offsetMax     = new Vector2(0f, -82f); // below description + separator

            Image scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = Color.clear;

            // Viewport
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform vpRT    = viewportGO.AddComponent<RectTransform>();
            vpRT.anchorMin        = Vector2.zero;
            vpRT.anchorMax        = Vector2.one;
            vpRT.offsetMin        = Vector2.zero;
            vpRT.offsetMax        = Vector2.zero;
            viewportGO.AddComponent<RectMask2D>();
            scrollRect.viewport   = vpRT;

            // Content with vertical layout
            GameObject scrollContent = new GameObject("Content");
            scrollContent.transform.SetParent(viewportGO.transform, false);
            statsScrollContent    = scrollContent;

            RectTransform scRT    = scrollContent.AddComponent<RectTransform>();
            scRT.anchorMin        = new Vector2(0f, 1f);
            scRT.anchorMax        = new Vector2(1f, 1f);
            scRT.pivot            = new Vector2(0f, 1f);
            scRT.offsetMin        = Vector2.zero;
            scRT.offsetMax        = Vector2.zero;

            VerticalLayoutGroup vlg       = scrollContent.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth         = true;
            vlg.childControlHeight        = true;
            vlg.childForceExpandWidth     = true;
            vlg.childForceExpandHeight    = false;
            vlg.spacing                   = 4f;
            vlg.padding                   = new RectOffset(0, 0, 8, 8);

            ContentSizeFitter csf         = scrollContent.AddComponent<ContentSizeFitter>();
            csf.verticalFit               = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content            = scRT;
            scrollRect.horizontal         = false;
            scrollRect.vertical           = true;
            scrollRect.scrollSensitivity  = 20f;
            scrollRect.movementType       = ScrollRect.MovementType.Clamped;

            // ── Resize handle (bottom-right corner of window) ─────────────────
            AddResizeHandle(window);
        }

        /// <summary>
        /// Adds a small triangular drag handle to the bottom-right corner of the window.
        /// Dragging it resizes the window between minWindowSize and maxWindowSize.
        /// </summary>
        private void AddResizeHandle(GameObject window)
        {
            // 28px is large enough to grab comfortably
            const float handleSize = 28f;

            GameObject handle = new GameObject("ResizeHandle");
            handle.transform.SetParent(window.transform, false);

            // Position: bottom-right corner, on top of everything else
            RectTransform handleRT  = handle.AddComponent<RectTransform>();
            handleRT.anchorMin      = new Vector2(1f, 0f);
            handleRT.anchorMax      = new Vector2(1f, 0f);
            handleRT.pivot          = new Vector2(1f, 0f);
            handleRT.sizeDelta      = new Vector2(handleSize, handleSize);
            handleRT.anchoredPosition = Vector2.zero;

            // Background Image on the handle root — this is the ONLY Graphic on this object.
            // TextMeshProUGUI must go on a CHILD because Unity only allows one Graphic per GO.
            Image handleImg         = handle.AddComponent<Image>();
            handleImg.raycastTarget = true; // this is what receives drag events

            // ⇲ icon / custom sprite on a child
            GameObject iconGO = new GameObject("ResizeIcon");
            iconGO.transform.SetParent(handle.transform, false);
            RectTransform iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = new Vector2(-2f, -2f);

            if (resizeHandleSprite != null)
            {
                Image iconImg         = iconGO.AddComponent<Image>();
                iconImg.sprite        = resizeHandleSprite;
                iconImg.color         = new Color(1f, 1f, 1f, 0.7f);
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                handleImg.color       = Color.clear; // background transparent when using sprite
            }
            else
            {
                TextMeshProUGUI resizeIcon  = iconGO.AddComponent<TextMeshProUGUI>();
                resizeIcon.text             = "⇲";
                resizeIcon.fontSize         = 14f;
                resizeIcon.color            = new Color(1f, 1f, 1f, 0.6f);
                resizeIcon.alignment        = TextAlignmentOptions.BottomRight;
                resizeIcon.raycastTarget    = false;
                if (uiFont != null) resizeIcon.font = uiFont;
                handleImg.color             = new Color(1f, 1f, 1f, 0.18f);
            }

            // EventTrigger on the handle root (which has the raycasting Image)
            EventTrigger et = handle.AddComponent<EventTrigger>();

            var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginDrag.callback.AddListener(e => OnResizeBeginDrag((PointerEventData)e));
            et.triggers.Add(beginDrag);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(e => OnResizeDrag((PointerEventData)e));
            et.triggers.Add(drag);

            var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endDrag.callback.AddListener(e => OnResizeEndDrag((PointerEventData)e));
            et.triggers.Add(endDrag);

            // Hover tint — brightens on hover to signal interactivity.
            // Only applies to the fallback text handle; when using a custom sprite the
            // handleImg is fully transparent and should not be tinted.
            if (resizeHandleSprite == null)
            {
                EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => handleImg.color = new Color(1f, 1f, 1f, 0.45f));
                et.triggers.Add(enter);

                EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => handleImg.color = new Color(1f, 1f, 1f, 0.18f));
                et.triggers.Add(exit);
            }
        }
    }
}
