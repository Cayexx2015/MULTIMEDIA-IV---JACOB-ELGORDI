using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Attach to Basket ball.
    /// Muestra un destello/outline cuando el jugador puede agarrar la pelota.
    /// Usa emisión del material para el efecto de brillo — compatible con URP.
    /// </summary>
    public class BallPickupHighlight : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("El ShootingSystem del jugador.")]
        public MonoBehaviour shootingSystem;

        [Header("Highlight")]
        [Tooltip("Color del destello cuando se puede agarrar.")]
        public Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f); // dorado
        [Tooltip("Intensidad del brillo (HDR).")]
        public float highlightIntensity = 1.8f;
        [Tooltip("Velocidad del pulso del destello.")]
        public float pulseSpeed = 3f;
        [Tooltip("Radio de detección — tiene que coincidir con el Pickup Radius del ShootingSystem.")]
        public float pickupRadius = 2f;

        private Renderer _renderer;
        private Material _material;
        private bool _highlighted = false;
        private float _pulseTimer = 0f;

        // Color emisivo original (para restaurar)
        private Color _originalEmission = Color.black;
        private bool _hadEmission = false;

        // Cache de la resolución dinámica de ShootingSystem (evita escanear toda
        // la escena todos los frames cuando "shootingSystem" no está asignado a mano).
        private FieldUnlocked.Wheelchair.ShootingSystem _resolvedSystem;
        private float _resolveTimer = 0f;
        private const float ResolveInterval = 0.5f;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) return;

            // Instanciar material para no modificar el asset original
            _material = _renderer.material;

            // Guardar emisión original
            if (_material.HasProperty("_EmissionColor"))
            {
                _originalEmission = _material.GetColor("_EmissionColor");
                _hadEmission = _material.IsKeywordEnabled("_EMISSION");
            }
        }

        private void Update()
        {
            if (_renderer == null || _material == null) return;

            // Resolver qué ShootingSystem usar.
            // OJO: la escena tiene más de una instancia de ShootingSystem (la de
            // primera persona y la de tercera persona / cuerpo completo). Si nos
            // bindeamos para siempre a la primera que encontremos (FindFirstObjectByType
            // una sola vez), podemos quedar pegados a la instancia que NO es el
            // jugador activo — y entonces el destello de "podés agarrar la pelota"
            // nunca aparece (la distancia se calcula contra el jugador equivocado).
            // Por eso, si no se asignó a mano en el Inspector, cada frame usamos la
            // instancia más CERCANA a la pelota — esa siempre es la del jugador que
            // realmente está jugando, sin importar qué modo de cámara esté activo.
            FieldUnlocked.Wheelchair.ShootingSystem sys = shootingSystem as FieldUnlocked.Wheelchair.ShootingSystem;
            if (sys == null)
            {
                // O bien "shootingSystem" no se asignó en el Inspector, o se asignó
                // pero a un componente que NO es un ShootingSystem (el cast de arriba
                // da null sin avisar) — en ambos casos resolvemos dinámicamente.
                _resolveTimer -= Time.deltaTime;
                if (_resolvedSystem == null || _resolveTimer <= 0f)
                {
                    _resolveTimer = ResolveInterval;
                    _resolvedSystem = FindClosestShootingSystem();
                }
                sys = _resolvedSystem;
            }

            // Si después de todo esto seguimos sin una referencia válida, no hay nada
            // para comparar todavía (recién arrancó la escena, por ejemplo) — salir
            // sin tirar NullReferenceException.
            if (sys == null) return;

            // Verificar distancia al jugador
            float dist = Vector3.Distance(transform.position, sys.transform.position);
            bool canPickup = dist <= pickupRadius;

            // Verificar si el jugador ya tiene la pelota
            if (sys.HasBall) canPickup = false;

            if (canPickup && !_highlighted)
                SetHighlight(true);
            else if (!canPickup && _highlighted)
                SetHighlight(false);

            // Pulso animado mientras está cerca
            if (_highlighted)
            {
                _pulseTimer += Time.deltaTime * pulseSpeed;
                float pulse = (Mathf.Sin(_pulseTimer) * 0.5f + 0.5f); // 0..1
                float intensity = Mathf.Lerp(highlightIntensity * 0.4f,
                                             highlightIntensity, pulse);

                if (_material.HasProperty("_EmissionColor"))
                    _material.SetColor("_EmissionColor",
                        highlightColor * intensity);
            }
        }

        /// <summary>
        /// Busca, entre TODAS las instancias de ShootingSystem en la escena, la más
        /// cercana a esta pelota. Evita quedar pegado a una instancia "inactiva"
        /// (ej. la de tercera persona cuando se juega en primera persona, o viceversa).
        /// </summary>
        private FieldUnlocked.Wheelchair.ShootingSystem FindClosestShootingSystem()
        {
            var all = FindObjectsByType<FieldUnlocked.Wheelchair.ShootingSystem>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return null;

            FieldUnlocked.Wheelchair.ShootingSystem closest = null;
            float closestDist = float.MaxValue;
            foreach (var candidate in all)
            {
                float d = Vector3.Distance(transform.position, candidate.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = candidate;
                }
            }
            return closest;
        }

        private void SetHighlight(bool on)
        {
            _highlighted = on;
            _pulseTimer = 0f;

            if (!_material.HasProperty("_EmissionColor")) return;

            if (on)
            {
                _material.EnableKeyword("_EMISSION");
                _material.SetColor("_EmissionColor", highlightColor * highlightIntensity);
            }
            else
            {
                if (!_hadEmission)
                    _material.DisableKeyword("_EMISSION");
                _material.SetColor("_EmissionColor", _originalEmission);
            }
        }

        private void OnDestroy()
        {
            // Restaurar material original al destruir
            if (_material != null && _material.HasProperty("_EmissionColor"))
            {
                if (!_hadEmission) _material.DisableKeyword("_EMISSION");
                _material.SetColor("_EmissionColor", _originalEmission);
            }
        }
    }
}