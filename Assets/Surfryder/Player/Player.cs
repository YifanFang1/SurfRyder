using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Speedometer (Don't Touch!)")]
    [SerializeField] float forwardVelocity;
    [SerializeField] float sidewaysVelocity;
    [SerializeField] bool isGrounded;
    [SerializeField] float leftRight = 0.0f;

    [Header("Vars")]
    [SerializeField] float forwardSpeed;
    [SerializeField] float forwardAcceleration;
    [SerializeField] float brakeSpeed;
    [SerializeField] float brakeForce;
    [SerializeField] float sidewaysSpeed;
    [SerializeField] float sidewaysAcceleration;
    [SerializeField] float jumpForce;
    [SerializeField] float bouyantConstant;
    [SerializeField] float bouyantDamping;
    [SerializeField] float gravityForce;
    Rigidbody rb;
    PlayerInput playerInput;
    InputAction boostAction, brakeAction, diveAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        boostAction = playerInput.actions["Boost"];
        brakeAction = playerInput.actions["Brake"];
        diveAction = playerInput.actions["Dive"];
    }

    void FixedUpdate() {
        GameManager manager = GameManager.gameManager;
        
        IsGrounded();
        WaterPhysics();
        MoveForward();
        Steer();

        if(brakeAction.IsPressed()) {Braking(); Debug.Log("Braking");}

        manager.SetSideWaysSpeed(sidewaysVelocity);
        manager.SetForwardSpeed(forwardVelocity);

    }

    void OnLeftRight(InputValue value) {
        float dir = value.Get<float>();
        leftRight = dir;
    }

    void OnJump() {
        if (!isGrounded) return;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    void Braking() {
        if (forwardVelocity > brakeSpeed)
        {
            forwardVelocity -= brakeForce;
        }
    }

    void OnBoost(){

    }

    void OnDive(){
        // temp no implementation
    }

    void IsGrounded()
    {
        isGrounded = ((Mathf.Abs(transform.position.y)) < 1f);
    }

    void WaterPhysics()
    {
        rb.AddForce(Vector3.down * gravityForce, ForceMode.Force);

        if(transform.position.y < -0.01f)
        {
            float bouyantForce = -transform.position.y * bouyantConstant - bouyantDamping * rb.linearVelocity.y;

            rb.AddForce(Vector3.up * bouyantForce, ForceMode.Force);
        }
    }
    
    void MoveForward()
    {
        if (forwardVelocity < forwardSpeed)
        {
            forwardVelocity += forwardAcceleration;
        }
    }

    void Steer()
    {
        
        if(Mathf.Abs(sidewaysVelocity) < sidewaysSpeed)
        {
            sidewaysVelocity += leftRight * sidewaysAcceleration;
        }
    }
}
