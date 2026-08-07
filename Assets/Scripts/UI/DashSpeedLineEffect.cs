using Mood.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Dash Speed Line Effect")]
    [DisallowMultipleComponent]
    public sealed class DashSpeedLineEffect : MonoBehaviour
    {
        private const string OverlayObjectName = "Dash SpeedLine Overlay";

        [Header("References")]
        [SerializeField] private HyperFpsFirstPersonController controller;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material speedLineMaterial;
        [SerializeField] private Shader speedLineShader;
        [SerializeField] private string shaderName = string.Empty;

        [Header("Fade")]
        [SerializeField, Min(0f)] private float fadeInSpeed = 18f;
        [SerializeField, Min(0f)] private float fadeOutSpeed = 10f;
        [SerializeField, Range(0f, 1f)] private float maxOpacity = 0.9f;

        [Header("Overlay")]
        [SerializeField, Min(0.02f)] private float overlayDistance = 0.05f;
        [SerializeField] private Color tint = Color.white;

        [Header("Shader Properties")]
        [SerializeField] private string intensityProperty = "_Intensity";
        [SerializeField] private string alphaProperty = "_Alpha";
        [SerializeField] private string colorProperty = "_BaseColor";
        [SerializeField] private string fallbackColorProperty = "_Color";
        [SerializeField] private string opacityProperty = "_Opacity";

        private Transform overlayTransform;
        private MeshRenderer overlayRenderer;
        private Material runtimeMaterial;
        private float currentOpacity;
        private bool warnedMissingMaterial;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
            EnsureOverlayCreated();
            ApplyOpacity(0f);
        }

        private void OnEnable()
        {
            AssignReferences();
        }

        private void LateUpdate()
        {
            if (controller == null)
            {
                return;
            }

            if (!EnsureOverlayCreated())
            {
                return;
            }

            UpdateOverlayTransform();

            float targetOpacity = controller.IsDashing ? maxOpacity : 0f;
            float blendSpeed = targetOpacity > currentOpacity ? fadeInSpeed : fadeOutSpeed;
            currentOpacity = Mathf.MoveTowards(currentOpacity, targetOpacity, blendSpeed * Time.deltaTime);

            ApplyOpacity(currentOpacity);
        }

        private void OnDisable()
        {
            currentOpacity = 0f;
            ApplyOpacity(0f);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void AssignReferences()
        {
            if (controller == null)
            {
                controller = GetComponent<HyperFpsFirstPersonController>();
            }

            if (targetCamera != null)
            {
                return;
            }

            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            for (int index = 0; index < cameras.Length; index++)
            {
                if (cameras[index] != null && cameras[index].CompareTag("MainCamera"))
                {
                    targetCamera = cameras[index];
                    return;
                }
            }

            if (cameras.Length > 0)
            {
                targetCamera = cameras[0];
            }
        }

        private bool EnsureOverlayCreated()
        {
            if (targetCamera == null)
            {
                AssignReferences();
                if (targetCamera == null)
                {
                    return false;
                }
            }

            if (runtimeMaterial == null)
            {
                runtimeMaterial = CreateRuntimeMaterial();
                if (runtimeMaterial == null)
                {
                    WarnMissingMaterialOnce();
                    return false;
                }
            }

            if (overlayRenderer != null)
            {
                overlayRenderer.sharedMaterial = runtimeMaterial;
                return true;
            }

            Transform existingOverlay = targetCamera.transform.Find(OverlayObjectName);
            GameObject overlayObject;
            if (existingOverlay != null)
            {
                overlayObject = existingOverlay.gameObject;
            }
            else
            {
                overlayObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                overlayObject.name = OverlayObjectName;
                overlayObject.transform.SetParent(targetCamera.transform, false);
                overlayObject.layer = targetCamera.gameObject.layer;

                Collider overlayCollider = overlayObject.GetComponent<Collider>();
                if (overlayCollider != null)
                {
                    Destroy(overlayCollider);
                }
            }

            overlayTransform = overlayObject.transform;
            overlayRenderer = overlayObject.GetComponent<MeshRenderer>();
            if (overlayRenderer == null)
            {
                overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            }

            if (overlayObject.GetComponent<MeshFilter>() == null)
            {
                overlayObject.AddComponent<MeshFilter>();
            }

            overlayRenderer.sharedMaterial = runtimeMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            overlayRenderer.enabled = false;

            UpdateOverlayTransform();
            return true;
        }

        private Material CreateRuntimeMaterial()
        {
            Material sourceMaterial = speedLineMaterial;
            if (sourceMaterial != null)
            {
                return new Material(sourceMaterial);
            }

            Shader sourceShader = speedLineShader;
            if (sourceShader == null && !string.IsNullOrWhiteSpace(shaderName))
            {
                sourceShader = Shader.Find(shaderName);
            }

            return sourceShader != null ? new Material(sourceShader) : null;
        }

        private void UpdateOverlayTransform()
        {
            if (overlayTransform == null || targetCamera == null)
            {
                return;
            }

            float distance = Mathf.Max(targetCamera.nearClipPlane + 0.02f, overlayDistance);
            float height = 2f * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            float width = height * targetCamera.aspect;

            overlayTransform.localPosition = new Vector3(0f, 0f, distance);
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = new Vector3(width, height, 1f);
        }

        private void ApplyOpacity(float opacity)
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (runtimeMaterial.HasProperty(intensityProperty))
            {
                runtimeMaterial.SetFloat(intensityProperty, opacity);
            }

            if (runtimeMaterial.HasProperty(alphaProperty))
            {
                runtimeMaterial.SetFloat(alphaProperty, opacity);
            }

            if (runtimeMaterial.HasProperty(opacityProperty))
            {
                runtimeMaterial.SetFloat(opacityProperty, opacity);
            }

            string activeColorProperty = runtimeMaterial.HasProperty(colorProperty)
                ? colorProperty
                : runtimeMaterial.HasProperty(fallbackColorProperty)
                    ? fallbackColorProperty
                    : null;

            if (!string.IsNullOrEmpty(activeColorProperty))
            {
                runtimeMaterial.SetColor(activeColorProperty, tint);
            }

            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = opacity > 0.001f;
            }
        }

        private void WarnMissingMaterialOnce()
        {
            if (warnedMissingMaterial)
            {
                return;
            }

            warnedMissingMaterial = true;
            Debug.LogWarning($"[DashSpeedLineEffect:{name}] Assign a SpeedLine material or shader.", this);
        }
    }
}
