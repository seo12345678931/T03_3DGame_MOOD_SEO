using System.Collections.Generic;
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
    [AddComponentMenu("MOOD/AI/Boss NavMesh Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
    public sealed class BossNavMeshController : MonoBehaviour, WaveManager.IWaveSpeedScaler
    {
        private const string MoveXParameter = "MoveX";
        private const string MoveYParameter = "MoveY";
        private const string AttackTriggerParameter = "Attack";
        private const string JumpAttackTriggerParameter = "JumpAttack";
        private const string DamageTriggerParameter = "Damage";
        private const string DeathTriggerParameter = "Death";
        private const string DeadBoolParameter = "IsDead";

        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyHealth health;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Transform target;
        [SerializeField] private Transform attackOrigin;

        [Header("Targeting")]
        [SerializeField] private string targetTag = "Player";
        [SerializeField, Min(0f)] private float detectionRange = 24f;
        [SerializeField, Min(0f)] private float forgetRange = 32f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.15f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float attackRange = 3f;
        [SerializeField, Min(0f)] private float attackDamage = 20f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1.5f;
        [SerializeField, Min(0.01f)] private float attackAnimationTimeout = 1.5f;
        [SerializeField, Min(0.01f)] private float specialAttackGlobalCooldown = 4f;
        [SerializeField, Min(0f)] private float faceTargetSpeed = 540f;

        [Header("Charge Attack")]
        [SerializeField] private bool enableChargeAttack = true;
        [SerializeField, Min(0f)] private float chargeMinRange = 4.5f;
        [SerializeField, Min(0f)] private float chargeTriggerRange = 12f;
        [SerializeField, Min(0f)] private float chargeDistance = 10f;
        [SerializeField, Min(0f)] private float chargeWidth = 2.5f;
        [SerializeField, Min(0f)] private float chargeDamage = 35f;
        [SerializeField, Min(0.01f)] private float chargeSpeed = 18f;
        [SerializeField, Min(0.01f)] private float chargeTelegraphDuration = 1f;
        [SerializeField, Min(0.01f)] private float chargeCooldown = 5f;
        [SerializeField, Min(0f)] private float chargeHitPadding = 0.35f;
        [SerializeField, Min(0.01f)] private float chargeTelegraphLineWidth = 0.15f;
        [SerializeField, Min(0f)] private float chargeTelegraphVerticalOffset = 0.05f;
        [SerializeField] private Color chargeTelegraphColor = new Color(1f, 0.35f, 0.1f, 0.95f);
        [SerializeField] private Color chargeTelegraphFillColor = new Color(1f, 0.45f, 0.15f, 0.28f);

        [Header("Jump Attack")]
        [SerializeField] private bool enableJumpAttack = true;
        [SerializeField, Min(0f)] private float jumpMinRange = 8f;
        [SerializeField, Min(0f)] private float jumpTriggerRange = 18f;
        [SerializeField, Min(0f)] private float jumpDamage = 45f;
        [SerializeField, Min(0f)] private float jumpLandingRadius = 4f;
        [SerializeField, Min(0.01f)] private float jumpTelegraphDuration = 1.1f;
        [SerializeField, Min(0.01f)] private float jumpAnimationDuration = 1f;
        [SerializeField, Min(0.01f)] private float jumpCooldown = 7f;
        [SerializeField, Min(0f)] private float jumpArcHeight = 4f;
        [SerializeField, Min(0f)] private float jumpPredictionTime = 0.35f;
        [SerializeField, Min(0f)] private float jumpShockwaveForce = 14f;
        [SerializeField, Min(0f)] private float jumpShockwaveUpwardsModifier = 0.2f;
        [SerializeField] private LayerMask jumpImpactMask = ~0;
        [SerializeField] private QueryTriggerInteraction jumpImpactTriggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Min(8)] private int jumpTelegraphSegments = 40;
        [SerializeField, Min(0.01f)] private float jumpTelegraphLineWidth = 0.15f;
        [SerializeField, Min(0f)] private float jumpTelegraphVerticalOffset = 0.05f;
        [SerializeField] private Color jumpTelegraphColor = new Color(1f, 0.55f, 0.15f, 0.95f);
        [SerializeField] private Color jumpTelegraphFillColor = new Color(1f, 0.6f, 0.2f, 0.24f);

        [Header("Reaction")]
        [SerializeField, Min(0f)] private float damageStunDuration = 0.35f;

        [Header("Audio")]
        [SerializeField] private SfxPlayer sfxPlayer;
        [SerializeField] private AudioProfile attackStartAudioProfile;
        [SerializeField] private AudioProfile attackHitAudioProfile;
        [SerializeField] private AudioProfile jumpTelegraphAudioProfile;
        [SerializeField] private AudioProfile jumpLaunchAudioProfile;
        [SerializeField] private AudioProfile jumpImpactAudioProfile;
        [SerializeField] private AudioProfile chargeTelegraphAudioProfile;
        [SerializeField] private AudioProfile chargeStartAudioProfile;
        [SerializeField] private AudioProfile chargeHitAudioProfile;
        [SerializeField] private AudioProfile damagedAudioProfile;
        [SerializeField] private AudioProfile deathAudioProfile;

        [Header("Movement Blend")]
        [SerializeField, Min(0f)] private float movementDamping = 10f;
        [SerializeField] private bool faceTargetWhileMoving = true;

        [Header("Physics")]
        [SerializeField] private bool lockRigidBodyMotion = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("UI")]
        [SerializeField] private string bossDisplayName = "BOSS";

        private float nextAttackTime;
        private float nextSpecialAttackTime;
        private float nextJumpTime;
        private float nextChargeTime;
        private float nextRepathTime;
        private float damageStunEndTime;
        private float attackEndTime;
        private float jumpLaunchTime;
        private float jumpTelegraphEndTime;
        private float jumpEndTime;
        private float chargeTelegraphEndTime;
        private float chargeEndTime;
        private float remainingChargeDistance;
        private float baseChargeSpeed;
        private bool isDead;
        private bool isAttacking;
        private bool isJumpTelegraphing;
        private bool isJumping;
        private bool isJumpLandingRecovery;
        private bool isChargeTelegraphing;
        private bool isCharging;
        private bool hasAppliedJumpImpact;
        private bool hasAppliedChargeDamage;
        private bool hasCachedBaseSpeed;
        private Vector2 currentBlend;
        private Vector3 chargeDirection;
        private Vector3 chargeStartPosition;
        private Vector3 chargeLockedTargetPosition;
        private Vector3 jumpStartPosition;
        private Vector3 jumpLandingPosition;
        private LineRenderer chargeTelegraphRenderer;
        private MeshFilter chargeTelegraphFillFilter;
        private MeshRenderer chargeTelegraphFillRenderer;
        private Mesh chargeTelegraphFillMesh;
        private Material chargeTelegraphMaterial;
        private Material chargeTelegraphFillMaterial;
        private LineRenderer jumpTelegraphRenderer;
        private MeshFilter jumpTelegraphFillFilter;
        private MeshRenderer jumpTelegraphFillRenderer;
        private Mesh jumpTelegraphFillMesh;
        private Material jumpTelegraphMaterial;
        private Material jumpTelegraphFillMaterial;
        private readonly Collider[] jumpImpactResults = new Collider[32];
        private readonly HashSet<PlayerHealth> jumpDamagedPlayers = new HashSet<PlayerHealth>();
        private readonly HashSet<Rigidbody> jumpAffectedRigidbodies = new HashSet<Rigidbody>();

        public EnemyHealth Health => health;
        public bool IsDead => isDead;
        public string BossDisplayName => string.IsNullOrWhiteSpace(bossDisplayName) ? gameObject.name : bossDisplayName;

        public void ApplySpeedMultiplier(float multiplier)
        {
            CacheBaseSpeedValues();
            chargeSpeed = Mathf.Max(0.01f, baseChargeSpeed * Mathf.Max(0.01f, multiplier));
        }

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            body = GetComponent<Rigidbody>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (attackOrigin == null)
            {
                attackOrigin = transform;
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

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            if (agent != null)
            {
                agent.updateRotation = false;
            }

            AssignAudioReferences();
            ConfigureRigidBody();
            CacheBaseSpeedValues();
        }

        private void OnEnable()
        {
            ConfigureRigidBody();

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

            CancelAttack();
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

        private void LateUpdate()
        {
            StabilizeRigidBody();
        }

        private void Update()
        {
            if (isDead)
            {
                return;
            }

            RefreshTarget();

            if (isJumping)
            {
                UpdateJumpAttack();
                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            if (isJumpLandingRecovery)
            {
                if (target != null)
                {
                    FaceTarget();
                }

                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            if (isJumpTelegraphing)
            {
                UpdateJumpTelegraph();
                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            if (isCharging)
            {
                UpdateCharge();
                UpdateMoveAnimation(chargeDirection * chargeSpeed);
                return;
            }

            if (isChargeTelegraphing)
            {
                UpdateChargeTelegraph();
                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            if (target == null)
            {
                StopMoving();
                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget > forgetRange)
            {
                target = null;
                StopMoving();
                UpdateMoveAnimation(Vector3.zero);
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
                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            if (Time.time < damageStunEndTime)
            {
                StopMoving();
                FaceTarget();
                UpdateMoveAnimation(Vector3.zero);
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

                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            if (CanStartJumpAttack(distanceToTarget))
            {
                BeginJumpTelegraph();
                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            if (CanStartCharge(distanceToTarget))
            {
                BeginChargeTelegraph();
                UpdateMoveAnimation(Vector3.zero);
                return;
            }

            Vector3 desiredVelocity = Vector3.zero;
            if (agent != null && agent.isOnNavMesh)
            {
                if (Time.time >= nextRepathTime)
                {
                    nextRepathTime = Time.time + repathInterval;
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                }

                desiredVelocity = agent.desiredVelocity;
            }
            else
            {
                StopMoving();
            }

            if (faceTargetWhileMoving)
            {
                FaceTarget();
            }

            UpdateMoveAnimation(desiredVelocity);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
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

        public void AnimationEventJumpAttackFinished()
        {
            if (!isJumpLandingRecovery)
            {
                return;
            }

            isJumpLandingRecovery = false;
            RestoreAgentAfterJump(jumpLandingPosition);
            LogDebug("AnimationEventJumpAttackFinished called.");
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

            if (health == null || !health.LastDamageWasHeadshot)
            {
                return;
            }

            damageStunEndTime = Mathf.Max(damageStunEndTime, Time.time + damageStunDuration);
            CancelAttack();
            StopMoving();
            PlaySfx(damagedAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, DamageTriggerParameter);
            UpdateMoveAnimation(Vector3.zero);
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
            currentBlend = Vector2.zero;
            PlaySfx(deathAudioProfile);
            AnimatorHelper.SetFloatIfExists(animator, MoveXParameter, 0f);
            AnimatorHelper.SetFloatIfExists(animator, MoveYParameter, 0f);
            AnimatorHelper.SetBoolIfExists(animator, DeadBoolParameter, true);
            AnimatorHelper.SetTriggerIfExists(animator, DeathTriggerParameter);
        }

        private void BeginAttack()
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;
            attackEndTime = Time.time + attackAnimationTimeout;
            StopMoving();
            FaceTarget();
            PlaySfx(attackStartAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, AttackTriggerParameter);
            LogDebug("Attack started.");
        }

        private void CancelAttack()
        {
            isAttacking = false;
            isJumpTelegraphing = false;
            isJumping = false;
            isJumpLandingRecovery = false;
            isChargeTelegraphing = false;
            isCharging = false;
            remainingChargeDistance = 0f;
            hasAppliedJumpImpact = false;
            hasAppliedChargeDamage = false;
            chargeStartPosition = transform.position;
            chargeLockedTargetPosition = transform.position;
            jumpStartPosition = transform.position;
            jumpLandingPosition = transform.position;
            jumpLaunchTime = 0f;
            RestoreAgentAfterJump(transform.position);
            HideChargeTelegraph();
            HideJumpTelegraph();
        }

        private void TryApplyAttackDamage()
        {
            if (target == null)
            {
                LogDebug("Attack canceled. Target is null.");
                return;
            }

            float attackTolerance = agent != null ? agent.radius : 0f;
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
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
            Vector3 hitNormal = (target.position - hitOrigin).sqrMagnitude > 0.0001f
                ? (target.position - hitOrigin).normalized
                : transform.forward;

            float previousHealth = playerHealth.CurrentHealth;
            playerHealth.ApplyDamage(attackDamage, hitOrigin, hitNormal, gameObject);
            PlaySfx(attackHitAudioProfile);
            LogDebug($"Applied {attackDamage:0.##} damage to {playerHealth.name}. HP {previousHealth:0.##} -> {playerHealth.CurrentHealth:0.##}");
        }

        private bool CanStartJumpAttack(float distanceToTarget)
        {
            if (!enableJumpAttack)
            {
                return false;
            }

            if (Time.time < nextSpecialAttackTime)
            {
                return false;
            }

            if (target == null || Time.time < nextJumpTime)
            {
                return false;
            }

            if (distanceToTarget < jumpMinRange || distanceToTarget > jumpTriggerRange)
            {
                return false;
            }

            return jumpLandingRadius > 0f && jumpAnimationDuration > 0f;
        }

        private void BeginJumpTelegraph()
        {
            if (target == null)
            {
                return;
            }

            isJumpTelegraphing = true;
            nextJumpTime = Time.time + jumpCooldown;
            jumpTelegraphEndTime = Time.time + Mathf.Max(jumpTelegraphDuration, jumpAnimationDuration);
            jumpLaunchTime = jumpTelegraphEndTime - jumpAnimationDuration;
            jumpLandingPosition = ResolvePredictedJumpLandingPosition();
            StopMoving();
            FaceTarget();
            PlaySfx(jumpTelegraphAudioProfile);
            ShowJumpTelegraph(jumpLandingPosition, 0f);
            LogDebug("Jump telegraph started.");
        }

        private void UpdateJumpTelegraph()
        {
            if (!enableJumpAttack || target == null)
            {
                CancelAttack();
                return;
            }

            StopMoving();
            FaceTarget();
            jumpLandingPosition = ResolvePredictedJumpLandingPosition();
            ShowJumpTelegraph(jumpLandingPosition, GetJumpTelegraphProgress());

            if (!isJumping && Time.time >= jumpLaunchTime)
            {
                BeginJumpAttack();
            }
        }

        private void BeginJumpAttack()
        {
            jumpStartPosition = transform.position;
            jumpLandingPosition = ResolvePredictedJumpLandingPosition();
            jumpLandingPosition.y = jumpStartPosition.y;

            isJumping = true;
            nextSpecialAttackTime = Time.time + specialAttackGlobalCooldown;
            hasAppliedJumpImpact = false;
            jumpEndTime = jumpTelegraphEndTime;

            StopMoving();
            DisableAgentForJump();

            Vector3 jumpDirection = jumpLandingPosition - jumpStartPosition;
            jumpDirection.y = 0f;
            FaceDirection(jumpDirection.sqrMagnitude > 0.0001f ? jumpDirection : GetPlanarForward());
            PlaySfx(jumpLaunchAudioProfile);
            AnimatorHelper.SetTriggerIfExists(animator, JumpAttackTriggerParameter);
            LogDebug("Jump attack started.");
        }

        private void UpdateJumpAttack()
        {
            if (!enableJumpAttack || jumpAnimationDuration <= 0f)
            {
                FinishJumpAttack();
                return;
            }

            float progress = 1f - Mathf.Clamp01((jumpEndTime - Time.time) / jumpAnimationDuration);
            Vector3 planarPosition = Vector3.Lerp(jumpStartPosition, jumpLandingPosition, progress);
            float jumpHeight = 4f * jumpArcHeight * progress * (1f - progress);
            Vector3 jumpPosition = planarPosition + (Vector3.up * jumpHeight);
            transform.position = jumpPosition;
            ShowJumpTelegraph(jumpLandingPosition, GetJumpTelegraphProgress());

            Vector3 jumpDirection = jumpLandingPosition - jumpStartPosition;
            jumpDirection.y = 0f;
            FaceDirection(jumpDirection.sqrMagnitude > 0.0001f ? jumpDirection : GetPlanarForward());

            if (progress >= 1f || Time.time >= jumpEndTime)
            {
                FinishJumpAttack();
            }
        }

        private void FinishJumpAttack()
        {
            if (!isJumping)
            {
                return;
            }

            isJumpTelegraphing = false;
            isJumping = false;
            isJumpLandingRecovery = true;
            transform.position = jumpLandingPosition;
            ApplyJumpImpact();
            HideJumpTelegraph();
            LogDebug("Jump attack finished.");
        }

        private void ApplyJumpImpact()
        {
            if (hasAppliedJumpImpact)
            {
                return;
            }

            hasAppliedJumpImpact = true;
            jumpDamagedPlayers.Clear();
            jumpAffectedRigidbodies.Clear();
            PlaySfx(jumpImpactAudioProfile);

            int hitCount = Physics.OverlapSphereNonAlloc(
                jumpLandingPosition,
                jumpLandingRadius,
                jumpImpactResults,
                jumpImpactMask,
                jumpImpactTriggerInteraction);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = jumpImpactResults[hitIndex];
                if (hitCollider == null)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                PlayerHealth playerHealth = hitCollider.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null && jumpDamagedPlayers.Add(playerHealth))
                {
                    Vector3 hitNormal = playerHealth.transform.position - jumpLandingPosition;
                    hitNormal.y = 0f;
                    if (hitNormal.sqrMagnitude <= 0.0001f)
                    {
                        hitNormal = transform.forward;
                    }

                    playerHealth.ApplyDamage(jumpDamage, jumpLandingPosition, hitNormal.normalized, gameObject);
                }

                Rigidbody hitRigidbody = hitCollider.attachedRigidbody;
                if (hitRigidbody != null && jumpAffectedRigidbodies.Add(hitRigidbody))
                {
                    hitRigidbody.AddExplosionForce(jumpShockwaveForce, jumpLandingPosition, jumpLandingRadius, jumpShockwaveUpwardsModifier, ForceMode.Impulse);
                }
            }
        }

        private bool CanStartCharge(float distanceToTarget)
        {
            if (!enableChargeAttack)
            {
                return false;
            }

            if (Time.time < nextSpecialAttackTime)
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

            return chargeDistance > 0f && chargeWidth > 0f && chargeSpeed > 0f;
        }

        private void BeginChargeTelegraph()
        {
            Vector3 telegraphDirection = ResolveChargeDirection();
            if (telegraphDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            isChargeTelegraphing = true;
            nextChargeTime = Time.time + chargeCooldown;
            chargeTelegraphEndTime = Time.time + chargeTelegraphDuration;
            StopMoving();
            FaceDirection(telegraphDirection);
            PlaySfx(chargeTelegraphAudioProfile);
            ShowChargeTelegraph(telegraphDirection, 0f);
            LogDebug("Charge telegraph started.");
        }

        private void UpdateChargeTelegraph()
        {
            if (!enableChargeAttack)
            {
                CancelAttack();
                return;
            }

            if (target == null)
            {
                CancelAttack();
                return;
            }

            StopMoving();
            Vector3 telegraphDirection = ResolveChargeDirection();
            if (telegraphDirection.sqrMagnitude <= 0.0001f)
            {
                CancelAttack();
                return;
            }

            FaceTarget();
            ShowChargeTelegraph(telegraphDirection, GetChargeTelegraphProgress());

            if (Time.time >= chargeTelegraphEndTime)
            {
                BeginCharge();
            }
        }

        private void BeginCharge()
        {
            chargeStartPosition = transform.position;
            chargeLockedTargetPosition = ResolveChargeLockedTargetPosition();

            Vector3 lockedChargeVector = chargeLockedTargetPosition - chargeStartPosition;
            lockedChargeVector.y = 0f;

            chargeDirection = lockedChargeVector.sqrMagnitude > 0.0001f
                ? lockedChargeVector.normalized
                : GetPlanarForward();

            isChargeTelegraphing = false;
            isCharging = true;
            nextSpecialAttackTime = Time.time + specialAttackGlobalCooldown;
            hasAppliedChargeDamage = false;
            remainingChargeDistance = ResolveChargeTravelDistance(chargeStartPosition, chargeLockedTargetPosition);
            chargeEndTime = Time.time + (remainingChargeDistance / Mathf.Max(0.01f, chargeSpeed)) + 0.05f;

            StopMoving();
            HideChargeTelegraph();
            FaceDirection(chargeDirection);
            PlaySfx(chargeStartAudioProfile);
            LogDebug("Charge started.");
        }

        private void UpdateCharge()
        {
            if (!enableChargeAttack || chargeSpeed <= 0f || chargeDistance <= 0f)
            {
                FinishCharge();
                return;
            }

            StopMoving();
            FaceDirection(chargeDirection);

            Vector3 startPosition = transform.position;
            float stepDistance = Mathf.Min(chargeSpeed * Time.deltaTime, remainingChargeDistance);
            if (stepDistance <= 0.0001f)
            {
                FinishCharge();
                return;
            }

            MoveAlongCharge(chargeDirection * stepDistance);

            Vector3 endPosition = transform.position;
            float traveledDistance = Vector3.Distance(ProjectPlanar(startPosition), ProjectPlanar(endPosition));
            if (traveledDistance <= 0.0001f)
            {
                FinishCharge();
                return;
            }

            TryApplyChargeDamage(startPosition, endPosition);
            remainingChargeDistance = Mathf.Max(0f, remainingChargeDistance - traveledDistance);

            if (remainingChargeDistance <= 0.001f || Time.time >= chargeEndTime)
            {
                FinishCharge();
            }
        }

        private void MoveAlongCharge(Vector3 delta)
        {
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.Move(delta);
                return;
            }

            transform.position += delta;
        }

        private void FinishCharge()
        {
            isCharging = false;
            remainingChargeDistance = 0f;
            hasAppliedChargeDamage = false;
            HideChargeTelegraph();
            LogDebug("Charge finished.");
        }

        private void TryApplyChargeDamage(Vector3 startPosition, Vector3 endPosition)
        {
            if (hasAppliedChargeDamage || target == null)
            {
                return;
            }

            PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || playerHealth.IsDead)
            {
                return;
            }

            float hitRadius = (chargeWidth * 0.5f) + chargeHitPadding;
            float distanceToPath = DistanceToPlanarSegment(playerHealth.transform.position, startPosition, endPosition);
            if (distanceToPath > hitRadius)
            {
                return;
            }

            Vector3 hitOrigin = attackOrigin != null ? attackOrigin.position : transform.position;
            Vector3 hitNormal = chargeDirection.sqrMagnitude > 0.0001f ? chargeDirection : transform.forward;
            float previousHealth = playerHealth.CurrentHealth;
            playerHealth.ApplyDamage(chargeDamage, hitOrigin, hitNormal, gameObject);
            hasAppliedChargeDamage = true;
            PlaySfx(chargeHitAudioProfile);
            LogDebug($"Applied {chargeDamage:0.##} charge damage to {playerHealth.name}. HP {previousHealth:0.##} -> {playerHealth.CurrentHealth:0.##}");
        }

        private void AssignAudioReferences()
        {
            // 보스는 여러 패턴이 한 오브젝트에서 재생되므로 공통 SFX 재생기를 하나로 유지한다.
            if (sfxPlayer != null)
            {
                return;
            }

            sfxPlayer = GetComponentInChildren<SfxPlayer>();
            if (sfxPlayer != null)
            {
                return;
            }

            GameObject sfxObject = new GameObject("Boss SFX Player");
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
            FaceDirection(direction);
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, faceTargetSpeed * Time.deltaTime);
        }

        private Vector3 ResolveChargeDirection()
        {
            if (target == null)
            {
                return GetPlanarForward();
            }

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : GetPlanarForward();
        }

        private Vector3 ResolveChargeLockedTargetPosition()
        {
            if (target == null)
            {
                return transform.position + (GetPlanarForward() * chargeDistance);
            }

            Vector3 lockedTargetPosition = target.position;
            lockedTargetPosition.y = transform.position.y;
            return lockedTargetPosition;
        }

        private Vector3 ResolvePredictedJumpLandingPosition()
        {
            if (target == null)
            {
                return transform.position + (GetPlanarForward() * jumpTriggerRange);
            }

            Vector3 predictedLandingPosition = target.position;
            HyperFpsFirstPersonController controller = target.GetComponentInParent<HyperFpsFirstPersonController>();
            if (controller != null && jumpPredictionTime > 0f)
            {
                predictedLandingPosition += controller.Velocity * jumpPredictionTime;
            }

            predictedLandingPosition.y = transform.position.y;
            Vector3 landingOffset = predictedLandingPosition - transform.position;
            landingOffset.y = 0f;

            float landingDistance = landingOffset.magnitude;
            if (landingDistance > jumpTriggerRange && landingDistance > 0.0001f)
            {
                predictedLandingPosition = transform.position + (landingOffset.normalized * jumpTriggerRange);
                predictedLandingPosition.y = transform.position.y;
            }

            if (agent != null)
            {
                int areaMask = agent.enabled ? agent.areaMask : NavMesh.AllAreas;
                if (NavMesh.SamplePosition(predictedLandingPosition, out NavMeshHit navMeshHit, Mathf.Max(1f, jumpLandingRadius), areaMask))
                {
                    predictedLandingPosition = navMeshHit.position;
                    predictedLandingPosition.y = transform.position.y;
                }
            }

            return predictedLandingPosition;
        }

        private float ResolveChargeTravelDistance(Vector3 startPosition, Vector3 lockedTargetPosition)
        {
            Vector3 planarStartPosition = ProjectPlanar(startPosition);
            Vector3 planarLockedTargetPosition = ProjectPlanar(lockedTargetPosition);
            float lockedDistance = Vector3.Distance(planarStartPosition, planarLockedTargetPosition);

            if (lockedDistance <= 0.0001f)
            {
                return chargeDistance;
            }

            return Mathf.Min(chargeDistance, lockedDistance);
        }

        private void DisableAgentForJump()
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        private void RestoreAgentAfterJump(Vector3 position)
        {
            if (agent == null)
            {
                return;
            }

            Vector3 groundedPosition = position;
            groundedPosition.y = jumpStartPosition.y;
            transform.position = groundedPosition;

            if (!agent.enabled)
            {
                agent.enabled = true;
                agent.updateRotation = false;
            }

            int areaMask = agent.areaMask;
            if (NavMesh.SamplePosition(groundedPosition, out NavMeshHit navMeshHit, Mathf.Max(1f, jumpLandingRadius), areaMask))
            {
                groundedPosition = navMeshHit.position;
                transform.position = groundedPosition;
            }

            if (agent.isOnNavMesh)
            {
                agent.Warp(groundedPosition);
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private Vector3 GetPlanarForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private void ShowChargeTelegraph(Vector3 direction)
        {
            ShowChargeTelegraph(direction, 1f);
        }

        private void ShowJumpTelegraph(Vector3 center, float progress)
        {
            if (!enableJumpAttack)
            {
                return;
            }

            progress = Mathf.Clamp01(progress);

            EnsureJumpTelegraphRenderer();
            EnsureJumpTelegraphFillRenderer();
            if (jumpTelegraphRenderer == null)
            {
                return;
            }

            int segmentCount = Mathf.Max(8, jumpTelegraphSegments);
            float angleStep = Mathf.PI * 2f / segmentCount;
            Vector3 circleCenter = center;
            circleCenter.y += jumpTelegraphVerticalOffset;

            jumpTelegraphRenderer.enabled = true;
            jumpTelegraphRenderer.startColor = jumpTelegraphColor;
            jumpTelegraphRenderer.endColor = jumpTelegraphColor;
            jumpTelegraphRenderer.startWidth = jumpTelegraphLineWidth;
            jumpTelegraphRenderer.endWidth = jumpTelegraphLineWidth;
            jumpTelegraphRenderer.positionCount = segmentCount + 1;

            for (int segmentIndex = 0; segmentIndex <= segmentCount; segmentIndex++)
            {
                float angle = angleStep * segmentIndex;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * jumpLandingRadius;
                jumpTelegraphRenderer.SetPosition(segmentIndex, circleCenter + offset);
            }

            UpdateJumpTelegraphFill(circleCenter, progress, segmentCount);
        }

        private void HideJumpTelegraph()
        {
            if (jumpTelegraphRenderer != null)
            {
                jumpTelegraphRenderer.enabled = false;
            }

            if (jumpTelegraphFillRenderer != null)
            {
                jumpTelegraphFillRenderer.enabled = false;
            }
        }

        private void EnsureJumpTelegraphRenderer()
        {
            if (jumpTelegraphRenderer != null)
            {
                return;
            }

            GameObject telegraphObject = new GameObject("Jump Telegraph");
            telegraphObject.transform.SetParent(transform, false);

            jumpTelegraphRenderer = telegraphObject.AddComponent<LineRenderer>();
            jumpTelegraphRenderer.useWorldSpace = true;
            jumpTelegraphRenderer.loop = false;
            jumpTelegraphRenderer.alignment = LineAlignment.View;
            jumpTelegraphRenderer.textureMode = LineTextureMode.Stretch;
            jumpTelegraphRenderer.numCornerVertices = 2;
            jumpTelegraphRenderer.numCapVertices = 2;
            jumpTelegraphRenderer.enabled = false;

            if (jumpTelegraphMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    jumpTelegraphMaterial = new Material(shader);
                }
            }

            if (jumpTelegraphMaterial != null)
            {
                jumpTelegraphMaterial.color = jumpTelegraphColor;
                jumpTelegraphRenderer.sharedMaterial = jumpTelegraphMaterial;
            }
        }

        private void EnsureJumpTelegraphFillRenderer()
        {
            if (jumpTelegraphFillRenderer != null && jumpTelegraphFillFilter != null)
            {
                return;
            }

            GameObject fillObject = new GameObject("Jump Telegraph Fill");
            fillObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            fillObject.transform.localScale = Vector3.one;

            jumpTelegraphFillFilter = fillObject.AddComponent<MeshFilter>();
            jumpTelegraphFillRenderer = fillObject.AddComponent<MeshRenderer>();
            jumpTelegraphFillRenderer.enabled = false;

            if (jumpTelegraphFillMesh == null)
            {
                jumpTelegraphFillMesh = new Mesh
                {
                    name = "JumpTelegraphFill"
                };
                jumpTelegraphFillMesh.MarkDynamic();
            }

            jumpTelegraphFillFilter.sharedMesh = jumpTelegraphFillMesh;

            if (jumpTelegraphFillMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    jumpTelegraphFillMaterial = new Material(shader);
                }
            }

            if (jumpTelegraphFillMaterial != null)
            {
                jumpTelegraphFillMaterial.color = jumpTelegraphFillColor;
                jumpTelegraphFillRenderer.sharedMaterial = jumpTelegraphFillMaterial;
            }
        }

        private void UpdateJumpTelegraphFill(Vector3 center, float progress, int segmentCount)
        {
            if (jumpTelegraphFillRenderer == null || jumpTelegraphFillFilter == null || jumpTelegraphFillMesh == null)
            {
                return;
            }

            if (progress <= 0.0001f)
            {
                jumpTelegraphFillRenderer.enabled = false;
                return;
            }

            float fillRadius = Mathf.Max(0.1f, jumpLandingRadius * progress);
            Vector3[] vertices = new Vector3[segmentCount + 2];
            int[] triangles = new int[segmentCount * 3];
            Vector2[] uvs = new Vector2[vertices.Length];
            float angleStep = Mathf.PI * 2f / segmentCount;

            vertices[0] = center;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int segmentIndex = 0; segmentIndex <= segmentCount; segmentIndex++)
            {
                float angle = angleStep * segmentIndex;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * fillRadius;
                vertices[segmentIndex + 1] = center + offset;
                uvs[segmentIndex + 1] = new Vector2((offset.x / (jumpLandingRadius * 2f)) + 0.5f, (offset.z / (jumpLandingRadius * 2f)) + 0.5f);

                if (segmentIndex == segmentCount)
                {
                    continue;
                }

                int triangleIndex = segmentIndex * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = segmentIndex + 1;
                triangles[triangleIndex + 2] = segmentIndex + 2;
            }

            jumpTelegraphFillMesh.Clear();
            jumpTelegraphFillMesh.vertices = vertices;
            jumpTelegraphFillMesh.triangles = triangles;
            jumpTelegraphFillMesh.uv = uvs;
            jumpTelegraphFillMesh.RecalculateBounds();
            jumpTelegraphFillMesh.RecalculateNormals();

            if (jumpTelegraphFillMaterial != null)
            {
                Color fillColor = jumpTelegraphFillColor;
                fillColor.a *= Mathf.Lerp(0.35f, 1f, progress);
                jumpTelegraphFillMaterial.color = fillColor;
            }

            jumpTelegraphFillRenderer.enabled = true;
        }

        private float GetJumpTelegraphProgress()
        {
            if (jumpTelegraphDuration <= 0.0001f)
            {
                return 1f;
            }

            float remaining = Mathf.Max(0f, jumpTelegraphEndTime - Time.time);
            return 1f - Mathf.Clamp01(remaining / jumpTelegraphDuration);
        }

        private void ShowChargeTelegraph(Vector3 direction, float progress)
        {
            if (!enableChargeAttack)
            {
                return;
            }

            progress = Mathf.Clamp01(progress);

            EnsureChargeTelegraphRenderer();
            EnsureChargeTelegraphFillRenderer();
            if (chargeTelegraphRenderer == null)
            {
                return;
            }

            chargeTelegraphRenderer.enabled = true;
            chargeTelegraphRenderer.startColor = chargeTelegraphColor;
            chargeTelegraphRenderer.endColor = chargeTelegraphColor;
            chargeTelegraphRenderer.startWidth = chargeTelegraphLineWidth;
            chargeTelegraphRenderer.endWidth = chargeTelegraphLineWidth;
            chargeTelegraphRenderer.positionCount = 5;

            Vector3 planarDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : GetPlanarForward();
            Vector3 right = Vector3.Cross(Vector3.up, planarDirection).normalized;
            Vector3 origin = transform.position;
            origin.y += chargeTelegraphVerticalOffset;

            float halfWidth = chargeWidth * 0.5f;
            Vector3 endCenter = origin + (planarDirection * chargeDistance);

            chargeTelegraphRenderer.SetPosition(0, origin - (right * halfWidth));
            chargeTelegraphRenderer.SetPosition(1, endCenter - (right * halfWidth));
            chargeTelegraphRenderer.SetPosition(2, endCenter + (right * halfWidth));
            chargeTelegraphRenderer.SetPosition(3, origin + (right * halfWidth));
            chargeTelegraphRenderer.SetPosition(4, origin - (right * halfWidth));

            UpdateChargeTelegraphFill(origin, planarDirection, right, halfWidth, progress);
        }

        private void HideChargeTelegraph()
        {
            if (chargeTelegraphRenderer != null)
            {
                chargeTelegraphRenderer.enabled = false;
            }

            if (chargeTelegraphFillRenderer != null)
            {
                chargeTelegraphFillRenderer.enabled = false;
            }
        }

        private void EnsureChargeTelegraphRenderer()
        {
            if (chargeTelegraphRenderer != null)
            {
                return;
            }

            GameObject telegraphObject = new GameObject("Charge Telegraph");
            telegraphObject.transform.SetParent(transform, false);

            chargeTelegraphRenderer = telegraphObject.AddComponent<LineRenderer>();
            chargeTelegraphRenderer.useWorldSpace = true;
            chargeTelegraphRenderer.loop = false;
            chargeTelegraphRenderer.alignment = LineAlignment.View;
            chargeTelegraphRenderer.textureMode = LineTextureMode.Stretch;
            chargeTelegraphRenderer.numCornerVertices = 2;
            chargeTelegraphRenderer.numCapVertices = 2;
            chargeTelegraphRenderer.enabled = false;

            if (chargeTelegraphMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    chargeTelegraphMaterial = new Material(shader);
                }
            }

            if (chargeTelegraphMaterial != null)
            {
                chargeTelegraphMaterial.color = chargeTelegraphColor;
                chargeTelegraphRenderer.sharedMaterial = chargeTelegraphMaterial;
            }
        }

        private void EnsureChargeTelegraphFillRenderer()
        {
            if (chargeTelegraphFillRenderer != null && chargeTelegraphFillFilter != null)
            {
                return;
            }

            GameObject fillObject = new GameObject("Charge Telegraph Fill");
            fillObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            fillObject.transform.localScale = Vector3.one;

            chargeTelegraphFillFilter = fillObject.AddComponent<MeshFilter>();
            chargeTelegraphFillRenderer = fillObject.AddComponent<MeshRenderer>();
            chargeTelegraphFillRenderer.enabled = false;

            if (chargeTelegraphFillMesh == null)
            {
                chargeTelegraphFillMesh = new Mesh
                {
                    name = "ChargeTelegraphFill"
                };
                chargeTelegraphFillMesh.MarkDynamic();
            }

            chargeTelegraphFillFilter.sharedMesh = chargeTelegraphFillMesh;

            if (chargeTelegraphFillMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    chargeTelegraphFillMaterial = new Material(shader);
                }
            }

            if (chargeTelegraphFillMaterial != null)
            {
                chargeTelegraphFillMaterial.color = chargeTelegraphFillColor;
                chargeTelegraphFillRenderer.sharedMaterial = chargeTelegraphFillMaterial;
            }
        }

        private void UpdateChargeTelegraphFill(Vector3 origin, Vector3 direction, Vector3 right, float halfWidth, float progress)
        {
            if (chargeTelegraphFillRenderer == null || chargeTelegraphFillFilter == null || chargeTelegraphFillMesh == null)
            {
                return;
            }

            if (progress <= 0.0001f)
            {
                chargeTelegraphFillRenderer.enabled = false;
                return;
            }

            float filledDistance = Mathf.Max(0.05f, chargeDistance * progress);
            Vector3 fillEndCenter = origin + (direction * filledDistance);

            Vector3[] vertices =
            {
                origin - (right * halfWidth),
                fillEndCenter - (right * halfWidth),
                fillEndCenter + (right * halfWidth),
                origin + (right * halfWidth)
            };

            int[] triangles = { 0, 1, 2, 0, 2, 3 };
            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(progress, 0f),
                new Vector2(progress, 1f),
                new Vector2(0f, 1f)
            };

            chargeTelegraphFillMesh.Clear();
            chargeTelegraphFillMesh.vertices = vertices;
            chargeTelegraphFillMesh.triangles = triangles;
            chargeTelegraphFillMesh.uv = uvs;
            chargeTelegraphFillMesh.RecalculateBounds();
            chargeTelegraphFillMesh.RecalculateNormals();

            if (chargeTelegraphFillMaterial != null)
            {
                Color fillColor = chargeTelegraphFillColor;
                fillColor.a *= Mathf.Lerp(0.35f, 1f, progress);
                chargeTelegraphFillMaterial.color = fillColor;
            }

            chargeTelegraphFillRenderer.enabled = true;
        }

        private float GetChargeTelegraphProgress()
        {
            if (chargeTelegraphDuration <= 0.0001f)
            {
                return 1f;
            }

            float remaining = Mathf.Max(0f, chargeTelegraphEndTime - Time.time);
            return 1f - Mathf.Clamp01(remaining / chargeTelegraphDuration);
        }

        private static Vector3 ProjectPlanar(Vector3 position)
        {
            position.y = 0f;
            return position;
        }

        private static float DistanceToPlanarSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            point = ProjectPlanar(point);
            start = ProjectPlanar(start);
            end = ProjectPlanar(end);

            Vector3 segment = end - start;
            if (segment.sqrMagnitude <= 0.0001f)
            {
                return Vector3.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude);
            Vector3 closestPoint = start + (segment * t);
            return Vector3.Distance(point, closestPoint);
        }

        private void UpdateMoveAnimation(Vector3 desiredVelocity)
        {
            if (animator == null)
            {
                return;
            }

            Vector2 targetBlend = Vector2.zero;
            if (desiredVelocity.sqrMagnitude > 0.0001f && agent != null && agent.speed > 0.001f)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(desiredVelocity);
                targetBlend.x = Mathf.Clamp(localVelocity.x / agent.speed, -1f, 1f);
                targetBlend.y = Mathf.Clamp(localVelocity.z / agent.speed, -1f, 1f);
            }

            float blendLerp = movementDamping > 0f ? 1f - Mathf.Exp(-movementDamping * Time.deltaTime) : 1f;
            currentBlend = Vector2.Lerp(currentBlend, targetBlend, blendLerp);

            AnimatorHelper.SetFloatIfExists(animator, MoveXParameter, currentBlend.x);
            AnimatorHelper.SetFloatIfExists(animator, MoveYParameter, currentBlend.y);
        }

        private void ConfigureRigidBody()
        {
            if (!lockRigidBodyMotion || body == null)
            {
                return;
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void StabilizeRigidBody()
        {
            if (!lockRigidBodyMotion || body == null)
            {
                return;
            }

            if (!body.isKinematic)
            {
                body.isKinematic = true;
            }

            if (body.useGravity)
            {
                body.useGravity = false;
            }

            if (body.linearVelocity.sqrMagnitude > 0f)
            {
                body.linearVelocity = Vector3.zero;
            }

            if (body.angularVelocity.sqrMagnitude > 0f)
            {
                body.angularVelocity = Vector3.zero;
            }
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[BossNavMeshController:{name}] {message}", this);
            }
        }

        private void OnDestroy()
        {
            if (chargeTelegraphFillRenderer != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(chargeTelegraphFillRenderer.gameObject);
                }
                else
                {
                    DestroyImmediate(chargeTelegraphFillRenderer.gameObject);
                }
            }

            if (jumpTelegraphFillRenderer != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(jumpTelegraphFillRenderer.gameObject);
                }
                else
                {
                    DestroyImmediate(jumpTelegraphFillRenderer.gameObject);
                }
            }

            if (Application.isPlaying)
            {
                if (chargeTelegraphMaterial != null)
                {
                    Destroy(chargeTelegraphMaterial);
                }

                if (chargeTelegraphFillMaterial != null)
                {
                    Destroy(chargeTelegraphFillMaterial);
                }

                if (chargeTelegraphFillMesh != null)
                {
                    Destroy(chargeTelegraphFillMesh);
                }

                if (jumpTelegraphMaterial != null)
                {
                    Destroy(jumpTelegraphMaterial);
                }

                if (jumpTelegraphFillMaterial != null)
                {
                    Destroy(jumpTelegraphFillMaterial);
                }

                if (jumpTelegraphFillMesh != null)
                {
                    Destroy(jumpTelegraphFillMesh);
                }
            }
            else
            {
                if (chargeTelegraphMaterial != null)
                {
                    DestroyImmediate(chargeTelegraphMaterial);
                }

                if (chargeTelegraphFillMaterial != null)
                {
                    DestroyImmediate(chargeTelegraphFillMaterial);
                }

                if (chargeTelegraphFillMesh != null)
                {
                    DestroyImmediate(chargeTelegraphFillMesh);
                }

                if (jumpTelegraphMaterial != null)
                {
                    DestroyImmediate(jumpTelegraphMaterial);
                }

                if (jumpTelegraphFillMaterial != null)
                {
                    DestroyImmediate(jumpTelegraphFillMaterial);
                }

                if (jumpTelegraphFillMesh != null)
                {
                    DestroyImmediate(jumpTelegraphFillMesh);
                }
            }
        }
    }
}







