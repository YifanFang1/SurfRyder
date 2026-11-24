using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// General / SFX / Music volume sliders for the Options page.
/// The game's volumes live in AudioManager (not the mixer), so these call its setters and also save to
/// the same PlayerPrefs keys it reads on startup. That means it works even when the AudioManager isn't in
/// the Options scene yet: the saved values are applied as soon as it loads.
/// Auto-creates itself in any scene whose name contains "Options", and builds its own sliders unless you
/// assign your own Slider objects in the inspector (place the component on any object in that case).
/// </summary>
public class VolumeSettings : MonoBehaviour
{
    [Header("Optional: assign your own sliders (0..1). Leave empty to auto-build.")]
    [SerializeField] Slider generalSlider;
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider musicSlider;

    // Same keys AudioManager uses
    const string GeneralKey = "MasterVol";
    const string SfxKey = "SfxVol";
    const string MusicKey = "MusicVol";

    // --- Auto-create in the Options scene -----------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (!SceneManager.GetActiveScene().name.Contains("Options")) return;
        if (FindFirstObjectByType<VolumeSettings>() != null) return;
        new GameObject("VolumeSettings").AddComponent<VolumeSettings>();
    }

    // --- Setup ----------------------------------------------------------------------------
    void Start()
    {
        if (generalSlider == null && sfxSlider == null && musicSlider == null) BuildRows();

        Hook(generalSlider, GeneralKey, 1f, v => { if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(v); });
        Hook(sfxSlider, SfxKey, 1f, v => { if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(v); });
        Hook(musicSlider, MusicKey, 1f, v => { if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v); });
    }

    void Hook(Slider slider, string key, float fallback, System.Action<float> apply)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, fallback));
        slider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(key, v);   // survives scene changes and restarts
            apply(v);                        // applies live if the AudioManager exists right now
        });
    }

    void OnDisable() { PlayerPrefs.Save(); }

    // --- Auto-built UI (dark bar, cyan fill, white handle, no sprites needed) -------------
    void BuildRows()
    {
        Transform parent = null;
        var layout = GameObject.Find("Layout");
        if (layout != null) parent = layout.transform;   // the Options page's vertical layout: rows slot in below the title
        else
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var box = new GameObject("VolumeRows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            box.transform.SetParent(canvas.transform, false);
            ((RectTransform)box.transform).sizeDelta = new Vector2(400, 200);
            parent = box.transform;
        }

        generalSlider = BuildRow(parent, "General", 1);
        sfxSlider = BuildRow(parent, "SFX", 2);
        musicSlider = BuildRow(parent, "Music", 3);
    }

    Slider BuildRow(Transform parent, string label, int siblingIndex)
    {
        var row = new GameObject(label + " Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        ((RectTransform)row.transform).sizeDelta = new Vector2(360, 40);
        row.transform.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));

        // Label
        var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(row.transform, false);
        var t = textObj.GetComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 22;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch((RectTransform)textObj.transform, new Vector2(0f, 0f), new Vector2(0.3f, 1f), Vector2.zero, Vector2.zero);

        // Slider
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(row.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.35f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 20f);

        var bg = NewImage("Background", go.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
        Stretch(bg.rectTransform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Stretch((RectTransform)fillArea.transform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(5f, 0f), new Vector2(-15f, 0f));
        var fill = NewImage("Fill", fillArea.transform, new Color(0.2f, 0.85f, 1f, 1f));
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Stretch((RectTransform)handleArea.transform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        var handle = NewImage("Handle", handleArea.transform, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }
}
