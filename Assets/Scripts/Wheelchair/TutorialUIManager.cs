using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gestiona los paneles Tutorial y Mensaje que flotan frente al jugador al inicio.
///
/// Flujo:
///   1. Al arrancar → aparece panel Tutorial 2.5 m frente al jugador.
///   2. Jugador pulsa Enter/Space → Tutorial se oculta, aparece Mensaje en el mismo lugar.
///   3. Jugador pulsa Enter/Space → Mensaje desaparece. Listo para interactuar con Arthur.
/// </summary>
public class TutorialUIManager : MonoBehaviour
{
    [Header("Posicionamiento")]
    public float distanceFromPlayer = 2.5f;
    public float heightOffset       = 0f;

    [Header("Tamaño del panel (metros)")]
    public Vector2 panelSizeMeters = new Vector2(1.6f, 0.9f);

    // ── internals ──────────────────────────────────────────────────────────────
    Transform      _camTransform;
    GameObject     _tutorialGO;
    GameObject     _mensajeGO;
    InputAction    _confirmAction;

    enum Phase { Tutorial, Mensaje, Done }
    Phase _phase = Phase.Tutorial;

    // ── lifecycle ──────────────────────────────────────────────────────────────
    void Start()
    {
        // Buscar cámara principal del jugador XR
        var xr = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xr != null && xr.Camera != null)
            _camTransform = xr.Camera.transform;
        else
            _camTransform = Camera.main != null ? Camera.main.transform : null;

        if (_camTransform == null) { enabled = false; return; }

        _tutorialGO = BuildPanel("Tutorial_Panel", "UI/Tutorial");
        _mensajeGO  = BuildPanel("Mensaje_Panel",  "UI/Mensaje");
        _mensajeGO.SetActive(false);

        PositionPanel(_tutorialGO);

        _confirmAction = new InputAction("TutConfirm");
        _confirmAction.AddBinding("<Keyboard>/return");
        _confirmAction.AddBinding("<Keyboard>/enter");
        _confirmAction.AddBinding("<Keyboard>/space");
        _confirmAction.AddBinding("<Gamepad>/buttonSouth"); // A
        _confirmAction.performed += OnConfirm;
        _confirmAction.Enable();
    }

    void OnDestroy()
    {
        _confirmAction?.Disable();
        _confirmAction?.Dispose();
    }

    // ── input ─────────────────────────────────────────────────────────────────
    void OnConfirm(InputAction.CallbackContext _)
    {
        switch (_phase)
        {
            case Phase.Tutorial:
                _tutorialGO.SetActive(false);
                PositionPanel(_mensajeGO);
                _mensajeGO.SetActive(true);
                _phase = Phase.Mensaje;
                break;

            case Phase.Mensaje:
                _mensajeGO.SetActive(false);
                _phase = Phase.Done;
                _confirmAction.Disable();
                _confirmAction.Dispose();
                _confirmAction = null;
                break;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────
    void PositionPanel(GameObject panel)
    {
        Vector3 fwd = _camTransform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        panel.transform.position = _camTransform.position
                                 + fwd * distanceFromPlayer
                                 + Vector3.up * heightOffset;
        panel.transform.rotation = Quaternion.LookRotation(fwd);
    }

    GameObject BuildPanel(string goName, string resourcePath)
    {
        var go = new GameObject(goName);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // 1 pixel = 1 mm → scale 0.001 → panel = panelSizeMeters en world space
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = panelSizeMeters * 1000f;
        go.transform.localScale = Vector3.one * 0.001f;

        go.AddComponent<CanvasScaler>();

        // Imagen que ocupa todo el canvas
        var imgGO = new GameObject("Image");
        imgGO.transform.SetParent(go.transform, false);
        var irt = imgGO.AddComponent<RectTransform>();
        irt.anchorMin   = Vector2.zero;
        irt.anchorMax   = Vector2.one;
        irt.offsetMin   = Vector2.zero;
        irt.offsetMax   = Vector2.zero;

        var img = imgGO.AddComponent<Image>();
        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex != null)
        {
            img.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                Vector2.one * 0.5f,
                100f);
        }
        img.preserveAspect = true;
        img.color = Color.white;

        return go;
    }
}
