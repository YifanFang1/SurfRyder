using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Colored-bar HUD for the player's energy and heat (no text).
// Creates itself automatically in any scene that contains a Player, so no scene setup is needed.
public class MeterBars : MonoBehaviour
{
    [Header("Layout (reference resolution 1920x1080)")]
    [SerializeField] float barWidth = 600f;
    [SerializeField] float barHeight = 24f;
    [SerializeField] float bottomMargin = 30f;
    [SerializeField] float barSpacing = 10f;

    [Header("Colors")]
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] Color energyColor = new Color(0.2f, 0.9f, 1f);
    [SerializeField] Color energyLowColor = new Color(1f, 0.25f, 0.25f);
    [SerializeField] float energyLowThreshold = 0.25f;
    [SerializeField] Color heatCoolColor = new Color(1f, 0.9f, 0.2f);
    [SerializeField] Color heatHotColor = new Color(1f, 0.1f, 0.05f);

    Player player;
    RectTransform energyFill, heatFill;
    Image energyImage, heatImage;

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
        if (FindFirstObjectByType<MeterBars>() != null) return;
        if (FindFirstObjectByType<MeterUI>() != null) return;   // a hand-built HUD already exists
        new GameObject("MeterBars").AddComponent<MeterBars>();
    }

    // --- Build the UI ----------------------------------------------------------------------
    void Start()
    {
        player = FindFirstObjectByType<Player>();

        var canvasObj = new GameObject("MeterCanvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Energy on the bottom row, heat just above it.
        BuildBar(canvasObj.transform, "EnergyBar", bottomMargin, out energyFill, out energyImage, energyColor);
        BuildBar(canvasObj.transform, "HeatBar", bottomMargin + barHeight + barSpacing, out heatFill, out heatImage, heatCoolColor);
    }

    void BuildBar(Transform parent, string name, float y, out RectTransform fill, out Image fillImage, Color color)
    {
        // Background
        var bg = new GameObject(name + "_BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        var bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);
        bgRect.anchoredPosition = new Vector2(0f, y);
        bg.GetComponent<Image>().color = backgroundColor;

        // Fill: stretches to the full background, then its right anchor is scaled by the meter %.
        var fillObj = new GameObject(name + "_Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(bg.transform, false);
        fill = (RectTransform)fillObj.transform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fillImage = fillObj.GetComponent<Image>();
        fillImage.color = color;
    }

    // --- Update the bars -------------------------------------------------------------------
    void Update()
    {
        if (player == null || energyFill == null) return;

        float energy = Mathf.Clamp01(player.EnergyPercent);
        float heat = Mathf.Clamp01(player.HeatPercent);

        energyFill.anchorMax = new Vector2(energy, 1f);
        heatFill.anchorMax = new Vector2(heat, 1f);

        energyImage.color = energy <= energyLowThreshold ? energyLowColor : energyColor;
        heatImage.color = Color.Lerp(heatCoolColor, heatHotColor, heat);
    }
}
