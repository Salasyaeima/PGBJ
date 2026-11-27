using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoinRush.Networking
{
    public readonly struct LobbyPlayerInfo
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly bool IsHost;

        public LobbyPlayerInfo(string id, string displayName, bool isHost)
        {
            Id = id;
            DisplayName = displayName;
            IsHost = isHost;
        }
    }

    /// <summary>
    /// Coordinates Unity Services sign-in, P2P Relay sessions (Host & Join by Code),
    /// and transitions to the CoinRush gameplay scene (both Online P2P and Offline Vs AI).
    /// </summary>
    public class LobbySessionManager : MonoBehaviour
    {
        public static LobbySessionManager Instance { get; private set; }

        public const string LobbySceneName = "Lobby";
        public const string GameplaySceneName = "CoinRush";
        private const string DisplayNamePropertyKey = "displayName";

        [SerializeField] private int maxPlayers = 2;

        public ISession CurrentSession { get; private set; }
        public string LocalPlayerName { get; private set; } = "Player";
        public bool IsOfflineVsAIMode { get; private set; }

        public event Action SessionUpdated;
        public event Action<string> StatusMessageChanged;
        public event Action SessionClosed;

        private bool _isInitializing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnsubscribeSessionEvents();
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            UnsubscribeSessionEvents();
            if (CurrentSession != null)
            {
                try
                {
                    if (CurrentSession.IsHost && CurrentSession is IHostSession hostSession)
                    {
                        _ = hostSession.DeleteAsync();
                    }
                    else
                    {
                        _ = CurrentSession.LeaveAsync();
                    }
                }
                catch
                {
                    // Best-effort cleanup on process exit.
                }
                CurrentSession = null;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        public async Task<bool> CreateRelayRoomAsync(string playerName)
        {
            if (_isInitializing) return false;
            _isInitializing = true;

            try
            {
                IsOfflineVsAIMode = false;
                LocalPlayerName = SanitizeName(playerName, "Player 1");
                StatusMessageChanged?.Invoke("Menghubungkan ke Unity Services...");

                await EnsureSignedInAsync();

                StatusMessageChanged?.Invoke("Membuat room P2P...");
                await CleanupExistingSessionAsync();

                var options = new SessionOptions
                {
                    Name = $"CoinRush_{UnityEngine.Random.Range(1000, 9999)}",
                    MaxPlayers = maxPlayers,
                    IsPrivate = false,
                    PlayerProperties = BuildPlayerProperties(LocalPlayerName)
                }.WithRelayNetwork();

                CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                SubscribeSessionEvents();

                StatusMessageChanged?.Invoke($"Room dibuat! Kode: {CurrentSession.Code}");
                SessionUpdated?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbySessionManager] CreateRelayRoomAsync failed: {ex}");
                StatusMessageChanged?.Invoke($"Gagal membuat room: {ex.Message}");
                return false;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        public async Task<bool> JoinRelayRoomByCodeAsync(string roomCode, string playerName)
        {
            if (_isInitializing) return false;

            string normalizedCode = string.IsNullOrWhiteSpace(roomCode)
                ? string.Empty
                : roomCode.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(normalizedCode))
            {
                StatusMessageChanged?.Invoke("Masukkan kode room terlebih dahulu.");
                return false;
            }

            _isInitializing = true;
            try
            {
                IsOfflineVsAIMode = false;
                LocalPlayerName = SanitizeName(playerName, "Player 2");
                StatusMessageChanged?.Invoke("Menghubungkan ke Unity Services...");

                await EnsureSignedInAsync();

                StatusMessageChanged?.Invoke($"Bergabung ke room {normalizedCode}...");
                await CleanupExistingSessionAsync();

                var joinOptions = new JoinSessionOptions
                {
                    PlayerProperties = BuildPlayerProperties(LocalPlayerName)
                };

                CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode, joinOptions);
                SubscribeSessionEvents();

                StatusMessageChanged?.Invoke($"Berhasil bergabung ke room {CurrentSession.Code}!");
                SessionUpdated?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbySessionManager] JoinRelayRoomByCodeAsync failed: {ex}");
                StatusMessageChanged?.Invoke($"Gagal bergabung: {ex.Message}");
                return false;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// Direct local/LAN connection helper (used by automated tests or offline LAN setups).
        /// </summary>
        public bool StartDirectHost(string playerName, ushort port = 7777)
        {
            IsOfflineVsAIMode = false;
            LocalPlayerName = SanitizeName(playerName, "Player 1");
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return false;

            if (nm.NetworkConfig.NetworkTransport is UnityTransport utp)
            {
                utp.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            }
            return nm.StartHost();
        }

        public bool StartDirectClient(string playerName, string ip = "127.0.0.1", ushort port = 7777)
        {
            IsOfflineVsAIMode = false;
            LocalPlayerName = SanitizeName(playerName, "Player 2");
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return false;

            if (nm.NetworkConfig.NetworkTransport is UnityTransport utp)
            {
                utp.SetConnectionData(ip, port);
            }
            return nm.StartClient();
        }

        public void StartOfflineVsAI(string playerName)
        {
            IsOfflineVsAIMode = true;
            LocalPlayerName = SanitizeName(playerName, "Player 1");
            _ = CleanupExistingSessionAsync();
            SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
        }

        public bool CanHostStartMatch()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost)
            {
                return false;
            }

            if (CurrentSession != null)
            {
                return CurrentSession.Players.Count >= 2 && nm.ConnectedClientsIds.Count >= 2;
            }

            return nm.ConnectedClientsIds.Count >= 2;
        }

        public bool StartOnlineMatch()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost)
            {
                return false;
            }

            if (CurrentSession is IHostSession hostSession)
            {
                hostSession.IsLocked = true;
                _ = hostSession.SavePropertiesAsync();
            }

            nm.SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
            return true;
        }

        public async Task LeaveRoomAsync()
        {
            await CleanupExistingSessionAsync();
            SessionClosed?.Invoke();
        }

        public async Task ReturnToLobbyAsync()
        {
            await CleanupExistingSessionAsync();
            IsOfflineVsAIMode = false;
            SceneManager.LoadScene(LobbySceneName, LoadSceneMode.Single);
        }

        public List<LobbyPlayerInfo> GetLobbyPlayersInfo()
        {
            var result = new List<LobbyPlayerInfo>();
            if (CurrentSession == null)
            {
                return result;
            }

            var players = CurrentSession.Players;
            for (int i = 0; i < players.Count; i++)
            {
                IReadOnlyPlayer p = players[i];
                bool isHost = p.Id == CurrentSession.Host;
                string name = null;

                if (p.Properties != null && p.Properties.TryGetValue(DisplayNamePropertyKey, out PlayerProperty prop))
                {
                    name = prop.Value;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = isHost ? "Player 1" : $"Player {i + 1}";
                }

                result.Add(new LobbyPlayerInfo(p.Id, name, isHost));
            }

            return result;
        }

        public string GetPlayerDisplayNameBySlot(int slotIndex)
        {
            if (CurrentSession != null)
            {
                var players = GetLobbyPlayersInfo();
                if (slotIndex == 0)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (players[i].IsHost) return players[i].DisplayName;
                    }
                }
                else
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (!players[i].IsHost) return players[i].DisplayName;
                    }
                }
            }

            return slotIndex == 0 ? LocalPlayerName : "Player 2";
        }

        private async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }
            else
            {
                while (UnityServices.State == ServicesInitializationState.Initializing)
                {
                    await Task.Yield();
                }
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private async Task CleanupExistingSessionAsync()
        {
            UnsubscribeSessionEvents();

            if (CurrentSession != null)
            {
                ISession sessionToClose = CurrentSession;
                CurrentSession = null;

                try
                {
                    if (sessionToClose.IsHost && sessionToClose is IHostSession hostSession)
                    {
                        await hostSession.DeleteAsync();
                    }
                    else
                    {
                        await sessionToClose.LeaveAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbySessionManager] Session cleanup note: {ex.Message}");
                }
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        private void SubscribeSessionEvents()
        {
            if (CurrentSession == null) return;

            CurrentSession.Changed += OnSessionChanged;
            CurrentSession.PlayerJoined += OnPlayerJoined;
            CurrentSession.PlayerHasLeft += OnPlayerHasLeft;
            CurrentSession.RemovedFromSession += OnRemovedFromSession;
            CurrentSession.Deleted += OnSessionDeleted;
        }

        private void UnsubscribeSessionEvents()
        {
            if (CurrentSession == null) return;

            CurrentSession.Changed -= OnSessionChanged;
            CurrentSession.PlayerJoined -= OnPlayerJoined;
            CurrentSession.PlayerHasLeft -= OnPlayerHasLeft;
            CurrentSession.RemovedFromSession -= OnRemovedFromSession;
            CurrentSession.Deleted -= OnSessionDeleted;
        }

        private void OnSessionChanged() => SessionUpdated?.Invoke();
        private void OnPlayerJoined(string _) => SessionUpdated?.Invoke();
        private void OnPlayerHasLeft(string _) => SessionUpdated?.Invoke();

        private void OnRemovedFromSession()
        {
            UnsubscribeSessionEvents();
            CurrentSession = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            StatusMessageChanged?.Invoke("Koneksi ke room terputus.");
            SessionClosed?.Invoke();
        }

        private void OnSessionDeleted()
        {
            UnsubscribeSessionEvents();
            CurrentSession = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            StatusMessageChanged?.Invoke("Host menutup room.");
            SessionClosed?.Invoke();
        }

        private static Dictionary<string, PlayerProperty> BuildPlayerProperties(string playerName)
        {
            return new Dictionary<string, PlayerProperty>
            {
                { DisplayNamePropertyKey, new PlayerProperty(playerName, VisibilityPropertyOptions.Member) }
            };
        }

        private static string SanitizeName(string rawName, string fallback)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return fallback;
            }
            string trimmed = rawName.Trim();
            return trimmed.Length > 16 ? trimmed.Substring(0, 16) : trimmed;
        }
    }
}
