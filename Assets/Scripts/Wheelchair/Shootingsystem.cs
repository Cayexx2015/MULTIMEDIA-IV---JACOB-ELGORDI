using UnityEngine;
using UnityEngine.InputSystem;
using FieldUnlocked.Basketball;

namespace FieldUnlocked.Wheelchair
{
    public class ShootingSystem : MonoBehaviour
    {
        // ── References ────────────────────────────────────────────────────
        [Header("References")]
        public BasketballBall ball;
        public Transform ballHoldAnchor;
        public Transform hoopTarget;

        [Header("UI — Barra de carga")]
        public GameObject chargeBarCanvas;
        public UnityEngine.UI.Image chargeBarFill;
        public UnityEngine.UI.Text chargeLabel;

        [Header("Trayectoria")]
        [Tooltip("Arrastrá aquí un Material URP/Unlit para que se vea la línea.")]
        public Material trajectoryMaterial;
        public LineRenderer trajectoryLine;
        [Range(10, 60)]
        public int trajectoryPoints = 40;
        public float trajectoryTimeStep = 0.07f;

        // ── Ball hold ─────────────────────────────────────────────────────
        [Header("Ball Hold Position (camera-relative)")]
        public Vector3 ballHoldOffset = new Vector3(0f, -0.35f, 0.7f);
        public Transform cameraTransform;

        // ── Pickup ────────────────────────────────────────────────────────
        [Header("Pickup")]
        public float pickupRadius = 2f;

        // ── Tiro ──────────────────────────────────────────────────────────
        [Header("Tiro")]
        public float maxChargeTime = 1.3f;
        [Range(0f, 1f)] public float perfectWindowStart = 0.45f;
        [Range(0f, 1f)] public float perfectWindowEnd = 0.85f;
        public float minLaunchSpeed = 4f;
        public float maxLaunchSpeed = 20f;
        [Range(30f, 75f)] public float launchAngle = 66f;

        // ── Pase ──────────────────────────────────────────────────────────
        [Header("Pase")]
        public float passMaxChargeTime = 0.8f;
        [Range(0f, 1f)] public float passPerfectStart = 0.5f;
        [Range(0f, 1f)] public float passPerfectEnd = 0.7f;
        public float passMinSpeed = 3f;
        public float passMaxSpeed = 16f;
        [Range(0f, 30f)] public float passLiftAngle = 15f;

        // ── Precisión ─────────────────────────────────────────────────────
        [Header("Precisión")]
        public float perfectNoise = 0.02f;
        public float normalNoise = 0.15f;
        public float overchargeNoise = 0.4f;

        // ── Colores ───────────────────────────────────────────────────────
        [Header("Colores barra")]
        public Color colorWeak = new Color(0.3f, 0.5f, 1f);
        public Color colorPerfect = new Color(0.2f, 1f, 0.2f);
        public Color colorOver = new Color(1f, 0.2f, 0.2f);

        [Header("Colores trayectoria")]
        public Color trajectoryColorStart = new Color(1f, 1f, 1f, 0.8f);
        public Color trajectoryColorEnd = new Color(1f, 1f, 1f, 0f);

        [Header("Input — Teclado / Mouse (para testear sin gamepad)")]
        public bool useKeyboardMouseInput = true;
        public Key pickupKey = Key.F;

        [Header("Aim Assist")]
        [Tooltip("Si está activo, los tiros (no los pases) se corrigen levemente hacia el aro " +
                 "cuando se apunta cerca de él, para que sea más fácil embocar.")]
        public bool aimAssistEnabled = true;
        [Tooltip("Cuánto se corrige el tiro hacia el aro (0 = nada, 1 = casi automático).")]
        [Range(0f, 1f)] public float aimAssistStrength = 0.6f;
        [Tooltip("Ángulo máximo (grados) entre hacia dónde mirás y el aro para que el assist actúe.")]
        [Range(0f, 45f)] public float aimAssistMaxAngle = 18f;
        [Tooltip("Distancia máxima al aro para que el assist actúe.")]
        public float aimAssistMaxDistance = 12f;

        [Header("Debug")]
        public bool showDebugLogs = true;

        // ── Eventos ───────────────────────────────────────────────────────
        /// <summary>Se dispara cada vez que el jugador agarra la pelota (incluye la primera vez).</summary>
        public event System.Action OnBallPickedUp;

        /// <summary>
        /// Igual que <see cref="OnBallPickedUp"/> pero estático: se dispara sin importar
        /// cuál instancia de ShootingSystem la agarró (hay más de una en la escena,
        /// por ej. la de primera y la de tercera persona). El GameManager se suscribe
        /// a este evento para no depender de saber cuál instancia es la "activa".
        /// </summary>
        public static event System.Action OnAnyBallPickedUp;

