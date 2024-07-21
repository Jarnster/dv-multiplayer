using System;
using Multiplayer.Networking.Data;
using Multiplayer.Networking.Listeners;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Multiplayer.Components.Networking;
using DV.WeatherSystem;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Multiplayer.Networking.Managers.Server;
public class LobbyServerManager : MonoBehaviour
{
    //API endpoints
    private const string ENDPOINT_ADD_SERVER    = "add_game_server";
    private const string ENDPOINT_UPDATE_SERVER = "update_game_server";
    private const string ENDPOINT_REMOVE_SERVER = "remove_game_server";

    //RegEx
    private readonly Regex IPv4Match = new Regex(@"\b(?:(?:2[0-5]{2}|1[0-9]{2}|[1-9]?[0-9])\.){3}(?:2[0-5]{2}|1[0-9]{2}|[1-9]?[0-9])\b");

    private const int REDIRECT_MAX = 5;

    private const int UPDATE_TIME_BUFFER = 10;                  //We don't want to miss our update, let's phone in just a little early
    private const int UPDATE_TIME = 120 - UPDATE_TIME_BUFFER;   //How often to update the lobby server - this should match the lobby server's time-out period
    private const int PLAYER_CHANGE_TIME = 5;                   //Update server early if the number of players has changed in this time frame

    private NetworkServer server;
    private string server_id { get; set; }
    private string private_key { get; set; }

    private bool initialised = false;



    private bool sendUpdates = false;
    private float timePassed = 0f;

    private void Awake()
    {
        server = NetworkLifecycle.Instance.Server;

        Multiplayer.Log($"LobbyServerManager New({server != null})");
    }

    private IEnumerator Start()
    {
        server.serverData.ipv6 = GetStaticIPv6Address();
        StartCoroutine(GetIPv4(Multiplayer.Settings.Ipv4AddressCheck));

        yield return new WaitUntil(() => initialised);

        Multiplayer.Log("Public IPv4: " + server.serverData.ipv4);
        Multiplayer.Log("Public IPv6: " + server.serverData.ipv6);

        Multiplayer.Log("Registering server at: " + Multiplayer.Settings.LobbyServerAddress + "/add_game_server");

        StartCoroutine(RegisterWithLobbyServer(Multiplayer.Settings.LobbyServerAddress + "/add_game_server"));
    }

    private void OnDestroy()
    {
        Multiplayer.Log($"LobbyServerManager OnDestroy()");
        sendUpdates = false;
        StopAllCoroutines();
        StartCoroutine(RemoveFromLobbyServer($"{Multiplayer.Settings.LobbyServerAddress}/{ENDPOINT_REMOVE_SERVER}"));
    }

    private void Update()
    {
        if (sendUpdates)
        {
            timePassed += Time.deltaTime;

            if (timePassed > UPDATE_TIME || (server.serverData.CurrentPlayers != server.PlayerCount && timePassed > PLAYER_CHANGE_TIME))
            {
                timePassed = 0f;
                server.serverData.CurrentPlayers = server.PlayerCount;
                StartCoroutine(UpdateLobbyServer($"{Multiplayer.Settings.LobbyServerAddress}/{ENDPOINT_UPDATE_SERVER}"));
            }
        }
    }

    public void RemoveFromLobbyServer()
    {
        Multiplayer.Log($"RemoveFromLobbyServer OnDestroy()");
        sendUpdates = false;
        StopAllCoroutines();
        StartCoroutine(RemoveFromLobbyServer($"{Multiplayer.Settings.LobbyServerAddress}/{ENDPOINT_REMOVE_SERVER}"));
    }

    private IEnumerator RegisterWithLobbyServer(string uri)
    {
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        string json = JsonConvert.SerializeObject(server.serverData, jsonSettings);
        Multiplayer.LogDebug(()=>$"JsonRequest: {json}");

        yield return SendWebRequest(
            uri,
            json,
            webRequest =>
            {
                LobbyServerResponseData response = JsonConvert.DeserializeObject<LobbyServerResponseData>(webRequest.downloadHandler.text);
                if (response != null)
                {
                    private_key = response.private_key;
                    server_id = response.game_server_id;

                    sendUpdates = true;
                }
            },
            webRequest => Multiplayer.LogError("Failed to register with lobby server")
        );
    }

    private IEnumerator RemoveFromLobbyServer(string uri)
    {
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        string json = JsonConvert.SerializeObject(new LobbyServerResponseData(server_id, private_key), jsonSettings);
        Multiplayer.LogDebug(() => $"JsonRequest: {json}");

        yield return SendWebRequest(
            uri,
            json,
            webRequest => Multiplayer.Log("Successfully removed from lobby server"),
            webRequest => Multiplayer.LogError("Failed to remove from lobby server")
        );
    }

