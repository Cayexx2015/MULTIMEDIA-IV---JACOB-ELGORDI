using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Construye colliders circulares sobre el rim del aro.
    /// Maneja correctamente cualquier escala del parent (incluyendo Scale 100).
    /// Los colliders se crean en WORLD SPACE y se ponen bajo un objeto con
    /// escala 1 para evitar distorsiones.
    /// </summary>
    public class RimColliderBuilder : MonoBehaviour
    {
        [Header("Rim shape")]
        [Tooltip("Radio del aro en METROS (world space). Un aro NBA mide 0.23m de radio.")]
        public float rimRadiusWorld = 0.23f;
        [Tooltip("Offset vertical en METROS desde el centro del GameObject hasta el rim.")]
        public float heightOffsetWorld = 0f;
        [Range(4, 16)]
        public int segments = 8;

        [Header("Tamaño de cada segmento (metros)")]
        public float segmentWidth = 0.08f;
        public float segmentHeight = 0.06f;
        public float segmentDepth = 0.08f;

        [Header("Physics Material")]
        public PhysicsMaterial rimMaterial;

        [Header("Debug")]
        public bool drawGizmos = true;

        [ContextMenu("Build Rim Colliders")]
        public void BuildRimColliders()
        {
            // Limpiar colliders anteriores
            Transform existing = transform.Find("RimColliders");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            // Crear contenedor en WORLD SPACE con escala 1
            // Lo ponemos como hijo del parent del aro (no del aro mismo)
            // para evitar heredar el Scale 100
            GameObject container = new GameObject("RimColliders");
            Transform containerT = container.transform;

            // Posición world del centro del aro
            Vector3 worldCenter = transform.position + Vector3.up * heightOffsetWorld;
            containerT.position = worldCenter;
            containerT.rotation = Quaternion.identity;
            containerT.localScale = Vector3.one;

            // Parentar al mismo padre que el aro para mantener jerarquía limpia
            containerT.SetParent(transform.parent, worldPositionStays: true);

            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                float rad = angle * Mathf.Deg2Rad;

                // Posición world de cada segmento sobre el círculo
                Vector3 worldPos = worldCenter + new Vector3(
                    Mathf.Sin(rad) * rimRadiusWorld,
                    0f,
                    Mathf.Cos(rad) * rimRadiusWorld);

                Quaternion worldRot = Quaternion.Euler(0f, angle, 0f);

                GameObject seg = new GameObject($"RimSeg_{i:D2}");
                seg.transform.SetParent(containerT, false);
                seg.transform.position = worldPos;
                seg.transform.rotation = worldRot;
                seg.transform.localScale = Vector3.one;

                BoxCollider bc = seg.AddComponent<BoxCollider>();
                bc.size = new Vector3(segmentWidth, segmentHeight, segmentDepth);
                bc.center = Vector3.zero;

                if (rimMaterial != null)
                    bc.sharedMaterial = rimMaterial;
            }

            Debug.Log($"[RimColliderBuilder] Construidos {segments} segmentos en world space. " +
                      $"Radio={rimRadiusWorld}m  Centro={worldCenter}");
        }

        [ContextMenu("Auto Detect Rim Center Height")]
        public void AutoDetectHeight()
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend == null)
            {
                Debug.LogWarning("[RimColliderBuilder] No se encontró Renderer. Ajustá heightOffsetWorld manualmente.");
                return;
            }
            // Usar el centro de los bounds del renderer como altura del rim
            heightOffsetWorld = rend.bounds.center.y - transform.position.y;
            Debug.Log($"[RimColliderBuilder] Height detectado: {heightOffsetWorld:F4}m. Ahora corré 'Build Rim Colliders'.");
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Vector3 center = transform.position + Vector3.up * heightOffsetWorld;

            // Círculo verde = rim en world space
            Gizmos.color = Color.green;
            int steps = 64;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float a = i * 360f / steps * Mathf.Deg2Rad;
                Vector3 pt = center + new Vector3(
                    Mathf.Sin(a) * rimRadiusWorld,
                    0f,
                    Mathf.Cos(a) * rimRadiusWorld);
                if (i > 0) Gizmos.DrawLine(prev, pt);
                prev = pt;
            }

            // Puntos amarillos = posición de cada segmento
            Gizmos.color = Color.yellow;
            float step = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * step * Mathf.Deg2Rad;
                Vector3 pt = center + new Vector3(
                    Mathf.Sin(a) * rimRadiusWorld,
                    0f,
                    Mathf.Cos(a) * rimRadiusWorld);
                Gizmos.DrawSphere(pt, 0.05f);
            }

            // Cruz en el centro
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(center, 0.06f);
        }
    }
}