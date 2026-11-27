using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoinRush.Audio;
using CoinRush.Core;
using CoinRush.Gameplay;
using CoinRush.UI;

namespace CoinRush.Networking
{
    /// <summary>
    /// Host-authoritative NGO match coordinator for online P2P gameplay in CoinRush.
    /// Replicates scores, server-time deadline, countdown, and match outcome to both peers.
    /// </summary>
    public class NetworkMatchController : NetworkBehaviour
    {
        public static NetworkMatchController Instance { get; private set; }

        [Header("Match Settings")]
        [SerializeField] private float roundDuration = 60f;
        [SerializeField] private int countdownSeconds = 3;
        [SerializeField] private int initialCoinCount = 3;

        [Header("References")]
        [SerializeField] private NetworkPlayer networkPlayerPrefab;
        [SerializeField] private CoinSpawner coinSpawner;
        [SerializeField] private GameHUD hud;
        [SerializeField] private ParticleSystem coinPickupEffectPrefab;
        [SerializeField] private SynthAudioManager audioManager;

        [Header("Spawn Points")]
        [SerializeField] private Vector3 player1SpawnPos = new Vector3(-4.5f, 0.55f, 0f);
        [SerializeField] private Vector3 player2SpawnPos = new Vector3(4.5f, 0.55f, 0f);

        public readonly NetworkVariable<int> P1Score = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> P2Score = new NetworkVariable<int>(0);
        public readonly NetworkVariable<double> MatchEndServerTime = new NetworkVariable<double>(0d);
        public readonly NetworkVariable<int> MatchStateValue = new NetworkVariable<int>((int)MatchState.Countdown);
        public readonly NetworkVariable<FixedString32Bytes> CountdownBanner = new NetworkVariable<FixedString32Bytes>("3");
        public readonly NetworkVariable<bool> ShowCountdownBanner = new NetworkVariable<bool>(true);
        public readonly NetworkVariable<FixedString64Bytes> P1Name = new NetworkVariable<FixedString64Bytes>("Player 1");
        public readonly NetworkVariable<FixedString64Bytes> P2Name = new NetworkVariable<FixedString64Bytes>("Player 2");

        public float RemainingTime
        {
            get
            {
                if (MatchStateValue.Value == (int)MatchState.Countdown)
                {
                    return roundDuration;
                }
                if (MatchStateValue.Value == (int)MatchState.GameOver || NetworkManager == null)
                {
                    return 0f;
                }
                return Mathf.Max(0f, (float)(MatchEndServerTime.Value - NetworkManager.ServerTime.Time));
            }
        }

        private readonly List<NetworkPlayer> _spawnedPlayers = new List<NetworkPlayer>();
        private Coroutine _matchRoutine;
        private bool _matchStarted;

