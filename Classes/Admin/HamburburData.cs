using GorillaNetworking;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using JoinType = GorillaNetworking.JoinType;
using WsSharpWebSocket = WebSocketSharp.WebSocket;

namespace FortniteEmoteWheel.Classes.Admin;

public class HamburburData : MonoBehaviour
{
    public static Action<JObject> OnDataReloaded;

    public static readonly Dictionary<string, string> Admins = [];
    public static readonly List<string> HamburburSuperAdmins = [];

    private static Action<bool> onPlayerConfirmedToBeAdmin;
    private static bool hasSubscribedToAddingAdminMods;
    private static bool hasSubscribedToAddingSuperAdminMods;
    public static bool givenAdminMods;

    public static WsSharpWebSocket DeezWebsocket;
    public static readonly string DeezServerWebsocket = "wss://deez.uk/ws";

    public static WsSharpWebSocket HamburburWebsocket;
    public static readonly string HamburburServerWebsocket = "wss://api.hamburbur.org";

    private const float DeezReconnectDelay = 5f;
    private const float DeezPingDelay = 10f;

    private const float HamburburReconnectDelay = 5f;
    private const float HamburburPingDelay = 10f;

    private Coroutine deezWebsocketCoroutine;
    private Coroutine hamburburWebsocketCoroutine;

    private readonly Queue<string> deezReceivedMessages = [];
    private readonly object deezMessageLock = new();

    private readonly Queue<string> hamburburReceivedMessages = [];
    private readonly object hamburburMessageLock = new();

    public static Action<string> OnDeezMessageReceived;
    public static Action<string> OnHamburburMessageReceived;

    private static JObject dataBackingField;

    private bool hasLoadedConsole;
    public static bool DataLoaded { get; private set; }

    public static bool IsLocalAdmin { get; private set; }
    public static bool IsLocalSuperAdmin { get; private set; }

    public static HamburburData Instance { get; private set; }

    public static JObject Data
    {
        get
        {
            if (dataBackingField != null)
                return dataBackingField;

            using HttpClient httpClient = new();
            HttpResponseMessage dataResponse = httpClient.GetAsync(Constants.DeezUrl + "/data").Result;
            using Stream dataStream = dataResponse.Content.ReadAsStreamAsync().Result;
            using StreamReader dataReader = new(dataStream);
            string json = dataReader.ReadToEnd().Trim();
            dataBackingField = JObject.Parse(json);

            return dataBackingField;
        }

        private set => dataBackingField = value;
    }

    private void Awake() => Instance = this;

