using UnityEngine;
using UnityEngine.Serialization;

public class SunScript : MonoBehaviour
{
    [Header("References")]
    [FormerlySerializedAs("light")]
    [SerializeField] GameObject sunLight;      // child directional light
    [SerializeField] LayerMask pillarMask;     // layer(s) that cast shadows

    [Header("Sun Position")]
    [SerializeField] float radius = 100f;          // distance from the player
    [FormerlySerializedAs("baseElevation")]
    [SerializeField] float startElevation = 35f;   // degrees above the horizon at start
    [SerializeField] float maxElevation = 60f;     // highest the sun can climb
    [SerializeField] float minElevation = -15f;    // lowest it can sink (so boosting can recover it)
    [SerializeField] float horizonElevation = 0f;  // at or below this the sun is set (light hidden)

    [Header("Skyboxes")]
    [SerializeField] Material daySkybox;           // Procedural skybox presets: the sky smoothly blends between them
    [SerializeField] Material duskSkybox;
    [SerializeField] Material nightSkybox;
    [SerializeField] float dayFullElevation = 30f;   // at or above this the sky is fully "day"
    [SerializeField] float duskElevation = 12f;      // the sky is fully "dusk" at exactly this height; night at the horizon

    [Header("Sun Motion")]
    [SerializeField] float sinkRate = 2f;          // degrees/sec the sun constantly sinks
    [SerializeField] float risePerSpeed = 0.5f;    // degrees/sec gained per unit of speed above cruising

    Transform playerTransform;
    Player player;
    float elevation;
    bool wasInShadow;
    Material runtimeSky;   // the one live skybox; its values are blended between your three
    float nextGIUpdate;

    /// <summary>Continuous time-of-day: 0 = night, 1 = dusk, 2 = day. Other scripts (water) blend with it.</summary>
    public float SkyPhase { get; private set; } = 2f;

    void Start()
    {
        GameObject playerObj = GameManager.gameManager.player;
        playerTransform = playerObj.transform;
        player = playerObj.GetComponent<Player>();
        elevation = startElevation;
        if (daySkybox == null) daySkybox = RenderSettings.skybox;
        UpdateSky();

        if (pillarMask.value == -1)
            Debug.LogWarning("SunScript: Pillar Mask is set to Everything, so the ray hits the player/water/obstacles and isInSun will always be false. Set it to the Pillar layer only.", this);
    }

    void FixedUpdate()
    {
        if (GameManager.gameManager.gameOver) return;

        // 1. Constant sinking, pushed back up only when going faster than cruising speed.
        float speedAboveCruise = Mathf.Max(0f, GameManager.gameManager.forwardVelocity - player.ForwardSpeed);
        float rate = -sinkRate + speedAboveCruise * risePerSpeed;   // degrees/sec
        elevation = Mathf.Clamp(elevation + rate * Time.fixedDeltaTime, minElevation, maxElevation);

        // 2. Place the sun on a vertical arc straight ahead of the player.
        float rad = elevation * Mathf.Deg2Rad;
        Vector3 p = playerTransform.position;
        transform.position = new Vector3(
            p.x,
            p.y + Mathf.Sin(rad) * radius,
            p.z + Mathf.Cos(rad) * radius
        );

        // 3. Below the horizon: hide the light, player is in darkness.
        bool belowHorizon = elevation <= horizonElevation;
        sunLight.SetActive(!belowHorizon);
        UpdateSky();

        if (belowHorizon)
        {
            player.isInSun = false;
            return;
        }

        // 4. Aim the light and check for pillar shadows.
        sunLight.transform.LookAt(playerTransform);

        Vector3 toPlayer = playerTransform.position - transform.position;
        Vector3 dir = toPlayer.normalized;
        float dist = toPlayer.magnitude;

        // 1. Physics colliders on the mask layer (pillars, etc.).
        string blocker = null;
        if (Physics.Raycast(transform.position, dir, out RaycastHit hit, dist, pillarMask, QueryTriggerInteraction.Collide))
            blocker = hit.collider.name;

        // 2. Visible building meshes: their bounds match the shadow the sun actually casts on the water.
        if (blocker == null)
        {
            Ray ray = new Ray(transform.position, dir);
            foreach (Obstacle obstacle in Obstacle.Active)
            {
                if (obstacle != null && obstacle.BlocksRay(ray, dist))
                {
                    blocker = obstacle.name;
                    break;
                }
            }
        }

        bool inShadow = blocker != null;

        // Debug: log whenever the shadow state flips, and what blocked the sun.
        if (inShadow != wasInShadow)
        {
            Debug.Log(inShadow ? $"SunScript: SHADOW from '{blocker}'" : "SunScript: back in sun");
            wasInShadow = inShadow;
        }

        player.isInSun = !inShadow;

        Debug.DrawRay(transform.position, toPlayer, inShadow ? Color.red : Color.yellow);
    }

    // Blends one live Procedural skybox between your day / dusk / night presets by sun height.
    void UpdateSky()
    {
        // Time-of-day phase is published even if no skyboxes are assigned (the water uses it too).
        SkyPhase = elevation >= duskElevation
            ? 1f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duskElevation, dayFullElevation, elevation))
            : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(horizonElevation, duskElevation, elevation));

        if (daySkybox == null) return;
        if (runtimeSky == null)
        {
            runtimeSky = new Material(daySkybox);   // a copy, so your preset assets are never changed
            RenderSettings.skybox = runtimeSky;
        }

        Material dusk = duskSkybox != null ? duskSkybox : daySkybox;
        Material night = nightSkybox != null ? nightSkybox : dusk;

        Material from, to;
        float t;
        if (elevation >= duskElevation)
        {
            from = dusk; to = daySkybox;
            t = Mathf.InverseLerp(duskElevation, dayFullElevation, elevation);
        }
        else
        {
            from = night; to = dusk;
            t = Mathf.InverseLerp(horizonElevation, duskElevation, elevation);
        }
        t = Mathf.SmoothStep(0f, 1f, t);

        BlendColor("_SkyTint", from, to, t);
        BlendColor("_GroundColor", from, to, t);
        BlendFloat("_Exposure", from, to, t);
        BlendFloat("_AtmosphereThickness", from, to, t);
        BlendFloat("_SunSize", from, to, t);
        BlendFloat("_SunSizeConvergence", from, to, t);

        // Ambient light comes from the sky; refreshing it is costly, so a few times a second is plenty.
        if (Time.time >= nextGIUpdate)
        {
            nextGIUpdate = Time.time + 0.25f;
            DynamicGI.UpdateEnvironment();
        }
    }

    void BlendColor(string prop, Material a, Material b, float t)
    {
        if (runtimeSky.HasProperty(prop) && a.HasProperty(prop) && b.HasProperty(prop))
            runtimeSky.SetColor(prop, Color.Lerp(a.GetColor(prop), b.GetColor(prop), t));
    }

    void BlendFloat(string prop, Material a, Material b, float t)
    {
        if (runtimeSky.HasProperty(prop) && a.HasProperty(prop) && b.HasProperty(prop))
            runtimeSky.SetFloat(prop, Mathf.Lerp(a.GetFloat(prop), b.GetFloat(prop), t));
    }

    void OnDestroy()
    {
        if (runtimeSky != null) Destroy(runtimeSky);
    }
}
