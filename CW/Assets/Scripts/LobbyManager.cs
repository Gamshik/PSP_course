using System; // Обязательно для DateTime
using System.Collections.Generic;
using System.IO;
using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LobbyPlayerSlot[] _slots; 
    [SerializeField] private Transform[] _spawnPoints; 

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _consoleText;
    [SerializeField] private Button _exitButton;
    [Header("Server Info UI")]
    [SerializeField] private TextMeshProUGUI _ipDisplayText; 
    [SerializeField] private Button _copyIpButton;           


    [Header("Settings")]
    public Color[] PlayerColors = new Color[] { Color.red, Color.blue, Color.green, Color.yellow };

    private List<PlayerInfo> _players = new List<PlayerInfo>();
    private float _timer;
    private float _keepAliveTimer;

    private DateTime _lastServerHeartbeat;

    private string _currentIpAddress;

    private void Start()
    {
        Application.runInBackground = true;

        NetworkManager.Instance.OnDataReceived += HandleData;

        SetupButtons();

        if (_exitButton != null)
            _exitButton.onClick.AddListener(OnClickExit);

        LogToConsole("Добро пожаловать в лобби!");

        _lastServerHeartbeat = DateTime.Now;

        if (!NetworkManager.Instance.IsServer)
        {
            SendJoinPacket();
        }
        else
        {
            PlayerInfo host = new PlayerInfo
            {
                Name = NetworkManager.Instance.LocalPlayerName,
                SlotIndex = 0,
                IsReady = false,
                GearLevels = new int[] { 0, 0, 0, 0 },
                IpEndpointStr = "Host",
                LastPacketTime = DateTime.Now
            };
            _players.Add(host);
            LogToConsole("Сервер запущен. Ожидание игроков...");
        }

        if (NetworkManager.Instance.IsServer)
        {
            _currentIpAddress = NetworkManager.GetLocalIPv4();
            if (_ipDisplayText != null)
                _ipDisplayText.text = $"IP: {_currentIpAddress}\n";

            if (_copyIpButton != null)
            {
                _copyIpButton.gameObject.SetActive(true);
                _copyIpButton.onClick.AddListener(CopyToClipboard);
            }
        }
        else
        {
            if (_ipDisplayText != null)
                _ipDisplayText.text = $"Connected to: {NetworkManager.Instance.ServerIp}";

            if (_copyIpButton != null) _copyIpButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnDataReceived -= HandleData;

        if (_exitButton != null) _exitButton.onClick.RemoveAllListeners();
    }

    private void Update()
    {
        UpdateVisuals();

        if (NetworkManager.Instance.IsServer)
        {
            ServerLogic();
        }
        else
        {
            _keepAliveTimer += Time.deltaTime;
            if (_keepAliveTimer > 1.0f)
            {
                _keepAliveTimer = 0;
                SendInput(255, 0);
            }

            double secondsSilence = (DateTime.Now - _lastServerHeartbeat).TotalSeconds;

            if (secondsSilence > 5.0 && _players.Count > 0)
            {
                ForceDisconnect("Связь с сервером потеряна (Timeout)");
            }
        }
    }
    private void SetupButtons()
    {
        for (int i = 0; i < 4; i++)
        {
            int slotIndex = i;
            _slots[i].ChangeSlotButton.onClick.AddListener(() => OnClickChangeSlot(slotIndex));
            _slots[i].ReadyButton.onClick.AddListener(() => OnClickReady());

            for (int g = 0; g < 4; g++)
            {
                int gearType = g;
                _slots[i].GearButtons[g].onClick.AddListener(() => OnClickUpgradeGear(gearType));
            }
        }
    }

    private void OnClickExit()
    {
        Debug.Log("Leaving Lobby...");

        if (NetworkManager.Instance.IsServer)
        {
            SendServerShutdownPacket();
        }

        NetworkManager.Instance.Shutdown();
        SceneManager.LoadScene("MenuScene");
    }

    private void ForceDisconnect(string reason)
    {
        Debug.LogWarning($"Disconnecting: {reason}");
        NetworkManager.Instance.DisconnectReason = reason; 
        NetworkManager.Instance.Shutdown();
        SceneManager.LoadScene("MenuScene");
    }

    private void SendServerShutdownPacket()
    {
        byte[] data = new byte[] { (byte)PacketType.ServerShutdown };

        for (int i = 0; i < 3; i++)
        {
            foreach (var p in _players)
            {
                if (p.IpEndpointStr == "Host") continue;
                IPEndPoint target = ParseEndPoint(p.IpEndpointStr);
                if (target != null) NetworkManager.Instance.SendPacketTo(data, target);
            }
        }
    }

    private void LogToConsole(string message)
    {
        if (_consoleText == null) return;
        string time = DateTime.Now.ToString("HH:mm:ss");
        _consoleText.text = $"[{time}] {message}\n" + _consoleText.text;
        if (_consoleText.text.Length > 1500)
            _consoleText.text = _consoleText.text.Substring(0, 1500);
    }

    private void HandleData(byte[] data, IPEndPoint sender)
    {
        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            PacketType type = (PacketType)reader.ReadByte();

            if (NetworkManager.Instance.IsServer)
            {
                if (type == PacketType.Join)
                {
                    string name = reader.ReadString();
                    ServerHandleJoin(name, sender);
                }
                else if (type == PacketType.Input)
                {
                    ServerHandleInput(reader, sender);
                }
            }
            else
            {
                if (type == PacketType.LobbyState)
                {
                    ClientHandleState(reader);
                }
                else if (type == PacketType.StartGame)
                {
                    NetworkManager.Instance.FinalPlayerList = new List<PlayerInfo>(_players);
                    SceneManager.LoadScene("GameMap");
                }
                else if (type == PacketType.ServerShutdown)
                {
                    ForceDisconnect("Сервер был закрыт хостом");
                }
            }
        }
    }

    private void ServerLogic()
    {
        _timer += Time.deltaTime;
        if (_timer > 0.1f)
        {
            _timer = 0;
            BroadcastState();
            if (_players.Count > 0 && _players.TrueForAll(p => p.IsReady))
            {
                NetworkManager.Instance.FinalPlayerList = new List<PlayerInfo>(_players); 
                SendStartGamePacket(); 
                SceneManager.LoadScene("GameMap");
            }
        }

        if (Time.frameCount % 60 == 0)
        {
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                var p = _players[i];
                if (p.IpEndpointStr == "Host") continue;

                if ((DateTime.Now - p.LastPacketTime).TotalSeconds > 5)
                {
                    LogToConsole($"Игрок {p.Name} отключился (таймаут).");
                    _players.RemoveAt(i);
                }
            }
        }
    }
    private void SendStartGamePacket()
    {
        byte[] data = new byte[] { (byte)PacketType.StartGame };

        foreach (var p in _players)
        {
            if (p.IpEndpointStr == "Host") continue;

            IPEndPoint target = ParseEndPoint(p.IpEndpointStr);
            if (target != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    NetworkManager.Instance.SendPacketTo(data, target);
                }
            }
        }
    }
    private void ServerHandleJoin(string name, IPEndPoint sender)
    {
        string id = sender.ToString();
        var existing = _players.Find(p => p.IpEndpointStr == id);
        if (existing != null)
        {
            existing.LastPacketTime = DateTime.Now;
            return;
        }

        if (_players.Count >= 4) return;

        int slot = -1;
        for (int i = 0; i < 4; i++)
        {
            if (!_players.Exists(p => p.SlotIndex == i))
            {
                slot = i;
                break;
            }
        }

        if (slot != -1)
        {
            PlayerInfo newPlayer = new PlayerInfo
            {
                Name = name,
                SlotIndex = slot,
                IsReady = false,
                GearLevels = new int[] { 0, 0, 0, 0 },
                IpEndpointStr = id,
                LastPacketTime = DateTime.Now
            };
            _players.Add(newPlayer);
            LogToConsole($"Игрок {name} присоединился.");
        }
    }

    private void ServerHandleInput(BinaryReader reader, IPEndPoint sender)
    {
        string id = (sender == null) ? "Host" : sender.ToString();
        if (sender == null) id = "Host";

        PlayerInfo player = _players.Find(p => p.IpEndpointStr == id);
        if (player == null) return;

        player.LastPacketTime = DateTime.Now;

        byte actionCode = reader.ReadByte();
        if (actionCode == 255) return; 

        if (actionCode == 0) 
        {
            player.IsReady = !player.IsReady;
        }
        else if (actionCode == 1)
        {
            int gearType = reader.ReadInt32();
            int currentUsedPoints = 0;
            foreach (int lvl in player.GearLevels) currentUsedPoints += lvl;

            if (player.GearLevels[gearType] >= 3)
            {
                player.GearLevels[gearType] = 0;
            }
            else
            {
                if (currentUsedPoints < 4) player.GearLevels[gearType]++;
                else player.GearLevels[gearType] = 0;
            }
        }
        else if (actionCode == 2) 
        {
            int targetSlot = reader.ReadInt32();
            if (!_players.Exists(p => p.SlotIndex == targetSlot))
            {
                player.SlotIndex = targetSlot;
            }
        }
    }

    private void BroadcastState()
    {
        byte[] dataPacket;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)PacketType.LobbyState);
            writer.Write(_players.Count);
            foreach (var p in _players)
            {
                writer.Write(p.Name);
                writer.Write(p.SlotIndex);
                writer.Write(p.IsReady);
                writer.Write(p.IpEndpointStr);
                for (int i = 0; i < 4; i++) writer.Write(p.GearLevels[i]);
            }
            dataPacket = ms.ToArray();
        }

        foreach (var p in _players)
        {
            if (p.IpEndpointStr == "Host") continue;
            IPEndPoint target = ParseEndPoint(p.IpEndpointStr);
            if (target != null)
            {
                NetworkManager.Instance.SendPacketTo(dataPacket, target);
            }
        }
    }

    private void SendJoinPacket()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)PacketType.Join);
            writer.Write(NetworkManager.Instance.LocalPlayerName);
            NetworkManager.Instance.SendPacket(ms.ToArray());
        }
    }

    private void ClientHandleState(BinaryReader reader)
    {
        _lastServerHeartbeat = DateTime.Now;

        List<string> oldNames = new List<string>();
        foreach (var p in _players) oldNames.Add(p.Name);

        _players.Clear();
        int count = reader.ReadInt32();

        string myName = NetworkManager.Instance.LocalPlayerName;

        for (int i = 0; i < count; i++)
        {
            PlayerInfo p = new PlayerInfo();
            p.Name = reader.ReadString();
            p.SlotIndex = reader.ReadInt32();
            p.IsReady = reader.ReadBoolean();
            p.IpEndpointStr = reader.ReadString();
            p.GearLevels = new int[4];
            for (int g = 0; g < 4; g++) p.GearLevels[g] = reader.ReadInt32();

            _players.Add(p);
        }

        foreach (var newP in _players)
        {
            if (!oldNames.Contains(newP.Name)) LogToConsole($"{newP.Name} присоединился.");
        }
        foreach (var oldN in oldNames)
        {
            bool stillHere = false;
            foreach (var newP in _players) { if (newP.Name == oldN) stillHere = true; }
            if (!stillHere) LogToConsole($"{oldN} покинул лобби.");
        }
    }

    private void SendInput(byte action, int value)
    {
        if (NetworkManager.Instance.IsServer)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(action);
                writer.Write(value);
                ms.Position = 0;
                using (BinaryReader r = new BinaryReader(ms)) ServerHandleInput(r, null);
            }
        }
        else
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.Input);
                writer.Write(action);
                writer.Write(value);
                NetworkManager.Instance.SendPacket(ms.ToArray());
            }
        }
    }

    private void OnClickReady() => SendInput(0, 0);
    private void OnClickUpgradeGear(int type) => SendInput(1, type);
    private void OnClickChangeSlot(int slotIndex) => SendInput(2, slotIndex);

    private void UpdateVisuals()
    {
        PlayerInfo me = _players.Find(p => p.Name == NetworkManager.Instance.LocalPlayerName);
        int mySlotIndex = (me != null) ? me.SlotIndex : -1;

        for (int i = 0; i < 4; i++)
        {
            LobbyPlayerSlot slotUi = _slots[i];
            PlayerInfo playerInSlot = _players.Find(p => p.SlotIndex == i);

            if (playerInSlot != null)
            {
                bool isMine = (playerInSlot == me);
                slotUi.SetVisual(true, PlayerColors[i], playerInSlot.Name, playerInSlot.IsReady);
                slotUi.UpdateControls(isMine, playerInSlot.GearLevels);
                slotUi.ChangeSlotButton.gameObject.SetActive(false);
            }
            else
            {
                slotUi.ShowEmpty();
                bool canMoveHere = (me != null) && (i != mySlotIndex);
                slotUi.ChangeSlotButton.gameObject.SetActive(canMoveHere);
                var btnText = slotUi.ChangeSlotButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText) btnText.text = "Занять";
            }
        }
    }

    private IPEndPoint ParseEndPoint(string endPointString)
    {
        string[] split = endPointString.Split(':');
        if (split.Length < 2) return null;
        return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
    }
    
    private void CopyToClipboard()
    {
        if (string.IsNullOrEmpty(_currentIpAddress)) return;

        GUIUtility.systemCopyBuffer = _currentIpAddress;

        if (_ipDisplayText != null)
            _ipDisplayText.text = $"COPIED! {_currentIpAddress}";

        Debug.Log("IP copied to clipboard: " + _currentIpAddress);
    }
}