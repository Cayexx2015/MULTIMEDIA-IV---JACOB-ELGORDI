using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Adjuntar al root del NPC Arthur.
///
/// Flujo:
///   1. Jugador se acerca (proximityRadius) → Arthur reluce con aura amarilla
///      (PointLight pulsante) y aparece texto "E  /  X" flotante.
///   2. Jugador pulsa E (teclado) o West button (gamepad) → panel de diálogo.
///   3. Jugador pulsa Enter/Space → diálogo se cierra, jugador teleporta a la
///      cancha y arranca BasketballGameManager.BeginMatch().
/// </summary>
public class NpcInteractable : MonoBehaviour
{
    [Header("Proximidad")]
    public float proximityRadius = 3.5f;

    [Header("Aura amarilla")]
    public Color auraColor        = new Color(1f, 0.95f, 0.1f, 0.55f);
    public float auraPulseSpeed   = 2.2f;
    public float auraIntensityMin = 1.2f;
    public float auraIntensityMax = 3.5f;

    [Header("Prompt flotante")]
    public string promptText        = "E  /  X";
    public float  promptHeightOffset = 2.2f;

    [Header("Panel de diálogo")]
    public Vector2 dialogueSizeMeters  = new Vector2(1.4f, 0.85f);
    public Vector3 dialogueLocalOffset = new Vector3(1.8f, 1.6f, 0f);

    [Header("Teleport al iniciar juego")]
    public Vector3 courtSpawnPosition  = new Vector3(0f, 0f, 10f);
    public float   courtSpawnYRotation = 0f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // ── private state ──────────────────────────────────────────────────────────
    Transform  _player;
    bool       _inRange;
    bool       _dialogueVisible;
    bool       _materialsCloned;

    readonly List<(Renderer r, Material[] originals, Material[] clones)> _renderers =
        new List<(Renderer, Material[], Material[])>();

    GameObject _promptGO;
    GameObject _dialogueGO;
    Light      _auraLight;

    // ── lifecycle ──────────────────────────────────────────────────────────────
    void Start()
    {
        // Find XR player
        var xr = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xr != null)
        {
            _player = xr.transform;
        }
        else
        {
            // Fallback: search by name
            var go = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin");
            if (go != null) _player = go.transform;
        }

        if (_player == null && showDebugLogs)
            Debug.LogWarning("[NpcInteractable] No se encontró XROrigin. El NPC no detectará proximidad.");

