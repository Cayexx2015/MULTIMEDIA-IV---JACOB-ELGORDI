using UnityEngine;

namespace FieldUnlocked.Wheelchair
{
    /// <summary>
    /// Animates first-person hand visuals to mimic wheelchair push-rim strokes.
    /// Visual only — does not affect movement. Reads state from WheelchairLocomotion.
    ///
    /// Setup:
    ///   1. Attach to the same GameObject as WheelchairLocomotion, or any GameObject.
    ///   2. Assign leftHandVisual / rightHandVisual, or enable createPrimitiveHandsIfMissing.
    ///   3. Hands are parented to cameraTransform and driven entirely in camera-local space.
    /// </summary>
    public class WheelchairHandAnimator : MonoBehaviour
    {
        [Header("References")]
        public WheelchairLocomotion locomotion;
        public Transform            cameraTransform;
        public Transform            leftHandVisual;
        public Transform            rightHandVisual;
        public bool                 createPrimitiveHandsIfMissing = true;
        public float                handScale                     = 0.12f;

        [Header("Hand Placement")]
        public Vector3 leftHandRestLocalPosition  = new Vector3(-0.42f, -0.42f, 0.55f);
        public Vector3 rightHandRestLocalPosition = new Vector3( 0.42f, -0.42f, 0.55f);

        [Header("Animation")]
        public float pushStrokeLength      = 0.22f;   // total Z travel of the push stroke
        public float pushVerticalArc       = 0.08f;   // Y dip/rise through the stroke
        public float pushAnimationSpeed    = 7f;      // phase advance rate (rad/s) at full wheel power
        public float boostStrokeMultiplier = 1.45f;   // stroke scale when RT/boost is held
        public float brakeHandTension      = 0.08f;   // downward Y offset when LT/brake is held
        public float turnHandOffset        = 0.12f;   // Z shift for the more-active hand during turns
        public float handSmoothness        = 12f;     // exponential smoothing coefficient

        [Header("Debug")]
        public bool showDebugLogs = false;

        // Per-hand push-cycle phases (radians); advance with abs(wheelPower)
        private float _leftPhase;
        private float _rightPhase;

        // Exponentially-smoothed local-space pose
        private Vector3    _leftSmoothed;
        private Vector3    _rightSmoothed;
        private Quaternion _leftRotSmoothed;
        private Quaternion _rightRotSmoothed;

        // ──────────────────────────────────────────────────────────────────
        private void Start()
        {
            // Auto-find locomotion on this GameObject or any parent
            if (locomotion == null) locomotion = GetComponent<WheelchairLocomotion>();
            if (locomotion == null) locomotion = GetComponentInParent<WheelchairLocomotion>();
            if (locomotion == null)
                Debug.LogWarning("[WheelchairHandAnimator] WheelchairLocomotion not found. Assign it in the Inspector.");

            // Auto-find camera
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (cameraTransform == null)
            {
                Debug.LogWarning("[WheelchairHandAnimator] No camera found — disabling.");
                enabled = false;
                return;
            }

            // Create placeholder hands if none are assigned
            if (leftHandVisual  == null && createPrimitiveHandsIfMissing)
                leftHandVisual  = CreatePrimitiveHand("LeftHandVisual",  new Color(0.82f, 0.69f, 0.56f));
            if (rightHandVisual == null && createPrimitiveHandsIfMissing)
                rightHandVisual = CreatePrimitiveHand("RightHandVisual", new Color(0.82f, 0.69f, 0.56f));

            // Parent manually-assigned hands to camera only when they have no parent
            if (leftHandVisual  != null && leftHandVisual.parent  == null)
                leftHandVisual.SetParent(cameraTransform,  worldPositionStays: false);
            if (rightHandVisual != null && rightHandVisual.parent == null)
                rightHandVisual.SetParent(cameraTransform, worldPositionStays: false);

            // Seed smoothed values at rest so there is no pop on the first frame
            _leftSmoothed     = leftHandRestLocalPosition;
            _rightSmoothed    = rightHandRestLocalPosition;
            _leftRotSmoothed  = HandBaseRotation(isLeft: true);
            _rightRotSmoothed = HandBaseRotation(isLeft: false);

            if (leftHandVisual != null)
            {
                leftHandVisual.localPosition = _leftSmoothed;
                leftHandVisual.localRotation = _leftRotSmoothed;
            }
            if (rightHandVisual != null)
            {
                rightHandVisual.localPosition = _rightSmoothed;
                rightHandVisual.localRotation = _rightRotSmoothed;
            }

            if (showDebugLogs)
                Debug.Log($"[WheelchairHandAnimator] Initialized on {gameObject.name}");
        }

