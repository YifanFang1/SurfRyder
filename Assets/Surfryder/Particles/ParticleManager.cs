using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds and drives every particle effect in code (no prefabs needed). Auto-created in any scene with a Player.
/// Effects: ocean spray, smoke (heat), fire (overheating), steam (braking), electric sparks (low energy / pickups).
/// Other scripts call the static helpers: ParticleManager.Splash / SparkBurst / Explosion (all null-safe).
/// </summary>
public class ParticleManager : MonoBehaviour
{
    static ParticleManager instance;

    [Header("Spray")]
    [SerializeField] float sprayRate = 70f;
    [SerializeField] float boostSprayBonus = 60f;

    [Header("Smoke / Fire (heat)")]
    [SerializeField] float smokeStartHeat = 0.5f;
    [SerializeField] float smokeMaxRate = 30f;
    [SerializeField] float fireStartHeat = 0.85f;
    [SerializeField] float fireMaxRate = 60f;
    [SerializeField] Vector3 exhaustOffset = new Vector3(0f, 0.5f, -1.8f);   // local to the player

    [Header("Steam (braking)")]
    [SerializeField] float steamRate = 35f;

    [Header("Sparks (low energy)")]
    [SerializeField] float lowEnergy = 0.25f;
    [SerializeField] float sparkRate = 25f;

    Player player;
    [Header("Effects: assign your own ParticleSystems to author them in the editor. Empty slots get a code-built default.")]
    [SerializeField] ParticleSystem spray;
    [SerializeField] ParticleSystem smoke;
    [SerializeField] ParticleSystem fire;
    [SerializeField] ParticleSystem steam;
    [SerializeField] ParticleSystem sparks;
    readonly System.Collections.Generic.HashSet<ParticleSystem> authored = new System.Collections.Generic.HashSet<ParticleSystem>();
    float sprayAcc, smokeAcc, fireAcc, steamAcc, sparkAcc;
    bool dead;

    // --- Auto-create in any scene that has a Player -----------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindFirstObjectByType<Player>() == null) return;
        if (FindFirstObjectByType<ParticleManager>() != null) return;
        new GameObject("ParticleManager").AddComponent<ParticleManager>();
    }

    // --- Public API --------------------------------------------------------------------
    public static void Splash(Vector3 pos, float strength)
    {
        if (instance == null) return;
        int n = Mathf.RoundToInt(Mathf.Lerp(8, 40, Mathf.Clamp01(strength)));
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float s = Random.Range(1.5f, 4f) * (0.6f + strength);
            Vector3 v = new Vector3(Mathf.Cos(a) * s, Random.Range(3f, 7f) * (0.6f + strength), Mathf.Sin(a) * s);
            instance.Spawn(instance.spray, pos, v, Random.Range(0.15f, 0.3f), Random.Range(0.7f, 1.2f), Color.white);
        }
    }

    public static void SparkBurst(Vector3 pos)
    {
        if (instance == null) return;
        for (int i = 0; i < 35; i++)
            instance.Spawn(instance.sparks, pos, Random.onUnitSphere * Random.Range(4f, 9f), Random.Range(0.08f, 0.16f), Random.Range(0.3f, 0.6f), Color.white);
    }

    public static void Explosion(Vector3 pos)
    {
        if (instance == null) return;
        instance.dead = true;
        for (int i = 0; i < 45; i++)
            instance.Spawn(instance.fire, pos, Random.insideUnitSphere * 6f + Vector3.up * 2f, Random.Range(0.8f, 1.8f), Random.Range(0.6f, 1.2f), Color.white);
        for (int i = 0; i < 35; i++)
            instance.Spawn(instance.smoke, pos, Random.insideUnitSphere * 3f + Vector3.up * 2.5f, Random.Range(1.2f, 2.5f), Random.Range(1.5f, 2.8f), Color.white);
        for (int i = 0; i < 60; i++)
            instance.Spawn(instance.sparks, pos, Random.onUnitSphere * Random.Range(6f, 14f), Random.Range(0.08f, 0.18f), Random.Range(0.4f, 0.9f), Color.white);
    }

    // --- Setup -------------------------------------------------------------------------
    void Awake()
    {
        instance = this;
        Setup();
    }

    // Slots you filled are used as-is (you control look, shape, size, color, material, lifetime).
    // Empty slots get a built-in default.
    void Setup()
    {
        authored.Clear();
        Prepare(ref spray, "Spray", false, 1.6f, false, Fade(new Color(0.85f, 0.95f, 1f), new Color(0.7f, 0.9f, 1f), 0.75f), AnimationCurve.Linear(0, 1, 1, 0.6f));
        Prepare(ref smoke, "Smoke", false, -0.15f, false, Fade(new Color(0.25f, 0.25f, 0.27f), new Color(0.12f, 0.12f, 0.13f), 0.55f), AnimationCurve.Linear(0, 0.5f, 1, 2.2f));
        Prepare(ref steam, "Steam", false, -0.25f, false, Fade(Color.white, new Color(0.9f, 0.95f, 1f), 0.45f), AnimationCurve.Linear(0, 0.5f, 1, 2f));
        Prepare(ref fire, "Fire", true, -0.4f, false, Fade(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.2f, 0.05f), 1f), AnimationCurve.Linear(0, 1, 1, 0.2f));
        Prepare(ref sparks, "Sparks", true, 1.2f, true, Fade(new Color(0.8f, 1f, 1f), new Color(0.2f, 0.7f, 1f), 1f), AnimationCurve.Linear(0, 1, 1, 0.2f));
    }

    void Prepare(ref ParticleSystem ps, string name, bool additive, float gravity, bool stretched, Gradient color, AnimationCurve size)
    {
        if (ps == null)
        {
            ps = Make(name, additive, gravity, stretched, color, size);
            return;
        }

        // Your system: we only force the bits the manager needs to drive it.
        authored.Add(ps);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // so it stays behind as the world scrolls
        main.playOnAwake = false;
        var emission = ps.emission; emission.enabled = false;          // the manager emits, not the timeline
        var vol = ps.velocityOverLifetime;                             // used to drift particles with the world
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        ps.Play();
    }

