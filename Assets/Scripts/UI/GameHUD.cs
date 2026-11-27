using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoinRush.Audio;
using CoinRush.Core;
using CoinRush.Networking;

namespace CoinRush.UI
{
    /// <summary>
    /// Displays P1/P2 scores, round timer, countdown banner, and end-of-round result panel.
    /// Supports both offline GameManager and online NetworkMatchController.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("HUD Elements")]
        [SerializeField] private TMPro.TextMeshProUGUI player1ScoreText;
        [SerializeField] private TMPro.TextMeshProUGUI player2ScoreText;
        [SerializeField] private TMPro.TextMeshProUGUI timerText;
        [SerializeField] private TMPro.TextMeshProUGUI countdownText;

        [Header("Result Panel")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMPro.TextMeshProUGUI resultTitleText;
        [SerializeField] private TMPro.TextMeshProUGUI resultSummaryText;
        [SerializeField] private UnityEngine.UI.Button restartButton;
        [SerializeField] private UnityEngine.UI.Button backToLobbyButton;

        private GameManager _gameManager;
        private NetworkMatchController _networkController;
        private readonly Color _normalTimerColor = Color.white;
        private readonly Color _urgentTimerColor = new Color(1f, 0.32f, 0.32f, 1f);

        private void Awake()
        {
            WireButtons();
        }

        public void Configure(
            TMPro.TextMeshProUGUI p1Score,
            TMPro.TextMeshProUGUI p2Score,
            TMPro.TextMeshProUGUI timer,
            TMPro.TextMeshProUGUI countdown,
            GameObject resPanel,
            TMPro.TextMeshProUGUI resTitle,
            TMPro.TextMeshProUGUI resSummary,
            UnityEngine.UI.Button restartBtn,
            UnityEngine.UI.Button lobbyBtn = null)
        {
            player1ScoreText = p1Score;
            player2ScoreText = p2Score;
            timerText = timer;
            countdownText = countdown;
            resultPanel = resPanel;
            resultTitleText = resTitle;
            resultSummaryText = resSummary;
            restartButton = restartBtn;
            backToLobbyButton = lobbyBtn;

            WireButtons();
        }

        public void Bind(GameManager manager)
        {
            _gameManager = manager;
            _networkController = null;
        }

        public void BindNetworkController(NetworkMatchController controller)
        {
            _networkController = controller;
            _gameManager = null;
        }

        public void UpdateScores(int p1Score, int p2Score, string p1Name, string p2Name)
        {
            if (player1ScoreText != null)
            {
                player1ScoreText.text = $"{p1Name}: {p1Score}";
            }

            if (player2ScoreText != null)
            {
                player2ScoreText.text = $"{p2Name}: {p2Score}";
            }
        }

        public void UpdateTimer(float secondsRemaining)
        {
            if (timerText == null)
            {
                return;
            }

            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            int mins = totalSeconds / 60;
            int secs = totalSeconds % 60;
            timerText.text = $"{mins:00}:{secs:00}";

            bool urgent = secondsRemaining > 0f && secondsRemaining <= 10f;
            timerText.color = urgent ? _urgentTimerColor : _normalTimerColor;
            float pulse = urgent ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.12f : 1f;
            timerText.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        public void ShowCountdown(string text)
        {
            if (countdownText == null)
            {
                return;
            }
            countdownText.gameObject.SetActive(true);
            countdownText.text = text;
        }

        public void HideCountdown()
        {
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
            }
        }

        public void ShowResult(string headline, string p1Name, int p1Score, string p2Name, int p2Score)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            if (resultTitleText != null)
            {
                resultTitleText.text = headline;
            }

            if (resultSummaryText != null)
            {
                resultSummaryText.text = $"{p1Name}: {p1Score} Koin   |   {p2Name}: {p2Score} Koin";
            }

            if (restartButton != null && _networkController != null && NetworkManager.Singleton != null)
            {
                restartButton.interactable = NetworkManager.Singleton.IsHost;
            }
            else if (restartButton != null)
            {
                restartButton.interactable = true;
            }
        }

        public void HideResult()
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        public void ShowDisconnectResult(string headline, string message)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            if (resultTitleText != null)
            {
                resultTitleText.text = headline;
            }

            if (resultSummaryText != null)
            {
                resultSummaryText.text = message;
            }

            if (restartButton != null)
            {
                restartButton.interactable = false;
            }
        }

        private void WireButtons()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(HandleRestartClicked);
                restartButton.onClick.AddListener(HandleRestartClicked);
            }

            if (backToLobbyButton != null)
            {
                backToLobbyButton.onClick.RemoveListener(HandleBackToLobbyClicked);
                backToLobbyButton.onClick.AddListener(HandleBackToLobbyClicked);
            }
        }

        private void HandleRestartClicked()
        {
            SynthAudioManager.Instance?.PlayUiClick();
            if (_networkController != null)
            {
                _networkController.RequestRestart();
            }
            else if (_gameManager != null)
            {
                _gameManager.RestartMatch();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartMatch();
            }
        }

        private async void HandleBackToLobbyClicked()
        {
            SynthAudioManager.Instance?.PlayUiClick();
            if (LobbySessionManager.Instance != null)
            {
                await LobbySessionManager.Instance.ReturnToLobbyAsync();
            }
            else
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                SceneManager.LoadScene(LobbySessionManager.LobbySceneName, LoadSceneMode.Single);
            }
        }
    }
}