        private void Update()
        {
            if (locomotion == null || cameraTransform == null) return;

            float dt = Time.deltaTime;

            float leftPower  = locomotion.CurrentLeftWheelPower;
            float rightPower = locomotion.CurrentRightWheelPower;
            bool  boost      = locomotion.IsBoostHeld;
            bool  brake      = locomotion.IsBrakeHeld;
            bool  pulse      = locomotion.IsPushPulseActive;
            float turnInput  = locomotion.CurrentTurnInput;

            // Advance each hand's phase proportional to its wheel's activity.
            // Phases are independent so the left and right hands can be out of step
            // during turns and pivots — which looks natural for differential pushing.
            _leftPhase  += Mathf.Abs(leftPower)  * pushAnimationSpeed * dt;
            _rightPhase += Mathf.Abs(rightPower) * pushAnimationSpeed * dt;

            // Prevent unbounded growth (wrap at 1000 full cycles)
            const float kWrap = 6283.2f; // 2π × 1000
            if (_leftPhase  > kWrap) _leftPhase  -= kWrap;
            if (_rightPhase > kWrap) _rightPhase -= kWrap;

            // Boost scaling: peak amplitude during an active push pulse,
            // slightly reduced between pulses to create the push/coast rhythm.
            float boostScale = boost
                ? (pulse ? boostStrokeMultiplier : boostStrokeMultiplier * 0.82f)
                : 1f;

            // ── Target positions ───────────────────────────────────────────
            // Left hand turn contribution is +turnInput:
            //   when turning right (turnInput > 0), the left wheel is more active
            //   → left hand shifts forward to show higher engagement.
            // Right hand turn contribution is −turnInput (mirror).
            Vector3 leftTarget  = ComputeHandPosition(
                leftHandRestLocalPosition,  _leftPhase,  leftPower,  brake,  turnInput,  boostScale);
            Vector3 rightTarget = ComputeHandPosition(
                rightHandRestLocalPosition, _rightPhase, rightPower, brake, -turnInput, boostScale);

            // ── Target rotations ───────────────────────────────────────────
            Quaternion leftRotTarget  = ComputeHandRotation(isLeft: true,  phase: _leftPhase,  wheelPower: leftPower,  braking: brake);
            Quaternion rightRotTarget = ComputeHandRotation(isLeft: false, phase: _rightPhase, wheelPower: rightPower, braking: brake);

            // ── Frame-rate-independent exponential smoothing ───────────────
            float s = 1f - Mathf.Exp(-handSmoothness * dt);
            _leftSmoothed     = Vector3.Lerp(_leftSmoothed,     leftTarget,      s);
            _rightSmoothed    = Vector3.Lerp(_rightSmoothed,    rightTarget,     s);
            _leftRotSmoothed  = Quaternion.Slerp(_leftRotSmoothed,  leftRotTarget,  s);
            _rightRotSmoothed = Quaternion.Slerp(_rightRotSmoothed, rightRotTarget, s);

            // ── Apply ──────────────────────────────────────────────────────
            if (leftHandVisual != null)
            {
                leftHandVisual.localPosition = _leftSmoothed;
                leftHandVisual.localRotation = _leftRotSmoothed;
            }
            if (rightHandVisual != null)
            {
                rightHandVisual.localPosition = _rightSmoothed;
                rightHandVisual.localRotation = _rightRotSmoothed;
            }

            if (showDebugLogs)
                Debug.Log($"[WheelchairHandAnimator] " +
                          $"L={leftPower:F2} R={rightPower:F2} " +
                          $"boost={boost} pulse={pulse} brake={brake} " +
                          $"lPos={_leftSmoothed}  rPos={_rightSmoothed}");
        }

