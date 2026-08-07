using Mood.Combat;
using Mood.Audio;
using Mood.Player;
using Mood.Utils;
using UnityEngine;
using UnityEngine.AI;
using AudioProfile = Akila.FPSFramework.AudioProfile;

namespace Mood.AI
{
    [AddComponentMenu("MOOD/AI/Ranged Enemy NavMesh Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
    public sealed class RangedEnemyNavMeshController : MonoBehaviour
    {
        private const string MoveSpeedParameter = "MoveSpeed";
        private const string AttackTriggerParameter = "Attack";
        private const string DamageTriggerParameter = "Damage";
        private const string DeathTriggerParameter = "Death";
        private const string DeadBoolParameter = "IsDead";

        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyHealth health;
        [SerializeField] private Transform target;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private EnemyFireballProjectile projectilePrefab;

        [Header("Targeting")]
        [SerializeField] private string targetTag = "Player";
        [SerializeField, Min(0f)] private float detectionRange = 20f;
        [SerializeField, Min(0f)] private float forgetRange = 28f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.15f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Combat")]
        [SerializeField, Min(0f)] private float attackRange = 14f;
        [SerializeField, Min(0f)] private float stopDistance = 10f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1.8f;
        [SerializeField, Min(0.01f)] private float attackAnimationTimeout = 1.4f;
        [SerializeField, Min(0f)] private float faceTargetSpeed = 540f;

        [Header("Reaction")]
        [SerializeField, Min(0f)] private float damageStunDuration = 0.25f;

        [Header("Audio")]
        [SerializeField] private SfxPlayer sfxPlayer;
        [SerializeField] private AudioProfile attackStartAudioProfile;
        [SerializeField] private AudioProfile projectileReleaseAudioProfile;
        [SerializeField] private AudioProfile damagedAudioProfile;
        [SerializeField] private AudioProfile deathAudioProfile;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private float nextAttackTime;
        private float nextRepathTime;
        private float damageStunEndTime;
        private float attackEndTime;
        private bool isDead;
        private bool isAttacking;
        private bool hasReleasedProjectileThisCycle;

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (projectileSpawnPoint == null)
            {
                projectileSpawnPoint = transform;
            }

            AssignAudioReferences();
        }

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (projectileSpawnPoint == null)
            {
                projectileSpawnPoint = transform;
            }

            AssignAudioReferences();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += HandleDamaged;
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
        }

        private void Update()
        {
            if (isDead)
            {
                return;
            }

            RefreshTarget();

            if (target == null)
            {
                StopMoving();
                UpdateMoveAnimation();
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget > forgetRange)
            {
                target = null;
                StopMoving();
                UpdateMoveAnimation();
                return;
            }

            if (isAttacking)
            {
                if (Time.time >= attackEndTime)
                {
                    isAttacking = false;
                    hasReleasedProjectileThisCycle = false;
                }

                StopMoving();
                FaceTarget();
                UpdateMoveAnimation();
                return;
            }

            if (Time.time < damageStunEndTime)
            {
                StopMoving();
                FaceTarget();
                UpdateMoveAnimation();
                return;
            }

            bool hasLineOfSight = HasLineOfSight();
            if (distanceToTarget <= attackRange && hasLineOfSight)
            {
                StopMoving();
                FaceTarget();

                if (Time.time >= nextAttackTime)
                {
                    BeginAttack();
                }

                UpdateMoveAnimation();
                return;
            }

            MoveForCombat(distanceToTarget, hasLineOfSight);
            FaceTarget();
            UpdateMoveAnimation();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void AnimationEventFireProjectile()
        {
            if (isDead || !isAttacking || hasReleasedProjectileThisCycle)
            {
                return;
            }

            hasReleasedProjectileThisCycle = TryFireProjectile();
        }

        public void AnimationEventAttackFinished()
        {
            isAttacking = false;
            hasReleasedProjectileThisCycle = false;
            LogDebug("AnimationEventAttackFinished called.");
        }

        private void HandleDamaged(EnemyHealth _, GameObject instigator)
        {
            if (isDead)
            {
                return;
            }

            PlayerHealth instigatorPlayerHealth = instigator != null ? instigator.GetComponentInParent<PlayerHealth>() : null;
            if (instigatorPlayerHealth != null)
            {
                target = instigatorPlayerHealth.transform;
            }

            damageStunEndTime = Mathf.Max(damageStunEndTime, Time.time + damageStunDuration);
            ReleasePendingProjectileOnDamage();
            CancelAttack();
            StopMoving();
            PlaySfx(damagedAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, DamageTriggerParameter);
            UpdateMoveAnimation();
        }

        private void HandleDied(EnemyHealth _, GameObject __)
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            CancelAttack();
            StopMoving();
            PlaySfx(deathAudioProfile);
            AnimatorHelper.SetFloatIfExists(animator, MoveSpeedParameter, 0f);
            AnimatorHelper.SetBoolIfExists(animator, DeadBoolParameter, true);
            AnimatorHelper.SetTriggerIfExists(animator, DeathTriggerParameter);
        }

        private void BeginAttack()
        {
            isAttacking = true;
            hasReleasedProjectileThisCycle = false;
            nextAttackTime = Time.time + attackCooldown;
            attackEndTime = Time.time + attackAnimationTimeout;
            StopMoving();
            FaceTarget();
            PlaySfx(attackStartAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, AttackTriggerParameter);
            LogDebug("Attack started.");
        }

        private bool TryFireProjectile()
        {
            if (projectilePrefab == null || target == null)
            {
                LogDebug("Projectile fire canceled. Missing prefab or target.");
                return false;
            }

            Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
            Vector3 targetPoint = GetTargetAimPoint();
            Vector3 direction = targetPoint - spawnPoint.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
            }

            EnemyFireballProjectile projectile = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.LookRotation(direction.normalized, Vector3.up));
            projectile.Initialize(gameObject, direction.normalized);
            PlaySfx(projectileReleaseAudioProfile);
            LogDebug("Projectile fired.");
            return true;
        }

        private void AssignAudioReferences()
        {
            // 원거리 적은 투사체 프리팹과 별개로 본체 행동음을 내야 해서 전용 재생기를 자동 생성한다.
            if (sfxPlayer != null)
            {
                return;
            }

            sfxPlayer = GetComponentInChildren<SfxPlayer>();
            if (sfxPlayer != null)
            {
                return;
            }

            GameObject sfxObject = new GameObject("Ranged Enemy SFX Player");
            sfxObject.transform.SetParent(transform, false);
            sfxPlayer = sfxObject.AddComponent<SfxPlayer>();
        }

        private void PlaySfx(AudioProfile audioProfile)
        {
            if (audioProfile == null)
            {
                return;
            }

            AssignAudioReferences();
            sfxPlayer?.Play(audioProfile);
        }

        private void ReleasePendingProjectileOnDamage()
        {
            if (!isAttacking || hasReleasedProjectileThisCycle)
            {
                return;
            }

            hasReleasedProjectileThisCycle = TryFireProjectile();
            if (hasReleasedProjectileThisCycle)
            {
                LogDebug("Released pending projectile because attack was interrupted by damage.");
            }
        }

        private Vector3 GetTargetAimPoint()
        {
            PlayerHealth playerHealth = target != null ? target.GetComponentInParent<PlayerHealth>() : null;
            if (playerHealth != null)
            {
                CharacterController playerController = playerHealth.GetComponent<CharacterController>();
                if (playerController != null)
                {
                    return playerController.bounds.center;
                }
            }

            Collider targetCollider = target != null ? target.GetComponentInParent<Collider>() : null;
            return targetCollider != null ? targetCollider.bounds.center : target.position + (Vector3.up * 1.2f);
        }

        private void MoveForCombat(float distanceToTarget, bool hasLineOfSight)
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                StopMoving();
                return;
            }

            if (Time.time < nextRepathTime)
            {
                return;
            }

            nextRepathTime = Time.time + repathInterval;
            agent.isStopped = false;

            if (!hasLineOfSight || distanceToTarget > stopDistance)
            {
                agent.SetDestination(target.position);
                return;
            }

            StopMoving();
        }

        private bool HasLineOfSight()
        {
            if (target == null)
            {
                return false;
            }

            Vector3 origin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + (Vector3.up * 1.2f);
            Vector3 targetPoint = GetTargetAimPoint();
            Vector3 direction = targetPoint - origin;
            float distance = direction.magnitude;
            if (distance <= 0.05f)
            {
                return true;
            }

            direction /= distance;
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance, lineOfSightMask, triggerInteraction))
            {
                return true;
            }

            Transform hitTransform = hit.transform;
            return hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform);
        }

        private void RefreshTarget()
        {
            if (target != null || string.IsNullOrWhiteSpace(targetTag))
            {
                return;
            }

            GameObject targetObject = GameObject.FindWithTag(targetTag);
            if (targetObject == null)
            {
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, targetObject.transform.position);
            if (distanceToTarget <= detectionRange)
            {
                target = targetObject.transform;
                LogDebug($"Target acquired: {target.name}");
            }
        }

        private void StopMoving()
        {
            if (agent == null)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void FaceTarget()
        {
            if (target == null)
            {
                return;
            }

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, faceTargetSpeed * Time.deltaTime);
        }

        private void UpdateMoveAnimation()
        {
            if (animator == null)
            {
                return;
            }

            float normalizedSpeed = 0f;
            if (agent != null && !agent.isStopped && agent.speed > 0.001f)
            {
                normalizedSpeed = Mathf.Clamp01(agent.velocity.magnitude / agent.speed);
            }

            AnimatorHelper.SetFloatIfExists(animator, MoveSpeedParameter, normalizedSpeed);
        }

        private void CancelAttack()
        {
            isAttacking = false;
            hasReleasedProjectileThisCycle = false;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RangedEnemyNavMeshController:{name}] {message}", this);
            }
        }
    }
}


