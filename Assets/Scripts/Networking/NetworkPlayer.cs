using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CoinRush.Audio;
using CoinRush.Player;

namespace CoinRush.Networking
{
    /// <summary>
    /// Networked player wrapper for NGO. Only the owning client reads WASD input;
    /// movement replicates via owner-authoritative NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(CharacterMotor), typeof(PlayerAgent))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private Material player1Material;
        [SerializeField] private Material player2Material;

        public readonly NetworkVariable<int> PlayerSlot = new NetworkVariable<int>(0);
        public readonly NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>("Player");
        public readonly NetworkVariable<bool> ControlsEnabled = new NetworkVariable<bool>(false);

        private CharacterMotor _motor;
        private PlayerInputProvider _inputProvider;
        private PlayerAgent _agent;
        private CharacterVisualController _visualController;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _inputProvider = GetComponent<PlayerInputProvider>();
            _agent = GetComponent<PlayerAgent>();
            _visualController = GetComponent<CharacterVisualController>();
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<MeshRenderer>();
            }
        }

        public override void OnNetworkSpawn()
        {
            PlayerSlot.OnValueChanged += OnSlotChanged;
            PlayerName.OnValueChanged += OnNameChanged;
            ControlsEnabled.OnValueChanged += OnControlsEnabledChanged;

            if (_inputProvider != null)
            {
                _inputProvider.enabled = IsOwner;
            }

            ApplyIdentity();
            ApplyControlState(ControlsEnabled.Value);
        }

        public override void OnNetworkDespawn()
        {
            PlayerSlot.OnValueChanged -= OnSlotChanged;
            PlayerName.OnValueChanged -= OnNameChanged;
            ControlsEnabled.OnValueChanged -= OnControlsEnabledChanged;
        }

        public void ServerConfigure(int slotIndex, string playerName)
        {
            if (!IsServer) return;
            PlayerSlot.Value = slotIndex;
            PlayerName.Value = playerName;
        }

        public void ServerSetControlsEnabled(bool enabled)
        {
            if (!IsServer) return;
            ControlsEnabled.Value = enabled;
        }

        [Rpc(SendTo.Owner)]
        public void TeleportOwnerRpc(Vector3 position, Quaternion rotation)
        {
            if (_motor == null) _motor = GetComponent<CharacterMotor>();
            _motor.SetInputEnabled(false);
            _motor.Teleport(position, rotation);
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void PlayBumpFxRpc()
        {
            SynthAudioManager.Instance?.PlayBump();
        }

        private void OnSlotChanged(int _, int __) => ApplyIdentity();
        private void OnNameChanged(FixedString64Bytes _, FixedString64Bytes __) => ApplyIdentity();

        private void OnControlsEnabledChanged(bool _, bool current)
        {
            ApplyControlState(current);
        }

        private void ApplyIdentity()
        {
            int slot = PlayerSlot.Value;
            string pName = PlayerName.Value.ToString();
            if (string.IsNullOrEmpty(pName))
            {
                pName = slot == 0 ? "Player 1" : "Player 2";
            }

            if (_agent != null)
            {
                _agent.Configure(slot, pName);
            }

            if (_visualController == null)
            {
                _visualController = GetComponent<CharacterVisualController>();
            }
            if (_visualController != null)
            {
                _visualController.ApplyIdentity(slot, pName);
            }
            else if (bodyRenderer != null)
            {
                Material targetMat = slot == 0 ? player1Material : player2Material;
                if (targetMat != null)
                {
                    bodyRenderer.sharedMaterial = targetMat;
                }
            }
        }

        private void ApplyControlState(bool enabled)
        {
            if (_motor == null) _motor = GetComponent<CharacterMotor>();
            _motor.SetInputEnabled(IsOwner && enabled);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsSpawned || !IsServer || PlayerSlot.Value != 0)
            {
                return;
            }

            if (collision.gameObject.GetComponentInParent<PlayerAgent>() != null)
            {
                PlayBumpFxRpc();
            }
        }
    }
}
