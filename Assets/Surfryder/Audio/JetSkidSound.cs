using UnityEngine;

// Attach to your player. Plays the skidding loop while the player is on the water
// and fades it out when they leave it (jumping, airborne, etc.).
public class JetSkidSound : MonoBehaviour
{
    [Header("Ground check")]
    public LayerMask waterLayer;           // set this to your water layer in the Inspector
    public float checkDistance = 1.0f;     // how far below the player counts as "on the water"
    public Vector3 checkOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Sound")]
    public float fadeIn = 0.15f;
    public float fadeOut = 0.2f;

    private bool wasOnWater;

    void Update()
    {
        bool onWater = IsOnWater();

        // Only talk to the AudioManager when the state changes
        if (onWater && !wasOnWater)
            AudioManager.Instance.PlayLoopSFX(AudioNames.JetWaterSkidding, fadeIn);
        else if (!onWater && wasOnWater)
            AudioManager.Instance.StopLoopSFX(AudioNames.JetWaterSkidding, fadeOut);

        wasOnWater = onWater;
    }

    bool IsOnWater()
    {
        // Shoots a ray straight down from the player and checks for the water layer
        return Physics.Raycast(transform.position + checkOffset, Vector3.down, checkDistance, waterLayer);
    }

    void OnDisable()
    {
        // Don't leave the sound running if the player is disabled or destroyed
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopLoopSFX(AudioNames.JetWaterSkidding, 0.1f);
    }

    // Visualize the check in the Scene view (red line below the player)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position + checkOffset,
                        transform.position + checkOffset + Vector3.down * checkDistance);
    }
}