    private IEnumerator Start()
    {
        deezWebsocketCoroutine ??= StartCoroutine(DeezWebsocketLoop());
        hamburburWebsocketCoroutine ??= StartCoroutine(HamburburWebSocketLoop());

        NetworkSystem.Instance.OnJoinedRoomEvent += () =>
        {
            StartCoroutine(TelemetryManagement.TelemetryRequest(
                    PhotonNetwork.CurrentRoom.Name, PhotonNetwork.NickName,
                    PhotonNetwork.CloudRegion,
                    PhotonNetwork.LocalPlayer.UserId,
                    PhotonNetwork.CurrentRoom.IsVisible,
                    PhotonNetwork.PlayerList.Length,
                    NetworkSystem.Instance.GameModeString));
        };

        while (true)
        {
            UnityWebRequest deezWebRequest = UnityWebRequest.Get(Constants.DeezUrl + "/data");
            UnityWebRequest hamburburWebRequest = UnityWebRequest.Get(Constants.HamburburUrl + "/data");

            yield return deezWebRequest.SendWebRequest();
            yield return hamburburWebRequest.SendWebRequest();

            if (deezWebRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = deezWebRequest.downloadHandler.text;
                bool errored = false;

                try
                {
                    Data = JObject.Parse(jsonResponse);
                    DataLoaded = true;
                    try
                    {
                        OnDataReloaded?.Invoke(Data);
                    }
                    catch
                    {
                        // ignored
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse JSON from {Constants.DeezUrl}/data: {e}");
                    errored = true;
                }

                if (!errored)
                {
                    Admins.Clear();
                    HamburburSuperAdmins.Clear();

                    foreach (JToken adminPair in (JArray)Data["admins"]!)
                    {
                        string adminUserId = adminPair["userId"]!.ToString();
                        string adminName = adminPair["name"]!.ToString();
                        Admins[adminUserId] = adminName;
                    }

                    HamburburSuperAdmins.AddRange(((JArray)Data["superAdmins"]!).Select(token => token.ToString()));

                    if (Data["modSpecificAdmins"] is JArray modSpecificAdminsArray)
                        foreach (JToken modEntry in modSpecificAdminsArray)
                        {
                            string consoleName = modEntry["consoleName"]?.ToString();

                            if (string.IsNullOrEmpty(consoleName) || consoleName != "FortniteEmoteWheel")
                                continue;

                            if (modEntry["admins"] is not JArray specificAdmins)
                                continue;

                            foreach (JToken admin in specificAdmins)
                            {
                                string name = admin["name"]?.ToString();
                                string userId = admin["userId"]?.ToString();
                                string super = admin["superAdmin"]?.ToString();

                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(userId))
                                    continue;

                                Admins[userId] = name;

                                if (!bool.TryParse(super, out bool isSuper) || !isSuper)
                                    continue;

                                if (!HamburburSuperAdmins.Contains(name))
                                    HamburburSuperAdmins.Add(name);
                            }
                        }

                    if (!hasLoadedConsole)
                    {
                        Console.LoadConsole();
                        hasLoadedConsole = true;
                    }
                }
            }
            else
            {
                Debug.LogError($"Failed to fetch data from {Constants.DeezUrl}/data: {deezWebRequest.error}");
            }

            yield return new WaitForSeconds(60);
        }
    }

    private void Update()
    {
        while (true)
        {
            string message;

            lock (deezMessageLock)
            {
                if (deezReceivedMessages.Count <= 0)
                    break;

                message = deezReceivedMessages.Dequeue();
            }

            try
            {
                OnDeezMessageReceived?.Invoke(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deez Websocket] Failed to handle message: {e}");
            }

            if (message != null && message.StartsWith("join ") && message.Split(' ').Length > 1)
            {
                string room = message.Split(' ')[1].ToUpper();
                StartCoroutine(JoinRoomDelayed(room));
            }
        }

        while (true)
        {
            string message;

            lock (hamburburMessageLock)
            {
                if (hamburburReceivedMessages.Count <= 0)
                    break;

                message = hamburburReceivedMessages.Dequeue();
            }

            try
            {
                OnHamburburMessageReceived?.Invoke(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hamburbur Websocket] Failed to handle message: {e}");
            }
        }

        if (givenAdminMods || PhotonNetwork.LocalPlayer.UserId.IsNullOrEmpty() ||
            !Admins.TryGetValue(PhotonNetwork.LocalPlayer.UserId, out string playerName))
            return;

        IsLocalSuperAdmin = HamburburSuperAdmins.Contains(playerName);

        IsLocalAdmin = true;
        givenAdminMods = true;
        StartCoroutine(LoadAdminModsRoutine(playerName, IsLocalSuperAdmin));
    }

    private IEnumerator LoadAdminModsRoutine(string playerName, bool superAdmin)
    {
        yield return new WaitForSeconds(3f);

        if (superAdmin)
            Console.IsBlocked = 0L;

        givenAdminMods = true;
        IsLocalAdmin = true;
        IsLocalSuperAdmin = superAdmin;
        onPlayerConfirmedToBeAdmin?.Invoke(superAdmin);
    }

