using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [Header("Observerable Variables (Don't edit!)")]
    [SerializeField] float forwardVelocity;
    [SerializeField] float sidewaysVelocity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Debug.Log(GameManager.gameManger.forwardVelocity);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        
        if (GameManager.gameManager.gameOver) return;

        forwardVelocity = GameManager.gameManager.forwardVelocity;
        sidewaysVelocity = GameManager.gameManager.sidewaysVelocity;

        Vector3 movement = new Vector3(
        -sidewaysVelocity,
        0f,
        -forwardVelocity
        );

        transform.Translate(movement * Time.fixedDeltaTime, Space.World);

    }
}
