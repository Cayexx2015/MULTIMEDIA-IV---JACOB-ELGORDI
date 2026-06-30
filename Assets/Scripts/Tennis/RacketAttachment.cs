using UnityEngine;

namespace FieldUnlocked.Tennis
{
    /// <summary>
    /// Pega la raqueta a la mano derecha de Arthur.
    /// Attach a Arthur_Rigged_01.
    /// </summary>
    public class RacketAttachment : MonoBehaviour
    {
        [Header("Referencias")]
        public Transform racketObject;
        [Tooltip("Offset de posición relativo a la mano derecha")]
        public Vector3 positionOffset = new Vector3(0f, 0.1f, 0.15f);
        [Tooltip("Offset de rotación")]
        public Vector3 rotationOffset = new Vector3(0f, 0f, 90f);

        private Transform _rightHand;

        private void Start()
        {
            // Buscar el hueso de la mano derecha
            Transform[] bones = GetComponentsInChildren<Transform>(true);
            foreach (Transform bone in bones)
            {
                string n = bone.name.ToLower().Replace("_","").Replace("-","");
                if (n.Contains("righthand") || n.Contains("rhand"))
                {
                    _rightHand = bone;
                    break;
                }
            }

            if (_rightHand == null)
                Debug.LogWarning("[RacketAttachment] No se encontró RightHand. Asignala manualmente.");

            // Parentar la raqueta a la mano
            if (racketObject != null && _rightHand != null)
            {
                racketObject.SetParent(_rightHand, false);
                racketObject.localPosition = positionOffset;
                racketObject.localRotation = Quaternion.Euler(rotationOffset);
            }
        }

        private void LateUpdate()
        {
            // Mantener posición en caso de que el Animator la mueva
            if (racketObject != null && _rightHand != null)
            {
                racketObject.position = _rightHand.TransformPoint(positionOffset);
                racketObject.rotation = _rightHand.rotation * Quaternion.Euler(rotationOffset);
            }
        }
    }
}
