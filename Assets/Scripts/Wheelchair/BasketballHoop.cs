using FieldUnlocked.Basketball;
using FieldUnlocked.Wheelchair;
using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Attach to the hoop/basket GameObject.
    /// Detects when the ball passes through the rim from above → scores a point.
    ///
    /// Setup:
    ///   1. Add a Trigger collider (cylinder or sphere) positioned at the centre of the rim,
    ///      slightly below it, facing upward. Set it as a Trigger.
    ///   2. Assign this script to that GameObject.
    ///   3. The ball must have a Rigidbody and BasketballBall component.
    /// </summary>
    public class BasketballHoop : MonoBehaviour
    {
        [Header("Scoring")]
        [Tooltip("Points awarded for a successful basket (3 if far, else 2 — set manually or auto-calc).")]
        public int pointValue = 2;
        [Tooltip("If true, auto-sets pointValue to 3 when the shot origin was beyond this distance.")]
        public bool autoThreePointer = true;
        [Tooltip("Distance threshold for a 3-pointer (metres).")]
        public float threePointLine = 6.75f;

        [Header("References")]
        [Tooltip("Optional: the ShootingSystem to read shot origin for 3-point detection.")]
        public ShootingSystem shootingSystem;

        [Header("Debug")]
        public bool showDebugLogs = true;

        [Tooltip("Tolerancia para considerar que la pelota 'está cayendo'. Un valor chico " +
                 "(en vez de exigir y < 0 estricto) evita que un mini-rebote contra el aro " +
                 "justo al entrar en el trigger haga que no cuente como gol.")]
        public float fallingVelocityTolerance = 0.15f;

        // Simple score counter — hook up to your UI system
        public static int TeamScore { get; private set; } = 0;

        /// <summary>Se dispara cada vez que se emboca: (puntos, posición de la pelota).</summary>
        public event System.Action<int, Vector3> OnScored;

        // ── Trigger ───────────────────────────────────────────────────────
        // Si este mismo GameObject tiene un Collider trigger propio, Unity llama esto
        // directamente. Además, BasketballScoreTrigger crea en runtime una zona de gol
        // separada (un Collider limpio adentro del aro) y la reenvía acá mismo vía
        // ProcessPossibleScore — así no importa en qué GameObject esté el Collider real,
        // el chequeo de gol es siempre el mismo.
        private void OnTriggerEnter(Collider other)
        {
            ProcessPossibleScore(other);
        }

        /// <summary>
        /// Lógica de detección de gol. Pública para que un Collider trigger en OTRO
        /// GameObject (por ej. la zona de gol que crea BasketballScoreTrigger adentro
        /// del aro) pueda reenviar el aviso acá.
        /// </summary>
        public void ProcessPossibleScore(Collider other)
        {
            BasketballBall ball = other.GetComponent<BasketballBall>();
            if (ball == null) return;

            if (ball.State != BasketballBall.BallState.InFlight)
            {
                if (showDebugLogs)
                    Debug.Log($"[BasketballHoop] Pelota entró al trigger pero su estado es '{ball.State}' (se necesita 'InFlight') — no cuenta.");
                return;
            }

            // Must enter from ABOVE (ball moving downward through rim)
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[BasketballHoop] La pelota no tiene Rigidbody — no se puede chequear si está cayendo.");
                return;
            }

            if (rb.linearVelocity.y >= fallingVelocityTolerance)
            {
                if (showDebugLogs)
                    Debug.Log($"[BasketballHoop] Pelota entró al trigger pero no está cayendo (velocity.y={rb.linearVelocity.y:F2}) — no cuenta.");
                return;
            }   // ball must be falling (con una pequeña tolerancia, ver fallingVelocityTolerance)

            // Score!
            int pts = pointValue;
            if (autoThreePointer && shootingSystem != null)
            {
                float dist = Vector3.Distance(
                    shootingSystem.transform.position, transform.position);
                if (dist >= threePointLine) pts = 3;
            }

            TeamScore += pts;
            Vector3 scorePos = other.transform.position;
            ball.Land();

            if (showDebugLogs)
                Debug.Log($"[BasketballHoop] SCORE! +{pts} pts  Total={TeamScore}");

            OnScore(pts, scorePos);
        }

        // Override or subscribe to this in your game manager
        protected virtual void OnScore(int points, Vector3 position)
        {
            if (showDebugLogs && OnScored == null)
                Debug.LogWarning("[BasketballHoop] ¡Encestó pero NADIE está escuchando el evento OnScored! " +
                                  "Revisá que el BasketballGameManager esté en la escena, habilitado, y con " +
                                  "'hoop' apuntando a este mismo BasketballHoop.");
            OnScored?.Invoke(points, position);
        }
    }
}