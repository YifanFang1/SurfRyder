using UnityEngine;

public class Collectible : MonoBehaviour
{
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] float bobHeight = 0.3f;
    [SerializeField] float bobSpeed = 2f;
    [SerializeField] Transform visual;

    public static int collected = 0;

    [Header("Observerable Variables (Don't edit!)")]
    [SerializeField] float forwardVelocity;
    [SerializeField] float sidewaysVelocity;

    Vector3 visualStartPos;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        visualStartPos = visual.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.gameManager.gameOver) return;

        visual.Rotate(Vector3.up, spinSpeed*Time.deltaTime, Space.World);
        visual.localPosition = visualStartPos + Vector3.up*Mathf.Sin(Time.time*bobSpeed)*bobHeight;
    }

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

        if (transform.position.z < -20.0f) { Destroy(gameObject); }
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            collected++;
            Debug.Log("Collected: " +collected);
            Destroy(gameObject);
        }
    }
}
