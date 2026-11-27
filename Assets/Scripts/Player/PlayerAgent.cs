using UnityEngine;
using CoinRush.Audio;

namespace CoinRush.Player
{
    /// <summary>
    /// Identifies a match participant (Player 1 / Player 2 or AI) and manages spawn resets.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class PlayerAgent : MonoBehaviour
    {
        [SerializeField] private int playerIndex;
        [SerializeField] private string displayName = "Player 1";

        private CharacterMotor _motor;
        private CharacterVisualController _visualController;

        public int PlayerIndex => playerIndex;
        public string DisplayName => displayName;
        public CharacterMotor Motor => _motor;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _visualController = GetComponent<CharacterVisualController>();
        }

        private void Start()
        {
            _visualController?.ApplyIdentity(playerIndex, displayName);
        }

        public void Configure(int index, string agentName)
        {
            playerIndex = index;
            displayName = agentName;
            if (_visualController == null)
            {
                _visualController = GetComponent<CharacterVisualController>();
            }
            _visualController?.ApplyIdentity(playerIndex, displayName);
        }

        public void ResetForMatch(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (_motor == null)
            {
                _motor = GetComponent<CharacterMotor>();
            }
            _motor.SetInputEnabled(false);
            _motor.Teleport(spawnPosition, spawnRotation);
        }

        public void SetControlActive(bool active)
        {
            if (_motor == null)
            {
                _motor = GetComponent<CharacterMotor>();
            }
            _motor.SetInputEnabled(active);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (playerIndex != 0)
            {
                return;
            }

            if (collision.gameObject.GetComponentInParent<PlayerAgent>() != null)
            {
                SynthAudioManager.Instance?.PlayBump();
            }
        }
    }
}
