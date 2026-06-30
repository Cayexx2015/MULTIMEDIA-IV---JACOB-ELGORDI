using UnityEngine;
using UnityEngine.InputSystem;

namespace FieldUnlocked.Wheelchair
{
    /// <summary>
    /// Wheelchair sports locomotion for VR prototype. Attach to XR Origin (VR).
    /// WheelchairVisual must be a child of XR Origin — it moves automatically.
    ///
    /// Keyboard : WASD = move/turn | Shift = boost | Q = brake | E = pivot
    ///            Space = action | Arrow keys = test camera
    /// Gamepad  : Left stick = move/turn | Right stick = camera
    ///            RT = boost | LT/East = brake | South = action
    ///            West = pass | North = pivot | L3 = sprint alt
    ///
    /// Future VR: call AddExternalPush(leftPush, rightPush) each frame.
    /// </summary>
    public enum WheelAxis { LocalX, LocalY, LocalZ }

    public class WheelchairLocomotion : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed    = 4.5f;
        public float sprintSpeed  = 7.5f;
        public float reverseSpeed = 2.8f;
        public float turnSpeed    = 130f;
        public float acceleration = 12f;
        public float damping      = 10f;
        public float brakeDamping = 30f;

        [Header("Movement Feel")]
        public float lowSpeedTurnBoost           = 1.4f;
        public float highSpeedTurnReduction      = 0.55f;
        public float speedForTurnReduction       = 4f;
        public float boostAccelerationMultiplier = 1.35f;
        public float brakeTurnBoost              = 1.25f;

        [Header("Boost / Push")]
        // How often an automatic push stroke fires while RT is held (seconds)
        public float pushPulseInterval   = 0.32f;
        // Multiplier applied to the velocity target during a forward/diagonal push pulse
        public float pushPulseStrength   = 1.15f;
        // How long each push pulse lasts (seconds)
        public float pushPulseDuration   = 0.12f;
        // Fraction of velocity retained over one recovery interval (between pulses)
        public float pushRecoveryDamping = 0.85f;
        // Target velocity multiplier when boosting with a mostly-sideways stick
        public float sideBoostMultiplier  = 1.35f;
        // Target velocity multiplier when boosting in pivot mode
        public float pivotBoostMultiplier = 1.6f;

        [Header("Pivot")]
        public float pivotForwardSuppression = 0.06f;   // near-zero forward drift when pivoting
        public float pivotTurnSharpness      = 1.7f;    // snappier in-place spin
        public float pivotLinearDamping      = 22f;     // how fast residual forward speed is killed during pivot

        [Header("Input")]
        public float joystickDeadzone = 0.15f;
        public float inputCurvePower  = 1.4f;
        public bool  useKeyboardInput = true;
        public bool  useGamepadInput  = true;

        [Header("Test Camera")]
        public Transform cameraTransform;
        public float cameraYawSpeed   = 180f;
        public float cameraPitchSpeed = 120f;

        public bool  limitCameraYaw = true;
        public float minCameraYaw   = -85f;
        public float maxCameraYaw   =  85f;

        public bool  limitCameraPitch = true;
        public float minCameraPitch   = -25f;
        public float maxCameraPitch   =  45f;

        [Header("VR Head Tracking")]
        [Tooltip("Transform PADRE de la cámara real (la que tiene el TrackedPoseDriver / es la HMD). " +
                 "Si se asigna, el joystick/teclado rota ESTE transform (yaw) en vez de la cámara, " +
                 "para no pelearse con el head-tracking real del visor VR — así el giro de cabeza " +
                 "contextual con los lentes funciona, y el joystick sigue funcionando para reorientar " +
                 "la vista. Si se deja vacío, se intenta auto-detectar como el padre de cameraTransform; " +
                 "si tampoco hay padre (testeo de escritorio sin VR), se usa el comportamiento viejo " +
                 "(rotar cameraTransform directamente con yaw + pitch).")]
        public Transform cameraOffsetTransform;
        [Tooltip("Con visor VR puesto, el pitch (mirar arriba/abajo) ya lo controla la cabeza real. " +
                 "Dejar en true evita que el joystick/teclado peleen con el head-tracking en ese eje.")]
        public bool ignoreJoystickPitchWhenVRActive = true;