        public void Configure(
            NetworkPlayer playerPrefab,
            CoinSpawner spawner,
            GameHUD gameHud,
            ParticleSystem pickupVfx,
            SynthAudioManager synth)
        {
            networkPlayerPrefab = playerPrefab;
            coinSpawner = spawner;
            hud = gameHud;
            coinPickupEffectPrefab = pickupVfx;
            audioManager = synth;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (!IsSpawned || hud == null)
            {
                return;
            }

            if (MatchStateValue.Value == (int)MatchState.Playing)
            {
                hud.UpdateTimer(RemainingTime);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (hud == null) hud = FindAnyObjectByType<GameHUD>();
            if (coinSpawner == null) coinSpawner = FindAnyObjectByType<CoinSpawner>();
            if (audioManager == null) audioManager = FindAnyObjectByType<SynthAudioManager>();

            P1Score.OnValueChanged += OnScoreChanged;
            P2Score.OnValueChanged += OnScoreChanged;
            P1Name.OnValueChanged += OnNameChanged;
            P2Name.OnValueChanged += OnNameChanged;
            MatchEndServerTime.OnValueChanged += OnMatchEndTimeChanged;
            MatchStateValue.OnValueChanged += OnMatchStateChanged;
            CountdownBanner.OnValueChanged += OnCountdownChanged;
            ShowCountdownBanner.OnValueChanged += OnShowCountdownChanged;

            if (hud != null)
            {
                hud.BindNetworkController(this);
            }

            RefreshAllHud();

            if (IsServer)
            {
                if (NetworkManager.SceneManager != null)
                {
                    NetworkManager.SceneManager.OnLoadEventCompleted += HandleSceneLoadCompleted;
                }
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;

                // Wait briefly for OnLoadEventCompleted so remote clients finish scene sync before spawning players.
                StartCoroutine(WaitForSceneSyncFallbackRoutine());
            }
            else if (NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            P1Score.OnValueChanged -= OnScoreChanged;
            P2Score.OnValueChanged -= OnScoreChanged;
            P1Name.OnValueChanged -= OnNameChanged;
            P2Name.OnValueChanged -= OnNameChanged;
            MatchEndServerTime.OnValueChanged -= OnMatchEndTimeChanged;
            MatchStateValue.OnValueChanged -= OnMatchStateChanged;
            CountdownBanner.OnValueChanged -= OnCountdownChanged;
            ShowCountdownBanner.OnValueChanged -= OnShowCountdownChanged;

            if (NetworkManager != null)
            {
                if (IsServer && NetworkManager.SceneManager != null)
                {
                    NetworkManager.SceneManager.OnLoadEventCompleted -= HandleSceneLoadCompleted;
                }
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RequestRestart()
        {
            if (!IsSpawned) return;
            RequestRestartRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestRestartRpc()
        {
            if (!IsServer) return;
            StartHostedMatchRoutine();
        }

        public void ServerNotifyCoinCollected(int playerIndex, int amount, Vector3 worldPosition)
        {
            if (!IsServer || MatchStateValue.Value != (int)MatchState.Playing)
            {
                return;
            }

            if (playerIndex == 0)
            {
                P1Score.Value += amount;
            }
            else
            {
                P2Score.Value += amount;
            }

            PlayCoinPickupFxRpc(playerIndex, worldPosition);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayCoinPickupFxRpc(int collectorSlot, Vector3 worldPosition)
        {
            if (coinPickupEffectPrefab != null)
            {
                ParticleSystem burst = Instantiate(coinPickupEffectPrefab, worldPosition, Quaternion.identity);
                burst.Play();
                Destroy(burst.gameObject, 1.5f);
            }

            int localSlot = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost) ? 0 : 1;
            GetAudio()?.PlayCoinPickup(collectorSlot == localSlot);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayCountdownFxRpc(bool isGo)
        {
            if (isGo) GetAudio()?.PlayMatchGo();
            else GetAudio()?.PlayCountdownTick();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayUrgentTickRpc()
        {
            GetAudio()?.PlayUrgentTick();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayMatchEndFxRpc(int s1, int s2)
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            int myScore = isHost ? s1 : s2;
            int otherScore = isHost ? s2 : s1;
            GetAudio()?.PlayMatchEnd(myScore, otherScore);
        }

        private void HandleSceneLoadCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!IsServer || sceneName != LobbySessionManager.GameplaySceneName)
            {
                return;
            }

            TryBeginHostedMatch();
        }

        private IEnumerator WaitForSceneSyncFallbackRoutine()
        {
            // Give NGO SceneManager time to complete LoadEventCompleted across all peers.
            float waitTime = (NetworkManager != null && NetworkManager.ConnectedClientsIds.Count <= 1) ? 0.1f : 3.5f;
            yield return new WaitForSeconds(waitTime);
            TryBeginHostedMatch();
        }

        private void TryBeginHostedMatch()
        {
            if (!IsServer || _matchStarted) return;
            _matchStarted = true;

            SpawnPlayersForConnectedClients();
            StartHostedMatchRoutine();
        }

        private void SpawnPlayersForConnectedClients()
        {
            if (!IsServer || networkPlayerPrefab == null) return;

            for (int i = _spawnedPlayers.Count - 1; i >= 0; i--)
            {
                if (_spawnedPlayers[i] != null && _spawnedPlayers[i].IsSpawned)
                {
                    _spawnedPlayers[i].NetworkObject.Despawn(true);
                }
            }
            _spawnedPlayers.Clear();

            var clients = NetworkManager.ConnectedClientsIds;
            Transform actor1 = null;
            Transform actor2 = null;

            string p1NameStr = LobbySessionManager.Instance != null
                ? LobbySessionManager.Instance.GetPlayerDisplayNameBySlot(0)
                : "Player 1";
            string p2NameStr = LobbySessionManager.Instance != null
                ? LobbySessionManager.Instance.GetPlayerDisplayNameBySlot(1)
                : "Player 2";

            P1Name.Value = p1NameStr;
            P2Name.Value = p2NameStr;

            int slotCounter = 0;
            for (int i = 0; i < clients.Count; i++)
            {
                ulong clientId = clients[i];
                int slot = (clientId == NetworkManager.ServerClientId) ? 0 : Mathf.Max(1, slotCounter);
                if (clientId != NetworkManager.ServerClientId)
                {
                    slotCounter++;
                }
                else
                {
                    slotCounter = Mathf.Max(slotCounter, 1);
                }

                Vector3 spawnPos = slot == 0 ? player1SpawnPos : player2SpawnPos;
                Quaternion spawnRot = Quaternion.LookRotation(slot == 0 ? Vector3.right : Vector3.left, Vector3.up);

                NetworkPlayer np = Instantiate(networkPlayerPrefab, spawnPos, spawnRot);
                np.NetworkObject.SpawnAsPlayerObject(clientId, true);
                np.ServerConfigure(slot, slot == 0 ? p1NameStr : p2NameStr);
                np.ServerSetControlsEnabled(false);

                _spawnedPlayers.Add(np);
                if (slot == 0) actor1 = np.transform;
                else actor2 = np.transform;
            }

            if (coinSpawner != null)
            {
                coinSpawner.SetTrackedActors(actor1, actor2);
            }
        }

        private void StartHostedMatchRoutine()
        {
            if (!IsServer) return;
            if (_matchRoutine != null) StopCoroutine(_matchRoutine);
            _matchRoutine = StartCoroutine(RunHostedMatchRoutine());
        }

        private IEnumerator RunHostedMatchRoutine()
        {
            P1Score.Value = 0;
            P2Score.Value = 0;
            MatchStateValue.Value = (int)MatchState.Countdown;

            if (coinSpawner != null)
            {
                coinSpawner.StopSpawning();
                coinSpawner.ClearAllCoins();
            }

            for (int i = 0; i < _spawnedPlayers.Count; i++)
            {
                NetworkPlayer np = _spawnedPlayers[i];
                if (np == null) continue;
                np.ServerSetControlsEnabled(false);
                bool isSlot0 = np.PlayerSlot.Value == 0;
                Vector3 pos = isSlot0 ? player1SpawnPos : player2SpawnPos;
                Quaternion rot = Quaternion.LookRotation(isSlot0 ? Vector3.right : Vector3.left, Vector3.up);
                np.TeleportOwnerRpc(pos, rot);
            }

            ShowCountdownBanner.Value = true;
            for (int sec = countdownSeconds; sec > 0; sec--)
            {
                CountdownBanner.Value = sec.ToString();
                PlayCountdownFxRpc(false);
                yield return new WaitForSeconds(1f);
            }

            CountdownBanner.Value = "GO!";
            PlayCountdownFxRpc(true);

            MatchEndServerTime.Value = NetworkManager.ServerTime.Time + roundDuration;
            MatchStateValue.Value = (int)MatchState.Playing;
            for (int i = 0; i < _spawnedPlayers.Count; i++)
            {
                _spawnedPlayers[i]?.ServerSetControlsEnabled(true);
            }

            if (coinSpawner != null)
            {
                coinSpawner.BeginSpawning(initialCoinCount);
            }

            float goBannerTimer = 0.6f;
            int lastUrgentSecond = -1;
            while (RemainingTime > 0f)
            {
                float rem = RemainingTime;
                if (rem > 0f && rem <= 10f)
                {
                    int ceilSec = Mathf.CeilToInt(rem);
                    if (ceilSec != lastUrgentSecond)
                    {
                        lastUrgentSecond = ceilSec;
                        PlayUrgentTickRpc();
                    }
                }

                if (goBannerTimer > 0f)
                {
                    goBannerTimer -= Time.deltaTime;
                    if (goBannerTimer <= 0f)
                    {
                        ShowCountdownBanner.Value = false;
                    }
                }

                yield return null;
            }

            MatchStateValue.Value = (int)MatchState.GameOver;
            for (int i = 0; i < _spawnedPlayers.Count; i++)
            {
                _spawnedPlayers[i]?.ServerSetControlsEnabled(false);
            }

            if (coinSpawner != null)
            {
                coinSpawner.StopSpawning();
            }

            PlayMatchEndFxRpc(P1Score.Value, P2Score.Value);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (IsServer)
            {
                if (clientId == NetworkManager.ServerClientId || MatchStateValue.Value == (int)MatchState.GameOver)
                {
                    return;
                }

                if (_matchRoutine != null)
                {
                    StopCoroutine(_matchRoutine);
                }

                MatchStateValue.Value = (int)MatchState.GameOver;
                for (int i = 0; i < _spawnedPlayers.Count; i++)
                {
                    _spawnedPlayers[i]?.ServerSetControlsEnabled(false);
                }
                coinSpawner?.StopSpawning();
                PlayMatchEndFxRpc(P1Score.Value, P2Score.Value);
                return;
            }

            // Client lost connection to Host: avoid soft-locking in gameplay scene.
            if (NetworkManager == null || clientId == NetworkManager.ServerClientId || clientId == NetworkManager.LocalClientId)
            {
                hud?.HideCountdown();
                hud?.ShowDisconnectResult(
                    "KONEKSI TERPUTUS",
                    "Host meninggalkan permainan. Kembali ke lobi untuk bermain lagi."
                );
            }
        }

        private void OnScoreChanged(int _, int __) => UpdateHudScores();
        private void OnNameChanged(FixedString64Bytes _, FixedString64Bytes __) => UpdateHudScores();
        private void OnMatchEndTimeChanged(double _, double __) => hud?.UpdateTimer(RemainingTime);

        private void OnCountdownChanged(FixedString32Bytes _, FixedString32Bytes current)
        {
            if (ShowCountdownBanner.Value)
            {
                hud?.ShowCountdown(current.ToString());
            }
        }

        private void OnShowCountdownChanged(bool _, bool show)
        {
            if (show) hud?.ShowCountdown(CountdownBanner.Value.ToString());
            else hud?.HideCountdown();
        }

        private void OnMatchStateChanged(int _, int current)
        {
            ApplyMatchStateHud((MatchState)current);
        }

        private void RefreshAllHud()
        {
            UpdateHudScores();
            hud?.UpdateTimer(RemainingTime);
            if (ShowCountdownBanner.Value) hud?.ShowCountdown(CountdownBanner.Value.ToString());
            else hud?.HideCountdown();
            ApplyMatchStateHud((MatchState)MatchStateValue.Value);
        }

        private void UpdateHudScores()
        {
            hud?.UpdateScores(P1Score.Value, P2Score.Value, P1Name.Value.ToString(), P2Name.Value.ToString());
        }

        private void ApplyMatchStateHud(MatchState state)
        {
            if (hud == null) return;

            if (state == MatchState.GameOver)
            {
                string n1 = P1Name.Value.ToString();
                string n2 = P2Name.Value.ToString();
                int s1 = P1Score.Value;
                int s2 = P2Score.Value;
                string headline = s1 > s2
                    ? $"{n1.ToUpperInvariant()} MENANG!"
                    : (s2 > s1 ? $"{n2.ToUpperInvariant()} MENANG!" : "SERI!");
                hud.HideCountdown();
                hud.ShowResult(headline, n1, s1, n2, s2);
            }
            else
            {
                hud.HideResult();
            }
        }

        private SynthAudioManager GetAudio()
        {
            if (audioManager == null) audioManager = SynthAudioManager.Instance;
            return audioManager;
        }
    }
}
