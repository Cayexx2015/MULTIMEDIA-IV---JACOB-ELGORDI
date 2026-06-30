using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace FieldUnlocked.Tennis
{
    /// <summary>
    /// Trigger en la cancha de tenis.
    /// Cuando Arthur entra, pregunta si quiere jugar.
    /// Attach a un GameObject con Collider (Is Trigger) en la entrada de la cancha.
    /// </summary>
    public class TennisTrigger : MonoBehaviour
    {
        [Header("Referencias")]
        public TennisGameManager gameManager;

        [Header("UI")]
        public GameObject promptUI;
        public Button yesButton;
        public Button noButton;
        public TextMeshProUGUI promptText;

        private bool _playerInZone = false;
        private bool _promptShown = false;

        private void Start()
        {
            if (promptUI != null) promptUI.SetActive(false);

            if (yesButton != null)
                yesButton.onClick.AddListener(OnYes);
            if (noButton != null)
                noButton.onClick.AddListener(OnNo);
        }

        private void Update()
        {
            if (!_playerInZone || _promptShown) return;

            Gamepad gp = Gamepad.current;
            if (gp == null) return;

            // Mostrar prompt al entrar a la zona
            ShowPrompt();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") &&
                !other.name.Contains("Arthur") &&
                !other.name.Contains("XR Origin")) return;

            _playerInZone = true;
            ShowPrompt();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player") &&
                !other.name.Contains("Arthur") &&
                !other.name.Contains("XR Origin")) return;

            _playerInZone = false;
            HidePrompt();
        }

        private void ShowPrompt()
        {
            if (_promptShown) return;
            _promptShown = true;

            if (promptUI != null) promptUI.SetActive(true);
            if (promptText != null)
                promptText.text = "¿Querés jugar al tenis?\n\nA/Cruz = Sí\nB/Círculo = No";
        }

        private void HidePrompt()
        {
            _promptShown = false;
            if (promptUI != null) promptUI.SetActive(false);
        }

        private void OnYes()
        {
            HidePrompt();
            if (gameManager != null)
                gameManager.StartMatch();
        }

        private void OnNo()
        {
            HidePrompt();
            _playerInZone = false;
        }
    }
}