        private bool _useVROffset; // true cuando cameraOffsetTransform quedó resuelto

        [Header("Wheel Animation")]
        public Transform leftWheelVisual;
        public Transform rightWheelVisual;
        public float     wheelRotationSpeed          = 420f;
        public float     turnWheelRotationMultiplier = 0.65f;
        public WheelAxis wheelRotationAxis           = WheelAxis.LocalX;
        public bool      invertLeftWheelRotation         = false;
        public bool      invertRightWheelRotation        = false;
        public bool      useRuntimeWheelPivots           = true;
        public bool      useRendererBoundsForWheelPivot  = true;
        public bool      drawWheelPivotGizmos            = true;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // Smoothed velocities
        private float _currentLinear;    // m/s  (+ = forward)
        private float _currentAngular;   // °/s  (+ = turn right)

        // Per-wheel normalized power [-1, 1] — drives animation
        private float _leftWheelPower;
        private float _rightWheelPower;

        // Push pulse state
        private float _pushTimer;    // time since last pulse fired
        private float _pulseTimer;   // time the current pulse has been active
        private bool  _pulseActive;

        // ── Read-only state — consumed by WheelchairHandAnimator and other systems ──
        public float CurrentForwardInput    { get; private set; }
        public float CurrentTurnInput       { get; private set; }
        public float CurrentLeftWheelPower  { get; private set; }
        public float CurrentRightWheelPower { get; private set; }
        public bool  IsBoostHeld            { get; private set; }
        public bool  IsBrakeHeld            { get; private set; }
        public bool  IsPushPulseActive      { get; private set; }

        // Camera
        private float _cameraYaw;
        private float _cameraPitch;

        // External push accumulator — consumed and reset every Update
        private float _externalLinearInput;
        private float _externalAngularInput;

        // Runtime spin pivots (created in Start when useRuntimeWheelPivots is true)
        private Transform _leftWheelPivot;
        private Transform _rightWheelPivot;

