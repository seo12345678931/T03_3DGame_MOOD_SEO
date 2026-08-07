using Mood.Combat;
using Mood.Audio;
using Mood.Events;
using Mood.Player;
using Mood.Utils;
using UnityEngine;
using UnityEngine.AI;
using AudioProfile = Akila.FPSFramework.AudioProfile;

namespace Mood.AI
{
    [AddComponentMenu("MOOD/AI/Enemy NavMesh Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
    public sealed class EnemyNavMeshController : MonoBehaviour, WaveManager.IWaveSpeedScaler
    {
        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyHealth health;
        [SerializeField] private Transform target;
        [SerializeField] private Transform attackOrigin;

        [Header("Targeting")]
        [SerializeField] private string targetTag = "Player";
        [SerializeField, Min(0f)] private float detectionRange = 18f;
        [SerializeField, Min(0f)] private float forgetRange = 24f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.15f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float attackRange = 2.2f;
        [SerializeField, Min(0f)] private float attackDamage = 12f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1.2f;
        [SerializeField, Min(0.01f)] private float attackAnimationTimeout = 1.2f;
        [SerializeField, Min(0f)] private float faceTargetSpeed = 540f;

        [Header("Short Charge Attack")]
        [SerializeField] private bool enableShortChargeAttack = false;
        [SerializeField, Min(0f)] private float chargeMinRange = 2.8f;
        [SerializeField, Min(0f)] private float chargeTriggerRange = 6f;
        [SerializeField, Min(0f)] private float chargeDistance = 3.2f;
        [SerializeField, Min(0.01f)] private float chargeSpeed = 9f;
        [SerializeField, Min(0f)] private float chargeDamage = 18f;
        [SerializeField, Min(0.01f)] private float chargeCooldown = 3.5f;
        [SerializeField, Min(0f)] private float chargeHitRadius = 1.35f;
        [SerializeField] private string chargeTriggerParameter = "Charge";

        [Header("Reaction")]
        [SerializeField, Min(0f)] private float damageStunDuration = 0.25f;

        [Header("Audio")]
        [SerializeField] private SfxPlayer sfxPlayer;
        [SerializeField] private AudioProfile defaultMoveAudioProfile;
        [SerializeField, Min(0.05f)] private float defaultMoveAudioInterval = 1.1f;
        [SerializeField, Min(0f)] private float minimumDefaultMoveAudioSpeed = 0.15f;
        [SerializeField] private AudioProfile attackStartAudioProfile;
        [SerializeField] private AudioProfile attackHitAudioProfile;
        [SerializeField] private AudioProfile chargeStartAudioProfile;
        [SerializeField] private AudioProfile chargeHitAudioProfile;
        [SerializeField] private AudioProfile damagedAudioProfile;
        [SerializeField] private AudioProfile deathAudioProfile;

        [Header("Animation Parameters")]
        [SerializeField] private string moveSpeedParameter = "MoveSpeed";
        [SerializeField] private string attackTriggerParameter = "Attack";
        [SerializeField] private string damageTriggerParameter = "Damage";
        [SerializeField] private string deathTriggerParameter = "Death";
        [SerializeField] private string deadBoolParameter = "IsDead";
        [SerializeField] private bool enableDebugLogs = true;

        private float nextAttackTime;
        private float nextChargeTime;
        private float nextRepathTime;
        private float damageStunEndTime;
        private float attackEndTime;
        private float nextDefaultMoveAudioTime;
        private float remainingChargeDistance;
        private float baseChargeSpeed;
        private bool isDead;
        private bool isAttacking;
        private bool isCharging;
        private bool hasAppliedChargeDamage;
        private bool hasCachedBaseSpeed;
        private Vector3 chargeDirection;

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            AssignAudioReferences();
            CacheBaseSpeedValues();
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

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            AssignAudioReferences();
            CacheBaseSpeedValues();
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
            bool canTrackTarget = distanceToTarget <= forgetRange;
            if (!canTrackTarget)
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
                }

                StopMoving();
                FaceTarget();
                UpdateMoveAnimation();
                return;
            }

            if (isCharging)
            {
                UpdateCharge();
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

            if (CanStartCharge(distanceToTarget))
            {
                BeginCharge();
                UpdateMoveAnimation();
                return;
            }

            if (distanceToTarget <= attackRange)
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

            if (agent != null && agent.isOnNavMesh)
            {
                if (Time.time >= nextRepathTime)
                {
                    nextRepathTime = Time.time + repathInterval;
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                }
            }
            else
            {
                StopMoving();
            }

            UpdateMoveAnimation();
            UpdateDefaultMoveAudio();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void ApplySpeedMultiplier(float multiplier)
        {
            CacheBaseSpeedValues();
            chargeSpeed = Mathf.Max(0.01f, baseChargeSpeed * Mathf.Max(0.01f, multiplier));
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
            CancelAttack();
            StopMoving();
            PlaySfx(damagedAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, damageTriggerParameter);
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
            AnimatorHelper.SetFloatIfExists(animator, moveSpeedParameter, 0f);
            AnimatorHelper.SetBoolIfExists(animator, deadBoolParameter, true);
            AnimatorHelper.SetTriggerIfExists(animator, deathTriggerParameter);
        }

        public void AnimationEventAttackHit()
        {
            LogDebug("AnimationEventAttackHit called.");
            if (!isDead && isAttacking)
            {
                TryApplyAttackDamage();
            }
        }

        public void AnimationEventAttackFinished()
        {
            isAttacking = false;
            LogDebug("AnimationEventAttackFinished called.");
        }

        private void TryApplyAttackDamage()
        {
            if (target == null)
            {
                LogDebug("Attack canceled. Target is null.");
                return;
            }

            Vector3 targetCenter = target.position;
            float distanceToTarget = Vector3.Distance(transform.position, targetCenter);
            float attackTolerance = agent != null ? agent.radius : 0f;
            if (distanceToTarget > attackRange + attackTolerance)
            {
                LogDebug($"Attack missed. Distance {distanceToTarget:0.00} > {attackRange + attackTolerance:0.00}");
                return;
            }

            PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                LogDebug($"Attack failed. PlayerHealth not found on target {target.name}.");
                return;
            }

            Vector3 hitOrigin = attackOrigin != null ? attackOrigin.position : transform.position;
            Vector3 hitNormal = (targetCenter - hitOrigin).sqrMagnitude > 0.0001f
                ? (targetCenter - hitOrigin).normalized
                : transform.forward;

            float previousHealth = playerHealth.CurrentHealth;
            playerHealth.ApplyDamage(attackDamage, hitOrigin, hitNormal, gameObject);
            PlaySfx(attackHitAudioProfile);
            LogDebug($"Applied {attackDamage:0.##} damage to {playerHealth.name}. HP {previousHealth:0.##} -> {playerHealth.CurrentHealth:0.##}");
        }

        private bool CanStartCharge(float distanceToTarget)
        {
            if (!enableShortChargeAttack || isCharging)
            {
                return false;
            }

            if (target == null || Time.time < nextChargeTime)
            {
                return false;
            }

            if (distanceToTarget < chargeMinRange || distanceToTarget > chargeTriggerRange)
            {
                return false;
            }

            return chargeDistance > 0f && chargeSpeed > 0f;
        }

        private void BeginCharge()
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

            chargeDirection = direction.normalized;
            remainingChargeDistance = chargeDistance;
            nextChargeTime = Time.time + chargeCooldown;
            isCharging = true;
            hasAppliedChargeDamage = false;

            StopMoving();
            FaceTarget();
            PlaySfx(chargeStartAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, chargeTriggerParameter);
            LogDebug("Short charge started.");
        }

