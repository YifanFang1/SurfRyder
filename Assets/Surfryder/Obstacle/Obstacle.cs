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
        forwardVelocity = GameManager.gameManager.forwardVelocity;
        sidewaysVelocity = GameManager.gameManager.sidewaysVelocity;
    }
}
