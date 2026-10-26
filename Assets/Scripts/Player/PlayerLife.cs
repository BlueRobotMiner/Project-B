// PlayerLife
// Put on the Player. The monster has to land several hits before the run ends.
// Each hit is counted, and the last one shows the game over screen.
using UnityEngine;

public class PlayerLife : MonoBehaviour
{
    [SerializeField] private int maxHits = 3;

    private int hits;

    public int HitsLeft => maxHits - hits;

    public void TakeHit()
    {
        if (GameOverMenu.IsGameOver) return;

        hits++;
        Debug.Log($"Player hit: {hits}/{maxHits}");

        if (hits >= maxHits)
            GameOverMenu.Instance?.Show();
    }
}
