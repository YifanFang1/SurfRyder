using UnityEngine;

public class Collectible : MonoBehaviour
{
    public float spinSpeed = 90f;
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;

    public static int collected = 0;

    Vector3 startPos;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startPos = transform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed*Time.deltaTime, Space.World);
        transform.localPosition = startPos + Vector3.up*Mathf.Sin(Time.time*bobSpeed)*bobHeight;
    }

    void onTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            collected++;
            Debug.Log("Collected: " +collected);
            Destroy(gameObject);
        }
    }
}
