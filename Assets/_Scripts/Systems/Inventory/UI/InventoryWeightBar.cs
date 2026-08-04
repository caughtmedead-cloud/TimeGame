using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Displays weight usage for a single InventorySystem below its grid.
    /// Layout: [scale icon]  [current / max kg]
    /// Subscribes to InventorySystem.OnWeightChanged so it always stays in sync.
    /// Only visible when the system has a weight limit (UseWeightLimit == true).
    /// </summary>
    public class InventoryWeightBar : MonoBehaviour
    {
        [Header("References (assigned by factory)")]
        [SerializeField] private Image weightIcon;
        [SerializeField] private TextMeshProUGUI weightText;

        private InventorySystem inventorySystem;

        // ── Initialization ────────────────────────────────────────────────

        /// <summary>
        /// Called by InventoryGridFactory immediately after creating the bar.
        /// </summary>
        public void Initialize(InventorySystem system)
        {
            inventorySystem = system;

            if (system == null || !system.UseWeightLimit)
            {
                gameObject.SetActive(false);
                return;
            }

            // Subscribe to weight changes
            system.OnWeightChanged += OnWeightChanged;

            // Draw immediately with the current (probably zero) weight
            Refresh();
        }

        private void OnDestroy()
        {
            if (inventorySystem != null)
                inventorySystem.OnWeightChanged -= OnWeightChanged;
        }

        // ── Event handler ─────────────────────────────────────────────────

        private void OnWeightChanged(float newWeight)
        {
            Refresh();
        }

        // ── Display ───────────────────────────────────────────────────────

        private void Refresh()
        {
            if (inventorySystem == null || weightText == null) return;

            float current = inventorySystem.GetCurrentWeight();
            float max     = inventorySystem.MaxWeight;

            // Format: "4.2 / 10 kg"  (1 decimal place, trailing zeros stripped)
            weightText.text = $"{FormatWeight(current)} / {FormatWeight(max)} kg";

            // Always white — no color tinting
            weightText.color = Color.white;

            if (weightIcon != null)
                weightIcon.color = Color.white;
        }

        private static string FormatWeight(float kg)
        {
            // Show one decimal, but drop trailing ".0" for whole numbers
            return kg % 1f == 0f ? kg.ToString("0") : kg.ToString("0.#");
        }

        // ── Internal refs set by factory ──────────────────────────────────

        public void SetReferences(Image icon, TextMeshProUGUI text)
        {
            weightIcon = icon;
            weightText = text;
        }

        /// <summary>
        /// Resubscribe to a new InventorySystem after SwapInventorySystem is called.
        /// The bar was initialized against the factory-created system; this keeps it in
        /// sync when an equipment grid or floating window swaps in a ContainerInventory.
        /// </summary>
        public void Resubscribe(InventorySystem newSystem)
        {
            // Unsubscribe from old system
            if (inventorySystem != null)
                inventorySystem.OnWeightChanged -= OnWeightChanged;

            inventorySystem = newSystem;

            if (newSystem == null || !newSystem.UseWeightLimit)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            newSystem.OnWeightChanged += OnWeightChanged;
            Refresh();
        }
    }
}