        // ── Hand position ──────────────────────────────────────────────────
        /// <summary>
        /// Computes the target camera-local position for one hand.
        /// The stroke traces a vertical oval:
        ///   phase=0 → hand forward (contact/grip)
        ///   phase=π → hand backward (end of push, release)
        ///   phase=2π → back to forward (next contact)
        /// The Y arc dips during the push stroke and rises during recovery.
        /// </summary>
        private Vector3 ComputeHandPosition(Vector3 restPos, float phase, float wheelPower,
                                            bool braking, float turnContrib, float boostScale)
        {
            float mag       = Mathf.Abs(wheelPower) * boostScale;
            float strokeDir = wheelPower >= 0f ? 1f : -1f;

            // Z (camera-local forward/back):
            //   Cos(phase) starts at +1 (forward/contact) and swings to −1 (back/release).
            //   strokeDir flips the axis for reverse so the hand correctly "pulls" instead of "pushes".
            float offsetZ = strokeDir * Mathf.Cos(phase) * pushStrokeLength * 0.5f * mag;

            // Y (up/down):
            //   −Sin(phase) dips (−) during the first half (push stroke)
            //   and rises (+) during the second half (recovery).
            float offsetY = -Mathf.Sin(phase) * pushVerticalArc * mag;

            // Differential turn cue: active-side hand shifts forward (+Z), passive side lags (−Z).
            float turnZ = turnContrib * turnHandOffset;

            if (braking)
            {
                // Grip/braking posture: minimal stroke, hands pulled slightly down and steady.
                offsetZ *= 0.15f;
                offsetY  = -brakeHandTension;
                turnZ   *= 0.25f;
            }

            return new Vector3(
                restPos.x,
                restPos.y + offsetY,
                restPos.z + offsetZ + turnZ
            );
        }

        // ── Hand rotation ──────────────────────────────────────────────────
        /// <summary>
        /// Computes the target camera-local rotation for one hand.
        /// Base orientation points each hand outward toward its wheel with a slight downward tilt.
        /// A subtle wrist roll/pitch modulation through the push cycle adds liveliness.
        /// </summary>
        private Quaternion ComputeHandRotation(bool isLeft, float phase, float wheelPower, bool braking)
        {
            // Base rest orientation — hand angled toward its wheel rim
            float pitch = 15f;                     // tilt down toward rim
            float yaw   = isLeft ? -25f :  25f;    // rotate outward toward wheel side
            float roll  = isLeft ?   8f :  -8f;    // slight inward cant for a natural grip posture

            if (braking)
            {
                pitch += 18f;   // press down into the wheel grip
            }
            else
            {
                // Subtle wrist dynamics through the push cycle
                float mag = Mathf.Abs(wheelPower) * 0.6f;
                pitch += -Mathf.Sin(phase) * 10f * mag;   // pitches forward at contact, back at release
                roll  +=  Mathf.Cos(phase) *  4f * mag;   // slight roll at mid-stroke
            }

            return Quaternion.Euler(pitch, yaw, roll);
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private static Quaternion HandBaseRotation(bool isLeft)
        {
            return Quaternion.Euler(15f, isLeft ? -25f : 25f, isLeft ? 8f : -8f);
        }

        /// <summary>
        /// Creates a capsule primitive as a simple first-person hand stand-in.
        /// The collider is removed so it cannot interact with physics.
        /// </summary>
        private Transform CreatePrimitiveHand(string handName, Color skinColor)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = handName;

            // Slightly flattened capsule — reads as a hand/fist from first-person
            go.transform.localScale = new Vector3(handScale, handScale * 1.6f, handScale * 0.7f);

            // Visual only — remove all physics influence
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Tint with a skin tone so it reads as a hand
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                rend.material       = new Material(rend.sharedMaterial);
                rend.material.color = skinColor;
            }

            // Parent to camera; localPosition will be driven every frame in Update
            go.transform.SetParent(cameraTransform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = HandBaseRotation(handName.Contains("Left"));

            if (showDebugLogs)
                Debug.Log($"[WheelchairHandAnimator] Created primitive hand '{handName}'");

            return go.transform;
        }
    }
}
