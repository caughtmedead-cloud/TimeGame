using UnityEngine;

/// <summary>
/// Spawns the local player prefab at a configured spawn point when the scene starts.
/// Singleplayer replacement for the former FishNet CustomPlayerSpawner.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(0f, 2f, 0f);

    private void Start()
    {
        SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] No player prefab assigned!");
            return;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : fallbackSpawnPosition;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        Instantiate(playerPrefab, position, rotation);
    }
}
