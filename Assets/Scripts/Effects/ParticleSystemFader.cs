using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mood.Effects
{
    [AddComponentMenu("MOOD/Effects/Particle System Fader")]
    [DisallowMultipleComponent]
    public sealed class ParticleSystemFader : MonoBehaviour
    {
        private sealed class ParticleState
        {
            public ParticleSystem System;
            public ParticleSystemRenderer Renderer;
            public float BaseRateOverTime;
            public float BaseRateOverDistance;
            public bool BasePlayOnAwake;
            public bool BasePrewarm;
            public bool BaseRendererEnabled;
        }

        [Header("References")]
        [SerializeField] private GameObject[] targetParticleSystems;

        [Header("Fade")]
        [SerializeField, Min(0f)] private float fadeInDuration = 1.25f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 1f;
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private bool playOnAwake;
        [SerializeField] private bool deactivateObjectWhenStopped;
        [SerializeField] private bool forceDisablePlayOnAwake = true;
        [SerializeField] private bool forceDisablePrewarm = true;

        [Header("Fade Out")]
        [SerializeField] private bool stopEmittingOnFadeOutComplete = true;
        [SerializeField] private bool clearParticlesOnStop;
        [SerializeField, Min(0f)] private float extraStopDelay = 0.1f;
        [SerializeField] private bool hideRenderersWhileHidden = true;
        [SerializeField] private bool clearParticlesBeforeFadeIn = true;

        private readonly List<ParticleState> particleStates = new List<ParticleState>(8);
        private Coroutine transitionRoutine;
        private bool hasCachedState;

        private void Reset()
        {
            CacheParticleSystemsFromChildren();
        }

        private void Awake()
        {
            BuildParticleStateCache();

            if (playOnAwake)
            {
                ShowImmediate();
            }
            else
            {
                HideImmediate();
            }
        }

        private void OnDisable()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
        }

        public void FadeIn()
        {
            EnsureActiveForPlayback();
            BeginTransition(true, fadeInDuration, fadeInCurve);
        }

        public void FadeOut()
        {
            BeginTransition(false, fadeOutDuration, fadeOutCurve);
        }

        public void ShowImmediate()
        {
            EnsureActiveForPlayback();
            SetRenderersVisible(true);
            SetEmissionMultiplier(1f);

            for (int index = 0; index < particleStates.Count; index++)
            {
                ParticleSystem particleSystem = particleStates[index].System;
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Play(true);
            }

        }

        public void HideImmediate()
        {
            BuildParticleStateCache();
            SetEmissionMultiplier(0f);

            for (int index = 0; index < particleStates.Count; index++)
            {
                ParticleSystem particleSystem = particleStates[index].System;
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystemStopBehavior stopBehavior = clearParticlesOnStop
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting;

                particleSystem.Stop(true, stopBehavior);
            }

            SetRenderersVisible(false);

            if (deactivateObjectWhenStopped)
            {
                gameObject.SetActive(false);
            }
        }

        private void BeginTransition(bool show, float duration, AnimationCurve curve)
        {
            BuildParticleStateCache();

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(RunTransition(show, duration, curve));
        }

        private IEnumerator RunTransition(bool show, float duration, AnimationCurve curve)
        {
            if (show)
            {
                EnsureActiveForPlayback();
                SetEmissionMultiplier(0f);

                if (clearParticlesBeforeFadeIn)
                {
                    StopAndClearParticles();
                }

                SetRenderersVisible(true);

                for (int index = 0; index < particleStates.Count; index++)
                {
                    ParticleSystem particleSystem = particleStates[index].System;
                    if (particleSystem == null)
                    {
                        continue;
                    }

                    particleSystem.Play(true);
                }
            }

            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.0001f, duration);

            if (duration <= 0f)
            {
                SetEmissionMultiplier(show ? 1f : 0f);
            }
            else
            {
                while (elapsed < safeDuration)
                {
                    elapsed += Time.deltaTime;
                    float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
                    float curveTime = curve != null ? curve.Evaluate(normalizedTime) : normalizedTime;
                    float emissionMultiplier = show ? curveTime : curveTime;
                    SetEmissionMultiplier(Mathf.Clamp01(emissionMultiplier));
                    yield return null;
                }
            }

            SetEmissionMultiplier(show ? 1f : 0f);
            if (!show)
            {
                if (extraStopDelay > 0f)
                {
                    yield return new WaitForSeconds(extraStopDelay);
                }

                for (int index = 0; index < particleStates.Count; index++)
                {
                    ParticleSystem particleSystem = particleStates[index].System;
                    if (particleSystem == null)
                    {
                        continue;
                    }

                    if (stopEmittingOnFadeOutComplete)
                    {
                        ParticleSystemStopBehavior stopBehavior = clearParticlesOnStop
                            ? ParticleSystemStopBehavior.StopEmittingAndClear
                            : ParticleSystemStopBehavior.StopEmitting;
                        particleSystem.Stop(true, stopBehavior);
                    }
                }

                if (deactivateObjectWhenStopped)
                {
                    gameObject.SetActive(false);
                }
            }

            transitionRoutine = null;
        }

        private void BuildParticleStateCache()
        {
            if (hasCachedState && particleStates.Count > 0)
            {
                return;
            }

            particleStates.Clear();

            if (targetParticleSystems == null || targetParticleSystems.Length == 0)
            {
                CacheParticleSystemsFromChildren();
            }

            HashSet<ParticleSystem> uniqueSystems = new HashSet<ParticleSystem>();
            for (int index = 0; index < targetParticleSystems.Length; index++)
            {
                GameObject targetObject = targetParticleSystems[index];
                if (targetObject == null)
                {
                    continue;
                }

                // 인스펙터에서는 GameObject를 받되, 실제 제어는 연결된 ParticleSystem 기준으로 처리한다.
                ParticleSystem particleSystem = targetObject.GetComponent<ParticleSystem>();
                if (particleSystem == null || !uniqueSystems.Add(particleSystem))
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                ParticleSystem.MainModule main = particleSystem.main;
                ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                particleStates.Add(new ParticleState
                {
                    System = particleSystem,
                    Renderer = particleRenderer,
                    BaseRateOverTime = emission.rateOverTimeMultiplier,
                    BaseRateOverDistance = emission.rateOverDistanceMultiplier,
                    BasePlayOnAwake = main.playOnAwake,
                    BasePrewarm = main.prewarm,
                    BaseRendererEnabled = particleRenderer == null || particleRenderer.enabled
                });
            }

            hasCachedState = particleStates.Count > 0;
            ApplyRuntimeParticleOverrides();
        }

        private void ApplyRuntimeParticleOverrides()
        {
            for (int index = 0; index < particleStates.Count; index++)
            {
                ParticleSystem particleSystem = particleStates[index].System;
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                if (forceDisablePlayOnAwake)
                {
                    main.playOnAwake = false;
                }

                if (forceDisablePrewarm)
                {
                    main.prewarm = false;
                }
            }
        }

        private void CacheParticleSystemsFromChildren()
        {
            ParticleSystem[] childParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
            targetParticleSystems = new GameObject[childParticleSystems.Length];

            for (int index = 0; index < childParticleSystems.Length; index++)
            {
                targetParticleSystems[index] = childParticleSystems[index].gameObject;
            }
        }

        private void EnsureActiveForPlayback()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private void SetEmissionMultiplier(float multiplier)
        {
            for (int index = 0; index < particleStates.Count; index++)
            {
                ParticleState particleState = particleStates[index];
                ParticleSystem particleSystem = particleState.System;
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.rateOverTimeMultiplier = particleState.BaseRateOverTime * multiplier;
                emission.rateOverDistanceMultiplier = particleState.BaseRateOverDistance * multiplier;
            }
        }

        private void SetRenderersVisible(bool visible)
        {
            if (!hideRenderersWhileHidden && !visible)
            {
                return;
            }

            for (int index = 0; index < particleStates.Count; index++)
            {
                ParticleSystemRenderer particleRenderer = particleStates[index].Renderer;
                if (particleRenderer == null)
                {
                    continue;
                }

                particleRenderer.enabled = visible
                    ? particleStates[index].BaseRendererEnabled
                    : false;
            }
        }

        private void StopAndClearParticles()
        {
            for (int index = 0; index < particleStates.Count; index++)
            {
                ParticleSystem particleSystem = particleStates[index].System;
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
            }
        }
    }
}
