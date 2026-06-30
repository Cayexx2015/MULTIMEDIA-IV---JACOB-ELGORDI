using UnityEngine;

namespace FieldUnlocked.Tennis
{
    /// <summary>
    /// Attach al GameObject de la raqueta.
    /// Detecta colisión con la pelota y aplica el golpe según el tipo seleccionado.
    /// </summary>
    public class TennisRacket : MonoBehaviour
    {
        public enum ShotType { Drive, Slice, Lob, DropShot }

        [Header("Referencias")]
        public TennisBall ball;
        public Transform  racketFace; // punto en el centro de la raqueta

        [Header("Parámetros de golpe")]
        public float driveSpeed    = 28f;
        public float sliceSpeed    = 22f;
        public float lobSpeed      = 18f;
        public float dropShotSpeed = 12f;

        [Header("Spin")]
        public float topspinAmount  = 2f;
        public float backspinAmount = 1.5f;

        // Estado actual
        public ShotType CurrentShot { get; set; } = ShotType.Drive;
        public bool     IsSwinging  { get; set; } = false;
        public int      OwnerIndex  { get; set; } = 0; // 0=jugador, 1=CPU

        private void OnTriggerEnter(Collider other)
        {
            if (!IsSwinging) return;

            TennisBall hitBall = other.GetComponent<TennisBall>();
            if (hitBall == null) return;
            if (hitBall.LastHitter == OwnerIndex) return; // no golpear la propia pelota dos veces seguidas

            ApplyShot(hitBall);
        }

        public void ApplyShot(TennisBall hitBall, Vector3 targetDirection = default)
        {
            if (targetDirection == Vector3.zero)
                targetDirection = transform.forward;

            float   speed     = GetSpeed();
            Vector3 spinAxis  = Vector3.zero;
            float   spinAmt   = 0f;

            switch (CurrentShot)
            {
                case ShotType.Drive:
                    // Topspin — pelota baja rápido después del pico
                    spinAxis = Vector3.Cross(targetDirection, Vector3.up).normalized;
                    spinAmt  = topspinAmount;
                    break;

                case ShotType.Slice:
                    // Backspin — pelota se mantiene baja y rasa
                    spinAxis = -Vector3.Cross(targetDirection, Vector3.up).normalized;
                    spinAmt  = backspinAmount;
                    targetDirection = (targetDirection + Vector3.up * 0.15f).normalized;
                    break;

                case ShotType.Lob:
                    // Alto y lento
                    targetDirection = (targetDirection + Vector3.up * 0.6f).normalized;
                    spinAmt = 0f;
                    break;

                case ShotType.DropShot:
                    // Corto y suave, con backspin
                    spinAxis = -Vector3.Cross(targetDirection, Vector3.up).normalized;
                    spinAmt  = backspinAmount * 1.5f;
                    targetDirection = (targetDirection + Vector3.up * 0.2f).normalized;
                    break;
            }

            Vector3 velocity = targetDirection * speed;
            hitBall.Launch(velocity, spinAxis, spinAmt, OwnerIndex);
        }

        private float GetSpeed()
        {
            switch (CurrentShot)
            {
                case ShotType.Drive:    return driveSpeed;
                case ShotType.Slice:    return sliceSpeed;
                case ShotType.Lob:      return lobSpeed;
                case ShotType.DropShot: return dropShotSpeed;
                default:                return driveSpeed;
            }
        }

        public string GetShotName()
        {
            switch (CurrentShot)
            {
                case ShotType.Drive:    return "DRIVE";
                case ShotType.Slice:    return "SLICE";
                case ShotType.Lob:      return "LOB";
                case ShotType.DropShot: return "DROP";
                default:                return "DRIVE";
            }
        }
    }
}
