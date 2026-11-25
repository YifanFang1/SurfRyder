using UnityEngine;

public class WaterFloor : MonoBehaviour
{
    [Header("Waves (shared by the shader and the physics)")]
    [SerializeField] float waveAmplitude = 0.6f;   // big rolling swells
    [SerializeField] float waveLength = 22f;
    [SerializeField] float waveSpeed = 1.0f;

    [Header("Under the Skimmer")]
    [SerializeField] float calmRadius = 2.5f;      // water is flat within this distance of the skimmer

    [Header("Jump / Landing Ripples")]
    [SerializeField] float rippleSpeed = 9f;       // how fast the ring expands (units/sec)
    [SerializeField] float rippleWavelength = 8f;  // distance between crests in the ring
    [SerializeField] float rippleDecay = 0.6f;     // how quickly it fades (per second)
    [SerializeField] float rippleWidth = 12f;      // half-width of the traveling wave packet
    [SerializeField] float rippleLifetime = 6f;

    [System.Serializable]
    public struct WaterPalette
    {
        public Color shallow, deep, foam;
        public WaterPalette(Color shallow, Color deep, Color foam) { this.shallow = shallow; this.deep = deep; this.foam = foam; }
    }

    [Header("Time-of-Day Colors (blended by the sun's height, like the sky)")]
    [SerializeField] WaterPalette dayColors = new WaterPalette(new Color(0.20f, 0.78f, 0.88f), new Color(0.03f, 0.20f, 0.50f), Color.white);
    [SerializeField] WaterPalette duskColors = new WaterPalette(new Color(0.95f, 0.55f, 0.45f), new Color(0.25f, 0.15f, 0.38f), new Color(1f, 0.9f, 0.85f));
    [SerializeField] WaterPalette nightColors = new WaterPalette(new Color(0.08f, 0.16f, 0.32f), new Color(0.01f, 0.03f, 0.11f), new Color(0.55f, 0.65f, 0.85f));

    [Header("Mesh")]
    [SerializeField] float size = 500f;        // width/length of the water plane
    [SerializeField] int quads = 160;          // grid resolution (160 x 160 quads)
    [SerializeField] float surfaceY = 0f;      // matches the physics water level in Player.WaterPhysics

    Transform player;
    Player playerScript;
    Material material;
    Vector2 scroll;
    SunScript sun;

    public static WaterFloor Instance { get; private set; }

    const int MaxRipples = 4;
    readonly Vector4[] ripples = new Vector4[MaxRipples];   // xy origin (pattern space), z start time, w amplitude
    int nextRipple;

    static readonly int RipplesId = Shader.PropertyToID("_Ripples");
    static readonly int RippleParamsId = Shader.PropertyToID("_RippleParams");
    static readonly int WaterTimeId = Shader.PropertyToID("_WaterTime");
    static readonly int SkimmerPosId = Shader.PropertyToID("_SkimmerPos");
    static readonly int WaterScrollId = Shader.PropertyToID("_WaterScroll");