        // ── Estado ────────────────────────────────────────────────────────
        private enum ActionType { None, Shot, Pass }
        private ActionType _action = ActionType.None;
        private bool _chargeHeld = false;
        private float _chargeTimer = 0f;
        private bool _nearBall = false;
        private bool _hasBall = false;
        public bool HasBall => _hasBall;
        private GameObject _virtualAnchor;

        // ── Start ─────────────────────────────────────────────────────────
        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            _virtualAnchor = new GameObject("BallVirtualAnchor");
            if (cameraTransform != null)
                _virtualAnchor.transform.SetParent(cameraTransform, false);
            _virtualAnchor.transform.localPosition = ballHoldOffset;

            SetupTrajectoryLine();
            HideUI();
        }

        // ── Update ────────────────────────────────────────────────────────
        private void Update()
        {
            Gamepad gp = Gamepad.current;
            Keyboard kb = useKeyboardMouseInput ? Keyboard.current : null;
            Mouse mouse = useKeyboardMouseInput ? Mouse.current : null;

            if (gp == null && kb == null && mouse == null) return;

            if (_virtualAnchor != null)
                _virtualAnchor.transform.localPosition = ballHoldOffset;

            CheckProximity();
            HandlePickup(gp, kb);
            HandleShot(gp, mouse);
            HandlePass(gp, mouse);
            UpdateUI();
            UpdateTrajectory();
        }

        // ── Proximidad ────────────────────────────────────────────────────
        private void CheckProximity()
        {
            if (ball == null || _hasBall) { _nearBall = false; return; }
            if (ball.State != BasketballBall.BallState.Free) { _nearBall = false; return; }
            _nearBall = Vector3.Distance(transform.position, ball.transform.position) <= pickupRadius;
        }

        // ── Pickup — South (A/Cruz) o tecla F ──────────────────────────────
        private void HandlePickup(Gamepad gp, Keyboard kb)
        {
            if (_hasBall || !_nearBall) return;

            bool pressed = (gp != null && gp.buttonSouth.wasPressedThisFrame)
                         || (kb != null && kb[pickupKey].wasPressedThisFrame);
            if (!pressed) return;

            Transform anchor = _virtualAnchor != null ? _virtualAnchor.transform : ballHoldAnchor;
            ball.Pickup(anchor);
            _hasBall = true;
            OnBallPickedUp?.Invoke();
            OnAnyBallPickedUp?.Invoke();
            if (showDebugLogs) Debug.Log("[Shoot] Pelota agarrada! (" + name + ")");
        }

        // ── Tiro — West (X/Cuadrado) o Click Izquierdo ────────────────────
        private void HandleShot(Gamepad gp, Mouse mouse)
        {
            if (!_hasBall || _action == ActionType.Pass) return;

            bool pressedThisFrame = (gp != null && gp.buttonWest.wasPressedThisFrame)
                                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            bool releasedThisFrame = (gp != null && gp.buttonWest.wasReleasedThisFrame)
                                    || (mouse != null && mouse.leftButton.wasReleasedThisFrame);

            if (pressedThisFrame)
            {
                _action = ActionType.Shot;
                _chargeHeld = true;
                _chargeTimer = 0f;
            }

            if (_chargeHeld && _action == ActionType.Shot)
                _chargeTimer = Mathf.Min(_chargeTimer + Time.deltaTime, maxChargeTime * 1.3f);

            if (releasedThisFrame && _chargeHeld && _action == ActionType.Shot)
            {
                _chargeHeld = false;
                ExecuteShot();
            }
        }

        // ── Pase — East (B/Círculo) o Click Derecho ────────────────────────
        private void HandlePass(Gamepad gp, Mouse mouse)
        {
            if (!_hasBall || _action == ActionType.Shot) return;

            bool pressedThisFrame = (gp != null && gp.buttonEast.wasPressedThisFrame)
                                   || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            bool releasedThisFrame = (gp != null && gp.buttonEast.wasReleasedThisFrame)
                                    || (mouse != null && mouse.rightButton.wasReleasedThisFrame);

            if (pressedThisFrame)
            {
                _action = ActionType.Pass;
                _chargeHeld = true;
                _chargeTimer = 0f;
            }

            if (_chargeHeld && _action == ActionType.Pass)
                _chargeTimer = Mathf.Min(_chargeTimer + Time.deltaTime, passMaxChargeTime * 1.3f);

            if (releasedThisFrame && _chargeHeld && _action == ActionType.Pass)
            {
                _chargeHeld = false;
                ExecutePass();
            }
        }

