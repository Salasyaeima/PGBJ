using UnityEngine;

namespace CoinRush.Player
{
    /// <summary>
    /// Abstraction for movement input so human players, AI bots, and networked actors share one motor.
    /// </summary>
    public interface IInputProvider
    {
        Vector2 GetMoveInput();
    }
}
