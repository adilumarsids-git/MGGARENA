using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class ForwardMovement : MonoBehaviour
    {
        [Tooltip("Define the direction of the movement\nor rotate the object toward the movement direction")]
        [ReadOnly] public Vector3 Target;

        [BigHeader("Settings")]
        public bool Enabled = true;
        public float Speed;

        [Tooltip("Destroy/Disable projectile after X seconds (range limit)")]
        [Min(0f)] public float LifeTime = 3f;

        [Tooltip("If true, bullet will be disabled (pool friendly). If false, bullet will be destroyed.")]
        public bool UsePooling = true;

        [BigHeader("Reflection Settings")]
        public bool EnableBounce;
        public LayerMask ReflectLayer;
        [Min(0)] public int MaxReflections;

        [BigHeader("Collision Settings")]
        public LayerMask BlockLayers;
        public GameObject HitPartical;

        [BigHeader("Animations")]
        public Animation OnMoveAnimations;

        private void OnEnable()
        {
            // VERY IMPORTANT: In pooled systems, bullets are reused.
            // OnEnable runs every time it is reused, so we reset the timer here.
            if (LifeTime > 0f)
            {
                CancelInvoke(nameof(Kill));
                Invoke(nameof(Kill), LifeTime);
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(Kill));
        }

        private void Kill()
        {
            if (UsePooling)
                gameObject.SetActive(false);  // pool-friendly
            else
                Destroy(gameObject);          // normal destroy
        }

        void Start()
        {
            if (Target == Vector3.zero) Target = transform.TransformDirection(Vector3.forward);
            transform.LookAt(Target.normalized + transform.position);
        }

        void Update()
        {
            if (Enabled)
            {
                transform.position += Speed * Time.deltaTime * Target;
                if (OnMoveAnimations && !OnMoveAnimations.isPlaying) OnMoveAnimations.Play();
            }
            else if (OnMoveAnimations && OnMoveAnimations.isPlaying) OnMoveAnimations.Stop();
        }

        private void OnTriggerEnter(Collider obj)
        {
            if (ReflectLayer == (ReflectLayer | (1 << obj.gameObject.layer)))
            {
                if (!EnableBounce) return;

                Ray ray = new (transform.position + transform.TransformDirection(Vector3.back) * 2, Target);
                if (Physics.SphereCast(ray.origin, 1, ray.direction, out RaycastHit hit, 3, ReflectLayer))
                {
                    transform.position = hit.point;
                    Target = Vector3.Reflect(Target, hit.normal);
                    transform.LookAt(Target + transform.position);
                }

                if (--MaxReflections < 0)
                {
                    if (HitPartical)
                        Destroy(Instantiate(HitPartical, obj.ClosestPointOnBounds(transform.position),
                            Quaternion.Euler(0, transform.eulerAngles.y + 180, 0)), 3);

                    Kill();
                }
            }
            else if (BlockLayers == (BlockLayers | (1 << obj.gameObject.layer)))
            {
                if (HitPartical)
                    Destroy(Instantiate(HitPartical, obj.ClosestPointOnBounds(transform.position),
                        Quaternion.Euler(0, transform.eulerAngles.y + 180, 0)), 3);

                Kill();
            }
        }
    }
}
