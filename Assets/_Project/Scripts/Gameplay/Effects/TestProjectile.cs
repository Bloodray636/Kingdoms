using UnityEngine;
using KingdomsClone.Core.Pooling;

namespace KingdomsClone.Gameplay
{
    /// <summary>
    /// Простой снаряд для демонстрации пула. В Эпике 8 заменим настоящим.
    /// Летит по параболе, живёт N секунд, возвращается в пул.
    /// </summary>
    public class TestProjectile : MonoBehaviour, IPoolable
    {
        private Vector3 _velocity;
        private float _lifetime;

        public void Launch(Vector3 velocity, float lifetime)
        {
            _velocity = velocity;
            _lifetime = lifetime;
        }

        public void OnGetFromPool()
        {
            _lifetime = 0;
            _velocity = Vector3.zero;
            transform.localScale = Vector3.one * 0.3f;
        }

        public void OnReturnToPool() { }

        private void Update()
        {
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0)
            {
                if (PoolManager.Instance != null)
                    PoolManager.Instance.Despawn(gameObject);
                else
                    Destroy(gameObject);
                return;
            }

            transform.position += _velocity * Time.deltaTime;
            _velocity += Physics.gravity * Time.deltaTime;
        }

        public static GameObject CreatePrefab()
        {
            var root = new GameObject("TestProjectileTemplate");
            root.SetActive(false);
            DontDestroyOnLoad(root);

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 0.3f;
            Destroy(body.GetComponent<Collider>());

            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(0.9f, 0.2f, 0.2f);

            root.AddComponent<TestProjectile>();
            return root;
        }
    }
}