using UnityEngine;
using CoinRush.Gameplay;

namespace CoinRush.Player
{
    /// <summary>
    /// Simple testing AI that steers toward the nearest active coin in the arena.
    /// </summary>
    public class AIInputProvider : MonoBehaviour, IInputProvider
    {
        [SerializeField] private CoinSpawner coinSpawner;
        [SerializeField] private float decisionInterval = 0.18f;
        [SerializeField, Range(0.5f, 1.1f)] private float speedScale = 0.95f;
        [SerializeField] private float centerReturnRadius = 0.6f;

        private Coin _targetCoin;
        private float _nextDecisionTime;
        private Vector3 _lastCheckPosition;
        private float _stuckTimer;
        private float _sidestepTimer;
        private float _sidestepSign = 1f;

        private void Awake()
        {
            if (coinSpawner == null)
            {
                coinSpawner = FindAnyObjectByType<CoinSpawner>();
            }
            _lastCheckPosition = transform.position;
        }

        public void SetCoinSpawner(CoinSpawner spawner)
        {
            coinSpawner = spawner;
        }

        public Vector2 GetMoveInput()
        {
            if (coinSpawner == null)
            {
                return Vector2.zero;
            }

            if (_targetCoin == null || !_targetCoin.gameObject.activeInHierarchy || Time.time >= _nextDecisionTime)
            {
                _targetCoin = FindNearestCoin();
                _nextDecisionTime = Time.time + decisionInterval;
            }

            Vector3 targetPos = _targetCoin != null ? _targetCoin.transform.position : Vector3.zero;
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;

            if (_targetCoin == null && toTarget.magnitude <= centerReturnRadius)
            {
                return Vector2.zero;
            }

            if (toTarget.sqrMagnitude < 0.01f)
            {
                return Vector2.zero;
            }

            UpdateStuckSidestep();

            Vector3 dir = toTarget.normalized;
            if (_sidestepTimer > 0f)
            {
                _sidestepTimer -= Time.deltaTime;
                Vector3 perp = new Vector3(-dir.z, 0f, dir.x) * _sidestepSign;
                dir = (dir * 0.45f + perp * 0.85f).normalized;
            }

            return new Vector2(dir.x, dir.z) * speedScale;
        }

        private void UpdateStuckSidestep()
        {
            float movedDist = Vector3.Distance(transform.position, _lastCheckPosition);
            if (movedDist < 0.04f)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= 0.45f)
                {
                    _sidestepTimer = 0.35f;
                    _sidestepSign = -_sidestepSign;
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }

            _lastCheckPosition = transform.position;
        }

        private Coin FindNearestCoin()
        {
            var coins = coinSpawner.ActiveCoins;
            Coin nearest = null;
            float bestSqrDist = float.MaxValue;
            Vector3 selfPos = transform.position;

            for (int i = 0; i < coins.Count; i++)
            {
                Coin candidate = coins[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 delta = candidate.transform.position - selfPos;
                delta.y = 0f;
                float sqrDist = delta.sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
