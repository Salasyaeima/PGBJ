using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using CoinRush.Core;

namespace CoinRush.Gameplay
{
    /// <summary>
    /// Spawns coins inside the fenced arena while keeping a safe distance from actors.
    /// Uses pooling offline and NetworkObject Spawn/Despawn on the Host when online.
    /// </summary>
    public class CoinSpawner : MonoBehaviour
    {
        [SerializeField] private Coin coinPrefab;
        [SerializeField] private int maxActiveCoins = 6;
        [SerializeField] private float spawnInterval = 1.25f;
        [SerializeField] private Vector2 arenaHalfExtents = new Vector2(6.5f, 3.8f);
        [SerializeField] private float spawnHeight = 0.55f;
        [SerializeField] private float minDistanceFromActors = 1.6f;
        [SerializeField] private float minDistanceBetweenCoins = 1.2f;

        private readonly List<Coin> _activeCoins = new List<Coin>();
        private readonly Queue<Coin> _pool = new Queue<Coin>();
        private readonly List<Transform> _trackedActors = new List<Transform>();

        private GameManager _gameManager;
        private bool _isSpawning;
        private float _spawnTimer;

        public IReadOnlyList<Coin> ActiveCoins => _activeCoins;

        private static bool IsNetworkListening =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public void Configure(
            Coin prefab,
            GameManager gameManager,
            Transform actor1,
            Transform actor2,
            Vector2 halfExtents)
        {
            coinPrefab = prefab;
            _gameManager = gameManager;
            arenaHalfExtents = halfExtents;
            SetTrackedActors(actor1, actor2);
        }

        public void SetTrackedActors(Transform actor1, Transform actor2)
        {
            _trackedActors.Clear();
            if (actor1 != null) _trackedActors.Add(actor1);
            if (actor2 != null) _trackedActors.Add(actor2);
        }

        public void BeginSpawning(int initialCount)
        {
            if (IsNetworkListening && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            _isSpawning = true;
            _spawnTimer = 0f;

            int toSpawn = Mathf.Min(initialCount, maxActiveCoins);
            for (int i = 0; i < toSpawn; i++)
            {
                TrySpawnCoin();
            }
        }

        public void StopSpawning()
        {
            _isSpawning = false;
        }

        public void ClearAllCoins()
        {
            bool online = IsNetworkListening;
            for (int i = _activeCoins.Count - 1; i >= 0; i--)
            {
                Coin coin = _activeCoins[i];
                if (coin == null) continue;

                if (online)
                {
                    if (NetworkManager.Singleton.IsServer)
                    {
                        var netObj = coin.GetComponent<NetworkObject>();
                        if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
                        else Destroy(coin.gameObject);
                    }
                }
                else
                {
                    coin.gameObject.SetActive(false);
                    _pool.Enqueue(coin);
                }
            }
            _activeCoins.Clear();
        }

        public void ReturnCoin(Coin coin)
        {
            if (coin == null)
            {
                return;
            }

            _activeCoins.Remove(coin);

            if (IsNetworkListening)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    var netObj = coin.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
                    else Destroy(coin.gameObject);
                }
                return;
            }

            coin.gameObject.SetActive(false);
            _pool.Enqueue(coin);
        }

        private void Update()
        {
            if (!_isSpawning || coinPrefab == null)
            {
                return;
            }

            if (IsNetworkListening && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (_activeCoins.Count >= maxActiveCoins)
            {
                _spawnTimer = 0f;
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval)
            {
                _spawnTimer = 0f;
                TrySpawnCoin();
            }
        }

        private bool TrySpawnCoin()
        {
            if (coinPrefab == null || _activeCoins.Count >= maxActiveCoins)
            {
                return false;
            }

            Vector3 spawnPos = FindSafeSpawnPosition();

            if (IsNetworkListening)
            {
                Coin netCoin = Instantiate(coinPrefab, spawnPos, Quaternion.identity);
                var netObj = netCoin.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }
                netCoin.Initialize(this, null);
                _activeCoins.Add(netCoin);
                return true;
            }

            Coin coin = GetOrCreateCoin();
            coin.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);
            coin.gameObject.SetActive(true);
            coin.Initialize(this, _gameManager);
            _activeCoins.Add(coin);
            return true;
        }

        private Coin GetOrCreateCoin()
        {
            while (_pool.Count > 0)
            {
                Coin pooled = _pool.Dequeue();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            return Instantiate(coinPrefab, transform);
        }

        private Vector3 FindSafeSpawnPosition()
        {
            const int maxAttempts = 20;
            Vector3 fallback = Vector3.zero;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float x = Random.Range(-arenaHalfExtents.x, arenaHalfExtents.x);
                float z = Random.Range(-arenaHalfExtents.y, arenaHalfExtents.y);
                Vector3 candidate = new Vector3(x, spawnHeight, z);

                if (attempt == 0)
                {
                    fallback = candidate;
                }

                if (IsPositionClear(candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private bool IsPositionClear(Vector3 candidate)
        {
            float actorMinSqr = minDistanceFromActors * minDistanceFromActors;
            for (int i = 0; i < _trackedActors.Count; i++)
            {
                Transform actor = _trackedActors[i];
                if (actor == null) continue;

                Vector3 delta = actor.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < actorMinSqr)
                {
                    return false;
                }
            }

            float coinMinSqr = minDistanceBetweenCoins * minDistanceBetweenCoins;
            for (int i = 0; i < _activeCoins.Count; i++)
            {
                Coin active = _activeCoins[i];
                if (active == null || !active.gameObject.activeInHierarchy) continue;

                Vector3 delta = active.transform.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < coinMinSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
