using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayerSlot : MonoBehaviour
{
    [Header("Visuals")]
    public GameObject ModelObject;
    public MeshRenderer Renderer;

    [Header("UI Info")]
    public GameObject InfoPanel;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI ReadyStatusText;

    [Header("UI Controls")]
    public Button ChangeSlotButton;
    public Button ReadyButton;

    public Button[] GearButtons;
    public TextMeshProUGUI[] GearLevelTexts;

    [Header("Settings")]
    public string[] GearNames = new string[] { "Меч", "Лук", "Броня", "Аптечка" };

    [Header("Gear Colors")]
    public Color[] LevelColors = new Color[] { Color.white, Color.green, Color.cyan, Color.yellow };

    [Header("Economy UI")]
    public TextMeshProUGUI PointsText; 

    [Header("Positioning")]
    public RectTransform SphereAnchor; 
    public float DistanceFromCamera = 5.0f;
    public void SetVisual(bool isActive, Color color, string name, bool isReady)
    {
        ModelObject.SetActive(isActive);
        InfoPanel.SetActive(isActive);

        if (isActive)
        {
            NameText.gameObject.SetActive(true);
            ReadyStatusText.gameObject.SetActive(true);

            Renderer.material.color = color;
            NameText.text = name;
            ReadyStatusText.text = isReady ? "ГОТОВ" : "Не готов";
            ReadyStatusText.color = isReady ? Color.green : Color.red;
        }
    }

    public void UpdateControls(bool isMine, int[] gearLevels)
    {
        ChangeSlotButton.gameObject.SetActive(false); 

        ReadyButton.gameObject.SetActive(isMine);

        for (int i = 0; i < 4; i++)
        {
            GearButtons[i].interactable = isMine;
            GearButtons[i].gameObject.SetActive(true);

            string gearName = (i < GearNames.Length) ? GearNames[i] : "Item";
            int lvl = gearLevels[i] + 1;

            GearLevelTexts[i].text = $"{gearName}\nLvl {lvl}";

            var btnImage = GearButtons[i].GetComponent<Image>();
            if (btnImage != null)
            {
                int colorIndex = Mathf.Clamp(gearLevels[i], 0, LevelColors.Length - 1);
                btnImage.color = LevelColors[colorIndex];
            }

            PointsText.gameObject.SetActive(true);
            int usedPoints = 0;
            foreach (int _lvl in gearLevels) usedPoints += _lvl;
            int maxPoints = 4;
            int remaining = maxPoints - usedPoints;

            PointsText.text = $"Очки: {remaining}/{maxPoints}";

            PointsText.color = (remaining == 0) ? Color.red : Color.black;
        }
    }
    private void LateUpdate()
    {
        if (ModelObject != null && ModelObject.activeSelf && SphereAnchor != null)
        {
            Vector3 screenPosition = SphereAnchor.position;

            screenPosition.z = DistanceFromCamera;

            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);

            ModelObject.transform.position = worldPosition;

            ModelObject.transform.Rotate(Vector3.up * 50 * Time.deltaTime);
        }
    }
    public void ShowEmpty()
    {
        ModelObject.SetActive(false);
        NameText.gameObject.SetActive(false);
        ReadyStatusText.gameObject.SetActive(false);
        ReadyButton.gameObject.SetActive(false);

        foreach (var btn in GearButtons) btn.gameObject.SetActive(false);
        if (PointsText != null)
            PointsText.gameObject.SetActive(false);
        InfoPanel.SetActive(true);
    }
}