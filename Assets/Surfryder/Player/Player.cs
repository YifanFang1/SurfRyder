using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Speedometer (Don't Touch!)")]
    [SerializeField] float forwardVelocity;
    [SerializeField] float sidewaysVelocity;

    [Header("Vars")]
    [SerializeField] float forwardSpeed;
    [SerializeField] float sidewaysSpeed;
    float leftRight = 0.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate() {

    }

    void OnLeftRight() {
        
    }
}