        // ──────────────────────────────────────────────────────────────────
        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                if (showDebugLogs)
                    Debug.Log("[WheelchairLocomotion] cameraTransform not set — using Camera.main.");
            }

            // Auto-detectar el "Camera Offset" (padre de la cámara real) si no se asignó
            // a mano. Si la cámara cuelga de un rig de VR (XR Origin → Camera Offset →
            // Main Camera con TrackedPoseDriver), su padre directo es ese offset.
            // OJO: esto solo se usa más adelante si además hay un visor VR realmente
            // activo (ver _useVROffset en ApplyCameraRotation) — en escritorio sin
            // visor, el offset existe en la jerarquía pero no se usa, para no romper
            // el control con teclado/joystick.
            if (cameraOffsetTransform == null && cameraTransform != null && cameraTransform.parent != null)
                cameraOffsetTransform = cameraTransform.parent;

            _useVROffset = false; // se recalcula cada frame en ApplyCameraRotation según haya o no un visor activo

            Transform rotationSeed = _useVROffset ? cameraOffsetTransform : cameraTransform;
            if (rotationSeed != null)
            {
                _cameraYaw   = rotationSeed.eulerAngles.y;
                _cameraPitch = rotationSeed.eulerAngles.x;
                if (_cameraPitch > 180f) _cameraPitch -= 360f;
            }

            if (showDebugLogs)
                Debug.Log(_useVROffset
                    ? $"[WheelchairLocomotion] Cámara VR detectada — el joystick/teclado rota '{cameraOffsetTransform.name}' y deja el head-tracking real intacto en '{cameraTransform?.name}'."
                    : "[WheelchairLocomotion] Sin offset de VR — modo cámara de test de escritorio (rota cameraTransform directamente).");

            if (useRuntimeWheelPivots)
            {
                _leftWheelPivot  = SetupWheelPivot(leftWheelVisual,  "LeftWheelSpinPivot");
                _rightWheelPivot = SetupWheelPivot(rightWheelVisual, "RightWheelSpinPivot");
            }

            // Pre-charge so the very first frame RT is held fires a pulse immediately
            _pushTimer = pushPulseInterval;

            if (showDebugLogs)
                Debug.Log($"[WheelchairLocomotion] Start on: {gameObject.name}");
        }

        private void Update()
        {
            Keyboard keyboard = useKeyboardInput ? Keyboard.current : null;
            Gamepad  gamepad  = useGamepadInput  ? Gamepad.current  : null;

            float linearInput  = 0f;
            float angularInput = 0f;
            float topSpeed     = moveSpeed;
            bool  braking      = false;
            bool  boosting     = false;

            // ── Gamepad ────────────────────────────────────────────────────
            if (gamepad != null)
            {
                Vector2 leftStick  = Deadzone(gamepad.leftStick.ReadValue());
                Vector2 rightStick = Deadzone(gamepad.rightStick.ReadValue());

                linearInput  += leftStick.y;
                angularInput += leftStick.x;

                if (gamepad.rightTrigger.ReadValue() > 0.1f || gamepad.leftStickButton.isPressed)
                {
                    topSpeed = sprintSpeed;
                    boosting = true;
                }

                if (gamepad.leftTrigger.ReadValue() > 0.1f || gamepad.buttonEast.isPressed)
                    braking = true;

                // Camera from right stick
                _cameraYaw   += rightStick.x * cameraYawSpeed   * Time.deltaTime;
                _cameraPitch -= rightStick.y * cameraPitchSpeed * Time.deltaTime;

                // Action buttons
                if (gamepad.buttonSouth.wasPressedThisFrame) TriggerAction();
                if (gamepad.buttonWest.wasPressedThisFrame)  TriggerPass();
                if (gamepad.buttonNorth.wasPressedThisFrame) TriggerQuickPivot();
            }

            // ── Keyboard ───────────────────────────────────────────────────
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) linearInput  += 1f;
                if (keyboard.sKey.isPressed) linearInput  -= 1f;
                if (keyboard.dKey.isPressed) angularInput += 1f;
                if (keyboard.aKey.isPressed) angularInput -= 1f;

                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                {
                    topSpeed = sprintSpeed;
                    boosting = true;
                }

                if (keyboard.qKey.isPressed)
                    braking = true;

                // Arrow keys — test camera only, no movement
                if (keyboard.rightArrowKey.isPressed) _cameraYaw   += cameraYawSpeed   * Time.deltaTime;
                if (keyboard.leftArrowKey.isPressed)  _cameraYaw   -= cameraYawSpeed   * Time.deltaTime;
                if (keyboard.downArrowKey.isPressed)  _cameraPitch += cameraPitchSpeed * Time.deltaTime;
                if (keyboard.upArrowKey.isPressed)    _cameraPitch -= cameraPitchSpeed * Time.deltaTime;

                // Action keys
                if (keyboard.spaceKey.wasPressedThisFrame) TriggerAction();
                if (keyboard.eKey.wasPressedThisFrame)     TriggerQuickPivot();
            }

            // ── External push (VR hand push-rim) ──────────────────────────
            linearInput  += _externalLinearInput;
            angularInput += _externalAngularInput;
            _externalLinearInput  = 0f;
            _externalAngularInput = 0f;

            // Clamp combined input to [-1, 1]
            linearInput  = Mathf.Clamp(linearInput,  -1f, 1f);
            angularInput = Mathf.Clamp(angularInput, -1f, 1f);

            // Expose current frame state for external readers (e.g. WheelchairHandAnimator)
            CurrentForwardInput = linearInput;
            CurrentTurnInput    = angularInput;
            IsBoostHeld         = boosting;
            IsBrakeHeld         = braking;

            ApplyLocomotion(linearInput, angularInput, topSpeed, braking, boosting);
            ApplyCameraRotation();
            AnimateWheels();
        }

        // ── Locomotion ─────────────────────────────────────────────────────
        private void ApplyLocomotion(float linearInput, float angularInput,
                                     float topSpeed, bool braking, bool boosting)
        {
            float dt = Time.deltaTime;

            // ── 1. Push pulse timer ────────────────────────────────────────
            // While RT is held, automatically fire repeated push impulses.
            // Pre-charging the timer ensures the first impulse fires on the very first frame.
            if (boosting)
            {
                _pushTimer += dt;
                if (_pushTimer >= pushPulseInterval)
                {
                    _pushTimer  = 0f;
                    _pulseTimer = 0f;
                    _pulseActive = true;
                }
                if (_pulseActive)
                {
                    _pulseTimer += dt;
                    if (_pulseTimer >= pushPulseDuration)
                        _pulseActive = false;
                }
            }
            else
            {
                // Not boosting: pre-charge so the next RT press fires immediately
                _pushTimer   = pushPulseInterval;
                _pulseTimer  = 0f;
                _pulseActive = false;
            }

            // ── 2. Input curve — softens micro-corrections, full range at max ─
            float curvedLinear  = ApplyCurve(linearInput,  inputCurvePower);
            float curvedAngular = ApplyCurve(angularInput, inputCurvePower);

            float fwdMag  = Mathf.Abs(curvedLinear);
            float turnMag = Mathf.Abs(curvedAngular);

            // ── 3. Pivot mode: chair spins in place when turn clearly dominates ──
            // Fires when: low forward + any turn, OR turn input is 2.5× the forward input.
            bool pivotMode = (fwdMag < 0.2f  && turnMag > 0.2f)
                          || (turnMag > fwdMag * 2.5f && turnMag > 0.3f);

            // Suppress forward component so the chair stays mostly stationary while pivoting
            float effectiveFwd = pivotMode ? curvedLinear * pivotForwardSuppression : curvedLinear;

            // ── 4. Differential wheel powers ──────────────────────────────────
            // leftWheelPower  = forwardInput + turnInput
            // rightWheelPower = forwardInput - turnInput
            // When both inputs are large, clamping naturally splits the power budget.
            float leftPower  = Mathf.Clamp(effectiveFwd + curvedAngular, -1f, 1f);
            float rightPower = Mathf.Clamp(effectiveFwd - curvedAngular, -1f, 1f);

            // Net drives reconstructed from per-wheel values
            float driveFwd  = (leftPower + rightPower) * 0.5f;
            float driveTurn = (leftPower - rightPower) * 0.5f;  // positive = right

            // ── 5. Speed-dependent turning ─────────────────────────────────
            float speedFrac      = Mathf.Clamp01(Mathf.Abs(_currentLinear) / Mathf.Max(speedForTurnReduction, 0.001f));
            float turnMultiplier = Mathf.Lerp(lowSpeedTurnBoost, highSpeedTurnReduction, speedFrac);
            if (braking && Mathf.Abs(driveTurn) > 0.01f)
                turnMultiplier *= brakeTurnBoost;

            float angularSharpness = pivotMode ? pivotTurnSharpness : 1f;

            // ── 6. Pulse multiplier — chosen based on current input mode ────
            float pulseMult = 1f;
            if (_pulseActive && boosting)
            {
                if (pivotMode)                             pulseMult = pivotBoostMultiplier;
                else if (fwdMag < 0.25f && turnMag > 0.4f) pulseMult = sideBoostMultiplier;
                else                                       pulseMult = pushPulseStrength;
            }

            // ── 7. Target velocities ───────────────────────────────────────
            // During a pulse, targets temporarily exceed the normal ceiling to
            // create the "push" sensation; recovery naturally pulls speed back down.
            float targetLinear  = driveFwd >= 0f
                ? driveFwd  * topSpeed
                : driveFwd  * reverseSpeed;
            float targetAngular = driveTurn * turnMultiplier * turnSpeed * angularSharpness;

            if (_pulseActive && boosting)
            {
                targetLinear  *= pulseMult;
                targetAngular *= pulseMult;
            }

            // ── 8. Effective acceleration — boosted during pulse ────────────
            float effectiveAccel = acceleration * (boosting ? boostAccelerationMultiplier : 1f);

            // ── 9. Recovery damping between pulses ─────────────────────────
            // Creates the natural slowdown rhythm: push → coast → push → coast.
            // pushRecoveryDamping = fraction of velocity retained over one recovery interval.
            if (boosting && !_pulseActive)
            {
                float recoveryLen = Mathf.Max(pushPulseInterval - pushPulseDuration, 0.01f);
                float retainFrac  = Mathf.Pow(pushRecoveryDamping, dt / recoveryLen);
                _currentLinear  *= retainFrac;
                _currentAngular *= retainFrac;
            }

            // ── 10. Linear velocity ────────────────────────────────────────
            if (braking)
            {
                _currentLinear = Mathf.MoveTowards(_currentLinear, 0f,
                    brakeDamping * moveSpeed * dt);
            }
            else if (pivotMode)
            {
                // Aggressively kill forward speed so the chair spins without drifting sideways.
                _currentLinear = Mathf.MoveTowards(_currentLinear, 0f,
                    pivotLinearDamping * moveSpeed * dt);
            }
            else if (Mathf.Approximately(driveFwd, 0f))
            {
                // Frame-rate independent exponential coast — chair glides naturally to a stop
                float coastFactor = 1f - Mathf.Exp(-damping * dt);
                _currentLinear = Mathf.Lerp(_currentLinear, 0f, coastFactor);
            }
            else
            {
                _currentLinear = Mathf.MoveTowards(_currentLinear, targetLinear,
                    effectiveAccel * topSpeed * dt);
            }

            // ── 11. Angular velocity ───────────────────────────────────────
            if (Mathf.Approximately(driveTurn, 0f))
                _currentAngular = Mathf.MoveTowards(_currentAngular, 0f,
                    damping * turnSpeed * dt);
            else
                _currentAngular = Mathf.MoveTowards(_currentAngular, targetAngular,
                    effectiveAccel * turnSpeed * dt);

            // ── 12. Rotate then translate ───────────────────────────────────
            transform.rotation *= Quaternion.AngleAxis(_currentAngular * dt, Vector3.up);
            transform.position  += transform.forward * (_currentLinear  * dt);

            // ── 13. Per-wheel normalized power for animation ────────────────
            // Velocity-based so wheels reflect actual motion, including pivot counter-spin.
            float normLinear  = _currentLinear  / Mathf.Max(sprintSpeed, 0.001f);
            float normAngular = _currentAngular / Mathf.Max(turnSpeed,   0.001f);
            _leftWheelPower  = Mathf.Clamp(normLinear + normAngular * turnWheelRotationMultiplier, -1f, 1f);
            _rightWheelPower = Mathf.Clamp(normLinear - normAngular * turnWheelRotationMultiplier, -1f, 1f);

            // Publish final per-frame values for external readers
            CurrentLeftWheelPower  = _leftWheelPower;
            CurrentRightWheelPower = _rightWheelPower;
            IsPushPulseActive      = _pulseActive;

            if (showDebugLogs)
                Debug.Log($"[WheelchairLocomotion] " +
                          $"forwardInput={linearInput:F2}  turnInput={angularInput:F2}  " +
                          $"boostHeld={boosting}  pulseActive={_pulseActive}  " +
                          $"leftWheelPower={_leftWheelPower:F2}  rightWheelPower={_rightWheelPower:F2}  " +
                          $"pivotMode={pivotMode}");
        }

        /// <summary>
        /// True solo si hay un visor VR realmente puesto y trackeando en este momento
        /// (no alcanza con que el rig de XR Origin exista en la escena — esa jerarquía
        /// está siempre presente, con headset conectado o no). Testeando en la compu sin
        /// visor esto devuelve false, así las flechas/joystick controlan la cámara como
        /// siempre.
        /// </summary>
        private static bool IsVRHeadsetActive()
        {
#if ENABLE_VR || UNITY_XR
            return UnityEngine.XR.XRSettings.isDeviceActive;
#else
            return false;
#endif
        }

        // ── Camera ─────────────────────────────────────────────────────────
        private void ApplyCameraRotation()
        {
            // Solo usamos el offset de VR si hay un visor REALMENTE activo en este
            // momento (no alcanza con que el Camera Offset exista en la jerarquía:
            // esa jerarquía está siempre ahí, con o sin visor puesto). Si estás
            // testeando en la compu sin headset, esto es false y la cámara vuelve a
            // responder 100% a flechas/joystick como antes.
            _useVROffset = cameraOffsetTransform != null && IsVRHeadsetActive();

            if (limitCameraYaw)
            {
                // Express _cameraYaw as an angle relative to the wheelchair's current
                // world yaw, clamp it, then convert back to world yaw.
                // DeltaAngle returns the signed shortest difference in [-180, 180].
                float wheelchairYaw = transform.eulerAngles.y;
                float relativeYaw   = Mathf.DeltaAngle(wheelchairYaw, _cameraYaw);
                relativeYaw = Mathf.Clamp(relativeYaw, minCameraYaw, maxCameraYaw);
                _cameraYaw  = wheelchairYaw + relativeYaw;
            }

            if (limitCameraPitch)
                _cameraPitch = Mathf.Clamp(_cameraPitch, minCameraPitch, maxCameraPitch);

            if (_useVROffset)
            {
                // No tocamos cameraTransform: tiene un TrackedPoseDriver que lo rota según
                // la orientación real del visor (giro de cabeza). En vez de eso, rotamos
                // su padre (el "Camera Offset" del rig de VR) solo en yaw — así el joystick
                // sigue permitiendo reorientar la silla/vista sin pelearse con la cabeza real.
                // El pitch real (mirar arriba/abajo) queda 100% en manos del head-tracking.
                Vector3 euler = cameraOffsetTransform.eulerAngles;
                euler.y = _cameraYaw;
                if (!ignoreJoystickPitchWhenVRActive)
                    euler.x = _cameraPitch;
                cameraOffsetTransform.eulerAngles = euler;
            }
            else if (cameraTransform != null)
            {
                // Sin VR (testeo de escritorio con joystick/teclado): comportamiento de
                // siempre, rota directamente la cámara en yaw + pitch.
                cameraTransform.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
            }
        }

        // ── Wheel animation ────────────────────────────────────────────────
        private void AnimateWheels()
        {
            if (leftWheelVisual == null && rightWheelVisual == null) return;

            // Each wheel uses its own power value — differential and counter-spin are already baked in.
            // When pivoting, normAngular dominates, making _leftWheelPower and _rightWheelPower
            // opposite in sign → the wheel visuals spin in opposite directions automatically.
            float leftDeg  = _leftWheelPower  * wheelRotationSpeed * Time.deltaTime;
            float rightDeg = _rightWheelPower * wheelRotationSpeed * Time.deltaTime;

            if (invertLeftWheelRotation)  leftDeg  = -leftDeg;
            if (invertRightWheelRotation) rightDeg = -rightDeg;

            Vector3 axis = WheelAxisToVector(wheelRotationAxis);

            // Prefer pivot (fixes off-centre/cambered wheel wobble); fall back to direct mesh rotation
            if (_leftWheelPivot != null)
                _leftWheelPivot.Rotate(axis, leftDeg, Space.Self);
            else if (leftWheelVisual != null)
                leftWheelVisual.Rotate(axis, leftDeg, Space.Self);

            if (_rightWheelPivot != null)
                _rightWheelPivot.Rotate(axis, rightDeg, Space.Self);
            else if (rightWheelVisual != null)
                rightWheelVisual.Rotate(axis, rightDeg, Space.Self);
        }

        // Creates an empty pivot centred on the wheel mesh, reparents the mesh under it.
        private Transform SetupWheelPivot(Transform wheelVisual, string pivotName)
        {
            if (wheelVisual == null) return null;

            Vector3 pivotWorldPos = wheelVisual.position;
            if (useRendererBoundsForWheelPivot)
            {
                Renderer rend = wheelVisual.GetComponentInChildren<Renderer>();
                if (rend != null) pivotWorldPos = rend.bounds.center;
            }

            GameObject pivotGO = new GameObject(pivotName);
            Transform  pivot   = pivotGO.transform;
            pivot.position = pivotWorldPos;
            pivot.rotation = wheelVisual.rotation;

            pivot.SetParent(wheelVisual.parent, worldPositionStays: true);
            wheelVisual.SetParent(pivot,         worldPositionStays: true);

            if (showDebugLogs)
                Debug.Log($"[WheelchairLocomotion] Created {pivotName} at {pivotWorldPos}");

            return pivot;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawWheelPivotGizmos) return;
            Gizmos.color = Color.cyan;
            if (_leftWheelPivot  != null) Gizmos.DrawSphere(_leftWheelPivot.position,  0.05f);
            if (_rightWheelPivot != null) Gizmos.DrawSphere(_rightWheelPivot.position, 0.05f);
        }

        private static Vector3 WheelAxisToVector(WheelAxis axis)
        {
            switch (axis)
            {
                case WheelAxis.LocalY: return Vector3.up;
                case WheelAxis.LocalZ: return Vector3.forward;
                default:               return Vector3.right;   // LocalX
            }
        }

        // Remaps input through a power curve: softens small deflections, preserves sign and full range.
        private static float ApplyCurve(float input, float power)
        {
            return Mathf.Sign(input) * Mathf.Pow(Mathf.Abs(input), power);
        }

        // ── External push — future VR hand push-rim ────────────────────────
        /// <summary>
        /// Call each frame from a hand-tracking script to drive movement via push-rims.
        /// leftPush / rightPush: push strength per hand (0–1 typical, can exceed for fast push).
        /// Average (L+R)/2 → forward. Difference (R−L) → clockwise turn.
        /// Values are summed with controller input and clamped before applying.
        /// </summary>
        public void AddExternalPush(float leftPush, float rightPush)
        {
            _externalLinearInput  += (leftPush + rightPush) * 0.5f;
            _externalAngularInput += (rightPush - leftPush);
        }

        // ── Action placeholders ────────────────────────────────────────────
        public void TriggerAction()
        {
            if (showDebugLogs) Debug.Log("[WheelchairLocomotion] Action / throw requested.");
            // TODO: hook up to ball/throw system
        }

        public void TriggerPass()
        {
            if (showDebugLogs) Debug.Log("[WheelchairLocomotion] Pass / secondary action requested.");
            // TODO: hook up to pass system
        }

        public void TriggerQuickPivot()
        {
            if (showDebugLogs) Debug.Log("[WheelchairLocomotion] Quick pivot / trick requested.");
            // TODO: implement pivot burst turn
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private Vector2 Deadzone(Vector2 input)
        {
            float mag = input.magnitude;
            if (mag < joystickDeadzone) return Vector2.zero;
            // Rescale so the output starts at 0 at the deadzone edge, not inside it
            return input.normalized * ((mag - joystickDeadzone) / (1f - joystickDeadzone));
        }
    }
}
