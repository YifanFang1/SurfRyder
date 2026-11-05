using UnityEngine;

public class SunScript : MonoBehaviour
{
    [SerializeField] GameObject light;
    GameObject player;
    [SerializeField] float radius;
    [SerializeField] float radiualSpeed;

    Transform lightOrientation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        lightOrientation = light.transform;
        player = GameManager.gameManager.player;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
