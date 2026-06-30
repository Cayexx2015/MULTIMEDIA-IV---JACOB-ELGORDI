using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Attach to the SAME GameObject as BasketballHoop.
    ///
    /// El problema que resuelve: el aro importado del FBX del estadio no tiene ningún
    /// Collider trigger "limpio" adentro del rim — sólo tiene los colliders sólidos que
    /// genera RimColliderBuilder (el rim físico), que no son triggers. Sin un Collider
    /// trigger, BasketballHoop.OnTriggerEnter nunca se llama, así que el gol nunca se
    /// detecta aunque la pelota entre perfecto.
    ///
    /// Este script crea, en tiempo de ejecución, un Collider trigger nuevo (una cápsula)
    /// posicionado en el CENTRO del aro (misma fórmula de posición que usa
    /// RimColliderBuilder: transform.position + heightOffsetWorld), en un GameObject
    /// hijo separado con escala 1 — así no importa qué escala rara tenga el padre del
    /// modelo del estadio. Cuando la pelota lo atraviesa, le avisa a BasketballHoop para
    /// que cuente el punto.
    /// </summary>
    [RequireComponent(typeof(BasketballHoop))]
    public class BasketballScoreTrigger : MonoBehaviour
    {
        [Header("Referencia")]
        [Tooltip("Si se deja vacío, se usa el BasketballHoop de este mismo GameObject.")]
        public BasketballHoop hoop;

        [Header("Posición del aro")]
        [Tooltip("Debe coincidir con el mismo valor que tengas en RimColliderBuilder para este aro.")]
        public float rimRadiusWorld = 0.3f;
        [Tooltip("Debe coincidir con el mismo valor que tengas en RimColliderBuilder para este aro. " +
                 "Este offset es el del PLANO DEL RIM — la zona de gol se ubica heightOffsetWorld " +
                 "MENOS belowRimOffset, o sea, por debajo de este plano (ver belowRimOffset).")]
        public float heightOffsetWorld = 0f;
        [Tooltip("Cuánto más abajo del plano del rim se ubica la zona de gol, en metros. " +
                 "Así la pelota tiene que haber pasado de largo por el aro — no alcanza con " +
                 "tocar el rim o asomar la mitad de arriba — para que recién ahí sume el punto.")]
        public float belowRimOffset = 0.18f;

        [Header("Tamaño de la zona de gol")]
        [Tooltip("Radio de la cápsula de detección, en metros. A propósito MENOR al radio " +
                 "de la pelota (una pelota NBA mide ~0.12m de radio) para que SOLO cuente " +
                 "cuando el CENTRO de la pelota pasa bien por el medio del rim — tocar o " +
                 "rozar el borde del aro no tiene que sumar punto.")]
        public float triggerRadius = 0.08f;
        [Tooltip("Altura total de la cápsula de detección, en metros. Chica a propósito " +
                 "para que sea una 'rebanada' fina, no un cilindro largo que la pelota " +
                 "pueda tocar de costado mucho antes/después de pasar por el medio.")]
        public float triggerHeight = 0.08f;

        [Header("Debug")]
        public bool showDebugLogs = true;
        public bool drawGizmo = true;

        private GameObject _zone;

        private void Awake()
        {
            if (hoop == null) hoop = GetComponent<BasketballHoop>();
            BuildZone();
        }

        private void BuildZone()
        {
            Vector3 worldCenter = transform.position + Vector3.up * (heightOffsetWorld - belowRimOffset);

            _zone = new GameObject(name + "_ScoreTriggerZone (auto)");
            // Parentamos al MISMO padre que el aro (no al aro), con escala 1, igual que
            // hace RimColliderBuilder — así evitamos heredar cualquier escala rara del
            // modelo importado del estadio.
            _zone.transform.SetParent(transform.parent, worldPositionStays: true);
            _zone.transform.position = worldCenter;
            _zone.transform.rotation = Quaternion.identity;
            _zone.transform.localScale = Vector3.one;

            CapsuleCollider col = _zone.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = triggerRadius;
            col.height = triggerHeight;
            col.direction = 1; // eje Y

            BasketballScoreTriggerRelay relay = _zone.AddComponent<BasketballScoreTriggerRelay>();
            relay.hoop = hoop;
            relay.showDebugLogs = showDebugLogs;

            if (showDebugLogs)
                Debug.Log($"[BasketballScoreTrigger] Zona de gol creada en {worldCenter} " +
                          $"(radio={triggerRadius}, alto={triggerHeight}).");
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Vector3 worldCenter = transform.position + Vector3.up * (heightOffsetWorld - belowRimOffset);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
            Gizmos.DrawSphere(worldCenter, triggerRadius);
        }
    }
}
