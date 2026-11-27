using UnityEngine;

namespace CoinRush.Player
{
    /// <summary>
    /// Drives procedural top-down character animation (run bounce, arm swing, tilt)
    /// and applies distinct visual identity (team colors, crest silhouette, glow ring, name tag).
    /// </summary>
    public class CharacterVisualController : MonoBehaviour
    {
        [Header("Rig Parts")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform nameTagRoot;

        [Header("Identity Elements")]
        [SerializeField] private GameObject player1Crest;
        [SerializeField] private GameObject player2Crest;
        [SerializeField] private MeshRenderer[] teamColoredRenderers;
        [SerializeField] private MeshRenderer[] accentGlowRenderers;
        [SerializeField] private MeshRenderer underfootRingRenderer;
        [SerializeField] private TMPro.TextMeshProUGUI nameTagText;

        [Header("Theme Materials")]
        [SerializeField] private Material player1BodyMat;
        [SerializeField] private Material player2BodyMat;
        [SerializeField] private Material player1GlowMat;
        [SerializeField] private Material player2GlowMat;

        [Header("Animation Tuning")]
        [SerializeField] private float walkCycleSpeed = 12f;
        [SerializeField] private float runBounceHeight = 0.09f;
        [SerializeField] private float maxForwardTilt = 11f;
        [SerializeField] private float armSwingAngle = 32f;

        private Vector3 _visualBaseLocalPos;
        private Vector3 _lastWorldPos;
        private float _walkPhase;
        private float _smoothedSpeedRatio;
        private Camera _cachedCamera;

        private static readonly Color P1TagColor = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color P2TagColor = new Color(1f, 0.58f, 0.52f, 1f);

        public void ConfigureRig(
            Transform vRoot,
            Transform lArm,
            Transform rArm,
            Transform tagRoot,
            GameObject crest1,
            GameObject crest2,
            MeshRenderer[] bodyRenderers,
            MeshRenderer[] glowRenderers,
            MeshRenderer ringRenderer,
            TMPro.TextMeshProUGUI tagText,
            Material p1Body,
            Material p2Body,
            Material p1Glow,
            Material p2Glow)
        {
            visualRoot = vRoot;
            leftArm = lArm;
            rightArm = rArm;
            nameTagRoot = tagRoot;
            player1Crest = crest1;
            player2Crest = crest2;
            teamColoredRenderers = bodyRenderers;
            accentGlowRenderers = glowRenderers;
            underfootRingRenderer = ringRenderer;
            nameTagText = tagText;
            player1BodyMat = p1Body;
            player2BodyMat = p2Body;
            player1GlowMat = p1Glow;
            player2GlowMat = p2Glow;
        }

        private void Awake()
        {
            if (visualRoot != null)
            {
                _visualBaseLocalPos = visualRoot.localPosition;
            }
            _lastWorldPos = transform.position;
        }

        private void OnEnable()
        {
            _lastWorldPos = transform.position;
            _smoothedSpeedRatio = 0f;
        }

        public void ApplyIdentity(int slotIndex, string displayName)
        {
            bool isSlot0 = slotIndex == 0;

            if (player1Crest != null) player1Crest.SetActive(isSlot0);
            if (player2Crest != null) player2Crest.SetActive(!isSlot0);

            Material bodyMat = isSlot0 ? player1BodyMat : player2BodyMat;
            if (bodyMat != null && teamColoredRenderers != null)
            {
                for (int i = 0; i < teamColoredRenderers.Length; i++)
                {
                    if (teamColoredRenderers[i] != null)
                    {
                        teamColoredRenderers[i].sharedMaterial = bodyMat;
                    }
                }
            }

            Material glowMat = isSlot0 ? player1GlowMat : player2GlowMat;
            if (glowMat != null)
            {
                if (accentGlowRenderers != null)
                {
                    for (int i = 0; i < accentGlowRenderers.Length; i++)
                    {
                        if (accentGlowRenderers[i] != null)
                        {
                            accentGlowRenderers[i].sharedMaterial = glowMat;
                        }
                    }
                }

                if (underfootRingRenderer != null)
                {
                    underfootRingRenderer.sharedMaterial = glowMat;
                }
            }

            if (nameTagText != null)
            {
                string label = string.IsNullOrWhiteSpace(displayName)
                    ? (isSlot0 ? "Player 1" : "Player 2")
                    : displayName;
                nameTagText.text = label;
                nameTagText.color = isSlot0 ? P1TagColor : P2TagColor;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            Vector3 delta = transform.position - _lastWorldPos;
            delta.y = 0f;
            float instantSpeed = delta.magnitude / dt;
            _lastWorldPos = transform.position;

            float targetRatio = Mathf.Clamp01(instantSpeed / 7.5f);
            _smoothedSpeedRatio = Mathf.Lerp(_smoothedSpeedRatio, targetRatio, dt * 12f);

            if (_smoothedSpeedRatio > 0.05f)
            {
                _walkPhase += dt * walkCycleSpeed * Mathf.Max(0.4f, _smoothedSpeedRatio);
            }

            if (visualRoot != null)
            {
                float runBob = Mathf.Abs(Mathf.Sin(_walkPhase)) * runBounceHeight * _smoothedSpeedRatio;
                float idleBreath = Mathf.Sin(Time.time * 3f) * 0.015f * (1f - _smoothedSpeedRatio);
                visualRoot.localPosition = _visualBaseLocalPos + new Vector3(0f, runBob + idleBreath, 0f);
                visualRoot.localRotation = Quaternion.Euler(maxForwardTilt * _smoothedSpeedRatio, 0f, 0f);
            }

            float swing = Mathf.Sin(_walkPhase) * armSwingAngle * _smoothedSpeedRatio;
            if (leftArm != null)
            {
                leftArm.localRotation = Quaternion.Euler(swing, 0f, 12f);
            }
            if (rightArm != null)
            {
                rightArm.localRotation = Quaternion.Euler(-swing, 0f, -12f);
            }
        }

        private void LateUpdate()
        {
            if (nameTagRoot == null) return;

            if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
            }

            if (_cachedCamera != null)
            {
                nameTagRoot.rotation = _cachedCamera.transform.rotation;
            }
        }
    }
}
