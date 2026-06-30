using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public static class SetupArthurArmIK
{
    [MenuItem("Tools/Wheelchair/Setup Arthur Arm IK")]
    private static void Setup()
    {
        var arthur = GameObject.Find("Arthur_Rigged_01");
        if (arthur == null) { Debug.LogError("[SetupArthurArmIK] Arthur_Rigged_01 not found in scene."); return; }

        var wheelchairVisual = GameObject.Find("WheelchairVisual");
        if (wheelchairVisual == null) { Debug.LogError("[SetupArthurArmIK] WheelchairVisual not found in scene."); return; }

        // Bone references
        Transform leftArm      = FindDescendant(arthur.transform, "LeftArm");
        Transform leftForeArm  = FindDescendant(arthur.transform, "LeftForeArm");
        Transform leftHand     = FindDescendant(arthur.transform, "LeftHand");
        Transform rightArm     = FindDescendant(arthur.transform, "RightArm");
        Transform rightForeArm = FindDescendant(arthur.transform, "RightForeArm");
        Transform rightHand    = FindDescendant(arthur.transform, "RightHand");

        if (!leftArm || !leftForeArm || !leftHand || !rightArm || !rightForeArm || !rightHand)
        {
            Debug.LogError("[SetupArthurArmIK] One or more arm bones not found. Check the hierarchy.");
            return;
        }

        // 3. RigBuilder on Arthur_Rigged_01
        var rigBuilder = arthur.GetComponent<RigBuilder>();
        if (rigBuilder == null)
            rigBuilder = Undo.AddComponent<RigBuilder>(arthur);

        // 4. Rig_Arms child
        var rigArmsT = arthur.transform.Find("Rig_Arms");
        if (rigArmsT == null)
        {
            var go = new GameObject("Rig_Arms");
            Undo.RegisterCreatedObjectUndo(go, "Create Rig_Arms");
            Undo.SetTransformParent(go.transform, arthur.transform, "Parent Rig_Arms");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            rigArmsT = go.transform;
        }

        // 5. Rig component
        var rig = rigArmsT.GetComponent<Rig>();
        if (rig == null)
            rig = Undo.AddComponent<Rig>(rigArmsT.gameObject);

        // 6. Add Rig_Arms to RigBuilder layers if missing
        bool alreadyInLayers = false;
        foreach (var layer in rigBuilder.layers)
            if (layer.rig == rig) { alreadyInLayers = true; break; }

        if (!alreadyInLayers)
        {
            Undo.RecordObject(rigBuilder, "Add Rig Layer");
            rigBuilder.layers.Add(new RigLayer(rig, true));
        }

        // 7 & 8. Target / hint objects under WheelchairVisual
        var leftHandTarget  = GetOrCreateTarget(wheelchairVisual.transform, "LeftHandTarget",  leftHand.position);
        var rightHandTarget = GetOrCreateTarget(wheelchairVisual.transform, "RightHandTarget", rightHand.position);

        // Hints: near elbow, offset outward (left = -X world, right = +X world) and slightly back
        Vector3 leftHintPos  = leftForeArm.position  + new Vector3(-0.15f, 0f, -0.1f);
        Vector3 rightHintPos = rightForeArm.position + new Vector3( 0.15f, 0f, -0.1f);
        var leftHandHint  = GetOrCreateTarget(wheelchairVisual.transform, "LeftHandHint",  leftHintPos);
        var rightHandHint = GetOrCreateTarget(wheelchairVisual.transform, "RightHandHint", rightHintPos);

        // 9. IK child objects under Rig_Arms
        var leftIKT  = GetOrCreateChild(rigArmsT, "LeftArmIK");
        var rightIKT = GetOrCreateChild(rigArmsT, "RightArmIK");

        // 10-13. TwoBoneIKConstraint — left
        SetupTwoBoneIK(leftIKT.gameObject,
            leftArm, leftForeArm, leftHand,
            leftHandTarget, leftHandHint);

        // 10-13. TwoBoneIKConstraint — right
        SetupTwoBoneIK(rightIKT.gameObject,
            rightArm, rightForeArm, rightHand,
            rightHandTarget, rightHandHint);

        // 14. Mark scene dirty
        EditorUtility.SetDirty(arthur);
        EditorUtility.SetDirty(wheelchairVisual);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(arthur.scene);

        Debug.Log("[SetupArthurArmIK] Arthur Arm IK setup complete.");
    }

    private static void SetupTwoBoneIK(
        GameObject ikGO,
        Transform root, Transform mid, Transform tip,
        Transform target, Transform hint)
    {
        var constraint = ikGO.GetComponent<TwoBoneIKConstraint>();
        if (constraint == null)
            constraint = Undo.AddComponent<TwoBoneIKConstraint>(ikGO);

        Undo.RecordObject(constraint, "Configure TwoBoneIK");

        constraint.weight = 1f;

        constraint.data.root = root;
        constraint.data.mid  = mid;
        constraint.data.tip  = tip;

        constraint.data.target = target;
        constraint.data.hint   = hint;

        constraint.data.targetPositionWeight = 1f;
        constraint.data.targetRotationWeight = 0.35f;
        constraint.data.hintWeight           = 1f;

        EditorUtility.SetDirty(ikGO);
    }

    private static Transform GetOrCreateTarget(Transform parent, string name, Vector3 worldPos)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Undo.SetTransformParent(go.transform, parent, "Parent " + name);
        go.transform.position      = worldPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;
        return go.transform;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Undo.SetTransformParent(go.transform, parent, "Parent " + name);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;
        return go.transform;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            var found = FindDescendant(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
