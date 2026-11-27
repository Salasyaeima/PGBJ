using Unity.Netcode;
using UnityEngine;
using CoinRush.Core;
using CoinRush.Networking;
using CoinRush.Player;

namespace CoinRush.Gameplay
{
    /// <summary>
    /// Collectible coin with spin/bob visual animation and single-pickup trigger guard.
    /// Supports both offline matches and host-authoritative NGO multiplayer.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Coin : MonoBehaviour
    {
        [SerializeField] private int value = 1;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float spinSpeed = 140f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 3.2f;

        private CoinSpawner _spawner;
        private GameManager _gameManager;
        private bool _collected;
        private Vector3 _visualBaseLocalPos;
        private float _bobPhase;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }

            if (visualRoot != null)
            {
                _visualBaseLocalPos = visualRoot.localPosition;
            }
        }

        public void Initialize(CoinSpawner spawner, GameManager gameManager)
        {
            _spawner = spawner;
            _gameManager = gameManager;
            _collected = false;
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);

            if (visualRoot != null)
            {
                visualRoot.localPosition = _visualBaseLocalPos;
            }
        }

        private void Update()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            float yOffset = Mathf.Sin(Time.time * bobFrequency + _bobPhase) * bobAmplitude;
            visualRoot.localPosition = _visualBaseLocalPos + new Vector3(0f, yOffset, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected)
            {
                return;
            }

            bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isOnline && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            PlayerAgent agent = other.GetComponentInParent<PlayerAgent>();
            if (agent == null)
            {
                return;
            }

            _collected = true;

            if (isOnline)
            {
                NetworkMatchController.Instance?.ServerNotifyCoinCollected(agent.PlayerIndex, value, transform.position);
            }
            else
            {
                GameManager gm = _gameManager != null ? _gameManager : GameManager.Instance;
                gm?.NotifyCoinCollected(agent.PlayerIndex, value, transform.position);
            }

            if (_spawner != null)
            {
                _spawner.ReturnCoin(this);
            }
            else if (isOnline)
            {
                var netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
