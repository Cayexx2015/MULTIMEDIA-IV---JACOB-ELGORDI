using System.Collections;
using TMPro;
using UnityEngine;

namespace FieldUnlocked.Basketball
{
    /// <summary>
    /// Maneja la partida de básquet en silla de ruedas.
    ///
    /// Flujo:
    ///   1. El jugador agarra la pelota por primera vez → arranca el HUD:
    ///      contador "0/5" y timer de 2:00 en cuenta regresiva. Se escucha el
    ///      evento ESTÁTICO ShootingSystem.OnAnyBallPickedUp, así que no importa
    ///      cuál instancia de ShootingSystem hay en la escena (primera o tercera
    ///      persona) — cualquiera que agarre la pelota dispara el inicio.
    ///   2. Cada vez que emboca, suma 1 punto y aparece un "+1" verde flotante
    ///      en el aro.
    ///   3. Apenas llega a 5 puntos (ganaste) o se acaba el tiempo (perdiste),
    ///      se muestra el resultado y, después de una pausa, todo vuelve al
    ///      estado default (sin pelota, HUD oculto) listo para empezar de nuevo.
    ///
    /// Setup en el Inspector:
    ///   - shootingSystem: el ShootingSystem del jugador.
    ///   - hoop: el BasketballHoop de la cancha.
    ///   - hudRoot / scoreText / timerText: UI que se muestra durante la partida.
    ///   - resultPanel / resultText: UI de "¡GANASTE!" / "PERDISTE".
    /// </summary>
    public class BasketballGameManager : MonoBehaviour
    {
        [Header("Referencias")]
        public FieldUnlocked.Wheelchair.ShootingSystem shootingSystem;
        public BasketballHoop hoop;

        [Header("Configuración")]
        public int pointsToWin = 5;
        public float matchDuration = 120f; // 2 minutos
        [Tooltip("Si no se detecta el agarre de la pelota (por ej. testeando sin el evento andando), " +
                 "arranca igual a los X segundos como red de seguridad. 0 = desactivado.")]
        public float fallbackStartDelay = 0f;

        [Header("UI — HUD")]
        public GameObject hudRoot;
        public TextMeshProUGUI scoreText;   // ej: "0/5"
        public TextMeshProUGUI timerText;   // ej: "02:00"

        [Header("UI — Resultado")]
        public GameObject resultPanel;
        public TextMeshProUGUI resultText;
        public float resultDisplayTime = 2.5f;

        [Header("UI — Popup de puntos")]
        [Tooltip("Prefab con un TextMeshPro 3D (World Space) para el '+1' flotante. " +
                 "Si se deja vacío, se crea uno automáticamente en runtime.")]
        public TextMeshPro scorePopupPrefab;
        public float popupRiseDistance = 1f;
        public float popupDuration = 1.1f;
        public Color popupColor = new Color(0.2f, 1f, 0.2f);
        public Color loseColor = new Color(1f, 0.25f, 0.25f);

        [Header("Titileo — Contador (al llegar a la meta)")]
        public Color scoreBlinkColor = new Color(0.2f, 1f, 0.2f); // verde
        public float scoreBlinkSpeed = 8f;

        [Header("Titileo — Timer (cuando quedan pocos segundos)")]
        [Tooltip("Por debajo de este tiempo restante (segundos), el timer empieza a titilar en rojo.")]
        public float timerBlinkThreshold = 10f;
        public Color timerBlinkColor = new Color(1f, 0.2f, 0.2f); // rojo
        public float timerBlinkSpeed = 8f;

        [Header("Debug")]
        public bool showDebugLogs = true;

        private bool _matchStarted = false;
        private bool _matchOver = false;
        private int _score = 0;
        private float _timeRemaining;

        private Color _scoreNormalColor = Color.white;
        private Color _timerNormalColor = Color.white;
        private bool _scoreColorsCached = false;
        private bool _timerColorsCached = false;
        private Coroutine _scoreBlinkRoutine;

