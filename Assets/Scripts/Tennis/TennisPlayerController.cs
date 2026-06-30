using UnityEngine;
using UnityEngine.InputSystem;
using FieldUnlocked.Wheelchair;

namespace FieldUnlocked.Tennis
{
    /// <summary>
    /// Controla al jugador en el modo tenis.
    /// Se adjunta a Arthur_Rigged_01.
    ///
    /// Controles:
    ///   Cruz    (South) → Drive
    ///   Cuadrado (West) → Slice  
    ///   Triángulo (North) → Lob
    ///   Círculo  (East) → Drop Shot
    ///   Mantener = cargar potencia, soltar = golpear
    /// </summary>
    public class TennisPlayerController : MonoBehaviour
    {
        [Header("Referencias")]
        public TennisRacket      racket;
        public TennisBall        ball;
        public TennisGameManager gameManager;
        public Transform         courtCenter; // centro de la cancha del jugador

        [Header("Movimiento en cancha")]
        public float moveSpeed    = 5f;
        public float rotateSpeed  = 120f;

        [Header("Carga del golpe")]
        public float maxChargeTime    = 1.2f;
        [Range(0f, 1f)]
        public float perfectStart     = 0.5f;
        [Range(0f, 1f)]
        public float perfectEnd       = 0.75f;
        public float minPowerMult     = 0.4f;
        public float maxPowerMult     = 1.0f;

        [Header("Auto-posicionamiento")]
        [Tooltip("Distancia máxima para intentar golpear la pelota")]
        public float hitRadius        = 3f;
        public float autoMoveSpeed    = 8f;

        [Header("UI")]
        public UnityEngine.UI.Image  chargeBarFill;
        public UnityEngine.UI.Text   shotTypeLabel;
        public GameObject            chargeBarCanvas;

        [Header("Debug")]
        public bool showDebugLogs = true;

        // Estado
        private bool  _charging      = false;
        private float _chargeTimer   = 0f;
        private TennisRacket.ShotType _pendingShot = TennisRacket.ShotType.Drive;
        private bool  _canHit        = false;
        private WheelchairLocomotion _locomotion;

        // Colores
        private readonly Color _colorWeak    = new Color(0.3f, 0.5f, 1f);
        private readonly Color _colorPerfect = new Color(0.2f, 1f,  0.2f);
        private readonly Color _colorOver    = new Color(1f,   0.2f, 0.2f);

        private void Start()
        {
            _locomotion = GetComponentInParent<WheelchairLocomotion>();
            if (_locomotion == null)
                _locomotion = FindFirstObjectByType<WheelchairLocomotion>();

            if (racket != null) racket.OwnerIndex = 0;
            HideChargeBar();
        }

        private void Update()
        {
            if (gameManager != null && !gameManager.IsPlaying) return;

            Gamepad gp = Gamepad.current;
            if (gp == null) return;

            HandleShotSelection(gp);
            HandleCharge(gp);
            UpdateChargeBar();
            CheckBallProximity();
        }

        // ── Selección de tipo de golpe ───────────────────────────────────
        private void HandleShotSelection(Gamepad gp)
        {
            if (_charging) return; // no cambiar mientras se carga

            if (gp.buttonSouth.wasPressedThisFrame)
            {
                _pendingShot = TennisRacket.ShotType.Drive;
                StartCharge();
            }
            else if (gp.buttonWest.wasPressedThisFrame)
            {
                _pendingShot = TennisRacket.ShotType.Slice;
                StartCharge();
            }
            else if (gp.buttonNorth.wasPressedThisFrame)
            {
                _pendingShot = TennisRacket.ShotType.Lob;
                StartCharge();
            }
            else if (gp.buttonEast.wasPressedThisFrame)
            {
                _pendingShot = TennisRacket.ShotType.DropShot;
                StartCharge();
            }
        }

