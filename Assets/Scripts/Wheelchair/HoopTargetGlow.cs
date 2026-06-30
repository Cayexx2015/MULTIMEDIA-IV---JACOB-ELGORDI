using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Attach to the hoop GameObject (the same one that has BasketballHoop).
    /// Aplica un leve tinte/brillo verde sobre el aro para que el jugador
    /// identifique a simple vista a qué aro tiene que tirarle.
    ///
    /// No reemplaza el material original: instancia una copia y le agrega
    /// emisión, así el resto de la escena no se ve afectado.
    /// </summary>
    public class HoopTargetGlow : MonoBehaviour
    {
        [Header("Renderers a iluminar")]
        [Tooltip("Renderers específicos del aro/rim a tintar (recomendado: arrastrar solo " +
                 "la malla del rim/red, NO todo el tablero, para que el efecto sea sutil). " +
                 "Si se deja vacío, se usan automáticamente todos los Renderer hijos de este " +
                 "GameObject.")]
        public Renderer[] targetRenderers;

        [Header("Brillo")]
        [Tooltip("Color del tinte. Por defecto un verde suave.")]
        public Color glowColor = new Color(0.25f, 1f, 0.35f);
        [Tooltip("Intensidad del brillo (HDR). Bajo = sutil, alto = muy notorio.")]
        public float glowIntensity = 0.6f;
        [Tooltip("Si está activo, el brillo respira suavemente en vez de quedar fijo.")]
        public bool pulse = true;
        [Tooltip("Velocidad del respirado (si 'pulse' está activo).")]
        public float pulseSpeed = 1.5f;
        [Tooltip("Cuánto varía la intensidad durante el respirado (0 = nada, 1 = de 0 a glowIntensity).")]
        [Range(0f, 1f)] public float pulseAmount = 0.35f;

        private Material[] _materials;
        private float _t;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<Renderer>(true);

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                Debug.LogWarning("[HoopTargetGlow] No se encontró ningún Renderer para iluminar en " + name + ".");
                return;
            }

            // Instanciar materiales propios (uno por renderer) para no tocar el asset original.
            _materials = new Material[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] == null) continue;
                Material mat = targetRenderers[i].material; // instancia automática
                _materials[i] = mat;
                if (mat.HasProperty("_EmissionColor"))
                    mat.EnableKeyword("_EMISSION");
            }
        }

        private void Update()
        {
            if (_materials == null) return;

            float intensity = glowIntensity;
            if (pulse)
            {
                _t += Time.deltaTime * pulseSpeed;
                float wave = (Mathf.Sin(_t) * 0.5f + 0.5f); // 0..1
                intensity = glowIntensity * (1f - pulseAmount + pulseAmount * wave);
            }

            Color emission = glowColor * intensity;

            foreach (Material mat in _materials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", emission);
            }
        }

        private void OnDestroy()
        {
            if (_materials == null) return;
            foreach (Material mat in _materials)
            {
                if (mat != null) Destroy(mat);
            }
        }
    }
}
