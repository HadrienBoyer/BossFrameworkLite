using TappyTale.BossFight.Combat;
using UnityEngine;

namespace TappyTale.BossFight.Projectiles
{
    [DisallowMultipleComponent]
    public sealed class BossProjectile : MonoBehaviour
    {
        private BossProjectilePool _owner;
        private ProjectileDefinition _definition;
        private Transform _sourceOwnerTransform;
        private Transform _homingTarget;
        private Vector3 _velocity;
        private float _remainingLifetime;
        private float _currentDamage;
        private DamageTeam _team = DamageTeam.Boss;

        public ProjectileDefinition Definition => _definition;
        public float Damage => _currentDamage;
        public bool CanBeParried => _definition != null && _definition.CanBeParried && _team == DamageTeam.Boss;
        public bool IsActive { get; private set; }
        public DamageTeam Team => _team;
        public Transform SourceOwnerTransform => _sourceOwnerTransform;
        public Vector3 Velocity => _velocity;

        internal void Launch(
            BossProjectilePool owner,
            ProjectileDefinition definition,
            Vector3 position,
            Vector3 direction,
            Transform sourceOwner = null,
            Transform homingTarget = null,
            float speedMultiplier = 1f,
            DamageTeam team = DamageTeam.Boss)
        {
            _owner = owner;
            _definition = definition;
            _sourceOwnerTransform = sourceOwner;
            _homingTarget = homingTarget;
            _team = team;
            _currentDamage = definition != null ? definition.Damage : 0f;

            Vector3 flatDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(flatDir));
            float speed = (definition != null ? definition.Speed : 10f) * Mathf.Max(0.1f, speedMultiplier);
            _velocity = flatDir * speed;
            _remainingLifetime = definition != null ? definition.Lifetime : 5f;

            if (definition != null && definition.Radius > 0.01f)
            {
                transform.localScale = Vector3.one * (definition.Radius * 2f);
            }

            ApplyRendererColor(definition != null ? definition.Color : new Color(1f, 0.35f, 0.15f, 1f));
            IsActive = true;
            gameObject.SetActive(true);
        }

        public bool TryDeflect(Transform deflector, Vector3 newDirection, DamageTeam newTeam = DamageTeam.Player)
        {
            if (!IsActive || !CanBeParried || _definition == null)
            {
                return false;
            }

            _sourceOwnerTransform = deflector;
            _team = newTeam;
            _homingTarget = null;
            Vector3 dir = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : -_velocity.normalized;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.forward;
            }
            dir.Normalize();

            float speed = _definition.Speed * Mathf.Max(1f, _definition.DeflectedSpeedMultiplier);
            _velocity = dir * speed;
            _currentDamage = _definition.Damage * Mathf.Max(1f, _definition.DeflectedDamageMultiplier);
            _remainingLifetime = Mathf.Max(_remainingLifetime, _definition.Lifetime);
            transform.rotation = Quaternion.LookRotation(dir);
            ApplyRendererColor(new Color(0.2f, 0.9f, 1f, 1f));
            return true;
        }

        internal void ResetForPool()
        {
            IsActive = false;
            _definition = null;
            _sourceOwnerTransform = null;
            _homingTarget = null;
            _velocity = Vector3.zero;
            _remainingLifetime = 0f;
            _currentDamage = 0f;
            _team = DamageTeam.Boss;
            if (gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }

        public void Despawn()
        {
            if (!IsActive)
            {
                return;
            }

            if (_owner != null)
            {
                _owner.Despawn(this);
            }
            else
            {
                ResetForPool();
            }
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            if (_definition != null && _definition.HomingStrength > 0f && _homingTarget != null)
            {
                Vector3 toTarget = _homingTarget.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    float speed = _velocity.magnitude;
                    Vector3 desiredDir = Vector3.RotateTowards(_velocity.normalized, toTarget.normalized, _definition.HomingStrength * Time.deltaTime, 0f);
                    _velocity = desiredDir.normalized * speed;
                    if (desiredDir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(desiredDir);
                    }
                }
            }

            transform.position += _velocity * Time.deltaTime;
            _remainingLifetime -= Time.deltaTime;
            if (_remainingLifetime <= 0f)
            {
                Despawn();
            }
        }

        private void ApplyRendererColor(Color color)
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend == null || rend.sharedMaterial == null)
            {
                return;
            }

            MaterialPropertyBlock block = new();
            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            rend.SetPropertyBlock(block);
        }
    }
}
