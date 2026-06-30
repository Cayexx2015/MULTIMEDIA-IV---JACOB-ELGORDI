using UnityEngine;
using FieldUnlocked.Basketball;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Attach to aro baket 1.
    /// Combina dos mecanismos para que la pelota entre más fácil:
    /// 1. Impulso hacia el centro al primer contacto con el rim
    /// 2. Atracción continua (gravedad extra) cuando la pelota está
    ///    dentro de la zona del aro
    /// </summary>
    public class RimFriendlyBounce : MonoBehaviour
    {
        [Header("Impulso al contacto")]
        [Tooltip("Fuerza de impulso hacia el centro al golpear el rim.")]
        [Range(0f, 20f)]
        public float centerPullForce = 8f;
        public bool onlyFromAbove = true;
        public float minImpactSpeed = 1f;

        [Header("Atracción continua (zona del aro)")]
        [Tooltip("Radio de la zona donde se activa la atracción continua.")]
        public float attractionRadius = 0.35f;
        [Tooltip("Fuerza de atracción continua hacia el centro. " +
                 "Actúa como gravedad extra hacia abajo-centro.")]
        [Range(0f, 30f)]
        public float continuousPull = 18f;
        [Tooltip("Solo atrae si la pelota está por encima del aro.")]
        public float heightThreshold = 0.3f;

        [Header("Debug")]
        public bool drawGizmos = true;

        private void FixedUpdate()
        {
            // Buscar pelota en la zona del aro cada frame
            Collider[] cols = Physics.OverlapSphere(transform.position, attractionRadius);
            foreach (Collider col in cols)
            {
                BasketballBall ball = col.GetComponent<BasketballBall>();
                if (ball == null) continue;
                if (ball.State != BasketballBall.BallState.InFlight) continue;

                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb == null) continue;

                // Solo si la pelota está sobre el aro
                float heightDiff = col.transform.position.y - transform.position.y;
                if (heightDiff < -heightThreshold) continue;

                // Dirección hacia el centro del aro
                Vector3 toCenter = transform.position - col.transform.position;
                toCenter.y = -Mathf.Abs(toCenter.y); // siempre empuja hacia abajo
                toCenter.Normalize();

                // Aplicar fuerza continua
                rb.AddForce(toCenter * continuousPull, ForceMode.Acceleration);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            BasketballBall ball = collision.gameObject.GetComponent<BasketballBall>();
            if (ball == null) return;
            if (ball.State != BasketballBall.BallState.InFlight) return;

            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            if (rb.linearVelocity.magnitude < minImpactSpeed) return;
            if (onlyFromAbove && rb.linearVelocity.y > 0f) return;

            // Impulso hacia el centro al contacto
            Vector3 toCenter = transform.position - collision.contacts[0].point;
            toCenter.y = 0f;
            if (toCenter.magnitude < 0.001f) return;
            toCenter.Normalize();

            rb.AddForce(toCenter * centerPullForce, ForceMode.Impulse);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawSphere(transform.position, attractionRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, attractionRadius);
        }
    }
}