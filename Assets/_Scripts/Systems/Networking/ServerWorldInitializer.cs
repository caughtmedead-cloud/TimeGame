using System.Collections;
using FishNet.Object;
using UnityEngine;
using TimeGame.Systems.Inventory;

namespace TimeGame.Systems.Networking
{
    /// <summary>
    /// ServerWorldInitializer
    ///
    /// A single NetworkBehaviour that owns all server-side world-state setup.
    /// Attach this to a scene object alongside the NetworkManager (or any persistent
    /// server-only GO).  It fires once in OnStartServer and runs initialization
    /// phases in a defined, deterministic order so the world is fully consistent
    /// before the first client interaction arrives.
    ///
    /// PHASE ORDER
    /// ───────────
    ///  1. Loot Containers     — roll loot tables into WorldLootContainers.
    ///                           All NetworkedWorldLootContainers then read already-
    ///                           populated data rather than re-rolling independently.
    ///  2. Resource Nodes      — seed harvestable nodes (ore, wood, herbs, etc.).  [TODO]
    ///  3. World Events        — select and schedule dynamic encounters/events.     [TODO]
    ///  4. Enemy Spawn Points  — determine initial enemy population per zone.       [TODO]
    ///  5. Persistence Load    — apply any saved world-state from disk/cloud.       [TODO]
    ///
    /// HOW TO GROW THIS CLASS
    /// ──────────────────────
    /// • Add a private [Server] method named InitializeXxx() for each new phase.
    /// • Call it from OnStartServer() in the correct position relative to other phases.
    /// • Use the established Log() helper and the [Header] grouping convention below.
    /// • Keep each phase self-contained — phases should not depend on the internal
    ///   state of other phases (prefer reading from scene components / registries).
    /// </summary>
    [RequireComponent(typeof(FishNet.Object.NetworkObject))]
    public class ServerWorldInitializer : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // ─────────────────────────────────────────────────────────────────────
        //  FishNet lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();
            StartCoroutine(RunInitializationPhases());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Initialization pipeline
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs all initialization phases in order.
        /// Wrapped in a coroutine so future phases can yield (e.g. waiting for an
        /// async persistence load or a scene-load confirmation from TimelineSceneSetup).
        /// </summary>
        private IEnumerator RunInitializationPhases()
        {
            Log("=== Server World Initialization BEGIN ===");

            // ── Phase 1: Loot Containers ──────────────────────────────────────
            InitializeLootContainers();
            yield return null;   // one-frame gap keeps the first frame snappy

            // ── Phase 2: Resource Nodes ───────────────────────────────────────
            // TODO: InitializeResourceNodes();
            // yield return null;

            // ── Phase 3: World Events / Encounters ───────────────────────────
            // TODO: InitializeWorldEvents();
            // yield return null;

            // ── Phase 4: Enemy Spawn Points ───────────────────────────────────
            // TODO: InitializeEnemySpawns();
            // yield return null;

            // ── Phase 5: Persistence Load ─────────────────────────────────────
            // TODO: yield return InitializePersistence();   // may be a coroutine itself

            Log("=== Server World Initialization COMPLETE ===");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 1 — Loot Containers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Roll loot tables into every WorldLootContainer in the scene (including
        /// inactive GameObjects) so that when NetworkedWorldLootContainer.ServerBuildCompartments()
        /// later calls WorldLootContainer.GetLootContainer(), loot is already populated
        /// and GetLootContainer() becomes a lightweight no-op.
        ///
        /// EnsureLootPopulated() is idempotent — safe to call even if a container was
        /// already populated for another reason (e.g. solo-mode test opens).
        /// </summary>
        [Server]
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

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 2 — Resource Nodes                                     [TODO]
        // ─────────────────────────────────────────────────────────────────────

        // /// <summary>
        // /// Seed harvestable resource nodes (ore veins, lumber, herb spawns, etc.).
        // /// Called after loot containers so any resource rewards reference already-
        // /// populated loot pools.
        // /// </summary>
        // [Server]
        // private void InitializeResourceNodes()
        // {
        //     // TODO: find all ResourceNode components and call Seed() on them.
        //     Log("[Phase 2] Resource nodes seeded.");
        // }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 3 — World Events / Encounters                          [TODO]
        // ─────────────────────────────────────────────────────────────────────

        // /// <summary>
        // /// Select and schedule dynamic world events (bandit raids, merchant caravans,
        // /// boss spawns, weather cycles, etc.) for this server session.
        // /// </summary>
        // [Server]
        // private void InitializeWorldEvents()
        // {
        //     // TODO: pull from a WorldEventTable SO and schedule via a WorldEventManager.
        //     Log("[Phase 3] World events scheduled.");
        // }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 4 — Enemy Spawn Points                                  [TODO]
        // ─────────────────────────────────────────────────────────────────────

        // /// <summary>
        // /// Determine initial enemy population across all zones / spawn regions.
        // /// Runs after world events so event-driven spawns can override defaults.
        // /// </summary>
        // [Server]
        // private void InitializeEnemySpawns()
        // {
        //     // TODO: query BaseZone / GenericZone components and call PopulateEnemies().
        //     Log("[Phase 4] Enemy spawn points determined.");
        // }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 5 — Persistence Load                                    [TODO]
        // ─────────────────────────────────────────────────────────────────────

        // /// <summary>
        // /// Apply saved world-state from disk or cloud storage (player inventories,
        // /// container states, world flags, etc.).  Runs last so it can override any
        // /// randomly generated state from earlier phases.
        // /// </summary>
        // [Server]
        // private IEnumerator InitializePersistence()
        // {
        //     // TODO: async load from SaveManager, yield until complete, then apply patches.
        //     Log("[Phase 5] Persistence loaded.");
        //     yield break;
        // }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            if (debugMode)
                Debug.Log($"[ServerWorldInitializer] {msg}");
        }
    }
}