#if UNITY_EDITOR
    // Right-click the component header > "Generate Editable Effects": creates the 5 child systems
    // (with saved material assets) so you can restyle them in the editor.
    [ContextMenu("Generate Editable Effects")]
    void GenerateEditable()
    {
        spray = smoke = fire = steam = sparks = null;
        Setup();
        const string dir = "Assets/Surfryder/Particles/Materials";
        if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/Surfryder/Particles", "Materials");
        foreach (var ps in new[] { spray, smoke, fire, steam, sparks })
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(r.sharedMaterial);
            UnityEditor.AssetDatabase.CreateAsset(mat, dir + "/" + ps.name + ".mat");
            r.sharedMaterial = mat;
        }
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        authored.Clear();
    }
#endif

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    static Gradient Fade(Color start, Color end, float peakAlpha)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.12f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    ParticleSystem Make(string name, bool additive, float gravity, bool stretched, Gradient color, AnimationCurve size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.loop = true;
        main.maxParticles = 2500;
        main.gravityModifier = gravity;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = 1f;

        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = 0f; vol.y = 0f; vol.z = 0f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(color);

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, size);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = MakeMaterial(additive);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (stretched)
        {
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2f;
            r.velocityScale = 0.06f;
        }
        else r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        return ps;
    }

    static Material MakeMaterial(bool additive)
    {
        Shader sh = Resources.Load<Shader>("SurfRyderParticle");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(sh);
        if (m.HasProperty("_SrcBlend"))
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        }
        return m;
    }

    // --- Per frame ---------------------------------------------------------------------
    void LateUpdate()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
            if (player == null) return;
        }

        // Make every particle drift with the moving world (the player never actually moves).
        float vx = dead ? 0f : -player.SidewaysVelocity;
        float vz = dead ? 0f : -player.ForwardVelocity;
        foreach (var ps in new[] { spray, smoke, fire, steam, sparks })
        {
            var v = ps.velocityOverLifetime;
            v.x = vx; v.y = 0f; v.z = vz;
        }

        if (dead) return;

        float dt = Time.deltaTime;
        Vector3 pos = player.transform.position;
        float speedFrac = player.ForwardSpeed > 0.01f ? Mathf.Clamp01(player.ForwardVelocity / player.ForwardSpeed) : 0f;
        float heat = player.HeatPercent;

        // Ocean spray: flung up from the hull while skimming
        if (player.OnWater)
        {
            float rate = sprayRate * speedFrac + (player.IsBoosting ? boostSprayBonus : 0f);
            int n = Take(ref sprayAcc, rate, dt);
            Vector3 surface = new Vector3(pos.x, WaterFloor.SurfaceAt(pos), pos.z);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = surface + player.transform.right * Random.Range(-0.9f, 0.9f) + player.transform.forward * Random.Range(0.4f, 1.6f);
                Vector3 v = new Vector3(Random.Range(-2f, 2f), Random.Range(1.5f, 3.5f), Random.Range(-0.5f, 1.5f));
                Spawn(spray, p, v, Random.Range(0.1f, 0.25f), Random.Range(0.5f, 0.9f), Color.white);
            }
        }

        Vector3 exhaust = player.transform.TransformPoint(exhaustOffset);

        // Smoke: grows with heat
        if (heat > smokeStartHeat)
        {
            float t = Mathf.InverseLerp(smokeStartHeat, 1f, heat);
            int n = Take(ref smokeAcc, smokeMaxRate * t, dt);
            for (int i = 0; i < n; i++)
                Spawn(smoke, exhaust + Random.insideUnitSphere * 0.3f, new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(1f, 2.2f), 0f),
                      Random.Range(0.5f, 0.9f), Random.Range(1.2f, 2.2f), Color.white);
        }

        // Fire: only when about to overheat
        if (heat > fireStartHeat)
        {
            float t = Mathf.InverseLerp(fireStartHeat, 1f, heat);
            int n = Take(ref fireAcc, fireMaxRate * t, dt);
            for (int i = 0; i < n; i++)
                Spawn(fire, exhaust + Random.insideUnitSphere * 0.25f, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(1.5f, 3f), 0f),
                      Random.Range(0.35f, 0.7f), Random.Range(0.35f, 0.7f), Color.white);
        }

        // Steam: vented from the brake flaps
        if (player.IsBraking && heat > 0.02f)
        {
            int n = Take(ref steamAcc, steamRate, dt);
            for (int i = 0; i < n; i++)
            {
                Transform flap = (i % 2 == 0) ? player.LeftFlapPivot : player.RightFlapPivot;
                Vector3 p = flap != null ? flap.position : exhaust;
                Spawn(steam, p + Random.insideUnitSphere * 0.15f, new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 3f), Random.Range(-0.5f, 0.5f)),
                      Random.Range(0.3f, 0.6f), Random.Range(0.7f, 1.2f), Color.white);
            }
        }

        // Electric sparks: crackle when energy is low
        if (player.EnergyPercent < lowEnergy)
        {
            float t = 1f - player.EnergyPercent / lowEnergy;
            int n = Take(ref sparkAcc, sparkRate * (0.4f + t), dt);
            for (int i = 0; i < n; i++)
                Spawn(sparks, pos + Random.insideUnitSphere * 1.1f, Random.onUnitSphere * Random.Range(2.5f, 6f),
                      Random.Range(0.06f, 0.12f), Random.Range(0.12f, 0.3f), Color.white);
        }
    }

    static int Take(ref float acc, float rate, float dt)
    {
        acc += rate * dt;
        int n = (int)acc;
        acc -= n;
        return n;
    }

    void Spawn(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color)
    {
        var e = new ParticleSystem.EmitParams();
        e.position = pos;
        if (authored.Contains(ps))
        {
            e.applyShapeToPosition = true;   // your Shape module (direction, spread, speed) is applied around pos
        }
        else
        {
            e.velocity = vel;
            e.startSize = size;
            e.startLifetime = life;
            e.startColor = color;
        }
        ps.Emit(e, 1);
    }
}
