using UnityEngine;

namespace Thelos.AI
{
    public class DeadBody : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool isInfinite = false;
        [SerializeField] private float despawnTime = 30f;
        
        private float timer;
        
        public bool IsInfinite => isInfinite;
        
        private void Start()
        {
            gameObject.tag = "DeadBody";
        }
        
        private void Update()
        {
            if (isInfinite) return;
            
            timer += Time.deltaTime;
            
            if (timer >= despawnTime)
            {
                Destroy(gameObject);
            }
        }
    }
}
