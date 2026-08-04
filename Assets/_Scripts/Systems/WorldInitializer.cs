using UnityEngine;
using TimeGame.Systems.Inventory;

namespace TimeGame.Systems
{
    /// <summary>
    /// WorldInitializer
    ///
    /// A single MonoBehaviour that owns all world-state setup for singleplayer.
    /// Attach this to a persistent scene object. It fires once on Start and runs
    /// initialization phases in a defined, deterministic order so the world is
    /// fully consistent before the player interacts with it.
    ///
    /// Singleplayer replacement for the former FishNet ServerWorldInitializer.
    ///
    /// PHASE ORDER
    /// ───────────
    ///  1. Loot Containers     — roll loot tables into WorldLootContainers.
    ///  2. Resource Nodes      — seed harvestable nodes (ore, wood, herbs, etc.).  [TODO]
    ///  3. World Events        — select and schedule dynamic encounters/events.     [TODO]
    ///  4. Enemy Spawn Points  — determine initial enemy population per zone.       [TODO]
    ///  5. Persistence Load    — apply any saved world-state from disk/cloud.       [TODO]
    ///
    /// HOW TO GROW THIS CLASS
    /// ──────────────────────
    /// • Add a private method named InitializeXxx() for each new phase.
    /// • Call it from Start() in the correct position relative to other phases.
    /// • Use the established Log() helper and the [Header] grouping convention below.
    /// • Keep each phase self-contained — phases should not depend on the internal
    ///   state of other phases (prefer reading from scene components / registries).
    /// </summary>
    public class WorldInitializer : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private void Start()
        {
            RunInitializationPhases();
        }

        /// <summary>
        /// Runs all initialization phases in order.
        /// </summary>
        private void RunInitializationPhases()
        {
            Log("=== World Initialization BEGIN ===");

            // ── Phase 1: Loot Containers ──────────────────────────────────────
            InitializeLootContainers();

            // ── Phase 2: Resource Nodes ───────────────────────────────────────
            // TODO: InitializeResourceNodes();

            // ── Phase 3: World Events / Encounters ───────────────────────────
            // TODO: InitializeWorldEvents();

            // ── Phase 4: Enemy Spawn Points ───────────────────────────────────
            // TODO: InitializeEnemySpawns();

            // ── Phase 5: Persistence Load ─────────────────────────────────────
            // TODO: InitializePersistence();

            Log("=== World Initialization COMPLETE ===");
        }

        /// <summary>
        /// Roll loot tables into every WorldLootContainer in the scene (including
        /// inactive GameObjects) so containers are pre-populated before the player
        /// finds them. EnsureLootPopulated() is idempotent — safe to call even if a
        /// container was already populated for another reason.
        /// </summary>
        private void InitializeLootContainers()
        {
            WorldLootContainer[] containers = FindObjectsByType<WorldLootContainer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int populated = 0;
            foreach (WorldLootContainer container in containers)
            {
                container.EnsureLootPopulated();
                populated++;
            }

            Log($"[Phase 1] Loot populated for {populated} container(s).");
        }

        private void Log(string msg)
        {
            if (debugMode)
                Debug.Log($"[WorldInitializer] {msg}");
        }
    }
}
