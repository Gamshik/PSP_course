using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ColorPuzzleVisuals : MonoBehaviour
{
    [Header("References")]
    public Renderer[] Pillars;
    public Renderer CentralPillar;
    public Transform UIContainer;

    [Header("UI")]
    public TextMeshProUGUI TimerText;
    public TextMeshProUGUI StreakText;
    public TextMeshProUGUI StateText;

    [Header("Colors")]
    public Color NeutralColor = Color.gray;
    public Color SuccessColor = Color.green; 
    public Color[] Palette = { Color.red, Color.blue, Color.green, Color.yellow };

    private void LateUpdate()
    {
        if (UIContainer != null && Camera.main != null)
            UIContainer.rotation = Camera.main.transform.rotation;
    }

    public void UpdateState(float timer, int streak, int activeIndex, int phase)
    {
        gameObject.SetActive(true);
        StreakText.text = $"Streak: {streak}/2";

        switch (phase)
        {
            case 0: // ПОКАЗ
                StateText.text = "ЗАПОМИНАЙ";
                StateText.color = Color.yellow;
                TimerText.text = timer.ToString("F1");
                break;

            case 1: // ВВОД
                StateText.text = "ПОВТОРЯЙТЕ";
                StateText.color = Color.white;
                TimerText.text = "";
                break;

            case 2: // ОШИБКА
                StateText.text = "ОШИБКА!";
                StateText.color = Color.red;
                TimerText.text = "!!!";
                break;

            case 3: // УСПЕХ (Пауза)
                StateText.text = "ОТЛИЧНО!";
                StateText.color = Color.green;
                TimerText.text = timer.ToString("F1"); 
                break;
        }

        if (phase == 2) // Ошибка
        {
            CentralPillar.material.color = Color.red;
        }
        else if (phase == 3) // Успех
        {
            CentralPillar.material.color = SuccessColor; 
        }
        else if (activeIndex != -1)
        {
            CentralPillar.material.color = Palette[activeIndex];
        }
        else
        {
            CentralPillar.material.color = NeutralColor;
        }

        for (int i = 0; i < Pillars.Length; i++)
            if (i < Palette.Length) Pillars[i].material.color = Palette[i];
    }
}