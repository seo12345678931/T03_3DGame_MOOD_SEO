using UnityEngine;

namespace Mood.Effects
{
    [AddComponentMenu("MOOD/Effects/Broken Light Flicker")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class BrokenLightFlicker : MonoBehaviour
    {
        private enum FlickerState
        {
            Stable,
            Burst
        }

        [Header("References")]
        [SerializeField] private Light targetLight;
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private string emissionColorProperty = "_EmissionColor";
        [SerializeField] private Color emissionColor = new Color(1f, 0.85f, 0.6f);

        [Header("Base Output")]
        [SerializeField, Min(0.01f)] private float baseIntensity = 2f;
        [SerializeField, Min(0.01f)] private float baseRange = 12f;
        [SerializeField, Range(0f, 0.4f)] private float stableIntensityJitter = 0.08f;
        [SerializeField, Range(0f, 0.3f)] private float stableRangeJitter = 0.04f;
        [SerializeField, Min(0.01f)] private float transitionSpeed = 18f;

        [Header("Broken Timing")]
        [SerializeField] private Vector2 stableIntervalRange = new Vector2(1.5f, 4f);
        [SerializeField] private Vector2 burstDurationRange = new Vector2(0.25f, 0.9f);
        [SerializeField] private Vector2 onDurationRange = new Vector2(0.03f, 0.12f);
        [SerializeField] private Vector2 offDurationRange = new Vector2(0.02f, 0.09f);

        [Header("Broken Output")]
        [SerializeField, Range(0f, 1f)] private float burstIntensityMin = 0.12f;
        [SerializeField, Range(0f, 1.2f)] private float burstIntensityMax = 0.85f;
        [SerializeField, Range(0f, 1f)] private float burstRangeMin = 0.2f;
        [SerializeField, Range(0f, 1.2f)] private float burstRangeMax = 0.95f;
        [SerializeField, Range(0f, 1f)] private float fullyOffChance = 0.4f;
        [SerializeField, Range(0f, 1f)] private float harshFlashChance = 0.16f;
        [SerializeField, Range(1f, 2.5f)] private float harshFlashMultiplier = 1.3f;

        [Header("Emissive Mesh")]
        [SerializeField] private bool syncEmissiveRenderers;
        [SerializeField, Min(0f)] private float emissiveOnMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float emissiveOffMultiplier = 0.05f;

        [Header("Playback")]
        [SerializeField] private bool randomizeOnEnable = true;

        private readonly MaterialPropertyBlock emissivePropertyBlock = new MaterialPropertyBlock();

        private FlickerState currentState;
        private int emissionColorPropertyId;
        private float stateTimer;
        private float stepTimer;
        private float currentIntensity;
        private float currentRange;
        private float targetIntensity;
        private float targetRange;

        private void Reset()
        {
            targetLight = GetComponent<Light>();

            if (targetLight != null)
            {
                baseIntensity = Mathf.Max(0.01f, targetLight.intensity);
                baseRange = Mathf.Max(0.01f, targetLight.range);
            }
        }

        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light>();
            }

            if (targetLight != null)
            {
                if (baseIntensity <= 0.01f)
                {
                    baseIntensity = Mathf.Max(0.01f, targetLight.intensity);
                }

                if (baseRange <= 0.01f)
                {
                    baseRange = Mathf.Max(0.01f, targetLight.range);
                }
            }

            emissionColorPropertyId = Shader.PropertyToID(string.IsNullOrWhiteSpace(emissionColorProperty)
                ? "_EmissionColor"
                : emissionColorProperty);
        }

        private void OnEnable()
        {
            currentIntensity = baseIntensity;
            currentRange = baseRange;
            targetIntensity = baseIntensity;
            targetRange = baseRange;

            if (randomizeOnEnable && Random.value < 0.45f)
            {
                EnterBurstState();
            }
            else
            {
                EnterStableState();
            }

            ApplyOutput(1f);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || targetLight == null)
            {
                return;
            }

            stateTimer -= deltaTime;

            if (currentState == FlickerState.Stable)
            {
                UpdateStableTargets();

                if (stateTimer <= 0f)
                {
                    EnterBurstState();
                }
            }
            else
            {
                stepTimer -= deltaTime;

                if (stepTimer <= 0f)
                {
                    GenerateBurstStep();
                }

                if (stateTimer <= 0f)
                {
                    EnterStableState();
                }
            }

