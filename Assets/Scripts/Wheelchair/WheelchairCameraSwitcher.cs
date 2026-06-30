using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WheelchairCameraSwitcher : MonoBehaviour
{
    public enum CameraMode { FirstPerson, ThirdPerson }

    [Header("References")]
    public Transform  cameraOffsetTransform;
    public Transform  firstPersonTarget;
    public Transform  thirdPersonTarget;
    public GameObject fullBodyCharacter;

    [Header("Mode")]
    public CameraMode currentMode = CameraMode.FirstPerson;

    [Header("Testing")]
    public bool toggleNow            = false;
    public bool instantSnapForTesting = true;

    [Header("Input")]
    public bool    useKeyboardToggle  = true;
    public KeyCode keyboardToggleKey  = KeyCode.C;
    public bool    useGamepadToggle   = true;
    public bool    useRightStickClick = true;
    public bool    useNorthButton     = true;

    [Header("Settings")]
    public float positionSmooth             = 12f;
    public bool  hideCharacterInFirstPerson = false;
    public bool  showDebugLogs              = true;

    // -------------------------------------------------------------------------

    private Renderer[] _bodyRenderers;
    private bool       _ready;

    // -------------------------------------------------------------------------

    void Start()
    {
        if (!ReferencesValid())
        {
            enabled = false;
            return;
        }

        CacheBodyRenderers();
        _ready = true;

        ApplyCurrentMode(snap: true);

        if (showDebugLogs)
            Debug.Log("[WheelchairCameraSwitcher] Ready. Mode: " + currentMode);
    }

    void Update()
    {
        if (!_ready) return;

        // Inspector toggle — set toggleNow = true in Play Mode to test switching.
        if (toggleNow)
        {
            toggleNow = false;
            ToggleCameraMode();
            return;
        }

        if (useKeyboardToggle && Input.GetKeyDown(keyboardToggleKey))
        {
            ToggleCameraMode();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (useGamepadToggle && Gamepad.current != null)
        {
            if (useRightStickClick && Gamepad.current.rightStickButton.wasPressedThisFrame)
            {
                ToggleCameraMode();
                return;
            }

            if (useNorthButton && Gamepad.current.buttonNorth.wasPressedThisFrame)
            {
                ToggleCameraMode();
                return;
            }
        }
#endif
    }

    // Runs after WheelchairLocomotion. Only writes cameraOffsetTransform.localPosition.
    // Never touches rotation. Never touches Main Camera.
    void LateUpdate()
    {
        if (!_ready) return;

        Transform activeTarget = currentMode == CameraMode.FirstPerson
            ? firstPersonTarget
            : thirdPersonTarget;

        if (instantSnapForTesting)
        {
            cameraOffsetTransform.localPosition = activeTarget.localPosition;
        }
        else
        {
            cameraOffsetTransform.localPosition = Vector3.Lerp(
                cameraOffsetTransform.localPosition,
                activeTarget.localPosition,
                Time.deltaTime * positionSmooth);
        }
    }

    // -------------------------------------------------------------------------

    public void ToggleCameraMode()
    {
        currentMode = currentMode == CameraMode.FirstPerson
            ? CameraMode.ThirdPerson
            : CameraMode.FirstPerson;

        ApplyCurrentMode(snap: false);

        if (showDebugLogs)
            Debug.Log("[WheelchairCameraSwitcher] Toggled -> " + currentMode);
    }

    // -------------------------------------------------------------------------

    void ApplyCurrentMode(bool snap)
    {
        bool showBody = currentMode == CameraMode.ThirdPerson || !hideCharacterInFirstPerson;
        SetBodyRenderersVisible(showBody);

        if (snap && cameraOffsetTransform != null)
        {
            Transform activeTarget = currentMode == CameraMode.FirstPerson
                ? firstPersonTarget
                : thirdPersonTarget;

            if (activeTarget != null)
                cameraOffsetTransform.localPosition = activeTarget.localPosition;
        }
    }

    bool ReferencesValid()
    {
        bool ok = true;

        if (cameraOffsetTransform == null)
        {
            Debug.LogWarning("[WheelchairCameraSwitcher] cameraOffsetTransform not assigned. Script disabled.");
            ok = false;
        }

        if (firstPersonTarget == null)
        {
            Debug.LogWarning("[WheelchairCameraSwitcher] firstPersonTarget not assigned. Script disabled.");
            ok = false;
        }

        if (thirdPersonTarget == null)
        {
            Debug.LogWarning("[WheelchairCameraSwitcher] thirdPersonTarget not assigned. Script disabled.");
            ok = false;
        }

        return ok;
    }

    void CacheBodyRenderers()
    {
        if (fullBodyCharacter == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[WheelchairCameraSwitcher] fullBodyCharacter not assigned — renderer toggling disabled.");
            return;
        }

        var all = fullBodyCharacter.GetComponentsInChildren<Renderer>(true);

        int count = 0;
        foreach (var r in all)
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
                count++;

        _bodyRenderers = new Renderer[count];
        int i = 0;
        foreach (var r in all)
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
                _bodyRenderers[i++] = r;

        if (showDebugLogs)
            Debug.Log("[WheelchairCameraSwitcher] Cached " + _bodyRenderers.Length +
                      " renderers in " + fullBodyCharacter.name);
    }

    void SetBodyRenderersVisible(bool visible)
    {
        if (_bodyRenderers == null) return;

        foreach (Renderer r in _bodyRenderers)
            r.enabled = visible;
    }
}