        BuildAuraLight();
        BuildPrompt();
        SetPromptVisible(false);
        BuildDialogue();
        _dialogueGO.SetActive(false);
    }

    void OnDestroy()
    {
        RestoreMaterials();
    }

    void Update()
    {
        if (_player == null) return;

        // ── Proximity check ──────────────────────────────────────────────
        bool nowInRange = Vector3.Distance(transform.position, _player.position) <= proximityRadius;
        if (nowInRange != _inRange)
        {
            _inRange = nowInRange;
            if (_inRange) EnterRange(); else ExitRange();
        }

        // ── Billboard del prompt ─────────────────────────────────────────
        if (_promptGO != null && _promptGO.activeSelf)
        {
            _promptGO.transform.LookAt(_player.position);
            _promptGO.transform.Rotate(0f, 180f, 0f);
        }

        // ── Pulsing aura light ───────────────────────────────────────────
        if (_auraLight != null && _auraLight.enabled)
        {
            float pulse  = Mathf.Sin(Time.time * auraPulseSpeed) * 0.5f + 0.5f;
            _auraLight.intensity = Mathf.Lerp(auraIntensityMin, auraIntensityMax, pulse);
        }

        // ── Input polling (new Input System via Keyboard.current) ────────
        var kb = Keyboard.current;
        if (kb == null) return;

        // E key → open dialogue
        if (_inRange && !_dialogueVisible)
        {
            bool ePressed = kb.eKey.wasPressedThisFrame;

            // Gamepad West button (X on PS / X on Xbox)
            bool padWest = false;
            var gp = Gamepad.current;
            if (gp != null) padWest = gp.buttonWest.wasPressedThisFrame;

            if (ePressed || padWest)
            {
                if (showDebugLogs) Debug.Log("[NpcInteractable] Interact pressed — opening dialogue.");
                _dialogueVisible = true;
                SetPromptVisible(false);
                _dialogueGO.SetActive(true);
            }
        }

        // Enter / Space → confirm dialogue
        if (_dialogueVisible)
        {
            bool confirm = kb.enterKey.wasPressedThisFrame
                        || kb.numpadEnterKey.wasPressedThisFrame
                        || kb.spaceKey.wasPressedThisFrame;

            bool padSouth = false;
            var gp = Gamepad.current;
            if (gp != null) padSouth = gp.buttonSouth.wasPressedThisFrame;

            if (confirm || padSouth)
            {
                if (showDebugLogs) Debug.Log("[NpcInteractable] Confirm pressed — starting game.");
                _dialogueGO.SetActive(false);
                _dialogueVisible = false;
                ExitRange();
                StartCoroutine(TeleportAndBeginMatch());
            }
        }
    }

    // ── range ─────────────────────────────────────────────────────────────────
    void EnterRange()
    {
        if (showDebugLogs) Debug.Log("[NpcInteractable] Player entered range.");
        EnsureMaterialsCloned();
        ApplyEmission(true);
        if (_auraLight != null) _auraLight.enabled = true;
        SetPromptVisible(true);
    }

    void ExitRange()
    {
        ApplyEmission(false);
        if (_auraLight != null) _auraLight.enabled = false;
        SetPromptVisible(false);
    }

    // ── teleport + begin match ────────────────────────────────────────────────
    IEnumerator TeleportAndBeginMatch()
    {
        yield return null; // wait one frame so dialogue is fully hidden

        if (_player != null)
        {
            _player.position = courtSpawnPosition;
            _player.rotation = Quaternion.Euler(0f, courtSpawnYRotation, 0f);
            if (showDebugLogs) Debug.Log("[NpcInteractable] Player teleported to " + courtSpawnPosition);
        }

        var gm = Object.FindAnyObjectByType<FieldUnlocked.Basketball.BasketballGameManager>();
        if (gm != null)
        {
            gm.BeginMatch();
            if (showDebugLogs) Debug.Log("[NpcInteractable] BeginMatch() called.");
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning("[NpcInteractable] BasketballGameManager not found in scene.");
        }
    }

    // ── aura light ────────────────────────────────────────────────────────────
    void BuildAuraLight()
    {
        var lightGO = new GameObject("NPC_AuraLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = Vector3.up * 1.0f;

        _auraLight           = lightGO.AddComponent<Light>();
        _auraLight.type      = LightType.Point;
        _auraLight.color     = new Color(auraColor.r, auraColor.g, auraColor.b, 1f);
        _auraLight.intensity = auraIntensityMin;
        _auraLight.range     = 3.5f;
        _auraLight.enabled   = false;
    }

    // ── emission (backup, may not work on all GLB materials) ─────────────────
    void EnsureMaterialsCloned()
    {
        if (_materialsCloned) return;
        _materialsCloned = true;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var orig   = r.sharedMaterials;
            var clones = r.materials;
            _renderers.Add((r, orig, clones));
        }
    }

    void ApplyEmission(bool on)
    {
        Color emColor = on ? new Color(auraColor.r, auraColor.g, auraColor.b) * 1.5f : Color.black;
        foreach (var (r, _, clones) in _renderers)
        {
            foreach (var mat in clones)
            {
                if (mat == null) continue;
                if (on)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                }
                mat.SetColor("_EmissionColor", emColor);
            }
            r.materials = clones;
        }
    }

    void RestoreMaterials()
    {
        foreach (var (r, orig, clones) in _renderers)
        {
            if (r != null) r.sharedMaterials = orig;
            foreach (var m in clones) if (m != null) Object.Destroy(m);
        }
        _renderers.Clear();
    }

    // ── UI builders ───────────────────────────────────────────────────────────
    void BuildPrompt()
    {
        _promptGO = new GameObject("NPC_Prompt");
        _promptGO.transform.SetParent(transform, false);
        _promptGO.transform.localPosition = Vector3.up * promptHeightOffset;

        var canvas = _promptGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = _promptGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(500f, 100f);
        _promptGO.transform.localScale = Vector3.one * 0.005f;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(_promptGO.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = promptText;
        tmp.fontSize  = 52;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color     = new Color(1f, 0.95f, 0f);
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
    }

    void BuildDialogue()
    {
        _dialogueGO = new GameObject("NPC_Dialogue");
        _dialogueGO.transform.SetParent(transform, false);
        _dialogueGO.transform.localPosition = dialogueLocalOffset;

        var canvas = _dialogueGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = _dialogueGO.GetComponent<RectTransform>();
        rt.sizeDelta = dialogueSizeMeters * 1000f;
        _dialogueGO.transform.localScale = Vector3.one * 0.001f;
        _dialogueGO.AddComponent<CanvasScaler>();

        var imgGO = new GameObject("Image");
        imgGO.transform.SetParent(_dialogueGO.transform, false);
        var irt = imgGO.AddComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = irt.offsetMax = Vector2.zero;

        var img = imgGO.AddComponent<Image>();
        var tex = Resources.Load<Texture2D>("UI/Dialogo");
        if (tex != null)
        {
            img.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                Vector2.one * 0.5f, 100f);
        }
        img.preserveAspect = true;
        img.color = Color.white;
    }

    void SetPromptVisible(bool v)
    {
        if (_promptGO != null) _promptGO.SetActive(v);
    }
}