        private void Awake()
        {
            _timeRemaining = matchDuration;

            if (scoreText != null) { _scoreNormalColor = scoreText.color; _scoreColorsCached = true; }
            if (timerText != null) { _timerNormalColor = timerText.color; _timerColorsCached = true; }

            // Fallback por si la referencia no quedó asignada en el Inspector
            // (por ejemplo, al editar la escena fuera del Editor de Unity).
            if (shootingSystem == null)
            {
                shootingSystem = FindAnyObjectByType<FieldUnlocked.Wheelchair.ShootingSystem>();
                if (showDebugLogs && shootingSystem != null)
                    Debug.LogWarning("[BasketballGameManager] 'shootingSystem' no estaba asignado en el Inspector, se encontró automáticamente: " + shootingSystem.name);
            }
            if (hoop == null)
            {
                hoop = FindAnyObjectByType<BasketballHoop>();
                if (showDebugLogs && hoop != null)
                    Debug.LogWarning("[BasketballGameManager] 'hoop' no estaba asignado en el Inspector, se encontró automáticamente: " + hoop.name);
            }

            if (shootingSystem == null)
                Debug.LogError("[BasketballGameManager] No se encontró ningún ShootingSystem en la escena. El contador y el timer no van a funcionar hasta que se asigne o exista uno.");
            if (hoop == null)
                Debug.LogError("[BasketballGameManager] No se encontró ningún BasketballHoop en la escena. El scoring no va a funcionar hasta que se asigne o exista uno.");

            if (hudRoot != null) hudRoot.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            // Evento estático: se dispara sin importar cuál ShootingSystem agarró la pelota.
            FieldUnlocked.Wheelchair.ShootingSystem.OnAnyBallPickedUp += HandleBallPickedUp;

            if (hoop != null)
            {
                hoop.OnScored += HandleScored;
                if (showDebugLogs)
                    Debug.Log("[BasketballGameManager] Suscripto a OnScored de '" + hoop.name + "'.");
            }
            else if (showDebugLogs)
            {
                Debug.LogError("[BasketballGameManager] No hay 'hoop' asignado al habilitarse — el scoring NO va a funcionar.");
            }
        }

        private void OnDisable()
        {
            FieldUnlocked.Wheelchair.ShootingSystem.OnAnyBallPickedUp -= HandleBallPickedUp;
            if (hoop != null) hoop.OnScored -= HandleScored;
        }

        private void Start()
        {
            if (fallbackStartDelay > 0f)
                StartCoroutine(StartMatchAfterDelay(fallbackStartDelay));
        }

        // ── Inicio de partida ───────────────────────────────────────────────
        private void HandleBallPickedUp()
        {
            if (showDebugLogs) Debug.Log("[BasketballGameManager] Evento de pickup recibido.");
            if (_matchStarted || _matchOver) return;
            StartMatch();
        }

