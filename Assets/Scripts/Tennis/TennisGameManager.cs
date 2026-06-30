using UnityEngine;
using TMPro;

namespace FieldUnlocked.Tennis
{
    /// <summary>
    /// Maneja la lógica del partido de tenis.
    /// Puntuación: 15-30-40-Juego. Sets al mejor de 3.
    /// </summary>
    public class TennisGameManager : MonoBehaviour
    {
        [Header("Referencias")]
        public TennisBall ball;
        public Transform player;
        public Transform cpu;
        public Transform playerServePosition;
        public Transform cpuServePosition;

        [Header("UI")]
        public TextMeshProUGUI playerScoreText;
        public TextMeshProUGUI cpuScoreText;
        public TextMeshProUGUI playerGamesText;
        public TextMeshProUGUI cpuGamesText;
        public TextMeshProUGUI playerSetsText;
        public TextMeshProUGUI cpuSetsText;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI shotTypeText;
        public GameObject gameUI;
        public GameObject startPromptUI;

        [Header("Configuración")]
        public float serveDelay = 2f;
        public float pointDelay = 1.5f;
        public bool playerServes = true;

        // ── Estado del partido ────────────────────────────────────────────
        private static readonly int[] _scoreValues = { 0, 15, 30, 40 };
        private int _playerPoints = 0;
        private int _cpuPoints = 0;
        private int _playerGames = 0;
        private int _cpuGames = 0;
        private int _playerSets = 0;
        private int _cpuSets = 0;
        private bool _deuce = false;
        private bool _playerAdv = false;

        private bool _isPlaying = false;
        private bool _serving = false;
        private float _timer = 0f;
        private bool _waitingPoint = false;

        public bool IsPlaying => _isPlaying;

        private void Start()
        {
            if (startPromptUI != null) startPromptUI.SetActive(true);
            if (gameUI != null) gameUI.SetActive(false);
            if (ball != null) ball.SetIdle();
        }

        private void Update()
        {
            if (!_isPlaying) return;

            if (_serving || _waitingPoint)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    if (_waitingPoint) StartNewPoint();
                    else if (_serving) ServeBall();
                    _serving = false;
                    _waitingPoint = false;
                }
                return;
            }

            if (ball != null && ball.State == TennisBall.BallState.Out)
            {
                DeterminePoint();
            }