    private IEnumerator DeezWebsocketLoop()
    {
        WaitForSeconds reconnectWait = new(DeezReconnectDelay);
        WaitForSeconds pingWait = new(DeezPingDelay);

        while (true)
        {
            if (DeezWebsocket == null || !DeezWebsocket.IsAlive)
            {
                ConnectDeezWebsocket();

                yield return reconnectWait;
                continue;
            }

            try
            {
                DeezWebsocket.Send("ping");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deez Websocket] Failed to send ping: {e}");
                CloseDeezWebsocket();
            }

            yield return pingWait;
        }
    }

    private void ConnectDeezWebsocket()
    {
        CloseDeezWebsocket();

        string url = $"{DeezServerWebsocket}/?modname={Uri.EscapeDataString(Constants.Name)}";

        DeezWebsocket = new WsSharpWebSocket(url);

        DeezWebsocket.OnOpen += (_, _) =>
        {
            Debug.Log("[Deez Websocket] Connected");
        };

        DeezWebsocket.OnClose += (_, e) =>
        {
            Debug.Log($"[Deez Websocket] Closed: {e.Code} {e.Reason}");
        };

        DeezWebsocket.OnError += (_, e) =>
        {
            Debug.LogError($"[Deez Websocket] Error: {e.Message}");
        };

        DeezWebsocket.OnMessage += (_, e) =>
        {
            if (e.Data == "pong")
                return;

            lock (deezMessageLock)
                deezReceivedMessages.Enqueue(e.Data);
        };

        try
        {
            DeezWebsocket.ConnectAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Deez Websocket] Failed to connect: {e}");
            CloseDeezWebsocket();
        }
    }

    private static void CloseDeezWebsocket()
    {
        if (DeezWebsocket == null)
            return;

        try
        {
            DeezWebsocket.CloseAsync();
        }
        catch
        {
        }

        DeezWebsocket = null;
    }

    private IEnumerator HamburburWebSocketLoop()
    {
        WaitForSeconds reconnectWait = new(HamburburReconnectDelay);
        WaitForSeconds pingWait      = new(HamburburPingDelay);

        while (true)
        {
            if (HamburburWebsocket == null || !HamburburWebsocket.IsAlive)
            {
                ConnectHamburburWebsocket();

                yield return reconnectWait;
                continue;
            }

            try
            {
                HamburburWebsocket.Send("ping");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hamburbur Websocket] Failed to send ping: {e}");
                CloseHamburburWebsocket();
            }

            yield return pingWait;
        }
    }

    private void ConnectHamburburWebsocket()
    {
        CloseHamburburWebsocket();

        string url = $"{HamburburServerWebsocket}/?modname={Uri.EscapeDataString(Constants.Name)}";

        HamburburWebsocket = new WsSharpWebSocket(url);

        HamburburWebsocket.OnOpen += (_, _) =>
        {
            Debug.Log("[Hamburbur Websocket] Connected");
        };

        HamburburWebsocket.OnClose += (_, e) =>
        {
            Debug.Log($"[Hamburbur Websocket] Closed: {e.Code} {e.Reason}");
        };

        HamburburWebsocket.OnError += (_, e) =>
        {
            Debug.LogError($"[Hamburbur Websocket] Error: {e.Message}");
        };

        HamburburWebsocket.OnMessage += (_, e) =>
        {
            if (e.Data == "pong")
                return;

            lock (hamburburMessageLock)
                hamburburReceivedMessages.Enqueue(e.Data);
        };

        try
        {
            HamburburWebsocket.ConnectAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Hamburbur Websocket] Failed to connect: {e}");
            CloseHamburburWebsocket();
        }
    }

    private static void CloseHamburburWebsocket()
    {
        if (HamburburWebsocket == null)
            return;

        try
        {
            HamburburWebsocket.CloseAsync();
        }
        catch
        {
        }

        HamburburWebsocket = null;
    }

    public static void ResetDataBackingField() => dataBackingField = null;

    private IEnumerator JoinRoomDelayed(string room)
    {
        NetworkSystem.Instance.ReturnToSinglePlayer();
        yield return new WaitForSeconds(5f);
        PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(room, JoinType.Solo);
    }
}