        private IEnumerator StartMatchAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_matchStarted && !_matchOver)
            {
                if (showDebugLogs) Debug.Log("[BasketballGameManager] Arranque de seguridad por tiempo (no se detectó pickup).");
                StartMatch();
            }
        }

        /// <summary>Llamado externamente (ej. desde NpcInteractable) para iniciar la partida.</summary>
        public void BeginMatch()
        {
            if (!_matchStarted && !_matchOver)
                StartMatch();
        }

        private void StartMatch()
        {
            _matchStarted = true;
            _matchOver = false;
            _score = 0;
            _timeRemaining = matchDuration;

            if (hudRoot != null) hudRoot.SetActive(true);
            if (resultPanel != null) resultPanel.SetActive(false);

            // Restaurar colores normales por si quedó algo titilando de una partida anterior.
            if (_scoreBlinkRoutine != null) { StopCoroutine(_scoreBlinkRoutine); _scoreBlinkRoutine = null; }
            if (scoreText != null && _scoreColorsCached) scoreText.color = _scoreNormalColor;
            if (timerText != null && _timerColorsCached) timerText.color = _timerNormalColor;

            UpdateScoreUI();
            UpdateTimerUI();

            if (showDebugLogs) Debug.Log("[BasketballGameManager] ¡Partida iniciada! Meta: " + pointsToWin + " puntos en " + matchDuration + "s.");
        }

        // ── Timer ─────────────────────────────────────────────────────────
        private void Update()
        {
            if (!_matchStarted || _matchOver) return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                UpdateTimerUI();
                if (timerText != null && _timerColorsCached) timerText.color = _timerNormalColor;
                EndMatch(false);
                return;
            }

            UpdateTimerUI();
            UpdateTimerBlink();
        }

        // Titilado del timer en rojo cuando quedan pocos segundos. Se hace acá (no con
        // una coroutine aparte) porque ya estamos en Update() corriendo todos los frames
        // mientras la partida está en curso.
        private void UpdateTimerBlink()
        {
            if (timerText == null || !_timerColorsCached) return;

            if (_timeRemaining <= timerBlinkThreshold)
            {
                float pulse = (Mathf.Sin(Time.time * timerBlinkSpeed) * 0.5f + 0.5f);
                timerText.color = Color.Lerp(_timerNormalColor, timerBlinkColor, pulse);
            }
            else if (timerText.color != _timerNormalColor)
            {
                timerText.color = _timerNormalColor;
            }
        }

        // ── Scoring ───────────────────────────────────────────────────────
        private void HandleScored(int points, Vector3 worldPos)
        {
            if (showDebugLogs)
                Debug.Log($"[BasketballGameManager] Evento OnScored recibido (matchStarted={_matchStarted}, matchOver={_matchOver}).");

            if (!_matchStarted || _matchOver)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[BasketballGameManager] Se ignoró el gol porque la partida no está en curso (¿agarraste la pelota para arrancarla?).");
                return;
            }

            _score++;
            UpdateScoreUI();
            SpawnPopup(worldPos, "+1", popupColor);

            if (showDebugLogs) Debug.Log($"[BasketballGameManager] ¡Encestó! {_score}/{pointsToWin}");

            if (_score >= pointsToWin)
            {
                if (scoreText != null && _scoreColorsCached)
                    _scoreBlinkRoutine = StartCoroutine(BlinkScoreGreen());
                EndMatch(true);
            }
        }

        // Titila el contador en verde al llegar a la meta (5/5). Sigue titilando durante
        // la pantalla de resultado y se detiene/limpia en StartMatch() la próxima partida.
        private IEnumerator BlinkScoreGreen()
        {
            while (true)
            {
                if (scoreText == null) yield break;
                float pulse = (Mathf.Sin(Time.time * scoreBlinkSpeed) * 0.5f + 0.5f);
                scoreText.color = Color.Lerp(_scoreNormalColor, scoreBlinkColor, pulse);
                yield return null;
            }
        }

        // ── Fin de partida ────────────────────────────────────────────────
        private void EndMatch(bool won)
        {
            _matchOver = true;

            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultText != null)
            {
                resultText.text = won ? "¡GANASTE!" : "PERDISTE";
                resultText.color = won ? popupColor : loseColor;
            }

            if (showDebugLogs) Debug.Log(won ? "[BasketballGameManager] GANASTE" : "[BasketballGameManager] PERDISTE (se acabó el tiempo)");

            StartCoroutine(ReturnToDefaultAfterDelay(resultDisplayTime));
        }

        private IEnumerator ReturnToDefaultAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (resultPanel != null) resultPanel.SetActive(false);
            if (hudRoot != null) hudRoot.SetActive(false);

            if (_scoreBlinkRoutine != null) { StopCoroutine(_scoreBlinkRoutine); _scoreBlinkRoutine = null; }
            if (scoreText != null && _scoreColorsCached) scoreText.color = _scoreNormalColor;
            if (timerText != null && _timerColorsCached) timerText.color = _timerNormalColor;

            // Vuelve todo a estado default — el jugador ya no tiene la pelota
            // (BasketballHoop.Land() ya la soltó en el aro), listo para empezar de nuevo
            // la próxima vez que la agarre.
            _matchStarted = false;
            _matchOver = false;
            _score = 0;
            _timeRemaining = matchDuration;

            // Si está activo el arranque de seguridad, lo reprograma para la próxima partida.
            if (fallbackStartDelay > 0f)
                StartCoroutine(StartMatchAfterDelay(fallbackStartDelay));
        }

        // ── UI helpers ───────────────────────────────────────────────────
        private void UpdateScoreUI()
        {
            if (scoreText != null) scoreText.text = $"{_score}/{pointsToWin}";
        }

        private void UpdateTimerUI()
        {
            if (timerText == null) return;
            int totalSeconds = Mathf.CeilToInt(_timeRemaining);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        // ── Popup "+1" ────────────────────────────────────────────────────
        private void SpawnPopup(Vector3 worldPos, string text, Color color)
        {
            TextMeshPro popup;
            if (scorePopupPrefab != null)
            {
                popup = Instantiate(scorePopupPrefab, worldPos, Quaternion.identity);
            }
            else
            {
                GameObject go = new GameObject("ScorePopup");
                go.transform.position = worldPos;
                popup = go.AddComponent<TextMeshPro>();
                popup.fontSize = 6;
                popup.alignment = TextAlignmentOptions.Center;
            }

            popup.text = text;
            popup.color = color;

            if (Camera.main != null)
                popup.transform.rotation = Camera.main.transform.rotation;

            StartCoroutine(AnimatePopup(popup));
        }

        private IEnumerator AnimatePopup(TextMeshPro popup)
        {
            if (popup == null) yield break;

            Vector3 startPos = popup.transform.position;
            Vector3 endPos = startPos + Vector3.up * popupRiseDistance;
            Color startColor = popup.color;
            float t = 0f;

            while (t < popupDuration)
            {
                if (popup == null) yield break;
                t += Time.deltaTime;
                float ratio = t / popupDuration;

                popup.transform.position = Vector3.Lerp(startPos, endPos, ratio);
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, ratio);
                popup.color = c;

                if (Camera.main != null)
                    popup.transform.rotation = Camera.main.transform.rotation;

                yield return null;
            }

            if (popup != null) Destroy(popup.gameObject);
        }
    }
}