            UpdateUI();
        }

        // ── Inicio del partido ────────────────────────────────────────────
        public void StartMatch()
        {
            _playerPoints = 0; _cpuPoints = 0;
            _playerGames = 0; _cpuGames = 0;
            _playerSets = 0; _cpuSets = 0;
            _deuce = false;
            _playerAdv = false;
            _isPlaying = true;
            playerServes = true;

            if (startPromptUI != null) startPromptUI.SetActive(false);
            if (gameUI != null) gameUI.SetActive(true);

            SetStatus("¡A jugar!");
            PrepareServe();
        }

        private void PrepareServe()
        {
            if (ball == null) return;
            ball.SetIdle();

            Transform servePos = playerServes ? playerServePosition : cpuServePosition;
            if (servePos != null)
                ball.transform.position = servePos.position + Vector3.up * 1.5f;

            _serving = true;
            _timer = serveDelay;

            SetStatus(playerServes ? "Tu saque..." : "Saque CPU...");
        }

        private void ServeBall()
        {
            if (ball == null) return;

            Vector3 dir;
            if (playerServes)
            {
                dir = (cpuServePosition != null
                    ? cpuServePosition.position - ball.transform.position
                    : Vector3.forward).normalized;
                dir = (dir + Vector3.up * 0.4f).normalized;
                ball.Launch(dir * 20f, Vector3.zero, 0f, 0);
            }
            else
            {
                dir = (playerServePosition != null
                    ? playerServePosition.position - ball.transform.position
                    : -Vector3.forward).normalized;
                dir = (dir + Vector3.up * 0.4f).normalized;
                ball.Launch(dir * 18f, Vector3.zero, 0f, 1);
            }

            SetStatus("¡En juego!");
        }

        // ── Puntuación ────────────────────────────────────────────────────
        private void DeterminePoint()
        {
            bool playerLost = ball.LastHitter == 0;
            if (playerLost) AddPoint(false);
            else AddPoint(true);
        }

        private void AddPoint(bool toPlayer)
        {
            if (!_isPlaying) return;

            if (toPlayer) _playerPoints++;
            else _cpuPoints++;

            if (_playerPoints >= 3 && _cpuPoints >= 3)
            {
                if (_playerPoints == _cpuPoints)
                {
                    _deuce = true;
                    _playerAdv = false;
                    SetStatus("DEUCE");
                }
                else if (_deuce)
                {
                    if (_playerPoints > _cpuPoints)
                    {
                        if (_playerAdv) WinGame(true);
                        else { _playerAdv = true; SetStatus("VENTAJA - Jugador"); }
                    }
                    else
                    {
                        if (!_playerAdv) WinGame(false);
                        else { _playerAdv = false; SetStatus("VENTAJA - CPU"); }
                    }
                    return;
                }
            }

            if (_playerPoints > 3 && !_deuce) WinGame(true);
            else if (_cpuPoints > 3 && !_deuce) WinGame(false);
            else
            {
                string pScore = _playerPoints <= 3 ? _scoreValues[_playerPoints].ToString() : "40";
                string cScore = _cpuPoints <= 3 ? _scoreValues[_cpuPoints].ToString() : "40";
                SetStatus($"{pScore} - {cScore}");
                _waitingPoint = true;
                _timer = pointDelay;
            }

            UpdateUI();
        }

        private void WinGame(bool player)
        {
            _playerPoints = 0;
            _cpuPoints = 0;
            _deuce = false;
            _playerAdv = false;

            if (player) _playerGames++;
            else _cpuGames++;

            SetStatus(player ? "¡GAME! - Jugador" : "GAME - CPU");

            if (_playerGames >= 6 && _playerGames - _cpuGames >= 2) WinSet(true);
            else if (_cpuGames >= 6 && _cpuGames - _playerGames >= 2) WinSet(false);
            else
            {
                playerServes = !playerServes;
                _waitingPoint = true;
                _timer = pointDelay * 2f;
            }

            UpdateUI();
        }

        private void WinSet(bool player)
        {
            _playerGames = 0;
            _cpuGames = 0;

            if (player) _playerSets++;
            else _cpuSets++;

            SetStatus(player ? "¡SET! - Jugador" : "SET - CPU");

            if (_playerSets >= 2) WinMatch(true);
            else if (_cpuSets >= 2) WinMatch(false);
            else
            {
                playerServes = !playerServes;
                _waitingPoint = true;
                _timer = pointDelay * 3f;
            }

            UpdateUI();
        }

        private void WinMatch(bool player)
        {
            _isPlaying = false;
            SetStatus(player ? "¡GANASTE EL PARTIDO!" : "CPU GANÓ EL PARTIDO");
            if (ball != null) ball.SetIdle();

            Invoke(nameof(ShowStartPrompt), 5f);
        }

        private void StartNewPoint()
        {
            PrepareServe();
        }

        private void ShowStartPrompt()
        {
            if (startPromptUI != null) startPromptUI.SetActive(true);
            if (gameUI != null) gameUI.SetActive(false);
        }

        // ── UI ────────────────────────────────────────────────────────────
        private void UpdateUI()
        {
            string pScore = _playerPoints <= 3 ? _scoreValues[_playerPoints].ToString() : "AD";
            string cScore = _cpuPoints <= 3 ? _scoreValues[_cpuPoints].ToString() : "AD";

            if (playerScoreText != null) playerScoreText.text = pScore;
            if (cpuScoreText != null) cpuScoreText.text = cScore;
            if (playerGamesText != null) playerGamesText.text = _playerGames.ToString();
            if (cpuGamesText != null) cpuGamesText.text = _cpuGames.ToString();
            if (playerSetsText != null) playerSetsText.text = _playerSets.ToString();
            if (cpuSetsText != null) cpuSetsText.text = _cpuSets.ToString();
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[Tennis] {msg}");
        }
    }
}