        // ── Ejecutar tiro ─────────────────────────────────────────────────
        private void ExecuteShot()
        {
            float ratio = Mathf.Clamp01(_chargeTimer / maxChargeTime);
            bool perfect = ratio >= perfectWindowStart && ratio <= perfectWindowEnd;
            bool over = _chargeTimer > maxChargeTime;
            float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, ratio);
            float noise = GetNoise(perfect, over, ratio, perfectWindowStart);
            ball.Launch(CalculateLaunch(speed, launchAngle, noise));
            ResetState();
            if (showDebugLogs) Debug.Log($"[Shoot] Tiro! charge={ratio:P0} speed={speed:F1} perfect={perfect}");
        }

        // ── Ejecutar pase ─────────────────────────────────────────────────
        private void ExecutePass()
        {
            float ratio = Mathf.Clamp01(_chargeTimer / passMaxChargeTime);
            bool perfect = ratio >= passPerfectStart && ratio <= passPerfectEnd;
            bool over = _chargeTimer > passMaxChargeTime;
            float speed = Mathf.Lerp(passMinSpeed, passMaxSpeed, ratio);
            float noise = GetNoise(perfect, over, ratio, passPerfectStart) * 0.5f;
            ball.Launch(CalculateLaunch(speed, passLiftAngle, noise));
            ResetState();
            if (showDebugLogs) Debug.Log($"[Shoot] Pase! charge={ratio:P0} speed={speed:F1}");
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private Vector3 CalculateLaunch(float speed, float angleDeg, float noise)
        {
            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 flat = new Vector3(camFwd.x, 0f, camFwd.z);
            if (flat.magnitude < 0.001f) flat = transform.forward;
            flat.Normalize();

            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            Vector2 n2d = Random.insideUnitCircle * noise;
            Vector3 rawVelocity = flat * (speed * Mathf.Cos(rad))
                 + Vector3.up * (speed * Mathf.Sin(rad))
                 + right * n2d.x + Vector3.up * n2d.y;

            // El aim assist sólo corrige tiros al aro, nunca pases entre jugadores.
            if (_action == ActionType.Shot && aimAssistEnabled && hoopTarget != null)
                return ApplyAimAssist(rawVelocity, flat, angleDeg);

            return rawVelocity;
        }

        /// <summary>
        /// Si se apunta dentro de un cono cercano al aro, calcula la velocidad "ideal" que
        /// llegaría exacto al aro (con el mismo ángulo de tiro) y mezcla esa velocidad con la
        /// del jugador según <see cref="aimAssistStrength"/>. Mientras más alejado del centro
        /// del cono, menos corrige — así sigue importando apuntar y cargar el tiro.
        /// </summary>
        private Vector3 ApplyAimAssist(Vector3 rawVelocity, Vector3 aimFlat, float angleDeg)
        {
            Vector3 origin = ball != null ? ball.transform.position : transform.position;
            Vector3 toHoopFull = hoopTarget.position - origin;
            Vector3 toHoopFlat = new Vector3(toHoopFull.x, 0f, toHoopFull.z);
            float dist = toHoopFlat.magnitude;
            if (dist < 0.05f || dist > aimAssistMaxDistance) return rawVelocity;

            Vector3 toHoopDir = toHoopFlat / dist;
            float angleToHoop = Vector3.Angle(aimFlat, toHoopDir);
            if (angleToHoop > aimAssistMaxAngle) return rawVelocity;

            float rad = angleDeg * Mathf.Deg2Rad;
            float g = Mathf.Abs(Physics.gravity.y);
            float h = toHoopFull.y;
            float denom = 2f * Mathf.Pow(Mathf.Cos(rad), 2f) * (dist * Mathf.Tan(rad) - h);

            Vector3 idealVelocity = rawVelocity;
            if (denom > 0.01f)
            {
                float idealSpeed = Mathf.Sqrt((g * dist * dist) / denom);
                idealSpeed = Mathf.Clamp(idealSpeed, minLaunchSpeed, maxLaunchSpeed * 1.15f);
                idealVelocity = toHoopDir * (idealSpeed * Mathf.Cos(rad)) + Vector3.up * (idealSpeed * Mathf.Sin(rad));
            }

            // Cuanto más centrado esté el apunte, más fuerte corrige (hasta aimAssistStrength).
            float t = aimAssistStrength * (1f - (angleToHoop / aimAssistMaxAngle));
            return Vector3.Lerp(rawVelocity, idealVelocity, Mathf.Clamp01(t));
        }

        private float GetNoise(bool perfect, bool over, float ratio, float winStart)
        {
            if (perfect) return perfectNoise;
            if (over) return overchargeNoise;
            return Mathf.Lerp(normalNoise, perfectNoise, Mathf.Clamp01(ratio / winStart));
        }

        private void ResetState()
        {
            _hasBall = false;
            _chargeTimer = 0f;
            _action = ActionType.None;
            HideUI();
            HideTrajectory();
        }

        // ── UI ────────────────────────────────────────────────────────────
        private void UpdateUI()
        {
            if (!_hasBall || !_chargeHeld) { HideUI(); return; }

            if (chargeBarCanvas != null) chargeBarCanvas.SetActive(true);
            if (chargeLabel != null)
                chargeLabel.text = _action == ActionType.Pass ? "PASE" : "TIRO";

            if (chargeBarFill == null) return;

            float maxT = _action == ActionType.Pass ? passMaxChargeTime : maxChargeTime;
            float pS = _action == ActionType.Pass ? passPerfectStart : perfectWindowStart;
            float pE = _action == ActionType.Pass ? passPerfectEnd : perfectWindowEnd;
            float ratio = Mathf.Clamp01(_chargeTimer / maxT);

            chargeBarFill.fillAmount = ratio;

            Color c;
            if (_chargeTimer > maxT) c = colorOver;
            else if (ratio >= pS && ratio <= pE) c = colorPerfect;
            else c = Color.Lerp(colorWeak, colorPerfect, Mathf.Clamp01(ratio / Mathf.Max(pS, 0.01f)));
            chargeBarFill.color = c;
        }

        private void HideUI()
        {
            if (chargeBarCanvas != null) chargeBarCanvas.SetActive(false);
        }

        // ── Trayectoria ───────────────────────────────────────────────────
        private void SetupTrajectoryLine()
        {
            if (trajectoryLine == null)
            {
                GameObject go = new GameObject("TrajectoryLine");
                go.transform.SetParent(transform, false);
                trajectoryLine = go.AddComponent<LineRenderer>();
            }

            trajectoryLine.positionCount = trajectoryPoints;
            trajectoryLine.startWidth = 0.05f;
            trajectoryLine.endWidth = 0.005f;
            trajectoryLine.useWorldSpace = true;

            // Usar material asignado en Inspector o intentar URP Unlit como fallback
            if (trajectoryMaterial != null)
            {
                trajectoryLine.material = trajectoryMaterial;
            }
            else
            {
                // Intentar shader URP primero, luego fallback
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                Shader fallback = Shader.Find("Unlit/Color");
                Shader chosen = urpUnlit != null ? urpUnlit : fallback;

                if (chosen != null)
                {
                    Material mat = new Material(chosen);
                    mat.color = new Color(1f, 1f, 1f, 0.8f);
                    trajectoryLine.material = mat;
                }
            }

            // Gradiente blanco → transparente
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(trajectoryColorStart.r, trajectoryColorStart.g, trajectoryColorStart.b), 0f),
                    new GradientColorKey(new Color(trajectoryColorEnd.r,   trajectoryColorEnd.g,   trajectoryColorEnd.b),   1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(trajectoryColorStart.a, 0f),
                    new GradientAlphaKey(trajectoryColorEnd.a,   1f)
                }
            );
            trajectoryLine.colorGradient = grad;
            trajectoryLine.enabled = false;
        }

        private void UpdateTrajectory()
        {
            if (trajectoryLine == null) return;
            if (!_hasBall || !_chargeHeld) { HideTrajectory(); return; }

            trajectoryLine.enabled = true;

            float maxT = _action == ActionType.Pass ? passMaxChargeTime : maxChargeTime;
            float angle = _action == ActionType.Pass ? passLiftAngle : launchAngle;
            float minSpd = _action == ActionType.Pass ? passMinSpeed : minLaunchSpeed;
            float maxSpd = _action == ActionType.Pass ? passMaxSpeed : maxLaunchSpeed;

            float ratio = Mathf.Clamp01(_chargeTimer / maxT);
            float speed = Mathf.Lerp(minSpd, maxSpd, ratio);

            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 flat = new Vector3(camFwd.x, 0f, camFwd.z);
            if (flat.magnitude < 0.001f) flat = transform.forward;
            flat.Normalize();

            float rad = angle * Mathf.Deg2Rad;
            Vector3 vel = flat * (speed * Mathf.Cos(rad)) + Vector3.up * (speed * Mathf.Sin(rad));
            Vector3 ori = ball != null ? ball.transform.position : transform.position + Vector3.up * 1.2f;

            trajectoryLine.positionCount = trajectoryPoints;
            for (int i = 0; i < trajectoryPoints; i++)
            {
                float t = i * trajectoryTimeStep;
                Vector3 pt = ori + vel * t + 0.5f * Physics.gravity * t * t;
                trajectoryLine.SetPosition(i, pt);
                if (pt.y < ori.y - 10f)
                {
                    trajectoryLine.positionCount = i + 1;
                    break;
                }
            }
        }

        private void HideTrajectory()
        {
            if (trajectoryLine != null) trajectoryLine.enabled = false;
        }
    }
}