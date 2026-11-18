using UnityEngine;
using UnityEngine.UI;

// Drives the heat and energy bars on the HUD.
// Setup: Canvas > two UI Sliders (or Images set to "Filled"), drag them in below.
public class MeterUI : MonoBehaviour
{
    [SerializeField] Player player;

    [Header("Use Sliders...")]
    [SerializeField] Slider heatSlider;
    [SerializeField] Slider energySlider;

    [Header("...or Filled Images")]
    [SerializeField] Image heatFill;
    [SerializeField] Image energyFill;

    [Header("Heat bar colors")]
    [SerializeField] Color coolColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] Color hotColor = new Color(1f, 0.15f, 0.1f);

    void Start()
    {
        if (player == null && GameManager.gameManager != null)
            player = GameManager.gameManager.player.GetComponent<Player>();

        if (heatSlider != null) { heatSlider.minValue = 0f; heatSlider.maxValue = 1f; heatSlider.interactable = false; }
        if (energySlider != null) { energySlider.minValue = 0f; energySlider.maxValue = 1f; energySlider.interactable = false; }
    }

    void Update()
    {
        if (player == null) return;

        float heat = player.HeatPercent;
        float energy = player.EnergyPercent;

        if (heatSlider != null) heatSlider.value = heat;
        if (energySlider != null) energySlider.value = energy;

        if (heatFill != null)
        {
            heatFill.fillAmount = heat;
            heatFill.color = Color.Lerp(coolColor, hotColor, heat);
        }
        if (energyFill != null) energyFill.fillAmount = energy;
    }
}
