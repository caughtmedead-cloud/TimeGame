using UnityEngine;
using System.Collections;

namespace Thelos.AI
{
    public class DeadBodySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject deadBodyPrefab;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private int maxBodies = 10;
        [SerializeField] private bool spawnOnStart = true;
        
        [Header("Spawn Area")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(20, 20);
        [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
        [SerializeField] private LayerMask spawnCheckMask;
        [SerializeField] private bool checkForObstacles = true;
        
        private int currentBodyCount = 0;
        private float spawnTimer = 0f;
        
        private void Start()
        {
            if (spawnOnStart)
            {
                StartCoroutine(SpawnRoutine());
            }
        }
        
        private void Update()
        {
            spawnTimer += Time.deltaTime;
            
            if (spawnTimer >= spawnInterval && currentBodyCount < maxBodies)
            {
                SpawnDeadBody();
                spawnTimer = 0f;
            }
        }
        
        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                if (currentBodyCount < maxBodies)
                {
                    SpawnDeadBody();
                }
                
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        
        private void SpawnDeadBody()
        {
            if (deadBodyPrefab == null)
            {
                Debug.LogWarning("DeadBodySpawner: No prefab assigned!");
                return;
            }
            
            Vector2 spawnPos = GetRandomSpawnPosition();
            
            if (checkForObstacles)
            {
                int maxAttempts = 10;
                int attempts = 0;
                
                while (IsPositionBlocked(spawnPos) && attempts < maxAttempts)
                {
                    spawnPos = GetRandomSpawnPosition();
                    attempts++;
                }
                
                if (attempts >= maxAttempts)
                {
                    return;
                }
            }
            
            GameObject body = Instantiate(deadBodyPrefab, spawnPos, Quaternion.identity);
            body.transform.SetParent(transform);
            
            DeadBody deadBodyComponent = body.GetComponent<DeadBody>();
            if (deadBodyComponent == null)
            {
                deadBodyComponent = body.AddComponent<DeadBody>();
            }
            
            currentBodyCount++;
            
            StartCoroutine(TrackBodyLifetime(body));
        }
        
        private IEnumerator TrackBodyLifetime(GameObject body)
        {
            while (body != null)
            {
                yield return null;
            }
            
            currentBodyCount--;
        }
        
        private Vector2 GetRandomSpawnPosition()
        {
            float x = Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2);
            float y = Random.Range(-spawnAreaSize.y / 2, spawnAreaSize.y / 2);
            
            return spawnAreaCenter + new Vector2(x, y);
        }
        
        private bool IsPositionBlocked(Vector2 position)
        {
            Collider2D hit = Physics2D.OverlapPoint(position, spawnCheckMask);
            return hit != null;
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
            
            Gizmos.color = new Color(1, 0, 1, 0.1f);
            Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
        }
    }
}
