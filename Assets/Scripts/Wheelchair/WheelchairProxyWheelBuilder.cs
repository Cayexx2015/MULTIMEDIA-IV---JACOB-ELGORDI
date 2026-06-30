using UnityEngine;

namespace FieldUnlocked.Wheelchair
{
    /// <summary>
    /// Creates clean proxy cylinder wheels at runtime to replace imported wheelchair wheel meshes
    /// that wobble due to bad mesh pivots or camber offsets.
    ///
    /// Usage:
    ///   1. Assign leftWheelReference / rightWheelReference to the imported wheel transforms.
    ///   2. Call BuildProxyWheels() (from a button, from another script, or set buildOnStart = true).
    ///   3. Assign the created LeftProxyWheel / RightProxyWheel transforms to
    ///      WheelchairLocomotion's leftWheelVisual and rightWheelVisual fields
    ///      so the locomotion script rotates the proxy instead of the original mesh.
    /// </summary>
    public class WheelchairProxyWheelBuilder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Parent transform for the proxy wheels. Defaults to this transform if not assigned.")]
        public Transform wheelParent;
        public Transform leftWheelReference;
        public Transform rightWheelReference;

        [Header("Proxy Appearance")]
        [Tooltip("Optional material applied to both proxy wheels.")]
        public Material proxyWheelMaterial;
        [Tooltip("Visual radius of the proxy wheel cylinder in metres.")]
        public float proxyWheelRadius    = 0.38f;
        [Tooltip("Visual thickness (tread width) of the proxy wheel cylinder in metres.")]
        public float proxyWheelThickness = 0.08f;

        [Header("Options")]
        [Tooltip("If true, disables all Renderers on the original wheel references and their children.")]
        public bool hideOriginalWheels = false;
        [Tooltip("If true, BuildProxyWheels() is called automatically in Start().")]
        public bool buildOnStart = false;

        // Stored so they can be destroyed cleanly if BuildProxyWheels() is called again
        private GameObject _leftProxy;
        private GameObject _rightProxy;

        private void Start()
        {
            if (buildOnStart)
                BuildProxyWheels();
        }

        // ── Public API ─────────────────────────────────────────────────────
        [ContextMenu("Build Proxy Wheels")]
        public void BuildProxyWheels()
        {
            Transform parent = wheelParent != null ? wheelParent : transform;

            // Destroy stored references (Play Mode / same session)
            DestroyProxy(ref _leftProxy);
            DestroyProxy(ref _rightProxy);

            // Also destroy by name under the parent — handles Edit Mode re-runs where
            // private references were not serialized and are null after a domain reload.
            DestroyChildByName(parent, "LeftProxyWheel");
            DestroyChildByName(parent, "RightProxyWheel");

            _leftProxy  = CreateProxyWheel("LeftProxyWheel",  leftWheelReference,  parent);
            _rightProxy = CreateProxyWheel("RightProxyWheel", rightWheelReference, parent);

            if (hideOriginalWheels)
            {
                SetRenderersEnabled(leftWheelReference,  false);
                SetRenderersEnabled(rightWheelReference, false);
            }
        }

        // ── Proxy construction ─────────────────────────────────────────────
        private GameObject CreateProxyWheel(string proxyName, Transform reference, Transform parent)
        {
            if (reference == null)
            {
                Debug.LogWarning($"[WheelchairProxyWheelBuilder] {proxyName}: reference not assigned — skipping.");
                return null;
            }

            // Unity cylinder primitive: default radius = 0.5, height = 2 (along local Y).
            // To reach the desired dimensions:
            //   localScale.x / z = proxyWheelRadius    / 0.5  = proxyWheelRadius * 2
            //   localScale.y     = proxyWheelThickness / 2
            GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            proxy.name = proxyName;

            // Remove the auto-generated collider — this object is visual only
            Collider col = proxy.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else                       DestroyImmediate(col);
            }

            // Match world position and rotation of the reference wheel
            proxy.transform.SetPositionAndRotation(reference.position, reference.rotation);

            // Scale into a thin disc matching the desired radius and thickness
            proxy.transform.localScale = new Vector3(
                proxyWheelRadius * 2f,
                proxyWheelThickness * 0.5f,
                proxyWheelRadius * 2f
            );

            // Insert under the chosen parent without disturbing world-space placement
            proxy.transform.SetParent(parent, worldPositionStays: true);

            // Apply material if provided
            if (proxyWheelMaterial != null)
            {
                MeshRenderer mr = proxy.GetComponent<MeshRenderer>();
                if (mr != null) mr.material = proxyWheelMaterial;
            }

            // NOTE: Assign proxy.transform to WheelchairLocomotion.leftWheelVisual or
            //       rightWheelVisual so the locomotion script drives this proxy, not the
            //       original imported mesh.

            Debug.Log($"[WheelchairProxyWheelBuilder] Created {proxyName} at {reference.position}");
            return proxy;
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private void SetRenderersEnabled(Transform root, bool enabled)
        {
            if (root == null) return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(includeInactive: true))
                r.enabled = enabled;
        }

        private void DestroyProxy(ref GameObject proxy)
        {
            if (proxy == null) return;
            if (Application.isPlaying) Destroy(proxy);
            else                       DestroyImmediate(proxy);
            proxy = null;
        }

        // Finds and destroys a direct child by name — used to clean up Edit Mode leftovers
        // whose GameObject references were lost after a domain reload or scene reopen.
        private void DestroyChildByName(Transform parent, string childName)
        {
            Transform found = parent.Find(childName);
            if (found == null) return;
            if (Application.isPlaying) Destroy(found.gameObject);
            else                       DestroyImmediate(found.gameObject);
        }
    }
}
