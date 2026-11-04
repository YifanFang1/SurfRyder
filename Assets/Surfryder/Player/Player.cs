using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Speedometer (Don't Touch!)")]
    [SerializeField] float forwardVelocity;
    [SerializeField] float sidewaysVelocity;
    [SerializeField] float leftRight = 0.0f;
    [SerializeField] bool isGrounded;

    [Header("Forward")]
    [SerializeField] float forwardSpeed;
    [SerializeField] float forwardAcceleration;

    [Header("Brakes")]
    [SerializeField] float brakeSpeed;
    [SerializeField] float brakeForce;

    [Header("Boost")]
    [SerializeField] float boostSpeed;
    [SerializeField] float boostForce;
    [SerializeField] float slowDownForce;

    [Header("Turning")]
    [SerializeField] float sidewaysSpeed;
    [SerializeField] float sidewaysAcceleration;

    [Header("Jump")]
    [SerializeField] float jumpForce;

    [Header("Bouyant")]
    [SerializeField] float bouyantConstant;
    [SerializeField] float bouyantDamping;
    Rigidbody rb;
    PlayerInput playerInput;
    bool boosting = false;
    InputAction boostAction, brakeAction, diveAction;
     
    [Header("Heat and Energy Meters")]
    public bool isInSun; // assume this bool will state whether the player is in the sunlight 

    // make a heat gauge : increases when in sun and boosting, decreases in shadow
    // make a energy gauge : increases in sun, decreases in shadow and boosting
    // make max capacity vars for each
    // make increase and decrease rate vars for each

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

        if(brakeAction.IsPressed()) {Braking();}
        
        if(boostAction.IsPressed()) {
            Boost();
            boosting = true;
        }
        else { boosting = false; }

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

    void Boost(){
        if (forwardVelocity < boostSpeed)
        {
            forwardVelocity += boostForce;
        }
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
        if (forwardVelocity > forwardSpeed && !boosting)
        {
            forwardVelocity -= slowDownForce;
        }

    }

    void Steer()
    {   
        if(Mathf.Abs(sidewaysVelocity) < sidewaysSpeed)
        {
            sidewaysVelocity += leftRight * sidewaysAcceleration;
        }
        if (leftRight == 0)
        {
            sidewaysVelocity += Math.Sign(-sidewaysVelocity) * sidewaysAcceleration;
        }
    }

    // void EnergyGauge() { update gauge values }
    // void HeatGauge() { update gauge values }
}