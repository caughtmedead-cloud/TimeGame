using UnityEngine;
using UnityEditor;
using Thelos.AI;
using Thelos.AI.Actions;

namespace Thelos.AI.Editor
{
    public class AITestSceneSetup : EditorWindow
    {
        private GameObject characterPrefab;
        private string aiCharacterName = "TestAI";
        private bool createDeadBodySpawner = true;
        private bool createPatrolPoints = false;
        private int patrolPointCount = 4;
        
        [MenuItem("Tools/AI/Setup Test Scene")]
        private static void ShowWindow()
        {
            GetWindow<AITestSceneSetup>("AI Test Scene Setup");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("AI Test Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            characterPrefab = EditorGUILayout.ObjectField("Character Prefab (Optional)", characterPrefab, typeof(GameObject), false) as GameObject;
            aiCharacterName = EditorGUILayout.TextField("AI Character Name", aiCharacterName);
            
            EditorGUILayout.Space();
            GUILayout.Label("Optional Components", EditorStyles.boldLabel);
            createDeadBodySpawner = EditorGUILayout.Toggle("Create Dead Body Spawner", createDeadBodySpawner);
            createPatrolPoints = EditorGUILayout.Toggle("Create Patrol Points", createPatrolPoints);
            
            if (createPatrolPoints)
            {
                patrolPointCount = EditorGUILayout.IntSlider("Patrol Point Count", patrolPointCount, 2, 10);
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Create AI Character"))
            {
                CreateAICharacter();
            }
            
            if (GUILayout.Button("Create Dead Body Spawner"))
            {
                CreateDeadBodySpawner();
            }
            
            if (GUILayout.Button("Create Patrol System"))
            {
                CreatePatrolSystem();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This tool helps you quickly set up AI test scenarios:\n\n" +
                "• Creates AI character with all components\n" +
                "• Optionally adds dead body spawner\n" +
                "• Optionally creates patrol points\n\n" +
                "Note: You still need to assign animation clips to the Animator Controller!",
                MessageType.Info
            );
        }
        
        private void CreateAICharacter()
        {
            GameObject aiCharacter;
            
            if (characterPrefab != null)
            {
                aiCharacter = PrefabUtility.InstantiatePrefab(characterPrefab) as GameObject;
                aiCharacter.name = aiCharacterName;
            }
            else
            {
                aiCharacter = new GameObject(aiCharacterName);
                
                SpriteRenderer sprite = aiCharacter.AddComponent<SpriteRenderer>();
                sprite.color = Color.cyan;
                
                BoxCollider2D collider = aiCharacter.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one;
            }
            
            if (aiCharacter.GetComponent<AIAgent>() == null)
            {
                AIAgent agent = aiCharacter.AddComponent<AIAgent>();
            }
            
            if (aiCharacter.GetComponent<SimpleAIController>() == null)
            {
                aiCharacter.AddComponent<SimpleAIController>();
            }
            
            if (aiCharacter.GetComponent<IdleAction>() == null)
            {
                aiCharacter.AddComponent<IdleAction>();
            }
            
            if (aiCharacter.GetComponent<WanderAction>() == null)
            {
                aiCharacter.AddComponent<WanderAction>();
            }
            
            if (aiCharacter.GetComponent<EatDeadBodyAction>() == null)
            {
                aiCharacter.AddComponent<EatDeadBodyAction>();
            }
            
            if (aiCharacter.GetComponent<AIBehaviorDebugger>() == null)
            {
                aiCharacter.AddComponent<AIBehaviorDebugger>();
            }
            
            if (aiCharacter.GetComponent<Animator>() == null)
            {
                aiCharacter.AddComponent<Animator>();
            }
            
            if (createPatrolPoints)
            {
                PatrolAction patrol = aiCharacter.AddComponent<PatrolAction>();
                Transform[] points = CreatePatrolPointsForCharacter(aiCharacter.transform);
                
                SerializedObject serializedPatrol = new SerializedObject(patrol);
                SerializedProperty pointsProp = serializedPatrol.FindProperty("patrolPoints");
                pointsProp.arraySize = points.Length;
                for (int i = 0; i < points.Length; i++)
                {
                    pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
                }
                serializedPatrol.ApplyModifiedProperties();
            }
            
            Selection.activeGameObject = aiCharacter;
            EditorGUIUtility.PingObject(aiCharacter);
            
            Debug.Log($"Created AI character: {aiCharacter.name}");
        }
        
        private void CreateDeadBodySpawner()
        {
            GameObject spawner = new GameObject("DeadBodySpawner");
            DeadBodySpawner spawnerComponent = spawner.AddComponent<DeadBodySpawner>();
            
            GameObject deadBodyPrefab = CreateDeadBodyPrefab();
            
            SerializedObject serializedSpawner = new SerializedObject(spawnerComponent);
            serializedSpawner.FindProperty("deadBodyPrefab").objectReferenceValue = deadBodyPrefab;
            serializedSpawner.FindProperty("spawnInterval").floatValue = 5f;
            serializedSpawner.FindProperty("maxBodies").intValue = 10;
            serializedSpawner.ApplyModifiedProperties();
            
            Selection.activeGameObject = spawner;
            EditorGUIUtility.PingObject(spawner);
            
            Debug.Log("Created Dead Body Spawner");
        }
        
        private GameObject CreateDeadBodyPrefab()
        {
            GameObject deadBody = new GameObject("DeadBody");
            
            SpriteRenderer sprite = deadBody.AddComponent<SpriteRenderer>();
            sprite.color = new Color(0.5f, 0.5f, 0.5f);
            
            CircleCollider2D collider = deadBody.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.isTrigger = true;
            
            deadBody.AddComponent<DeadBody>();
            deadBody.tag = "DeadBody";
            
            if (!System.IO.Directory.Exists("Assets/Prefabs"))
            {
                System.IO.Directory.CreateDirectory("Assets/Prefabs");
            }
            
            string path = "Assets/Prefabs/DeadBody.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(deadBody, path);
            
            DestroyImmediate(deadBody);
            
            return prefab;
        }
        
        private void CreatePatrolSystem()
        {
            GameObject patrolManager = new GameObject("PatrolSystem");
            PatrolPointManager manager = patrolManager.AddComponent<PatrolPointManager>();
            
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("numberOfPoints").intValue = patrolPointCount;
            serializedManager.ApplyModifiedProperties();
            
            manager.SendMessage("CreatePatrolPointsInCircle");
            
            Selection.activeGameObject = patrolManager;
            EditorGUIUtility.PingObject(patrolManager);
            
            Debug.Log("Created Patrol System with " + patrolPointCount + " points");
        }
        
        private Transform[] CreatePatrolPointsForCharacter(Transform parent)
        {
            Transform[] points = new Transform[patrolPointCount];
            float radius = 5f;
            
            for (int i = 0; i < patrolPointCount; i++)
            {
                float angle = (360f / patrolPointCount) * i;
                float radians = angle * Mathf.Deg2Rad;
                
                Vector3 position = parent.position + new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0
                );
                
                GameObject point = new GameObject($"PatrolPoint_{i}");
                point.transform.position = position;
                point.transform.SetParent(parent);
                
                points[i] = point.transform;
            }
            
            return points;
        }
    }
}