        // ── Carga y golpe ────────────────────────────────────────────────
        private void StartCharge()
        {
            _charging    = true;
            _chargeTimer = 0f;

            if (racket != null)
                racket.CurrentShot = _pendingShot;

            if (shotTypeLabel != null)
                shotTypeLabel.text = racket != null ? racket.GetShotName() : "";

            if (chargeBarCanvas != null)
                chargeBarCanvas.SetActive(true);
        }

        private void HandleCharge(Gamepad gp)
        {
            if (!_charging) return;

            _chargeTimer += Time.deltaTime;

            // Cualquier botón de golpe soltado → ejecutar
            bool released = gp.buttonSouth.wasReleasedThisFrame
                         || gp.buttonWest.wasReleasedThisFrame
                         || gp.buttonNorth.wasReleasedThisFrame
                         || gp.buttonEast.wasReleasedThisFrame;

            if (released)
            {
                _charging = false;
                ExecuteShot();
            }
        }

        private void ExecuteShot()
        {
            if (ball == null || racket == null) return;
            if (ball.State != TennisBall.BallState.InPlay &&
                ball.State != TennisBall.BallState.Idle) return;

            float ratio      = Mathf.Clamp01(_chargeTimer / maxChargeTime);
            float powerMult  = Mathf.Lerp(minPowerMult, maxPowerMult, ratio);

            // Dirección: hacia el lado contrario de la cancha
            Vector3 dir = GetShotDirection();

            // Aplicar potencia
            float originalSpeed = racket.driveSpeed;
            ScaleRacketSpeed(powerMult);

            racket.ApplyShot(ball, dir);

            RestoreRacketSpeed(originalSpeed);
            HideChargeBar();

            if (showDebugLogs)
                Debug.Log($"[TennisPlayer] {_pendingShot} ratio={ratio:P0} power={powerMult:F2}");
        }

        private Vector3 GetShotDirection()
        {
            // Dirección hacia la mitad contraria de la cancha
            if (courtCenter != null)
            {
                Vector3 toCenter = courtCenter.position - transform.position;
                toCenter.y = 0;
                // Añadir ligero arco hacia arriba
                return (toCenter.normalized + Vector3.up * 0.3f).normalized;
            }
            return (transform.forward + Vector3.up * 0.3f).normalized;
        }

        private void ScaleRacketSpeed(float mult)
        {
            if (racket == null) return;
            racket.driveSpeed    *= mult;
            racket.sliceSpeed    *= mult;
            racket.lobSpeed      *= mult;
            racket.dropShotSpeed *= mult;
        }

        private void RestoreRacketSpeed(float original)
        {
            if (racket == null) return;
            float ratio = original / Mathf.Max(racket.driveSpeed, 0.001f);
            racket.driveSpeed    *= ratio;
            racket.sliceSpeed    *= ratio;
            racket.lobSpeed      *= ratio;
            racket.dropShotSpeed *= ratio;
        }

        // ── Proximidad a la pelota ───────────────────────────────────────
        private void CheckBallProximity()
        {
            if (ball == null) return;
            float dist = Vector3.Distance(transform.position, ball.transform.position);
            _canHit = dist <= hitRadius && ball.State == TennisBall.BallState.InPlay;
        }

        // ── UI ────────────────────────────────────────────────────────────
        private void UpdateChargeBar()
        {
            if (!_charging || chargeBarFill == null) return;

            float ratio = Mathf.Clamp01(_chargeTimer / maxChargeTime);
            chargeBarFill.fillAmount = ratio;

            Color c;
            if (_chargeTimer > maxChargeTime)        c = _colorOver;
            else if (ratio >= perfectStart && ratio <= perfectEnd) c = _colorPerfect;
            else c = Color.Lerp(_colorWeak, _colorPerfect,
                                Mathf.Clamp01(ratio / Mathf.Max(perfectStart, 0.01f)));
            chargeBarFill.color = c;
        }

        private void HideChargeBar()
        {
            if (chargeBarCanvas != null)
                chargeBarCanvas.SetActive(false);
        }

        public bool CanHit    => _canHit;
        public bool IsCharging => _charging;
    }
}
