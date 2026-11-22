using UnityEngine;
using KingdomsClone.Core.Pooling;

namespace KingdomsClone.Gameplay
{
    public class PoofEffect : MonoBehaviour, IPoolable
    {
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private float startScale = 0.3f;
        [SerializeField] private float endScale = 2.5f;

        private float _t;

        public void OnGetFromPool()
        {
            _t = 0f;
            transform.localScale = Vector3.one * startScale;
        }

        public void OnReturnToPool() { }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= duration) {
                if (PoolManager.Instance != null) {
                    PoolManager.Instance.Despawn(gameObject);
                } else {
                    Destroy(gameObject);
                }
        
                return;
            }

            float k = _t / duration;
            float scale = Mathf.Lerp(startScale, endScale, k);
            transform.localScale = Vector3.one * scale;
        }

        public static GameObject CreatePrefab()
        {
            var root = new GameObject("PoofEffectTemplate");
            root.SetActive(false);
            DontDestroyOnLoad(root);

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            Destroy(body.GetComponent<Collider>());

            var mr = body.GetComponent<MeshRenderer>();

            if (mr != null) {
                mr.material.color = new Color(1f, 1f, 1f, 0.7f);
            }

            root.AddComponent<PoofEffect>();
            return root;
        }
    }
}