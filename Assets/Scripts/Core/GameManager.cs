using System.Collections;
using Unity.Netcode;
using UnityEngine;
using CoinRush.Audio;
using CoinRush.Gameplay;
using CoinRush.Player;
using CoinRush.UI;

namespace CoinRush.Core
{
    public enum MatchState
    {
        Countdown,
        Playing,
        GameOver
    }

    /// <summary>
    /// Coordinates match lifecycle, round timer, coin scores, and winner evaluation.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Match Settings")]
        [SerializeField] private float roundDuration = 60f;
        [SerializeField] private int countdownSeconds = 3;
        [SerializeField] private int initialCoinCount = 3;

        [Header("Scene References")]
        [SerializeField] private PlayerAgent player1;
        [SerializeField] private PlayerAgent player2;
        [SerializeField] private CoinSpawner coinSpawner;
        [SerializeField] private GameHUD hud;
        [SerializeField] private ParticleSystem coinPickupEffectPrefab;
        [SerializeField] private SynthAudioManager audioManager;

        [Header("Spawn Points")]
        [SerializeField] private Vector3 player1SpawnPos = new Vector3(-4.5f, 0.55f, 0f);
        [SerializeField] private Vector3 player2SpawnPos = new Vector3(4.5f, 0.55f, 0f);

        private MatchState _state = MatchState.Countdown;
        private int _player1Score;
        private int _player2Score;
        private float _remainingTime;
        private Coroutine _matchFlowRoutine;

        public MatchState State => _state;
        public int Player1Score => _player1Score;
        public int Player2Score => _player2Score;
        public float RemainingTime => _remainingTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                // Online P2P mode is driven by NetworkMatchController; hide offline actors.
                if (player1 != null) player1.gameObject.SetActive(false);
                if (player2 != null) player2.gameObject.SetActive(false);
                return;
            }

            if (coinSpawner != null)
            {
                coinSpawner.SetTrackedActors(
                    player1 != null ? player1.transform : null,
                    player2 != null ? player2.transform : null
                );
            }

            if (hud != null)
            {
                hud.Bind(this);
            }

            if (audioManager == null)
            {
                audioManager = FindAnyObjectByType<SynthAudioManager>();
            }

            StartMatch();
        }

        public void Configure(
            PlayerAgent p1,
            PlayerAgent p2,
            CoinSpawner spawner,
            GameHUD gameHud,
            ParticleSystem pickupVfx = null)
        {
            player1 = p1;
            player2 = p2;
            coinSpawner = spawner;
            hud = gameHud;
            coinPickupEffectPrefab = pickupVfx;
        }

        public void StartMatch()
        {
            if (_matchFlowRoutine != null)
            {
                StopCoroutine(_matchFlowRoutine);
            }

            _player1Score = 0;
            _player2Score = 0;
            _remainingTime = roundDuration;
            _state = MatchState.Countdown;

            if (coinSpawner != null)
            {
                coinSpawner.StopSpawning();
                coinSpawner.ClearAllCoins();
            }

            if (player1 != null)
            {
                player1.ResetForMatch(player1SpawnPos, Quaternion.LookRotation(Vector3.right, Vector3.up));
            }

            if (player2 != null)
            {
                player2.ResetForMatch(player2SpawnPos, Quaternion.LookRotation(Vector3.left, Vector3.up));
            }

            if (hud != null)
            {
                hud.UpdateScores(_player1Score, _player2Score, GetName(player1, "Player 1"), GetName(player2, "AI Bot"));
                hud.UpdateTimer(_remainingTime);
                hud.HideResult();
            }

            _matchFlowRoutine = StartCoroutine(RunMatchRoutine());
        }

        public void RestartMatch()
        {
            StartMatch();
        }

        public void NotifyCoinCollected(int playerIndex, int amount, Vector3 worldPosition)
        {
            if (_state != MatchState.Playing)
            {
                return;
            }

            if (playerIndex == 0)
            {
                _player1Score += amount;
            }
            else
            {
                _player2Score += amount;
            }

            if (coinPickupEffectPrefab != null)
            {
                ParticleSystem burst = Instantiate(coinPickupEffectPrefab, worldPosition, Quaternion.identity);
                burst.Play();
                Destroy(burst.gameObject, 1.5f);
            }

            GetAudio()?.PlayCoinPickup(playerIndex == 0);

            if (hud != null)
            {
                hud.UpdateScores(_player1Score, _player2Score, GetName(player1, "Player 1"), GetName(player2, "AI Bot"));
            }
        }

        private IEnumerator RunMatchRoutine()
        {
            for (int sec = countdownSeconds; sec > 0; sec--)
            {
                if (hud != null)
                {
                    hud.ShowCountdown(sec.ToString());
                }
                GetAudio()?.PlayCountdownTick();
                yield return new WaitForSeconds(1f);
            }

            if (hud != null)
            {
                hud.ShowCountdown("GO!");
            }
            GetAudio()?.PlayMatchGo();

            _state = MatchState.Playing;
            SetPlayersActive(true);

            if (coinSpawner != null)
            {
                coinSpawner.BeginSpawning(initialCoinCount);
            }

            float goBannerTimer = 0.6f;
            int lastUrgentSecond = -1;
            while (_remainingTime > 0f)
            {
                float dt = Time.deltaTime;
                _remainingTime = Mathf.Max(0f, _remainingTime - dt);

                if (_remainingTime > 0f && _remainingTime <= 10f)
                {
                    int ceilSec = Mathf.CeilToInt(_remainingTime);
                    if (ceilSec != lastUrgentSecond)
                    {
                        lastUrgentSecond = ceilSec;
                        GetAudio()?.PlayUrgentTick();
                    }
                }

                if (goBannerTimer > 0f)
                {
                    goBannerTimer -= dt;
                    if (goBannerTimer <= 0f && hud != null)
                    {
                        hud.HideCountdown();
                    }
                }

                if (hud != null)
                {
                    hud.UpdateTimer(_remainingTime);
                }

                yield return null;
            }

            EndMatch();
        }

        private void EndMatch()
        {
            _state = MatchState.GameOver;
            SetPlayersActive(false);

            if (coinSpawner != null)
            {
                coinSpawner.StopSpawning();
            }

            GetAudio()?.PlayMatchEnd(_player1Score, _player2Score);

            string p1Name = GetName(player1, "Player 1");
            string p2Name = GetName(player2, "AI Bot");
            string headline;

            if (_player1Score > _player2Score)
            {
                headline = $"{p1Name.ToUpperInvariant()} MENANG!";
            }
            else if (_player2Score > _player1Score)
            {
                headline = $"{p2Name.ToUpperInvariant()} MENANG!";
            }
            else
            {
                headline = "SERI!";
            }

            if (hud != null)
            {
                hud.HideCountdown();
                hud.ShowResult(headline, p1Name, _player1Score, p2Name, _player2Score);
            }
        }

        private void SetPlayersActive(bool active)
        {
            if (player1 != null) player1.SetControlActive(active);
            if (player2 != null) player2.SetControlActive(active);
        }

        private SynthAudioManager GetAudio()
        {
            if (audioManager == null)
            {
                audioManager = SynthAudioManager.Instance;
            }
            return audioManager;
        }

        private static string GetName(PlayerAgent agent, string fallback)
        {
            return agent != null && !string.IsNullOrEmpty(agent.DisplayName) ? agent.DisplayName : fallback;
        }
    }
}
