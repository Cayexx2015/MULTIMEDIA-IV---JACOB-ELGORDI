using UnityEngine;
using FieldUnlocked.Wheelchair;

/// <summary>
/// Animates Arthur's arms/hands with a realistic wheelchair push-rim stroke,
/// modelled after the Drag x Drive motion:
///
///   Phase 0.00 → hand reaches FORWARD-DOWN to contact the rim (top of wheel)
///   Phase 0.40 → hand pushes DOWN-BACK through the power stroke
///   Phase 0.68 → hand releases at the bottom-back of the rim
///   Phase 1.00 → arm LIFTS and swings back forward (recovery arc)
///
/// The stroke is applied as additive local-rotation on each arm bone,
/// so it layers on top of whatever pose the Animator already sets.
///
/// Setup:
///   • Attach to Arthur_Rigged_01 (same GameObject as the Animator).
///   • Drag the XR Origin (VR) GameObject into the Locomotion Source field.
///   • Leave Test Mode ON first to verify the motion looks right, then turn it OFF.
/// </summary>
[DefaultExecutionOrder(10000)]   // runs after Animator has written its pose
public class ArthurWheelchairHandMotion : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Drag the XR Origin (VR) GameObject here.")]
    public WheelchairLocomotion locomotionSource;

    // ── Mode ──────────────────────────────────────────────────────────────
    [Header("Mode")]
    [Tooltip("ON = arms move constantly so you can preview the motion. OFF = driven by joystick.")]
    public bool testMode = true;
    public bool showDebugLogs = false;

    // ── Timing ────────────────────────────────────────────────────────────
    [Header("Timing  (strokes per second at full speed)")]
    [Tooltip("Stroke frequency at normal speed.")]
    public float cycleSpeed = 1.2f;
    [Tooltip("Stroke frequency when boost is held.")]
    public float boostCycleSpeed = 2.2f;
    [Tooltip("Fraction of the cycle spent in the push stroke (rest = recovery lift).")]
    [Range(0.3f, 0.8f)]
    public float pushPortion = 0.60f;
    [Tooltip("How quickly the arm smooths toward the target rotation.")]
    public float smoothing = 10f;

    // ── Push stroke angles (degrees) ──────────────────────────────────────
    // These are LOCAL Euler offsets added on top of the animator pose.
    // Positive X = arm swings forward/down (into the push).
    // The numbers below are tuned for Arthur's rig where LeftArm rests at
    // approximately X=25 Y=-79 Z=-55 in world space.
    [Header("Upper Arm  (shoulder swing)")]
    [Tooltip("Rotation at the START of the push stroke (reaching forward to rim).")]
    public Vector3 leftArmReach = new Vector3(18f, 0f, -6f);
    [Tooltip("Rotation at the END of the push stroke (arm swept back after push).")]
    public Vector3 leftArmPushed = new Vector3(-14f, 0f, 4f);

    [Tooltip("Mirror of left – signs on Y and Z are flipped.")]
    public Vector3 rightArmReach = new Vector3(18f, 0f, 6f);
    public Vector3 rightArmPushed = new Vector3(-14f, 0f, -4f);

    [Header("Forearm  (elbow bend)")]
    public Vector3 leftForeReach = new Vector3(12f, 8f, 0f);
    public Vector3 leftForePushed = new Vector3(-8f, -6f, 0f);
    public Vector3 rightForeReach = new Vector3(12f, -8f, 0f);
    public Vector3 rightForePushed = new Vector3(-8f, 6f, 0f);

    [Header("Hand  (wrist grip/release)")]
    public Vector3 leftHandReach = new Vector3(8f, 6f, 0f);
    public Vector3 leftHandPushed = new Vector3(-6f, -4f, 0f);
    public Vector3 rightHandReach = new Vector3(8f, -6f, 0f);
    public Vector3 rightHandPushed = new Vector3(-6f, 4f, 0f);

    [Header("Torso sway")]
    [Tooltip("How much the spine leans forward during the push stroke.")]
    public float torsoLean = 3f;
    public float boostTorsoLean = 6f;

    // ── Brake ─────────────────────────────────────────────────────────────
    [Header("Brake")]
    [Tooltip("Arms stiffen to this fraction of normal amplitude when braking.")]
    [Range(0f, 1f)]
    public float brakeHoldAmount = 0.25f;

    // ── Private state ─────────────────────────────────────────────────────
    private Transform spine01, spine02;
    private Transform leftArm, leftForeArm, leftHand;
    private Transform rightArm, rightForeArm, rightHand;

    private float leftPhase, rightPhase;           // 0..1 stroke cycle
    private float smoothedLeft, smoothedRight;     // smoothed wheel power
    private float logTimer;

    // ── Start ─────────────────────────────────────────────────────────────
    private void Start()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        spine01 = Find(all, "Spine01");
        spine02 = Find(all, "Spine02");
        leftArm = Find(all, "LeftArm", "LeftUpperArm");
        leftForeArm = Find(all, "LeftForeArm", "LeftLowerArm");
        leftHand = Find(all, "LeftHand");
        rightArm = Find(all, "RightArm", "RightUpperArm");
        rightForeArm = Find(all, "RightForeArm", "RightLowerArm");
        rightHand = Find(all, "RightHand");

        if (locomotionSource == null)
            locomotionSource = FindFirstObjectByType<WheelchairLocomotion>();

        if (showDebugLogs)
        {
            Debug.Log($"[HandMotion] locomotion = {(locomotionSource ? locomotionSource.name : "NONE")}");
            Debug.Log($"[HandMotion] bones: spine01={N(spine01)} spine02={N(spine02)} " +
                      $"LA={N(leftArm)} LFA={N(leftForeArm)} LH={N(leftHand)} " +
                      $"RA={N(rightArm)} RFA={N(rightForeArm)} RH={N(rightHand)}");
        }
    }

    // ── LateUpdate — runs after Animator ──────────────────────────────────
    private void LateUpdate()
    {
        // 1. Read locomotion state
        float leftPower, rightPower;
        bool boost, brake;

        if (testMode || locomotionSource == null)
        {
            leftPower = 1f;
            rightPower = 1f;
            boost = false;
            brake = false;
        }
        else
        {
            leftPower = locomotionSource.CurrentLeftWheelPower;
            rightPower = locomotionSource.CurrentRightWheelPower;
            boost = locomotionSource.IsBoostHeld || locomotionSource.IsPushPulseActive;
            brake = locomotionSource.IsBrakeHeld;
        }

        // 2. Smooth power so arms don't snap on direction changes
        float dt = Time.deltaTime;
        float spd = smoothing;
        smoothedLeft = Mathf.Lerp(smoothedLeft, leftPower, dt * spd);
        smoothedRight = Mathf.Lerp(smoothedRight, rightPower, dt * spd);

        // 3. Advance stroke phase (each wheel independent → differential turning looks natural)
        float freq = boost ? boostCycleSpeed : cycleSpeed;
        float minF = 0.3f;   // arms keep a tiny idle motion even near zero to avoid a dead stop

        leftPhase += dt * freq * Mathf.Max(minF, Mathf.Abs(smoothedLeft));
        rightPhase += dt * freq * Mathf.Max(minF, Mathf.Abs(smoothedRight));

        // Prevent float overflow after a long session
        leftPhase = Mathf.Repeat(leftPhase, 1000f);
        rightPhase = Mathf.Repeat(rightPhase, 1000f);

        // 4. Compute stroke weight [0..1] for each hand
        //    strokeWeight = 0 at reach/contact, rises to 1 at full push, back to 0 at recovery
        float lw = StrokeWeight(leftPhase, Mathf.Sign(smoothedLeft == 0 ? 1 : smoothedLeft));
        float rw = StrokeWeight(rightPhase, Mathf.Sign(smoothedRight == 0 ? 1 : smoothedRight));

        if (brake)
        {
            lw *= brakeHoldAmount;
            rw *= brakeHoldAmount;
        }

        // 5. Interpolate arm rotations between REACH pose and PUSHED pose
        float la = Mathf.Abs(smoothedLeft);
        float ra = Mathf.Abs(smoothedRight);

        ApplyLerp(leftArm, leftArmReach, leftArmPushed, lw, la);
        ApplyLerp(leftForeArm, leftForeReach, leftForePushed, lw, la);
        ApplyLerp(leftHand, leftHandReach, leftHandPushed, lw, la);

        ApplyLerp(rightArm, rightArmReach, rightArmPushed, rw, ra);
        ApplyLerp(rightForeArm, rightForeReach, rightForePushed, rw, ra);
        ApplyLerp(rightHand, rightHandReach, rightHandPushed, rw, ra);

        // 6. Spine lean — average of both wheels → forward, difference → side sway
        float avg = (lw * la + rw * ra) * 0.5f;
        float diff = (rw * ra - lw * la) * 0.5f;
        float tAmt = boost ? boostTorsoLean : torsoLean;

        ApplyAdditive(spine01, new Vector3(avg * tAmt, diff * 1.5f, 0f));
        ApplyAdditive(spine02, new Vector3(avg * tAmt * 0.4f, 0f, 0f));

        // 7. Debug log
        if (showDebugLogs)
        {
            logTimer += dt;
            if (logTimer >= 1f)
            {
                logTimer = 0f;
                Debug.Log($"[HandMotion] L={leftPower:F2} R={rightPower:F2} " +
                          $"lw={lw:F2} rw={rw:F2} boost={boost} brake={brake}");
            }
        }
    }

    // ── Stroke curve ──────────────────────────────────────────────────────
    /// <summary>
    /// Returns a weight value that drives the arm from REACH (0) → PUSHED (1) → back to REACH (0)
    /// following the Drag-x-Drive style arc:
    ///   • Push half  (0 → pushPortion)   : smooth acceleration into the push
    ///   • Lift half  (pushPortion → 1.0) : fast lift/swing back (recovery)
    /// When direction is negative (reversing) the weight is negated so the arm
    /// pulls instead of pushes.
    /// </summary>
    private float StrokeWeight(float phase, float direction)
    {
        float t = Mathf.Repeat(phase, 1f);
        float w;

        if (t < pushPortion)
        {
            // Push stroke: slow at start (reaching), accelerates to peak at pushPortion
            float pt = t / pushPortion;
            w = Mathf.SmoothStep(0f, 1f, pt);
        }
        else
        {
            // Recovery lift: fast arc back to reach position
            float rt = (t - pushPortion) / (1f - pushPortion);
            w = Mathf.SmoothStep(1f, 0f, rt);
        }

        return w * direction;
    }

    // ── Bone helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Additively applies an euler offset to a bone's LOCAL rotation.
    /// Scaled by the wheel's absolute power so arms are still at rest when not moving.
    /// </summary>
    private void ApplyLerp(Transform bone, Vector3 reachEuler, Vector3 pushedEuler,
                           float strokeWeight, float powerMag)
    {
        if (bone == null) return;

        // strokeWeight in [-1..1]: negative = pulling/reverse, positive = pushing/forward
        // For display we use abs and flip reach/pushed when going backward
        float absW = Mathf.Abs(strokeWeight);
        Vector3 from = strokeWeight >= 0f ? reachEuler : pushedEuler;
        Vector3 to = strokeWeight >= 0f ? pushedEuler : reachEuler;

        Vector3 offset = Vector3.Lerp(from, to, absW) * powerMag;
        bone.localRotation *= Quaternion.Euler(offset);
    }

    private void ApplyAdditive(Transform bone, Vector3 eulerOffset)
    {
        if (bone == null) return;
        bone.localRotation *= Quaternion.Euler(eulerOffset);
    }

    private Transform Find(Transform[] bones, params string[] names)
    {
        foreach (Transform b in bones)
        {
            string cb = Clean(b.name);
            foreach (string n in names)
                if (cb == Clean(n) || cb.Contains(Clean(n)))
                    return b;
        }
        return null;
    }

    private string Clean(string s) =>
        s.ToLower().Replace(" ", "").Replace("_", "").Replace("-", "").Replace(":", "");

    private string N(Transform t) => t == null ? "MISSING" : t.name;
}
