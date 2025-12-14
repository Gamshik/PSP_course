using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilitySlotUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image _iconImage;      
    [SerializeField] private Image _cooldownOverlay;
    [SerializeField] private TextMeshProUGUI _keyText;

    public void Setup(Sprite icon, string keyName)
    {
        if (_iconImage == null)
        {
            Debug.LogError("ОШИБКА: В префабе AbilitySlotUI не назначена ссылка на Icon Image!");
            return;
        }

        if (icon == null)
        {
            Debug.LogError($"ОШИБКА: Иконка для {keyName} не передана (null)!");
        }
        else
        {
            _iconImage.sprite = icon;

            _iconImage.color = Color.white;
        }

        _keyText.text = keyName;
        _cooldownOverlay.fillAmount = 0;

    }

    public void SetCooldown(float normalizedTime)
    {
        _cooldownOverlay.fillAmount = normalizedTime;
    }
}