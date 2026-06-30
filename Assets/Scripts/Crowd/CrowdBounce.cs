using UnityEngine;

namespace FieldUnlocked.Crowd
{
    /// <summary>
    /// Hace que un sprite de público suba y baje suavemente, simulando
    /// que está saltando/alentando. Pensado para los NPCs estilo PES 2006.
    /// Attach a cada CrowdNPC.
    /// </summary>
    public class CrowdBounce : MonoBehaviour
    {
        [Header("Movimiento")]
        [Tooltip("Qué tan alto sube respecto a su posición original (en metros).")]
        public float bounceHeight = 0.08f;

        [Tooltip("Velocidad del movimiento de sube y baja.")]
        public float bounceSpeed = 2f;

        [Tooltip("Desfasa el movimiento de cada NPC para que no salten todos juntos. " +
                 "Dejar en -1 para que se asigne un valor random automáticamente.")]
        public float phaseOffset = -1f;

        private Vector3 _startPos;

        private void Start()
        {
            _startPos = transform.position;

            // Si no se asignó un offset manual, generar uno random
            // para que los NPCs no salten todos sincronizados
            if (phaseOffset < 0f)
                phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float offset = Mathf.Sin(Time.time * bounceSpeed + phaseOffset) * bounceHeight;
            transform.position = _startPos + new Vector3(0f, Mathf.Abs(offset), 0f);
        }
    }
}
