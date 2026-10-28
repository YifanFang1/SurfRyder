using UnityEngine;

public class GameManager : MonoBehaviour
{

    public GameObject player;
    public float forwardVelocity = 10.0f; // how fast obstacles move towards you
    public float sidewaysVelocity = 0.0f; // instaneous sideways speed

    [SerializeField] GameObject[] obstacleList;

    public bool gameOver = false;

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


    }
}
