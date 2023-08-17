using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies.Models;
using Unity.Services.Lobbies;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using System.Threading.Tasks;

public class GameManager : NetworkBehaviour
{
    [SerializeReference] private LevelSelect levelSelect;

    private Lobby currentLobby;

    public static GameManager instance = null;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        DontDestroyOnLoad(this);

        _ = ConnectToRelay();
    }

    private async Task ConnectToRelay() //run in Start
    {
        Debug.Log("Connecting...");

        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);
        };

        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            FindLobby();
        }
        catch
        {
            Debug.Log("Failed to connect");
        }
    }

    private IEnumerator HandleLobbyHeartbeat() //keep lobby active (lobbies are automatically hidden after 30 seconds of inactivity)
    {
        while (currentLobby != null)
        {
            SendHeartbeat();
            yield return new WaitForSeconds(15);
            Debug.Log("hearbeat");
        }
    }
    private async void SendHeartbeat()
    {
        await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
    }

    private async void FindLobby()
    {
        try
        {
            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(null);

            if (queryResponse.Results.Count > 0) //if a lobby exists
            {
                if (!queryResponse.Results[0].Data.ContainsKey("JoinCode"))
                {
                    //JoinCode is created when player is connected to relay. It's possible to join the lobby before the relay connection
                    //is established and before JoinCode is created
                    Debug.Log("Lobby is still being created, trying again in 2 seconds");
                    Invoke(nameof(FindLobby), 2);
                    return;
                }

                currentLobby = await Lobbies.Instance.JoinLobbyByIdAsync(queryResponse.Results[0].Id);
                Debug.Log("Joined Lobby!");

                string joinCode = currentLobby.Data["JoinCode"].Value;

                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

                NetworkManager.Singleton.StartClient();
            }
            else
                CreateLobby();

        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }


    public async void CreateLobby()
    {
        try
        {
            currentLobby = await LobbyService.Instance.CreateLobbyAsync("NewLobby", 6); //number of players

            Debug.Log("Created Lobby");

            StartCoroutine(HandleLobbyHeartbeat());

            Allocation hostAllocation = await RelayService.Instance.CreateAllocationAsync(5); //number of non-host connections
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(hostAllocation, "dtls"));

            NetworkManager.Singleton.StartHost();

            // Set up JoinAllocation
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

            //SaveJoinCodeInLobbyData
            try
            {
                //update currentLobby
                currentLobby = await Lobbies.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject> //JoinCode = S1
                    {
                        //only updates this piece of data--other lobby data remains unchanged
                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode, DataObject.IndexOptions.S1) }
                    }
                });
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public override void OnNetworkSpawn()
    {
        levelSelect.Connected(IsHost);
    }

    public async void ExitGame() //called by ExitGame
    {
        try
        {
            if (IsServer)
                await Lobbies.Instance.DeleteLobbyAsync(currentLobby.Id);
            else
                await Lobbies.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);

            currentLobby = null; //avoid heartbeat errors in editor since playmode doesn't stop

            NetworkManager.Singleton.Shutdown();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }

        Application.Quit();
    }
}