using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using CoinRush.Audio;
using CoinRush.Networking;

namespace CoinRush.UI
{
    /// <summary>
    /// Manages the Main Menu & P2P Room Lobby UI (Vs AI, Create Room, Join by Code, Player Slots).
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject roomLobbyPanel;

        [Header("Main Menu Controls")]
        [SerializeField] private TMPro.TMP_InputField playerNameInput;
        [SerializeField] private TMPro.TMP_InputField roomCodeInput;
        [SerializeField] private UnityEngine.UI.Button playVsAiButton;
        [SerializeField] private UnityEngine.UI.Button createRoomButton;
        [SerializeField] private UnityEngine.UI.Button joinRoomButton;
        [SerializeField] private UnityEngine.UI.Button quitGameButton;
        [SerializeField] private TMPro.TextMeshProUGUI statusText;

        [Header("Room Lobby Controls")]
        [SerializeField] private TMPro.TextMeshProUGUI roomCodeDisplayText;
        [SerializeField] private TMPro.TextMeshProUGUI player1SlotText;
        [SerializeField] private TMPro.TextMeshProUGUI player2SlotText;
        [SerializeField] private TMPro.TextMeshProUGUI roomInfoText;
        [SerializeField] private UnityEngine.UI.Button copyCodeButton;
        [SerializeField] private UnityEngine.UI.Button startMatchButton;
        [SerializeField] private UnityEngine.UI.Button leaveRoomButton;

        private bool _isBusy;

        public void Configure(
            GameObject mainPanel,
            GameObject roomPanel,
            TMPro.TMP_InputField nameInput,
            TMPro.TMP_InputField codeInput,
            UnityEngine.UI.Button vsAiBtn,
            UnityEngine.UI.Button createBtn,
            UnityEngine.UI.Button joinBtn,
            TMPro.TextMeshProUGUI status,
            TMPro.TextMeshProUGUI codeDisplay,
            TMPro.TextMeshProUGUI p1Slot,
            TMPro.TextMeshProUGUI p2Slot,
            TMPro.TextMeshProUGUI roomInfo,
            UnityEngine.UI.Button copyBtn,
            UnityEngine.UI.Button startBtn,
            UnityEngine.UI.Button leaveBtn,
            UnityEngine.UI.Button quitBtn = null)
        {
            mainMenuPanel = mainPanel;
            roomLobbyPanel = roomPanel;
            playerNameInput = nameInput;
            roomCodeInput = codeInput;
            playVsAiButton = vsAiBtn;
            createRoomButton = createBtn;
            joinRoomButton = joinBtn;
            quitGameButton = quitBtn;
            statusText = status;
            roomCodeDisplayText = codeDisplay;
            player1SlotText = p1Slot;
            player2SlotText = p2Slot;
            roomInfoText = roomInfo;
            copyCodeButton = copyBtn;
            startMatchButton = startBtn;
            leaveRoomButton = leaveBtn;
        }

        private void OnEnable()
        {
            WireButtons();

            if (LobbySessionManager.Instance != null)
            {
                LobbySessionManager.Instance.SessionUpdated += RefreshRoomLobby;
                LobbySessionManager.Instance.StatusMessageChanged += HandleStatusMessage;
                LobbySessionManager.Instance.SessionClosed += ShowMainMenu;
            }
        }

        private void OnDisable()
        {
            if (LobbySessionManager.Instance != null)
            {
                LobbySessionManager.Instance.SessionUpdated -= RefreshRoomLobby;
                LobbySessionManager.Instance.StatusMessageChanged -= HandleStatusMessage;
                LobbySessionManager.Instance.SessionClosed -= ShowMainMenu;
            }
        }

        private void Start()
        {
            if (LobbySessionManager.Instance != null && LobbySessionManager.Instance.CurrentSession != null)
            {
                ShowRoomLobby();
            }
            else
            {
                ShowMainMenu();
                HandleStatusMessage("Pilih mode bermain atau buat/gabung room P2P.");
            }
        }

        private void Update()
        {
            if (roomLobbyPanel != null && roomLobbyPanel.activeSelf && startMatchButton != null)
            {
                bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
                startMatchButton.gameObject.SetActive(isHost);
                if (isHost && LobbySessionManager.Instance != null)
                {
                    startMatchButton.interactable = LobbySessionManager.Instance.CanHostStartMatch();
                }
            }
        }

