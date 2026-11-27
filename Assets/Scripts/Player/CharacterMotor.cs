using UnityEngine;

namespace CoinRush.Player
{
    /// <summary>
    /// Drives top-down XZ movement and facing rotation on a Rigidbody from any IInputProvider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float rotationSpeed = 14f;

        private Rigidbody _rb;
        private IInputProvider _inputProvider;
        private bool _inputEnabled;
        private Vector2 _currentMoveInput;

        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.constraints = RigidbodyConstraints.FreezePositionY
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationZ;

            _inputProvider = GetComponent<IInputProvider>();
        }

        private void Update()
        {
            if (!_inputEnabled || _inputProvider == null)
            {
                _currentMoveInput = Vector2.zero;
                return;
            }

            _currentMoveInput = Vector2.ClampMagnitude(_inputProvider.GetMoveInput(), 1f);
        }

        private void FixedUpdate()
        {
            if (!_inputEnabled)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 velocity = new Vector3(_currentMoveInput.x, 0f, _currentMoveInput.y) * moveSpeed;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = Vector3.zero;

            if (_currentMoveInput.sqrMagnitude > 0.001f)
            {
                Vector3 forward = new Vector3(_currentMoveInput.x, 0f, _currentMoveInput.y);
                Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
                Quaternion smoothed = Quaternion.Slerp(
                    _rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                );
                _rb.MoveRotation(smoothed);
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled && _rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.position = position;
                _rb.rotation = rotation;
            }

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