    private IEnumerator UpdateLobbyServer(string uri)
    {
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        DateTime start = AStartGameData.BaseTimeAndDate;
        DateTime current = WeatherDriver.Instance.manager.DateTime;
        TimeSpan inGame = current - start;

        LobbyServerUpdateData reqData = new LobbyServerUpdateData(
                                                                    server_id,
                                                                    private_key,
                                                                    inGame.ToString("d\\d\\ hh\\h\\ mm\\m\\ ss\\s"),
                                                                    server.serverData.CurrentPlayers
                                                                  );

        string json = JsonConvert.SerializeObject(reqData, jsonSettings);
        Multiplayer.LogDebug(() => $"UpdateLobbyServer JsonRequest: {json}");

        yield return SendWebRequest(
            uri,
            json,
            webRequest => Multiplayer.Log("Successfully updated lobby server"),
            webRequest =>
            {
                Multiplayer.LogError("Failed to update lobby server, attempting to re-register");

                //cleanup
                sendUpdates = false;
                private_key = null;
                server_id = null;

                //Attempt to re-register
                StartCoroutine(RegisterWithLobbyServer($"{Multiplayer.Settings.LobbyServerAddress}/{ENDPOINT_ADD_SERVER}"));
            }
        );
    }

    private IEnumerator GetIPv4(string uri)
    {
 
        Multiplayer.Log("Preparing to get IPv4: " + uri);

        yield return SendWebRequest(
            uri,
            string.Empty,
            webRequest =>
            {
                Match match = IPv4Match.Match(webRequest.downloadHandler.text);
                if (match != null)
                {
                    Multiplayer.Log($"IPv4 address extracted: {match.Value}");
                    server.serverData.ipv4 = match.Value;     
                }
                else
                {
                    Multiplayer.LogError($"Failed to find IPv4 address. Server will only be available via IPv6");
                }

                initialised = true;

            },
            webRequest =>
            {
                Multiplayer.LogError("Failed to find IPv4 address. Server will only be available via IPv6");
                initialised = true;
            }
        );
    }

    private IEnumerator SendWebRequest(string uri, string json, Action<UnityWebRequest> onSuccess, Action<UnityWebRequest> onError, int depth=0)
    {
        if (depth > REDIRECT_MAX)
        {
            Multiplayer.LogError($"Reached maximum redirects: {uri}");
            yield break;
        }

        using (UnityWebRequest webRequest = UnityWebRequest.Post(uri, json))
        {
            webRequest.redirectLimit = 0;

            if (json != null && json.Length > 0)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)) { contentType = "application/json" };
            }
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            yield return webRequest.SendWebRequest();

            //check for redirect
            if (webRequest.responseCode >= 300 && webRequest.responseCode < 400)
            {
                string redirectUrl = webRequest.GetResponseHeader("Location");
                Multiplayer.LogWarning($"Lobby Server redirected, check address is up to date: '{redirectUrl}'");

                if (redirectUrl != null && redirectUrl.StartsWith("https://") && redirectUrl.Replace("https://", "http://") == uri)
                {
                    yield return SendWebRequest(redirectUrl, json, onSuccess, onError, ++depth);
                }
            }
            else
            {
                if (webRequest.isNetworkError || webRequest.isHttpError)
                {
                    Multiplayer.LogError($"Error: {webRequest.error}\r\n{webRequest.downloadHandler.text}");
                    onError?.Invoke(webRequest);
                }
                else
                {
                    Multiplayer.Log($"Received: {webRequest.downloadHandler.text}");
                    onSuccess?.Invoke(webRequest);
                }
            }
        }
    }

    private string GetStaticIPv6Address()
    {
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            bool flag = !networkInterface.Supports(NetworkInterfaceComponent.IPv6) || networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel;
            if (!flag)
            {
                foreach (UnicastIPAddressInformation unicastIPAddressInformation in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    bool flag2 = unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetworkV6;
                    if (flag2)
                    {
                        bool flag3 = !unicastIPAddressInformation.Address.IsIPv6LinkLocal && !unicastIPAddressInformation.Address.IsIPv6SiteLocal && unicastIPAddressInformation.IsDnsEligible;
                        if (flag3)
                        {
                            return unicastIPAddressInformation.Address.ToString();
                        }
                    }
                }
            }
        }
        return null;
    }
}