        private void WireButtons()
        {
            if (playVsAiButton != null)
            {
                playVsAiButton.onClick.RemoveListener(HandlePlayVsAi);
                playVsAiButton.onClick.AddListener(HandlePlayVsAi);
            }
            if (createRoomButton != null)
            {
                createRoomButton.onClick.RemoveListener(HandleCreateRoom);
                createRoomButton.onClick.AddListener(HandleCreateRoom);
            }
            if (joinRoomButton != null)
            {
                joinRoomButton.onClick.RemoveListener(HandleJoinRoom);
                joinRoomButton.onClick.AddListener(HandleJoinRoom);
            }
            if (copyCodeButton != null)
            {
                copyCodeButton.onClick.RemoveListener(HandleCopyCode);
                copyCodeButton.onClick.AddListener(HandleCopyCode);
            }
            if (startMatchButton != null)
            {
                startMatchButton.onClick.RemoveListener(HandleStartMatch);
                startMatchButton.onClick.AddListener(HandleStartMatch);
            }
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveListener(HandleLeaveRoom);
                leaveRoomButton.onClick.AddListener(HandleLeaveRoom);
            }
            if (quitGameButton != null)
            {
                quitGameButton.onClick.RemoveListener(HandleQuitGame);
                quitGameButton.onClick.AddListener(HandleQuitGame);
            }
        }

        private void HandleQuitGame()
        {
            SynthAudioManager.Instance?.PlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandlePlayVsAi()
        {
            if (_isBusy || LobbySessionManager.Instance == null) return;
            SynthAudioManager.Instance?.PlayUiClick();
            LobbySessionManager.Instance.StartOfflineVsAI(GetPlayerNameInput());
        }

        private async void HandleCreateRoom()
        {
            if (_isBusy || LobbySessionManager.Instance == null) return;
            SynthAudioManager.Instance?.PlayUiClick();
            SetBusy(true);

            bool ok = await LobbySessionManager.Instance.CreateRelayRoomAsync(GetPlayerNameInput());
            SetBusy(false);

            if (ok)
            {
                ShowRoomLobby();
            }
        }

        private async void HandleJoinRoom()
        {
            if (_isBusy || LobbySessionManager.Instance == null) return;
            SynthAudioManager.Instance?.PlayUiClick();
            SetBusy(true);

            string code = roomCodeInput != null ? roomCodeInput.text : string.Empty;
            bool ok = await LobbySessionManager.Instance.JoinRelayRoomByCodeAsync(code, GetPlayerNameInput());
            SetBusy(false);

            if (ok)
            {
                ShowRoomLobby();
            }
        }

        private void HandleCopyCode()
        {
            SynthAudioManager.Instance?.PlayUiClick();
            var session = LobbySessionManager.Instance?.CurrentSession;
            if (session != null && !string.IsNullOrEmpty(session.Code))
            {
                GUIUtility.systemCopyBuffer = session.Code;
                if (roomInfoText != null)
                {
                    roomInfoText.text = $"Kode {session.Code} disalin ke clipboard!";
                }
            }
        }

        private void HandleStartMatch()
        {
            if (_isBusy || LobbySessionManager.Instance == null) return;
            SynthAudioManager.Instance?.PlayUiClick();
            LobbySessionManager.Instance.StartOnlineMatch();
        }

        private async void HandleLeaveRoom()
        {
            if (_isBusy || LobbySessionManager.Instance == null) return;
            SynthAudioManager.Instance?.PlayUiClick();
            SetBusy(true);
            await LobbySessionManager.Instance.LeaveRoomAsync();
            SetBusy(false);
            ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (roomLobbyPanel != null) roomLobbyPanel.SetActive(false);
        }

        private void ShowRoomLobby()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (roomLobbyPanel != null) roomLobbyPanel.SetActive(true);
            RefreshRoomLobby();
        }

        private void RefreshRoomLobby()
        {
            if (LobbySessionManager.Instance == null || LobbySessionManager.Instance.CurrentSession == null)
            {
                return;
            }

            var session = LobbySessionManager.Instance.CurrentSession;
            if (roomCodeDisplayText != null)
            {
                roomCodeDisplayText.text = $"KODE ROOM: {session.Code}";
            }

            List<LobbyPlayerInfo> players = LobbySessionManager.Instance.GetLobbyPlayersInfo();
            string slot1 = "1. Menunggu Host...";
            string slot2 = "2. Menunggu Player 2...";

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].IsHost)
                {
                    slot1 = $"1. {players[i].DisplayName} (Host - Biru)";
                }
                else
                {
                    slot2 = $"2. {players[i].DisplayName} (Client - Merah)";
                }
            }

            if (player1SlotText != null) player1SlotText.text = slot1;
            if (player2SlotText != null) player2SlotText.text = slot2;

            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            if (roomInfoText != null)
            {
                if (players.Count < 2)
                {
                    roomInfoText.text = "Bagikan Kode Room ke Player 2 untuk bergabung...";
                }
                else
                {
                    roomInfoText.text = isHost
                        ? "Kedua pemain siap! Klik Mulai Match."
                        : "Kedua pemain siap! Menunggu Host memulai...";
                }
            }
        }

        private void HandleStatusMessage(string msg)
        {
            if (statusText != null)
            {
                statusText.text = msg;
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            if (playVsAiButton != null) playVsAiButton.interactable = !busy;
            if (createRoomButton != null) createRoomButton.interactable = !busy;
            if (joinRoomButton != null) joinRoomButton.interactable = !busy;
        }

        private string GetPlayerNameInput()
        {
            return playerNameInput != null ? playerNameInput.text : string.Empty;
        }
    }
}
