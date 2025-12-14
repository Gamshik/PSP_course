using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PacketType : byte
{
    Join = 1,
    LobbyState = 2,
    Input = 3,
    StartGame = 4,
    ServerShutdown = 5,
    ClientSceneLoaded = 6,
    MatchStart = 7,
    GameInput = 8,
    GameState = 9
}

[Serializable]
public class PlayerInfo
{
    public string Name;
    public int SlotIndex;
    public bool IsReady;
    public int[] GearLevels = new int[4];
    public string IpEndpointStr;
    public DateTime LastPacketTime;
}

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    public int Port = 8888;
    public string DisconnectReason = "";

    public bool IsServer { get; private set; }
    public string LocalPlayerName { get; private set; }
    public string ServerIp { get; private set; }

    public List<PlayerInfo> FinalPlayerList = new List<PlayerInfo>();

    private UdpClient _udpClient;
    private IPEndPoint _serverEndpoint;
    private bool _hasConnected = false;

    private bool _isRunning = false;

    public event Action<byte[], IPEndPoint> OnDataReceived;
    private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

    private void Awake()
    {
        // чтобы игра работала, когда окно не в фокусе
        Application.runInBackground = true;

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        int loops = 0;
        while (_mainThreadActions.TryDequeue(out var action))
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"UI Thread Error: {e}");
            }

            loops++;
            if (loops > 100) break; 
        }
    }

    public void StartHost(string nickname)
    {
        StopNetwork();

        IsServer = true;
        LocalPlayerName = nickname;
        _hasConnected = true;
        FinalPlayerList.Clear();
        _isRunning = true;

        try
        {
            _udpClient = new UdpClient(Port);
            // Включаем хак для Windows, чтобы не крашилось при ICMP ошибках (если вдург отправили на непрослушиваемый порт)
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            _udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { 0 }, null);

            _udpClient.BeginReceive(ReceiveCallback, null);
            SceneManager.LoadScene("LobbyScene");
        }
        catch (Exception e)
        {
            Debug.LogError($"Host Error: {e.Message}");
            StopNetwork();
        }
    }

    public void ConnectToServer(string ip, string nickname, Action<string> onFailure)
    {
        StopNetwork();

        IsServer = false;
        LocalPlayerName = nickname;
        ServerIp = ip;
        _hasConnected = false;
        FinalPlayerList.Clear();
        _isRunning = true;

        try
        {
            _udpClient = new UdpClient(); 

            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            _udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { 0 }, null);

            _serverEndpoint = new IPEndPoint(IPAddress.Parse(ip), Port);
            _udpClient.Connect(_serverEndpoint);

            _udpClient.BeginReceive(ReceiveCallback, null);
            StartCoroutine(ClientHandshakeRoutine(onFailure));
        }
        catch (Exception e)
        {
            onFailure?.Invoke($"Connection Error: {e.Message}");
            StopNetwork();
        }
    }

    public void Shutdown()
    {
        StopNetwork();
    }

    private void StopNetwork()
    {
        _isRunning = false;
        _hasConnected = false;

        if (_udpClient != null)
        {
            try { _udpClient.Close(); } catch { }
            _udpClient = null;
        }
        _mainThreadActions = new ConcurrentQueue<Action>(); 
    }

    private void OnApplicationQuit()
    {
        StopNetwork();
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        if (!_isRunning || _udpClient == null) return;

        try
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = _udpClient.EndReceive(ar, ref sender);

            _mainThreadActions.Enqueue(() =>
            {
                if (!IsServer && !_hasConnected) _hasConnected = true;
                OnDataReceived?.Invoke(data, sender);
            });

            if (_isRunning && _udpClient != null)
            {
                _udpClient.BeginReceive(ReceiveCallback, null);
            }
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                if (_isRunning && _udpClient != null)
                    _udpClient.BeginReceive(ReceiveCallback, null);
            }
            else
            {
                Debug.LogWarning($"Socket Error: {ex.Message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Receive Critical Error: {e.Message}");
        }
    }

    private IEnumerator ClientHandshakeRoutine(Action<string> onFailure)
    {
        byte[] joinPacket;
        using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
        using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(ms))
        {
            writer.Write((byte)PacketType.Join);
            writer.Write(LocalPlayerName);
            joinPacket = ms.ToArray();
        }

        float timer = 0;
        while (timer < 2.0f)
        {
            if (!_isRunning) yield break;

            if (_hasConnected)
            {
                SceneManager.LoadScene("LobbyScene");
                yield break;
            }
            SendPacket(joinPacket);
            yield return new WaitForSeconds(0.2f);
            timer += 0.2f;
        }

        StopNetwork();
        onFailure?.Invoke("Сервер не отвечает (Timeout)...");
    }

    public void SendPacket(byte[] data)
    {
        if (_udpClient != null && _isRunning)
        {
            try
            {
                _udpClient.Send(data, data.Length);
            }
            catch { }
        }
    }

    public void SendPacketTo(byte[] data, IPEndPoint target)
    {
        if (_udpClient != null && _isRunning)
        {
            try
            {
                _udpClient.Send(data, data.Length, target);
            }
            catch { }
        }
    }

    public static string GetLocalIPv4(bool preferWifi = true)
    {
        string output = "127.0.0.1";

        foreach (var item in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (item.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                item.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            bool isWifi = item.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211;
            bool isEthernet = item.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet;

            if (isWifi || isEthernet)
            {
                foreach (var ip in item.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        output = ip.Address.ToString();

                        if (preferWifi && isWifi) return output;
                    }
                }
            }
        }
        return output;
    }
}