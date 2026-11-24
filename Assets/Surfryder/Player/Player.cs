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
    [SerializeField] float steerAmount;      // -1..1: sideways velocity / max sideways speed (drives the turn flaps)
    [SerializeField] float leftFlapAngle;    // current flap angles in degrees (should change when you steer or brake)
    [SerializeField] float rightFlapAngle;

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

    [Header("Tilt Pivot")]
    [SerializeField] float tiltPivotDrop = 0.6f;  // the bank rotates around a point this far BELOW the transform

    [Header("Buoyancy Point (physics only)")]
    [SerializeField] float pivotOffset = 0f;      // the water is felt this far BELOW the transform; nothing visual or collider-related moves

    [Header("Brake Flaps")]
    [SerializeField] Transform leftFlapPivot;     // auto-found by name if left empty
    [SerializeField] Transform rightFlapPivot;
    [SerializeField] float flapOpenAngle = 60f;   // degrees the flaps lift while braking
    [SerializeField] float flapSpeed = 240f;      // degrees per second
    [SerializeField] float turnFlapAngle = 15f;   // small lift of the inside flap while steering
    [SerializeField] float turnFlapSpeed = 90f;   // degrees per second for the steering flap movement (slower than braking)
    [SerializeField] float brakeHeatVent = 6f;    // heat vented per second while braking (slight)

    [Header("Water Splashes")]
    [SerializeField] float jumpRippleStrength = 0.35f;     // wave height sent out when jumping
    [SerializeField] float landRippleStrength = 0.7f;      // wave height for a hard landing (scaled by impact speed)
    [SerializeField] float landRippleMinSpeed = 1f;        // landings slower than this make no ripple
    [SerializeField] float landRippleMaxSpeed = 8f;        // impact speed that gives the full strength

    [Header("Jump")]
    [SerializeField] float jumpForce;

    [Header("Bouyant")]
    [SerializeField] float bouyantConstant;
    [SerializeField] float bouyantDamping;
    Rigidbody rb;
    PlayerInput playerInput;
    bool boosting = false;
    bool braking = false;
    bool wasGrounded = true;
    float lastVerticalSpeed;
    float currentRoll;   // degrees of bank we've applied so far
    InputAction boostAction, brakeAction, diveAction;

    [Header("Sound")]
    [SerializeField, Range(0f, 1f)] float overheatWarnPercent = 0.8f;   // warning alarm starts at this much heat
    [SerializeField, Range(0f, 1f)] float lowEnergyWarnPercent = 0.2f;  // warning alarm starts at this little energy
    // Track which loops we've started so we only talk to the AudioManager when something changes
    bool skidPlaying = false;
    bool boostPlaying = false;
    bool overheatWarnPlaying = false;
    bool lowPowerWarnPlaying = false;
    bool airborneFromJump = false;   // true after pressing jump, until we splash back down onto the water

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
        if (leftFlapPivot == null || rightFlapPivot == null)
            Debug.LogWarning("Player: a brake flap pivot was not found, so that flap will not move.", this);
        playerInput = GetComponent<PlayerInput>();

        boostAction = playerInput.actions["Boost"];
        brakeAction = playerInput.actions["Brake"];
        diveAction = playerInput.actions["Dive"];
    }

    void Start()
    {
        // Background ambience. Safe to call again on scene reload: if it's already running it just fades back in.
        AudioManager.Instance.PlayAmbience(AudioNames.OceanWaves, 2f);
        AudioManager.Instance.PlayAmbience(AudioNames.WindWithRain, 2f);
    }

    void FixedUpdate() {
        GameManager manager = GameManager.gameManager;
        if (manager.gameOver) { StopAllLoopSounds(0.3f); return; }

        IsGrounded();
        CheckLanding();
        WaterPhysics();
        MoveForward();
        Steer();
        Tilt();

        bool wasBraking = braking;
        braking = brakeAction.IsPressed();
        if (braking) {Braking();}
        UpdateBrakeFlaps(braking);

        // Brake screech: once, at the moment the brake is pressed, if we're actually on the water and moving fast enough
        if (braking && !wasBraking && isGrounded && forwardVelocity > brakeSpeed)
            AudioManager.Instance.PlaySFX(AudioNames.CarBrake);

        if(boostAction.IsPressed()) {
            Boost();
            boosting = true;
        }
        else { boosting = false; }

        HeatGauge();
        EnergyGauge();
        UpdateLoopSounds();   // must come before CheckMeters so game over can silence them
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
        WaterFloor.Splash(transform.position, jumpRippleStrength);
        airborneFromJump = true;   // the splash sound waits until we come back down
    }

    // A hard landing sends a bigger sine-wave ring out across the water.
    void CheckLanding()
    {
        if (isGrounded && !wasGrounded && lastVerticalSpeed < -landRippleMinSpeed)
        {
            float impact = Mathf.InverseLerp(landRippleMinSpeed, landRippleMaxSpeed, -lastVerticalSpeed);
            WaterFloor.Splash(transform.position, landRippleStrength * Mathf.Max(0.3f, impact));
        }

        // Splash sound: only when we touch the water again, falling, after a jump
        if (isGrounded && !wasGrounded && airborneFromJump && lastVerticalSpeed < 0f)
        {
            AudioManager.Instance.PlaySFX(AudioNames.WaterSplash);
            airborneFromJump = false;
        }

        wasGrounded = isGrounded;
        lastVerticalSpeed = rb.linearVelocity.y;
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

        MoveFlap(leftFlapPivot, leftAngle, braking);
        MoveFlap(rightFlapPivot, rightAngle, braking);

        // Readouts so you can watch them in the Inspector while playing.
        steerAmount = steer;
        leftFlapAngle = leftFlapPivot != null ? Mathf.DeltaAngle(0f, leftFlapPivot.localEulerAngles.x) : 0f;
        rightFlapAngle = rightFlapPivot != null ? Mathf.DeltaAngle(0f, rightFlapPivot.localEulerAngles.x) : 0f;
    }

    // Braking (and closing back down after braking) uses the fast flap speed; steering uses the slower turn flap speed.
    void MoveFlap(Transform flap, float targetAngle, bool braking)
    {
        if (flap == null) return;

        Quaternion target = Quaternion.Euler(targetAngle, 0f, 0f);
        bool bigMove = braking || Quaternion.Angle(flap.localRotation, target) > turnFlapAngle * 1.5f;
        float step = (bigMove ? flapSpeed : turnFlapSpeed) * Time.fixedDeltaTime;
        flap.localRotation = Quaternion.RotateTowards(flap.localRotation, target, step);
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
        // Bank into the turn (right input rolls clockwise = negative Z) and self-right with no input,
        // rotating around a point BELOW the skimmer so it swings like a pendulum.
        float targetRoll = -leftRight * maxSideAngle;
        float newRoll = Mathf.MoveTowards(currentRoll, targetRoll, tiltSpeed * Time.fixedDeltaTime);
        float delta = newRoll - currentRoll;
        currentRoll = newRoll;

        Vector3 pivot = transform.position - transform.up * tiltPivotDrop;
        transform.RotateAround(pivot, transform.forward, delta);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position - transform.up * tiltPivotDrop, 0.12f);
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
        if (braking) change -= brakeHeatVent;

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
            StopAllLoopSounds(0.1f);
            AudioManager.Instance.PlaySFX(AudioNames.Explosion);
            GameManager.gameManager.GameOver();
        }
        else if (energy <= 0f)
        {
            Debug.Log("Out of energy!");
            StopAllLoopSounds(0.1f);
            AudioManager.Instance.PlaySFX(AudioNames.PowerOff);
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
        AudioManager.Instance.PlaySFX(AudioNames.ItemCollected);
    }

    // ---------------------------------------------------------------- Sound helpers

    // Starts or stops one looping sound, but only when its state actually changes.
    void SetLoopSound(string sound, bool shouldPlay, ref bool isPlaying, float fadeIn = 0.15f, float fadeOut = 0.2f)
    {
        if (AudioManager.Instance == null) return;

        if (shouldPlay && !isPlaying)
        {
            AudioManager.Instance.PlayLoopSFX(sound, fadeIn);
            isPlaying = true;
        }
        else if (!shouldPlay && isPlaying)
        {
            AudioManager.Instance.StopLoopSFX(sound, fadeOut);
            isPlaying = false;
        }
    }

    // Every physics frame, decide which loops should be running
    void UpdateLoopSounds()
    {
        SetLoopSound(AudioNames.JetWaterSkidding, true, ref skidPlaying);   // always on, even while airborne
        SetLoopSound(AudioNames.EngineBoost, boosting, ref boostPlaying, 0.1f, 0.3f);
        SetLoopSound(AudioNames.OverheatingWarning, HeatPercent >= overheatWarnPercent, ref overheatWarnPlaying, 0.1f, 0.3f);
        SetLoopSound(AudioNames.LowPowerWarning, EnergyPercent <= lowEnergyWarnPercent, ref lowPowerWarnPlaying, 0.1f, 0.3f);
    }

    void StopAllLoopSounds(float fade)
    {
        SetLoopSound(AudioNames.JetWaterSkidding, false, ref skidPlaying, 0.15f, fade);
        SetLoopSound(AudioNames.EngineBoost, false, ref boostPlaying, 0.1f, fade);
        SetLoopSound(AudioNames.OverheatingWarning, false, ref overheatWarnPlaying, 0.1f, fade);
        SetLoopSound(AudioNames.LowPowerWarning, false, ref lowPowerWarnPlaying, 0.1f, fade);
    }

    void OnDestroy()
    {
        StopAllLoopSounds(0.1f);
    }
}