            ApplyOutput(deltaTime);
        }

        private void OnDisable()
        {
            if (targetLight == null)
            {
                return;
            }

            targetLight.intensity = baseIntensity;
            targetLight.range = baseRange;
            ApplyEmissive(baseIntensity);
        }

        private void EnterStableState()
        {
            currentState = FlickerState.Stable;
            stateTimer = GetRandomRange(stableIntervalRange, 1f);
            targetIntensity = baseIntensity;
            targetRange = baseRange;
            stepTimer = 0f;
        }

        private void EnterBurstState()
        {
            currentState = FlickerState.Burst;
            stateTimer = GetRandomRange(burstDurationRange, 0.2f);
            stepTimer = 0f;
            GenerateBurstStep();
        }

        private void UpdateStableTargets()
        {
            float intensityMultiplier = 1f + Random.Range(-stableIntensityJitter, stableIntensityJitter);
            float rangeMultiplier = 1f + Random.Range(-stableRangeJitter, stableRangeJitter);

            targetIntensity = Mathf.Max(0f, baseIntensity * intensityMultiplier);
            targetRange = Mathf.Max(0f, baseRange * rangeMultiplier);
        }

        private void GenerateBurstStep()
        {
            bool fullyOff = Random.value < fullyOffChance;
            float intensityMultiplier;

            if (fullyOff)
            {
                intensityMultiplier = 0f;
                stepTimer = GetRandomRange(offDurationRange, 0.02f);
            }
            else
            {
                intensityMultiplier = Random.Range(
                    Mathf.Min(burstIntensityMin, burstIntensityMax),
                    Mathf.Max(burstIntensityMin, burstIntensityMax));

                if (Random.value < harshFlashChance)
                {
                    intensityMultiplier = Mathf.Max(intensityMultiplier, harshFlashMultiplier);
                }

                stepTimer = GetRandomRange(onDurationRange, 0.03f);
            }

            float rangeMultiplier = intensityMultiplier <= Mathf.Epsilon
                ? 0f
                : Random.Range(Mathf.Min(burstRangeMin, burstRangeMax), Mathf.Max(burstRangeMin, burstRangeMax));

            if (intensityMultiplier > 1f)
            {
                rangeMultiplier = Mathf.Max(rangeMultiplier, Mathf.Min(intensityMultiplier, harshFlashMultiplier));
            }

            targetIntensity = Mathf.Max(0f, baseIntensity * intensityMultiplier);
            targetRange = Mathf.Max(0f, baseRange * rangeMultiplier);
        }

        private void ApplyOutput(float deltaTime)
        {
            float blend = 1f - Mathf.Exp(-transitionSpeed * deltaTime);
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, blend);
            currentRange = Mathf.Lerp(currentRange, targetRange, blend);

            targetLight.intensity = currentIntensity;
            targetLight.range = currentRange;

            ApplyEmissive(currentIntensity);
        }

        private void ApplyEmissive(float intensityValue)
        {
            if (!syncEmissiveRenderers || emissiveRenderers == null || emissiveRenderers.Length == 0)
            {
                return;
            }

            float normalizedIntensity = baseIntensity > Mathf.Epsilon
                ? intensityValue / baseIntensity
                : 0f;

            float emissiveMultiplier = Mathf.Lerp(
                emissiveOffMultiplier,
                emissiveOnMultiplier,
                Mathf.Clamp01(normalizedIntensity));

            if (normalizedIntensity > 1f)
            {
                emissiveMultiplier *= normalizedIntensity;
            }

            Color finalEmissionColor = emissionColor * emissiveMultiplier;

            for (int index = 0; index < emissiveRenderers.Length; index++)
            {
                Renderer emissiveRenderer = emissiveRenderers[index];
                if (emissiveRenderer == null)
                {
                    continue;
                }

                emissiveRenderer.GetPropertyBlock(emissivePropertyBlock);
                emissivePropertyBlock.SetColor(emissionColorPropertyId, finalEmissionColor);
                emissiveRenderer.SetPropertyBlock(emissivePropertyBlock);
            }
        }

        private static float GetRandomRange(Vector2 range, float fallbackMinimum)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return Random.Range(Mathf.Max(fallbackMinimum, min), Mathf.Max(fallbackMinimum, max));
        }
    }
}