    // --- Auto-create in any scene that has a Player ----------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindFirstObjectByType<Player>() == null) return;
        if (FindFirstObjectByType<WaterFloor>() != null) return;
        new GameObject("WaterFloor").AddComponent<WaterFloor>();
    }

    // --- Setup -------------------------------------------------------------------------------
    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        playerScript = FindFirstObjectByType<Player>();
        player = playerScript.transform;

        gameObject.layer = LayerMask.NameToLayer("Water") >= 0 ? LayerMask.NameToLayer("Water") : 0;

        var mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildGrid();

        Shader shader = Resources.Load<Shader>("SurfRyderWater");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("WaterFloor: SurfRyderWater shader missing or unsupported, using a plain blue fallback.");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        material = new Material(shader);
        material.SetFloat("_WaveAmp", waveAmplitude);
        material.SetFloat("_WaveLength", waveLength);
        material.SetFloat("_WaveSpeed", waveSpeed);
        material.SetFloat("_CalmRadius", calmRadius);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.1f, 0.4f, 0.8f));

        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        transform.position = new Vector3(player.position.x, surfaceY, player.position.z);
    }

    Mesh BuildGrid()
    {
        int verts = (quads + 1) * (quads + 1);
        var vertices = new Vector3[verts];
        var triangles = new int[quads * quads * 6];
        float cell = size / quads;
        float half = size * 0.5f;

        for (int z = 0, i = 0; z <= quads; z++)
            for (int x = 0; x <= quads; x++, i++)
                vertices[i] = new Vector3(x * cell - half, 0f, z * cell - half);

        for (int z = 0, t = 0, v = 0; z < quads; z++, v++)
        {
            for (int x = 0; x < quads; x++, v++, t += 6)
            {
                triangles[t] = v;
                triangles[t + 1] = v + quads + 1;
                triangles[t + 2] = v + quads + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + quads + 2;
                triangles[t + 5] = v + 1;
            }
        }

        var mesh = new Mesh { name = "WaterGrid" };
        mesh.indexFormat = verts > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 10f, size));
        return mesh;
    }

    // --- Per frame ---------------------------------------------------------------------------
    void LateUpdate()
    {
        if (player == null) return;

        float cell = size / quads;
        transform.position = new Vector3(
            Mathf.Round(player.position.x / cell) * cell,
            surfaceY,
            Mathf.Round(player.position.z / cell) * cell);

        GameManager gm = GameManager.gameManager;
        float speedRatio = 1f;
        if (gm != null)
        {
            if (!gm.gameOver)
                scroll += new Vector2(gm.sidewaysVelocity, gm.forwardVelocity) * Time.deltaTime;
            if (playerScript != null && playerScript.ForwardSpeed > 0.01f)
                speedRatio = gm.forwardVelocity / playerScript.ForwardSpeed;
        }

        // Water color follows the time of day (0 night, 1 dusk, 2 day).
        if (sun == null) sun = FindFirstObjectByType<SunScript>();
        ApplyPalette(sun != null ? sun.SkyPhase : 2f);

        // Retire finished ripples, then upload the rest.
        for (int i = 0; i < MaxRipples; i++)
            if (ripples[i].w > 0f && Time.time - ripples[i].z > rippleLifetime) ripples[i].w = 0f;

        Shader.SetGlobalVectorArray(RipplesId, ripples);
        Shader.SetGlobalVector(RippleParamsId, new Vector4(rippleSpeed, rippleWavelength, rippleDecay, rippleWidth));
        Shader.SetGlobalFloat(WaterTimeId, Time.time);
        Shader.SetGlobalVector(WaterScrollId, new Vector4(scroll.x, scroll.y, 0f, 0f));
        Shader.SetGlobalVector(SkimmerPosId, new Vector4(player.position.x, player.position.y, player.position.z, speedRatio));
    }

    void ApplyPalette(float phase)
    {
        if (material == null) return;
        WaterPalette a = phase < 1f ? nightColors : duskColors;
        WaterPalette b = phase < 1f ? duskColors : dayColors;
        float t = phase < 1f ? phase : phase - 1f;
        material.SetColor("_ShallowColor", Color.Lerp(a.shallow, b.shallow, t));
        material.SetColor("_DeepColor", Color.Lerp(a.deep, b.deep, t));
        material.SetColor("_FoamColor", Color.Lerp(a.foam, b.foam, t));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (material != null) Destroy(material);
    }

    // ---- CPU copy of the shader's wave function: keep this identical to Waves() in SurfRyderWater.shader ----
    static readonly Vector2[] WaveDirs =
    {
        new Vector2(0.80f, 0.60f).normalized,
        new Vector2(-0.40f, 0.90f).normalized,
        new Vector2(0.95f, -0.30f).normalized,
    };
    static readonly float[] WaveLenScale = { 1.00f, 0.60f, 0.37f };
    static readonly float[] WaveAmpScale = { 1.00f, 0.55f, 0.30f };
    static readonly float[] WaveSpeedScale = { 1.0f, 1.3f, 1.7f };

    // Starts a sine-wave ring spreading out from a world position (jump or landing splash).
    public void AddRipple(Vector3 worldPosition, float amplitude)
    {
        // Stored in "pattern space" so the ring drifts with the world like everything else.
        ripples[nextRipple] = new Vector4(worldPosition.x + scroll.x, worldPosition.z + scroll.y, Time.time, amplitude);
        nextRipple = (nextRipple + 1) % MaxRipples;
    }

    public static void Splash(Vector3 worldPosition, float amplitude)
    {
        if (Instance != null) Instance.AddRipple(worldPosition, amplitude);
    }

    public float HeightAt(float x, float z)
    {
        float px = x + scroll.x;
        float pz = z + scroll.y;
        float h = 0f;
        for (int i = 0; i < 3; i++)
        {
            float k = 2f * Mathf.PI / (waveLength * WaveLenScale[i]);
            float a = waveAmplitude * WaveAmpScale[i];
            float phase = (WaveDirs[i].x * px + WaveDirs[i].y * pz) * k + Time.time * waveSpeed * WaveSpeedScale[i];
            h += a * Mathf.Sin(phase);
        }
        return surfaceY + h;
    }

    public static float SurfaceAt(Vector3 position)
    {
        return Instance != null ? Instance.HeightAt(position.x, position.z) : 0f;
    }
}
