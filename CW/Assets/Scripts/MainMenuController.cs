using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Net;
using System.Net.Sockets;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private GameObject _connectionPanel;

    [Header("Error Popup")]
    [SerializeField] private GameObject _errorPopupPanel;
    [SerializeField] private TextMeshProUGUI _errorPopupText;
    [SerializeField] private Button _errorOkButton;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _connectButton;
    [SerializeField] private Button _exitButton;

    [Header("Connection Panel UI")]
    [SerializeField] private TMP_InputField _ipInput;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private Button _startGameButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private TextMeshProUGUI _inlineErrorText;

    private bool _isHosting;
    private const string PREF_NICKNAME = "PlayerNickname";

    private void Start()
    {
        // Инициализация UI
        _mainPanel.SetActive(true);
        _connectionPanel.SetActive(false);
        _errorPopupPanel.SetActive(false); 
        _inlineErrorText.text = "";

        // Загрузка ника
        if (PlayerPrefs.HasKey(PREF_NICKNAME))
        {
            _nameInput.text = PlayerPrefs.GetString(PREF_NICKNAME);
        }

        _ipInput.text = "127.0.0.1";

        // Подписка на события кнопок
        _hostButton.onClick.AddListener(HandleHostClick);
        _connectButton.onClick.AddListener(HandleConnectClick);
        _exitButton.onClick.AddListener(HandleExitClick);

        _startGameButton.onClick.AddListener(HandleStartGameClick);
        _backButton.onClick.AddListener(HandleBackClick);

        _errorOkButton.onClick.AddListener(CloseErrorPopup);

        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.DisconnectReason))
        {
            string reason = NetworkManager.Instance.DisconnectReason;

            if (reason == "VICTORY")
            {
                ShowErrorPopup("ПОБЕДА!\nВы прошли подземелье!");

                if (_errorPopupText != null)
                    _errorPopupText.color = Color.green;
            }
            else
            {
                ShowErrorPopup(reason);

                if (_errorPopupText != null)
                    _errorPopupText.color = Color.red;
            }

            NetworkManager.Instance.DisconnectReason = "";
        }
    }

    private void OnDestroy()
    {
        _hostButton.onClick.RemoveAllListeners();
        _connectButton.onClick.RemoveAllListeners();
        _exitButton.onClick.RemoveAllListeners();
        _startGameButton.onClick.RemoveAllListeners();
        _backButton.onClick.RemoveAllListeners();
        _errorOkButton.onClick.RemoveAllListeners();
    }

    private void HandleHostClick()
    {
        _isHosting = true;
        OpenConnectionPanel();

        string myIp = GetLocalIPAddress();
        _ipInput.interactable = false;
        _ipInput.text = myIp;
    }

    private void HandleConnectClick()
    {
        _isHosting = false;
        OpenConnectionPanel();

        _ipInput.interactable = true;
        if (_ipInput.text == "Localhost (You)")
            _ipInput.text = "127.0.0.1";
    }

    private void HandleExitClick()
    {
        Debug.Log("Exit Game");
        Application.Quit();
    }

    private void HandleBackClick()
    {
        _connectionPanel.SetActive(false);
        _mainPanel.SetActive(true);
        _inlineErrorText.text = "";
    }

    private void HandleStartGameClick()
    {
        string nickname = _nameInput.text.Trim();
        string ip = _ipInput.text.Trim();

        if (string.IsNullOrEmpty(nickname))
        {
            ShowInlineError("Введите имя игрока!");
            return;
        }

        if (!_isHosting && string.IsNullOrEmpty(ip))
        {
            ShowInlineError("Введите IP адрес!");
            return;
        }

        PlayerPrefs.SetString(PREF_NICKNAME, nickname);
        PlayerPrefs.Save();

        SetUIInteractable(false);
        ShowInlineError("Подключение...");

        if (_isHosting)
        {
            NetworkManager.Instance.StartHost(nickname);
        }
        else
        {
            NetworkManager.Instance.ConnectToServer(ip, nickname, OnConnectionFailed);
        }
    }

    private void OnConnectionFailed(string errorMessage)
    {
        SetUIInteractable(true);

        ShowErrorPopup(errorMessage);
        ShowInlineError("");
    }
    private void SetUIInteractable(bool state)
    {
        _startGameButton.interactable = state;
        _backButton.interactable = state;
        _ipInput.interactable = state;
        _nameInput.interactable = state;
    }

    private void ShowErrorPopup(string message)
    {
        _errorPopupPanel.SetActive(true);
        _errorPopupText.text = message;
    }

    private void CloseErrorPopup()
    {
        _errorPopupPanel.SetActive(false);
    }

    private void ShowInlineError(string message)
    {
        _inlineErrorText.text = message;
    }

    private void OpenConnectionPanel()
    {
        _mainPanel.SetActive(false);
        _connectionPanel.SetActive(true);
        _inlineErrorText.text = "";
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error fetching IP: " + e.Message);
        }
        return "127.0.0.1";
    }
}