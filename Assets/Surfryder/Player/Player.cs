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
    public float ForwardSpeed => forwardSpeed;   // read-only for other scripts
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
    public bool isInSun; // set by the sun raycast (Yifan's part)

    [Header("Heat Gauge")]
    [SerializeField] float maxHeat = 100f;
    [SerializeField] float heat = 0f;                 // current heat (watch in Inspector)
    [SerializeField] float heatGainInSun = 8f;        // per second while in sunlight
    [SerializeField] float heatLossInShade = 12f;     // per second while in shadow
    [SerializeField] float boostHeatGain = 15f;       // extra per second while boosting

    [Header("Energy Gauge")]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float energy = 100f;             // current energy (watch in Inspector)
    [SerializeField] float energyGainInSun = 10f;     // per second while in sunlight
    [SerializeField] float energyLossInShade = 8f;    // per second while in shadow
    [SerializeField] float boostEnergyCost = 12f;     // extra drain per second while boosting
    [SerializeField] float collectibleEnergy = 25f;   // energy restored per collectible

    public float Heat => heat;
    public float Energy => energy;
    public float MaxHeat => maxHeat;
    public float MaxEnergy => maxEnergy;
    public float HeatPercent => heat / maxHeat;       // 0..1, handy for UI bars
    public float EnergyPercent => energy / maxEnergy; // 0..1, handy for UI bars

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
        if (manager.gameOver) return;
        
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

        HeatGauge();
        EnergyGauge();
        CheckMeters();

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

    // Heat: goes up in sun, down in shadow, boosting always adds heat
    void HeatGauge()
    {
        float change = isInSun ? heatGainInSun : -heatLossInShade;
        if (boosting) change += boostHeatGain;

        heat = Mathf.Clamp(heat + change * Time.fixedDeltaTime, 0f, maxHeat);
    }

    // Energy: recharges in sun, drains in shadow, boosting costs extra
    void EnergyGauge()
    {
        float change = isInSun ? energyGainInSun : -energyLossInShade;
        if (boosting) change -= boostEnergyCost;

        energy = Mathf.Clamp(energy + change * Time.fixedDeltaTime, 0f, maxEnergy);
    }

    // Overheat = explode, out of energy = power down. Both end the run.
    void CheckMeters()
    {
        if (heat >= maxHeat)
        {
            Debug.Log("Overheated!");
            GameManager.gameManager.GameOver();
        }
        else if (energy <= 0f)
        {
            Debug.Log("Out of energy!");
            GameManager.gameManager.GameOver();
        }
    }

    // Called by Collectible when the player picks one up
    public void AddEnergy(float amount)
    {
        energy = Mathf.Clamp(energy + amount, 0f, maxEnergy);
    }

    public void CollectEnergy()
    {
        AddEnergy(collectibleEnergy);
    }
}
