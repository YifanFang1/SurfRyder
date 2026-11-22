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
    [SerializeField] float maxSideAngle;
    [SerializeField] float tiltSpeed;

    [Header("Buoyancy Point (physics only)")]
    [SerializeField] float pivotOffset = 0f;      // the water is felt this far BELOW the transform; nothing visual or collider-related moves

    [Header("Brake Flaps")]
    [SerializeField] Transform leftFlapPivot;     // auto-found by name if left empty
    [SerializeField] Transform rightFlapPivot;
    [SerializeField] float flapOpenAngle = 60f;   // degrees the flaps lift while braking
    [SerializeField] float flapSpeed = 240f;      // degrees per second
    [SerializeField] float turnFlapAngle = 15f;   // small lift of the inside flap while steering

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

        if (leftFlapPivot == null) leftFlapPivot = FindChildByName("PivotLeft");
        if (rightFlapPivot == null) rightFlapPivot = FindChildByName("PivotRight");
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
        Tilt();

        bool braking = brakeAction.IsPressed();
        if (braking) {Braking();}
        UpdateBrakeFlaps(braking);
        
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
        float surface = WaterFloor.SurfaceAt(transform.position) + pivotOffset;   // measured at the buoyancy point below the transform
        isGrounded = Mathf.Abs(transform.position.y - surface) < 1f;
    }

    void WaterPhysics()
    {
        // Float on the actual wave height under the skimmer (0 if there is no water floor).
        float depth = WaterFloor.SurfaceAt(transform.position) + pivotOffset - transform.position.y;

        if(depth > 0.01f)
        {
            float bouyantForce = depth * bouyantConstant - bouyantDamping * rb.linearVelocity.y;

            rb.AddForce(Vector3.up * bouyantForce, ForceMode.Force);
        }
    }

    // Lifts the flap pivots upward (rotation about local X raises the flap's trailing edge).
    // Braking opens both flaps fully; steering lifts only the inside flap a little (like an airbrake).
    void UpdateBrakeFlaps(bool braking)
    {
        float steer = sidewaysSpeed > 0.001f ? Mathf.Clamp(sidewaysVelocity / sidewaysSpeed, -1f, 1f) : 0f;

        // Steering right (steer > 0) lifts the right flap; steering left lifts the left one.
        float leftTurn  = Mathf.Max(0f, -steer) * turnFlapAngle;
        float rightTurn = Mathf.Max(0f,  steer) * turnFlapAngle;

        float leftAngle  = braking ? flapOpenAngle : leftTurn;
        float rightAngle = braking ? flapOpenAngle : rightTurn;

        float step = flapSpeed * Time.fixedDeltaTime;

        if (leftFlapPivot != null)
            leftFlapPivot.localRotation = Quaternion.RotateTowards(leftFlapPivot.localRotation, Quaternion.Euler(leftAngle, 0f, 0f), step);
        if (rightFlapPivot != null)
            rightFlapPivot.localRotation = Quaternion.RotateTowards(rightFlapPivot.localRotation, Quaternion.Euler(rightAngle, 0f, 0f), step);
    }

    // Finds a child by name, ignoring spaces and case ("Pivot Right" matches "PivotRight").
    Transform FindChildByName(string wanted)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Replace(" ", "").Equals(wanted, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    void Tilt()
    {
        // Bank into the turn while steering (right input rolls clockwise = negative Z), self-right with no input.
        float targetZ = -leftRight * maxSideAngle;

        Vector3 euler = transform.eulerAngles;
        float newZ = Mathf.MoveTowardsAngle(euler.z, targetZ, tiltSpeed * Time.fixedDeltaTime);

        transform.rotation = Quaternion.Euler(euler.x, euler.y, newZ);
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
        // Move toward the target sideways speed (0 when no input). Handles start, stop and reversing.
        float target = leftRight * sidewaysSpeed;
        sidewaysVelocity = Mathf.MoveTowards(sidewaysVelocity, target, sidewaysAcceleration);
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
