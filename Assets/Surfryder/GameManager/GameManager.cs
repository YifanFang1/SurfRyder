using UnityEngine;
using System.Collections;

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

    [Header("Random Spawning")]
    [SerializeField] float minSpawnTime = 1.0f;
    [SerializeField] float maxSpawnTime = 3.0f;
    [SerializeField] float spawnDistance = 30.0f;
    [SerializeField] float spawnWidth = 8.0f;
    [SerializeField] float obstacleChance = 0.5f;

    [Header("GameState")]
    public bool gameOver = false;

    public static GameManager gameManager { get; private set; }

    void Awake() {
        gameManager = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(SpawnRandomly());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate() {
        if(gameOver) return;
        
    }

    IEnumerator SpawnRandomly()
    {
        while (!gameOver)
        {
            float waitTime = Random.Range(minSpawnTime, maxSpawnTime);

            yield return new WaitForSeconds(waitTime);

            if (gameOver) yield break;

            SpawnObject();
        }
    }

    void SpawnObject() {
        if(player == null) return;

        GameObject prefab;

        //Randomly choose obstacle or collectible
        bool spawnObstacle = Random.value < obstacleChance;

        if (spawnObstacle)
        {
            if (obstacleList.Length == 0) return;

            int index = Random.Range(0, obstacleList.Length);
            prefab = obstacleList[index];
           
        }
        else
        {
            prefab = collectable;
        }

        if (prefab == null) return;

        //Randomly position across the spawn line
        float randomX = Random.Range(-spawnWidth / 2f, spawnWidth / 2f);

        Vector3 spawnPosition = new Vector3(player.transform.position.x + randomX, prefab.transform.position.y, player.transform.position.z + spawnDistance);

        Instantiate(prefab, spawnPosition, prefab.transform.rotation);
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
