using System.Collections.Generic;
using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Attach to the basketball GameObject.
    /// Handles physics state: Free (rolling), Held (kinematic), InFlight (launched).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BasketballBall : MonoBehaviour
    {
        [Header("Physics")]
        [Tooltip("Linear drag while rolling on the floor.")]
        public float rollingDrag = 1.5f;
        [Tooltip("Linear drag while in the air — keep very low.")]
        public float flightDrag = 0.05f;
        [Tooltip("Angular drag while rolling — controls how fast spin decays.")]
        public float rollingAngularDrag = 0.8f;
        [Tooltip("Angular drag in the air — adds natural backspin decay.")]
        public float flightAngularDrag = 0.1f;

        [Header("Bounce")]
        [Tooltip("Bounciness applied via PhysicsMaterial (0=dead, 1=super bouncy). " +
                 "0.72 matches a real NBA ball drop test.")]
        public float bounciness = 0.72f;
        [Tooltip("How many bounces before the ball is considered 'resting' and pickup is allowed. " +
                 "Set to 1 so the player can grab it after the first bounce.")]
        public int bouncesUntilFree = 1;

        [Header("Visuals")]
        public TrailRenderer flightTrail;

        [Header("Court Containment")]
        [Tooltip("Raíz donde buscar la malla de la cancha. Si se deja vacío, busca en toda la " +
                 "escena cualquier objeto cuyo nombre contenga 'malla basket' (como viene del FBX " +
                 "del estadio) y combina sus límites.")]
        public Transform courtBoundaryRoot;
        [Tooltip("Texto a buscar en el nombre de los objetos que forman el límite de la cancha.")]
        public string boundaryNameFilter = "malla basket";
        [Tooltip("Margen interno (metros) respecto a la malla para que la pelota rebote antes de " +
                 "atravesarla visualmente.")]
        public float boundaryMargin = 0.15f;
        [Tooltip("Fracción de velocidad conservada al rebotar contra el límite invisible " +
                 "(0 = se frena en seco, 1 = rebote total).")]
        [Range(0f, 1f)] public float boundaryBounciness = 0.6f;
        [Tooltip("Si está activo, también limita la altura máxima (por si la malla no tiene techo " +
                 "y se tira con un arco muy alto).")]
        public bool clampTop = true;
        public bool showBoundaryGizmo = true;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // ── State ─────────────────────────────────────────────────────────
        public enum BallState { Free, Held, InFlight }
        public BallState State { get; private set; } = BallState.Free;

        private Rigidbody _rb;
        private Transform _holdTarget;
        private int _bounceCount;

        private Bounds _courtBounds;
        private bool _courtBoundsFound;

        // ── Setup ─────────────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Runtime PhysicsMaterial — realistic NBA ball
            SphereCollider col = GetComponent<SphereCollider>();
            PhysicsMaterial pm = new PhysicsMaterial("Basketball");
            pm.bounciness = bounciness;
            pm.dynamicFriction = 0.6f;
            pm.staticFriction = 0.6f;
            pm.frictionCombine = PhysicsMaterialCombine.Average;
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;
            col.sharedMaterial = pm;

            SetRollingPhysics();

            if (flightTrail != null)
                flightTrail.emitting = false;

            FindCourtBounds();
        }

        // ── Public API ────────────────────────────────────────────────────

        public void Pickup(Transform holdAnchor)
        {
            State = BallState.Held;
            _holdTarget = holdAnchor;
            _bounceCount = 0;

            _rb.isKinematic = true;
            _rb.useGravity = false;

            if (flightTrail != null) flightTrail.emitting = false;
        }

        public void UpdateHeldPosition()
        {
            if (_holdTarget == null) return;
            transform.position = _holdTarget.position;
            transform.rotation = _holdTarget.rotation;
        }

        public void Launch(Vector3 velocity)
        {
            State = BallState.InFlight;
            _holdTarget = null;
            _bounceCount = 0;

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearDamping = flightDrag;
            _rb.angularDamping = flightAngularDrag;
            _rb.linearVelocity = velocity;

            // Natural backspin — makes the ball arc look realistic
            _rb.angularVelocity = new Vector3(
                Random.Range(-6f, -10f),
                Random.Range(-1f, 1f),
                Random.Range(-2f, 2f));

            if (flightTrail != null) flightTrail.emitting = true;
        }

        /// <summary>Transition to Free rolling state.</summary>
        public void Land()
        {
            State = BallState.Free;
            _holdTarget = null;

            _rb.isKinematic = false;
            _rb.useGravity = true;

            SetRollingPhysics();

            if (flightTrail != null) flightTrail.emitting = false;
        }

        // ── Collision — bounce detection ──────────────────────────────────
        private void OnCollisionEnter(Collision collision)
        {
            if (State != BallState.InFlight) return;

            // Only count bounces off floor/walls/boards — not the player
            if (collision.gameObject.CompareTag("Player")) return;

            // IMPORTANTE: el aro tiene colliders físicos propios (los "RimSeg_XX" que
            // genera RimColliderBuilder, agrupados bajo un objeto "RimColliders") para que
            // la pelota pueda rebotar contra el rim de forma realista. El problema es que
            // CASI CUALQUIER tiro embocado roza uno de esos segmentos al pasar — y con
            // bouncesUntilFree=1, ese simple roce contra el aro aterrizaba la pelota
            // (State pasaba a Free) un instante ANTES de llegar al trigger de gol de
            // BasketballHoop, que exige State==InFlight. Resultado: el gol jamás se
            // contaba aunque la pelota entrara perfecto. Por eso, un golpe contra el aro
            // puede seguir rebotando físicamente (eso lo maneja Unity solo, vía el
            // PhysicsMaterial), pero NO cuenta como "bounce que aterriza la pelota".
            if (IsRimCollider(collision.collider))
            {
                if (showDebugLogs)
                    Debug.Log("[BasketballBall] Rozó el aro (collider del rim) — no cuenta como aterrizaje, sigue en vuelo.");
                return;
            }

            _bounceCount++;

            // Allow pickup after the first bounce
            if (_bounceCount >= bouncesUntilFree)
                Land();
        }

        /// <summary>
        /// Detecta si un collider pertenece a los segmentos del rim generados por
        /// RimColliderBuilder ("RimSeg_00", "RimSeg_01", ... agrupados bajo "RimColliders"),
        /// subiendo por los padres para cubrir cualquier profundidad de jerarquía.
        /// </summary>
        private bool IsRimCollider(Collider col)
        {
            if (col == null) return false;
            Transform t = col.transform;
            while (t != null)
            {
                string n = t.name;
                if (n.StartsWith("RimSeg", System.StringComparison.OrdinalIgnoreCase) ||
                    n.IndexOf("RimColliders", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        // ── Update ────────────────────────────────────────────────────────
        // Tiempo acumulado en el que la pelota viene "casi parada" mientras está en vuelo —
        // usado por el fallback de seguridad de más abajo.
        private float _slowFlightTimer = 0f;
        [Tooltip("Cuánto tiempo (segundos) tiene que estar la pelota por debajo de la velocidad " +
                 "mínima ANTES de forzar el aterrizaje. Sin este margen, un tiro con arco bien alto " +
                 "podía quedar casi sin velocidad justo en el punto más alto — exactamente cuando " +
                 "pasa por el aro — y el fallback la aterrizaba (State=Free) un frame antes de entrar " +
                 "al trigger del aro, haciendo que el gol nunca contara.")]
        public float minFlightSpeedDuration = 0.3f;
        public float minFlightSpeed = 0.4f;

        private void Update()
        {
            if (State == BallState.Held)
                UpdateHeldPosition();

            // Safety fallback: if somehow still InFlight but nearly stopped for a while, land it.
            // Requiere varios frames seguidos por debajo del umbral (no solo uno) para no confundir
            // un arco alto/lento cerca del aro con una pelota genuinamente detenida.
            if (State == BallState.InFlight)
            {
                if (_rb.linearVelocity.magnitude < minFlightSpeed)
                {
                    _slowFlightTimer += Time.deltaTime;
                    if (_slowFlightTimer >= minFlightSpeedDuration)
                        Land();
                }
                else
                {
                    _slowFlightTimer = 0f;
                }
            }
            else
            {
                _slowFlightTimer = 0f;
            }
        }

        // Runs every physics step so la pelota nunca puede "tunelear" a través del límite,
        // sin importar qué tan fuerte se lance: se vuelve a meter dentro de la malla básket
        // y se le invierte la velocidad de salida para que se vea como un rebote natural.
        private void FixedUpdate()
        {
            if (!_courtBoundsFound || State == BallState.Held) return;
            EnforceCourtBoundary();
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private void SetRollingPhysics()
        {
            _rb.linearDamping = rollingDrag;
            _rb.angularDamping = rollingAngularDrag;
        }

        // ── Court containment ────────────────────────────────────────────
        /// <summary>
        /// Busca todos los objetos cuyo nombre contenga <see cref="boundaryNameFilter"/>
        /// (por defecto "malla basket", como vienen los paneles de red importados desde el FBX
        /// del estadio) y combina sus bounds en un único volumen de contención.
        /// </summary>
        private void FindCourtBounds()
        {
            List<Renderer> matches = new List<Renderer>();
            string filter = string.IsNullOrEmpty(boundaryNameFilter) ? "malla basket" : boundaryNameFilter;
            filter = filter.ToLowerInvariant();

            Renderer[] candidates = courtBoundaryRoot != null
                ? courtBoundaryRoot.GetComponentsInChildren<Renderer>(true)
#if UNITY_2023_1_OR_NEWER
                : FindObjectsByType<Renderer>(FindObjectsInactive.Include);
#else
                : FindObjectsOfType<Renderer>(true);
#endif

            foreach (Renderer r in candidates)
            {
                if (r.gameObject.name.ToLowerInvariant().Contains(filter))
                    matches.Add(r);
            }

            if (matches.Count == 0)
            {
                _courtBoundsFound = false;
                if (showDebugLogs)
                    Debug.LogWarning($"[BasketballBall] No se encontró ningún objeto con '{filter}' en el nombre. " +
                                      "La pelota no tendrá límite de cancha — revisá el nombre o asigná " +
                                      "courtBoundaryRoot manualmente.");
                return;
            }

            Bounds combined = matches[0].bounds;
            for (int i = 1; i < matches.Count; i++)
                combined.Encapsulate(matches[i].bounds);

            _courtBounds = combined;
            _courtBoundsFound = true;

            if (showDebugLogs)
                Debug.Log($"[BasketballBall] Límite de cancha detectado a partir de {matches.Count} malla(s). " +
                          $"Bounds center={_courtBounds.center} size={_courtBounds.size}");
        }

        /// <summary>
        /// Si la pelota quedó fuera del volumen de la malla, la reposiciona justo adentro
        /// del margen y refleja la componente de velocidad que la hizo salir, para que el
        /// rebote contra el límite invisible se vea natural en vez de un corte brusco.
        /// </summary>
        private void EnforceCourtBoundary()
        {
            Vector3 pos = transform.position;
            Vector3 vel = _rb.linearVelocity;
            bool corrected = false;

            float minX = _courtBounds.min.x + boundaryMargin;
            float maxX = _courtBounds.max.x - boundaryMargin;
            float minZ = _courtBounds.min.z + boundaryMargin;
            float maxZ = _courtBounds.max.z - boundaryMargin;
            float maxY = _courtBounds.max.y - boundaryMargin;

            if (pos.x < minX)
            {
                pos.x = minX;
                if (vel.x < 0f) { vel.x = -vel.x * boundaryBounciness; corrected = true; }
            }
            else if (pos.x > maxX)
            {
                pos.x = maxX;
                if (vel.x > 0f) { vel.x = -vel.x * boundaryBounciness; corrected = true; }
            }

            if (pos.z < minZ)
            {
                pos.z = minZ;
                if (vel.z < 0f) { vel.z = -vel.z * boundaryBounciness; corrected = true; }
            }
            else if (pos.z > maxZ)
            {
                pos.z = maxZ;
                if (vel.z > 0f) { vel.z = -vel.z * boundaryBounciness; corrected = true; }
            }

            if (clampTop && pos.y > maxY)
            {
                pos.y = maxY;
                if (vel.y > 0f) { vel.y = -vel.y * boundaryBounciness; corrected = true; }
            }

            if (!corrected) return;

            transform.position = pos;
            _rb.linearVelocity = vel;

            // Cuenta como un "golpe" contra el límite si está en vuelo, así se puede
            // agarrar la pelota después de pegar contra la malla igual que con cualquier rebote.
            if (State == BallState.InFlight)
            {
                _bounceCount++;
                if (_bounceCount >= bouncesUntilFree)
                    Land();
            }

            if (showDebugLogs)
                Debug.Log("[BasketballBall] Rebote contra el límite de la cancha (malla basket).");
        }

        private void OnDrawGizmosSelected()
        {
            if (!showBoundaryGizmo || !_courtBoundsFound) return;
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(_courtBounds.center, _courtBounds.size);
        }
    }
}