using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameUIController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _waitingPanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private GameObject _hudPanel;

    [Header("HUD Generation")]
    [SerializeField] private AbilitySlotUI _slotPrefab; 
    [SerializeField] private Transform _hudContainer;   

    [Header("Icons")]
    [SerializeField] private Sprite _meleeIcon;
    [SerializeField] private Sprite _rangeIcon;
    [SerializeField] private Sprite _medkitIcon;

    [Header("Pause Buttons")]
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _disconnectButton;

    private AbilitySlotUI[] _slots;
    private bool _isPaused = false;

    private void Start()
    {
        _waitingPanel.SetActive(true);
        _pausePanel.SetActive(false);
        _hudPanel.SetActive(true);

        _resumeButton.onClick.AddListener(TogglePause);
        _disconnectButton.onClick.AddListener(DisconnectToMenu);

        GenerateSlots();
    }

    private void GenerateSlots()
    {
        foreach (Transform child in _hudContainer) Destroy(child.gameObject);

        _slots = new AbilitySlotUI[3];

        _slots[0] = Instantiate(_slotPrefab, _hudContainer);
        _slots[0].Setup(_meleeIcon, "LMB");

        _slots[1] = Instantiate(_slotPrefab, _hudContainer);
        _slots[1].Setup(_rangeIcon, "RMB");

        _slots[2] = Instantiate(_slotPrefab, _hudContainer);
        _slots[2].Setup(_medkitIcon, "Q");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
    }

    public void HideWaitingPanel() => _waitingPanel.SetActive(false);

    public void UpdateCooldowns(float meleeNorm, float rangeNorm, float medkitNorm)
    {
        if (_slots == null || _slots.Length < 3) return;

        _slots[0].SetCooldown(meleeNorm);
        _slots[1].SetCooldown(rangeNorm);
        _slots[2].SetCooldown(medkitNorm);
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        _pausePanel.SetActive(_isPaused);
    }

    private void DisconnectToMenu()
    {
        var gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.OnExitButtonPress();
        }
        else
        {
            NetworkManager.Instance.Shutdown();
            SceneManager.LoadScene("MenuScene");
        }
    }

    public bool IsPaused => _isPaused;
}