using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Vive en el GameObject "ScoreTriggerZone" que crea BasketballScoreTrigger en
    /// runtime. Unity exige que el Collider y el método OnTriggerEnter estén en el
    /// MISMO GameObject, así que este componente simplemente reenvía el aviso al
    /// BasketballHoop real (que puede estar en otro GameObject, el del aro).
    /// No hace falta tocarlo a mano — BasketballScoreTrigger lo agrega y configura solo.
    /// </summary>
    public class BasketballScoreTriggerRelay : MonoBehaviour
    {
        public BasketballHoop hoop;
        public bool showDebugLogs = true;

        private void OnTriggerEnter(Collider other)
        {
            if (hoop == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[BasketballScoreTriggerRelay] No tengo referencia a BasketballHoop — no puedo avisar el gol.");
                return;
            }
            hoop.ProcessPossibleScore(other);
        }
    }
}
