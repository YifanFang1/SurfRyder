using UnityEngine;

public class GameManager : MonoBehaviour
{

    [Header("Player")]
    public GameObject player;
    public float forwardVelocity = 10.0f; // how fast obstacles move towards you
    public float sidewaysVelocity = 0.0f; // instaneous sideways speed

    [Header("Obstacles")]
    [SerializeField] GameObject[] obstacleList;
    
    [Header("Collectable")]
    [SerializeField] GameObject collectable;

    [Header("GameState")]
    public bool gameOver = false;

    public static GameManager gameManager { get; private set; }

    void Awake() {
        gameManager = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate() {
        if(gameOver) return;
        
        // some kind of coroutine that creates obstacles and collectables at a set interval
    }

    public void SetSideWaysSpeed(float input) {
        sidewaysVelocity = input;
    }

    public void SetForwardSpeed(float input) {
        forwardVelocity = input;
    }

    public void GameOver()
    {
        gameOver = true;
    }
}
