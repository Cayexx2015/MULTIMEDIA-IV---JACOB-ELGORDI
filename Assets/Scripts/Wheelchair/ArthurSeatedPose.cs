using System.Collections.Generic;
using UnityEngine;

public class ArthurSeatedPose : MonoBehaviour
{
    [Header("Pose Control")]
    public bool applyPoseOnStart = true;
    public bool applyEveryFrame = true;
    public bool showDebugLogs = true;

    [Header("Hips / Spine")]
    public Vector3 hipsAxis    = Vector3.zero;
    public Vector3 spine01Axis = new Vector3(-5f, 0f, 0f);
    public Vector3 spine02Axis = new Vector3(-5f, 0f, 0f);

    [Header("Left Leg")]
    public Vector3 leftUpperLegAxis = new Vector3(85f,  0f, 0f);
    public Vector3 leftLowerLegAxis = new Vector3(-95f, 0f, 0f);
    public Vector3 leftFootAxis     = new Vector3(10f,  0f, 0f);

    [Header("Right Leg")]
    public Vector3 rightUpperLegAxis = new Vector3(85f,  0f, 0f);
    public Vector3 rightLowerLegAxis = new Vector3(-95f, 0f, 0f);
    public Vector3 rightFootAxis     = new Vector3(10f,  0f, 0f);

    [Header("Left Arm")]
    public Vector3 leftUpperArmAxis = new Vector3(0f,  0f, 20f);
    public Vector3 leftLowerArmAxis = new Vector3(45f, 0f, 0f);
    public Vector3 leftHandAxis     = new Vector3(15f, 0f, 0f);

    [Header("Right Arm")]
    public Vector3 rightUpperArmAxis = new Vector3(0f,  0f, -20f);
    public Vector3 rightLowerArmAxis = new Vector3(45f, 0f,  0f);
    public Vector3 rightHandAxis     = new Vector3(15f, 0f,  0f);

    // Maps search name → found bone transform
    private readonly Dictionary<string, Transform>  _bones     = new Dictionary<string, Transform>();
    // Maps search name → original localRotation captured in Start
    private readonly Dictionary<string, Quaternion> _restPose  = new Dictionary<string, Quaternion>();

    // Exact bone names to search for, ordered so shorter names come after longer ones
    // that share a prefix (e.g. LeftForeArm before LeftArm) to keep partial-match safe.
    private static readonly string[] BoneSearchNames =
    {
        "Hips",
        "Spine01",
        "Spine02",
        "LeftUpLeg",
        "RightUpLeg",
        "LeftLeg",
        "RightLeg",
        "LeftFoot",
        "RightFoot",
        "LeftForeArm",   // must be before LeftArm so partial match can't steal it
        "RightForeArm",  // must be before RightArm
        "LeftArm",
        "RightArm",
        "LeftHand",
        "RightHand",
    };

    private bool _poseAppliedOnce;

    void Start()
    {
        FindBones();

        if (applyPoseOnStart)
            ApplySeatedPose();
    }

    void LateUpdate()
    {
        if (!applyEveryFrame)
            return;

        ApplySeatedPose();
    }

    // -------------------------------------------------------------------------

    void FindBones()
    {
        _bones.Clear();
        _restPose.Clear();

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        foreach (string searchName in BoneSearchNames)
        {
            Transform found = FindBoneTransform(allChildren, searchName);

            if (found != null)
            {
                _bones[searchName]    = found;
                _restPose[searchName] = found.localRotation;

                if (showDebugLogs)
                    Debug.Log("[ArthurSeatedPose] Found bone: " + searchName + " -> " + found.name);
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogWarning("[ArthurSeatedPose] Missing bone: " + searchName);
            }
        }

        if (showDebugLogs)
            Debug.Log("[ArthurSeatedPose] Bone search complete. Found " + _bones.Count + " / " + BoneSearchNames.Length);
    }

    // Exact match (case-insensitive) first, then partial match fallback.
    // Exact-first prevents "LeftArm" from matching "LeftForeArm".
    Transform FindBoneTransform(Transform[] children, string searchName)
    {
        foreach (Transform t in children)
        {
            if (string.Equals(t.name, searchName, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }

        foreach (Transform t in children)
        {
            if (t.name.IndexOf(searchName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }

        return null;
    }

    // -------------------------------------------------------------------------

    void ApplySeatedPose()
    {
        if (showDebugLogs && !_poseAppliedOnce)
        {
            Debug.Log("[ArthurSeatedPose] Applying seated pose on " + gameObject.name);
            _poseAppliedOnce = true;
        }

        SetBone("Hips",       hipsAxis);
        SetBone("Spine01",    spine01Axis);
        SetBone("Spine02",    spine02Axis);

        SetBone("LeftUpLeg",  leftUpperLegAxis);
        SetBone("LeftLeg",    leftLowerLegAxis);
        SetBone("LeftFoot",   leftFootAxis);

        SetBone("RightUpLeg", rightUpperLegAxis);
        SetBone("RightLeg",   rightLowerLegAxis);
        SetBone("RightFoot",  rightFootAxis);

        SetBone("LeftArm",     leftUpperArmAxis);
        SetBone("LeftForeArm", leftLowerArmAxis);
        SetBone("LeftHand",    leftHandAxis);

        SetBone("RightArm",     rightUpperArmAxis);
        SetBone("RightForeArm", rightLowerArmAxis);
        SetBone("RightHand",    rightHandAxis);
    }

    void SetBone(string searchName, Vector3 axis)
    {
        if (!_bones.TryGetValue(searchName, out Transform t) || t == null)
            return;

        if (!_restPose.TryGetValue(searchName, out Quaternion rest))
            return;

        t.localRotation = rest * Quaternion.Euler(axis);
    }
}
