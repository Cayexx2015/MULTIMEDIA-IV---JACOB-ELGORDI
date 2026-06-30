using UnityEngine;

namespace FieldUnlocked.Tennis
{
    /// <summary>
    /// IA del oponente de tenis.
    /// Se mueve hacia la pelota y devuelve cuando está en rango.
    /// Attach al GameObject del oponente CPU.
    /// </summary>
    public class TennisAI : MonoBehaviour
    {
        [Header("Referencias")]
        public TennisRacket      racket;
        public TennisBall        ball;
        public TennisGameManager gameManager;
        public Transform         playerTransform;

        [Header("Movimiento")]
        public float moveSpeed      = 4f;
        public float rotateSpeed    = 180f;
        public float hitRadius      = 3.5f;

        [Header("Comportamiento")]
        [Tooltip("Tiempo de reacción de la IA en segundos")]
        public float reactionTime   = 0.4f;
        [Tooltip("Precisión de la IA (0=perfecta, 1=muy errática)")]
        [Range(0f, 1f)]
        public float errorAmount    = 0.3f;
        [Tooltip("Posición de descanso de la IA en la cancha")]
        public Transform restPosition;

        [Header("Dificultad")]
        public float difficulty = 0.5f; // 0=fácil, 1=difícil

        // Estado
        private enum AIState { Idle, MovingToBall, Hitting, Returning }
        private AIState _state      = AIState.Idle;
        private float   _reactionTimer = 0f;
        private bool    _hasReacted    = false;
        private Vector3 _targetPos;

        private void Start()
        {
            if (racket != null) racket.OwnerIndex = 1;
            _targetPos = restPosition != null ? restPosition.position : transform.position;
        }

        private void Update()
        {
            if (gameManager != null && !gameManager.IsPlaying) return;
            if (ball == null) return;

            // Solo actuar si la pelota viene hacia la IA
            if (ball.State == TennisBall.BallState.InPlay && ball.LastHitter == 0)
            {
                HandleReaction();
            }
            else if (ball.State == TennisBall.BallState.Out ||
                     ball.State == TennisBall.BallState.Idle)
            {
                ReturnToRest();
            }

            MoveToTarget();
        }

        private void HandleReaction()
        {
            if (!_hasReacted)
            {
                _reactionTimer += Time.deltaTime;
                if (_reactionTimer >= reactionTime)
                {
                    _hasReacted    = true;
                    _reactionTimer = 0f;

                    // Predecir posición de la pelota
                    _targetPos = PredictBallPosition();
                    _state     = AIState.MovingToBall;
                }
                return;
            }

            // Ya reaccionó — moverse hacia la pelota
            float dist = Vector3.Distance(transform.position, ball.transform.position);
            if (dist <= hitRadius)
            {
                _state = AIState.Hitting;
                Hit();
            }
        }

        private Vector3 PredictBallPosition()
        {
            // Predecir dónde va a caer la pelota basándose en velocidad
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb == null) return ball.transform.position;

            // Predicción simple: posición actual + velocidad * tiempo estimado
            float timeToReach = Vector3.Distance(transform.position, ball.transform.position) / 20f;
            Vector3 predicted = ball.transform.position + rb.linearVelocity * timeToReach;

            // Añadir error según dificultad
            float error = errorAmount * (1f - difficulty);
            predicted += new Vector3(
                Random.Range(-error, error) * 3f,
                0f,
                Random.Range(-error, error) * 3f);

            return predicted;
        }

        private void Hit()
        {
            if (racket == null || ball == null) return;
            if (ball.LastHitter == 1) return; // no golpear dos veces seguidas

            // Elegir tipo de golpe según dificultad
            racket.CurrentShot = ChooseShot();

            // Dirección hacia el jugador con variación según dificultad
            Vector3 dir = Vector3.zero;
            if (playerTransform != null)
            {
                dir = (playerTransform.position - transform.position).normalized;
                dir.y = 0f;

                // Añadir variación
                float error = errorAmount * (1f - difficulty);
                dir += new Vector3(Random.Range(-error, error), 0f, Random.Range(-error, error));
                dir = (dir + Vector3.up * 0.3f).normalized;
            }
            else
            {
                dir = (-transform.forward + Vector3.up * 0.3f).normalized;
            }

            racket.ApplyShot(ball, dir);
            _hasReacted = false;
            _state      = AIState.Returning;
        }

        private TennisRacket.ShotType ChooseShot()
        {
            float r = Random.value;
            if (difficulty > 0.7f)
            {
                // Difícil: más variedad
                if (r < 0.5f) return TennisRacket.ShotType.Drive;
                if (r < 0.75f) return TennisRacket.ShotType.Slice;
                if (r < 0.9f) return TennisRacket.ShotType.Lob;
                return TennisRacket.ShotType.DropShot;
            }
            else
            {
                // Fácil: principalmente drives
                if (r < 0.7f) return TennisRacket.ShotType.Drive;
                return TennisRacket.ShotType.Lob;
            }
        }

        private void ReturnToRest()
        {
            if (restPosition != null)
                _targetPos = restPosition.position;
            _state      = AIState.Returning;
            _hasReacted = false;
        }

        private void MoveToTarget()
        {
            Vector3 flatTarget = _targetPos;
            flatTarget.y = transform.position.y;

            Vector3 dir = flatTarget - transform.position;
            if (dir.magnitude > 0.3f)
            {
                transform.position += dir.normalized * moveSpeed * Time.deltaTime;
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation  = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            }
        }
    }
}
