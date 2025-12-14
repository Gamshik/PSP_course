using UnityEngine;
using TMPro;

public class PuzzlePlateVisual : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI PercentText;
    public Transform CanvasRoot; 

    [Header("Visuals")]
    public Renderer PlateRenderer;
    public Color EmptyColor = Color.red;
    public Color ChargingColor = Color.yellow;
    public Color FullColor = Color.green;

    private void LateUpdate()
    {
        if (CanvasRoot != null && Camera.main != null)
            CanvasRoot.rotation = Camera.main.transform.rotation;
    }

    public void UpdateState(float progress, bool isStanding)
    {
        PercentText.text = $"{Mathf.FloorToInt(progress)}%";

        if (progress >= 100f)
        {
            PlateRenderer.material.color = FullColor;
            PercentText.color = Color.green;
        }
        else
        {
            PlateRenderer.material.color = isStanding ? ChargingColor : EmptyColor;
            PercentText.color = Color.white;
        }
    }
}