        private void UpdateCharge()
        {
            if (!enableShortChargeAttack || target == null)
            {
                CancelAttack();
                return;
            }

            float chargeStep = chargeSpeed * Time.deltaTime;
            if (chargeStep <= 0f)
            {
                return;
            }

            Vector3 frameMove = chargeDirection * Mathf.Min(chargeStep, remainingChargeDistance);

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.Move(frameMove);
            }
            else
            {
                transform.position += frameMove;
            }

            remainingChargeDistance -= frameMove.magnitude;
            if (chargeDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(chargeDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, faceTargetSpeed * Time.deltaTime);
            }

            TryApplyChargeDamage();

            if (remainingChargeDistance <= 0.001f)
            {
                isCharging = false;
                hasAppliedChargeDamage = false;
                LogDebug("Short charge finished.");
            }
        }

        private void TryApplyChargeDamage()
        {
            if (hasAppliedChargeDamage || target == null)
            {
                return;
            }

            PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            Vector3 hitOrigin = attackOrigin != null ? attackOrigin.position : transform.position;
            Vector3 targetPosition = playerHealth.transform.position;
            targetPosition.y = hitOrigin.y;

            if (Vector3.Distance(hitOrigin, targetPosition) > chargeHitRadius)
            {
                return;
            }

            Vector3 hitNormal = (target.position - hitOrigin).sqrMagnitude > 0.0001f
                ? (target.position - hitOrigin).normalized
                : transform.forward;

            float previousHealth = playerHealth.CurrentHealth;
            playerHealth.ApplyDamage(chargeDamage, hitOrigin, hitNormal, gameObject);
            hasAppliedChargeDamage = true;
            PlaySfx(chargeHitAudioProfile);
            LogDebug($"Applied {chargeDamage:0.##} charge damage to {playerHealth.name}. HP {previousHealth:0.##} -> {playerHealth.CurrentHealth:0.##}");
        }

        private void RefreshTarget()
        {
            if (target != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(targetTag))
            {
                return;
            }

            GameObject targetObject = GameObject.FindWithTag(targetTag);
            if (targetObject != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, targetObject.transform.position);
                if (distanceToTarget <= detectionRange)
                {
                    target = targetObject.transform;
                    LogDebug($"Target acquired: {target.name}");
                }
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

            AnimatorHelper.SetFloatIfExists(animator, moveSpeedParameter, normalizedSpeed);
        }

        private void UpdateDefaultMoveAudio()
        {
            if (defaultMoveAudioProfile == null || Time.time < nextDefaultMoveAudioTime)
            {
                return;
            }

            if (isDead || isAttacking || isCharging || Time.time < damageStunEndTime)
            {
                return;
            }

            if (agent == null || agent.isStopped || !agent.isOnNavMesh)
            {
                return;
            }

            if (agent.velocity.magnitude < minimumDefaultMoveAudioSpeed)
            {
                return;
            }

            // 적이 평소 이동 중일 때만 기본 사운드를 주기적으로 재생해
            // 공격/피격 SFX와 겹쳐서 너무 시끄러워지는 상황을 줄인다.
            nextDefaultMoveAudioTime = Time.time + defaultMoveAudioInterval;
            PlaySfx(defaultMoveAudioProfile);
        }

        private void CancelAttack()
        {
            isAttacking = false;
            isCharging = false;
            hasAppliedChargeDamage = false;
            remainingChargeDistance = 0f;
            chargeDirection = Vector3.zero;
        }

        private void BeginAttack()
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;
            attackEndTime = Time.time + attackAnimationTimeout;
            StopMoving();
            FaceTarget();
            PlaySfx(attackStartAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, attackTriggerParameter);
            LogDebug("Attack started.");
        }

        private void AssignAudioReferences()
        {
            // 프리팹을 건드리지 않아도 적 행동 SFX를 바로 연결할 수 있도록 런타임 재생기를 보정한다.
            if (sfxPlayer != null)
            {
                return;
            }

            sfxPlayer = GetComponentInChildren<SfxPlayer>();
            if (sfxPlayer != null)
            {
                return;
            }

            GameObject sfxObject = new GameObject("Enemy SFX Player");
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

        private void CacheBaseSpeedValues()
        {
            if (hasCachedBaseSpeed)
            {
                return;
            }

            baseChargeSpeed = chargeSpeed;
            hasCachedBaseSpeed = true;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[EnemyNavMeshController:{name}] {message}", this);
            }
        }

    }
}

