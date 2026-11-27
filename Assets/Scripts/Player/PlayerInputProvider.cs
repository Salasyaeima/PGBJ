using UnityEngine;
using UnityEngine.InputSystem;

namespace CoinRush.Player
{
    /// <summary>
    /// Reads 2D movement input from Unity's New Input System (WASD composite).
    /// </summary>
    public class PlayerInputProvider : MonoBehaviour, IInputProvider
    {
        [SerializeField] private InputActionReference moveAction;

        private InputAction _runtimeFallbackAction;
        private InputAction _activeAction;

        private void Awake()
        {
            if (moveAction != null && moveAction.action != null)
            {
                _activeAction = moveAction.action;
            }
            else
            {
                _runtimeFallbackAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
                _runtimeFallbackAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                _activeAction = _runtimeFallbackAction;
            }
        }

        private void OnEnable()
        {
            // Enable only when this component is active; never disable a shared InputActionReference
            // in OnDisable because remote NetworkPlayer proxies share the same asset reference.
            if (_runtimeFallbackAction != null)
            {
                _runtimeFallbackAction.Enable();
            }
            else if (moveAction != null && moveAction.action != null && !moveAction.action.enabled)
            {
                moveAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            _runtimeFallbackAction?.Disable();
        }

        private void OnDestroy()
        {
            _runtimeFallbackAction?.Dispose();
        }

        public Vector2 GetMoveInput()
        {
            if (!enabled || _activeAction == null)
            {
                return Vector2.zero;
            }
            return _activeAction.ReadValue<Vector2>();
        }
    }
}
