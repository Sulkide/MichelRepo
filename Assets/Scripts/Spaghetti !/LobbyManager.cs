using System.Collections.Generic;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;


public class LobbyManager : MonoBehaviour
{
  public static    LobbyManager   instance;
  [SerializeField] TMP_InputField lobbyCodeTextField;
  [SerializeField] TMP_InputField playerNameTextField;
  [SerializeField] Transform      startGameButtonTransform;
  float                           heartBeatTimer;


  Lobby           hostLobby;
  Lobby           joinedLobby;
  readonly string keyGameMode           = "GameMode";
  readonly string keyMap                = "Map";
  readonly string keyPlayerName         = "PlayerName";
  readonly string keyStartGameRelayCode = "StartGameRelayCode";
  float           lobbyUpdateTimer;
  string          playerName;


  void Awake()
  {
    instance = this;
  }


  async void Start()
  {
    //Sert a t'autentifier, tu t'en servira pour relier le compte steam
    // il faut installer le  Steamworks SDK
    //SignInWithSteamAsync


    await UnityServices.InitializeAsync();


    AuthenticationService.Instance.SignedIn += () => { Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId); };


    await AuthenticationService.Instance.SignInAnonymouslyAsync();


    playerName = "Mickael" + Random.Range(10, 99);
    Debug.Log("Player Name: " + playerName);
  }


  void Update()
  {
    HandleLobbyHeartBeat();
    HandleLobbyPollForUpdates();
  }


  async void HandleLobbyHeartBeat()
  {
    if (hostLobby != null)
    {
      heartBeatTimer -= Time.deltaTime;


      if (heartBeatTimer < 0f)
      {
        float heartbeatTimerMax = 15;
        heartBeatTimer = heartbeatTimerMax;


        await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
      }
    }
  }


  async void HandleLobbyPollForUpdates()
  {
    if (joinedLobby != null)
    {
      lobbyUpdateTimer -= Time.deltaTime;


      if (lobbyUpdateTimer < 0f)
      {
        float lobbyUpdateTimerMax = 1.1f;
        lobbyUpdateTimer = lobbyUpdateTimerMax;


        Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
        joinedLobby = lobby;


        if (joinedLobby.Data[keyStartGameRelayCode].Value != "0")
        {
          if (!IsLobbyHost())
          {
            //Lobby Host already joined Relay
            RelayManager.instance.JoinRelay(joinedLobby.Data[keyStartGameRelayCode].Value);
            Debug.Log("Joining Relay");
          }


          joinedLobby = null;
        }
      }
    }
  }


  [ContextMenu("Create Lobby")]
  public void CreateLobbyButton()
  {
    CreateLobby();
  }


  async void CreateLobby()
  {
    try
    {
      string lobbyName  = "MyLobby";
      int    maxPlayers = 4;


      CreateLobbyOptions createLobbyOptions = new()
      {
        IsPrivate = false,
        Player    = GetPlayer(),


        Data = new Dictionary<string, DataObject>
        {
          {keyGameMode, new DataObject(DataObject.VisibilityOptions.Public,           "CaptureTheFlag" /*,DataObject.IndexOptions.S1*/)},
          {keyMap, new DataObject(DataObject.VisibilityOptions.Public,                "Dust1")},
          {keyStartGameRelayCode, new DataObject(DataObject.VisibilityOptions.Member, "0")}
        }
      };


      Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);


      hostLobby   = lobby;
      joinedLobby = hostLobby;


      startGameButtonTransform.gameObject.SetActive(true);


      Debug.Log("Lobby Created      Lobby Name: " + lobby.Name + "      Max Player: " + maxPlayers + "      Lobby Id: " + lobby.Id + "      LobbyCode: " + lobby.LobbyCode + "      Game Mode: " + lobby.Data[keyGameMode].Value);
      PrintPlayers(hostLobby);
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  [ContextMenu("List Lobbies")]
  public void ListLobbiesButton()
  {
    ListLobbies();
  }


  async void ListLobbies()
  {
    try
    {
      QueryLobbiesOptions queryLobbiesOptions = new()
      {
        Count = 25,
        Filters = new List<QueryFilter>
        {
          //Filtre tout les lobby avec au moins 1 slot de libre
          new(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
          //Filtre tout les lobby sont CaptureTheFlag
          // new QueryFilter(QueryFilter.FieldOptions.S1,"CaptureTheFlag", QueryFilter.OpOptions.EQ)
        },
        Order = new List<QueryOrder>
        {
          new(false, QueryOrder.FieldOptions.Created)
        }
      };


      // QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryLobbiesOptions);
      QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions);
      // QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync();


      Debug.Log("Lobbies found: " + queryResponse.Results.Count);


      foreach (Lobby lobby in queryResponse.Results)
      {
        Debug.Log("Lobby Name: " + lobby.Name + "      Max Players: " + lobby.MaxPlayers + "      Game Mode: " + lobby.Data[keyGameMode].Value);
      }
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  public void JointLobbyButton()
  {
    JointLobby();
  }


  [ContextMenu("Joint Lobby")]
  async void JointLobby()
  {
    try
    {
      JoinLobbyByIdOptions joinLobbyByIdOptions = new()
      {
        Player = GetPlayer()
      };


      QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();


      Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(queryResponse.Results[0].Id, joinLobbyByIdOptions);
      joinedLobby = lobby;


      Debug.Log("Joined Lobby");


      PrintPlayers(lobby);
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  [ContextMenu("Joint Lobby By Code")]
  public void JointLobbyByCodeButton()
  {
    JointLobbyByCode(lobbyCodeTextField.text);
  }


  async void JointLobbyByCode(string lobbyCode)
  {
    try
    {
      JoinLobbyByCodeOptions joinLobbyByCodeOptions = new()
      {
        Player = GetPlayer()
      };


      Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions);
      joinedLobby = lobby;


      Debug.Log("Joined Lobby with code: " + lobbyCode);


      PrintPlayers(lobby);
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  [ContextMenu("Quick Join Lobby")]
  //[Button("Quick Join Lobby")]
  public async void QuickJoinLobby()
  {
    try
    {
      QuickJoinLobbyOptions quickJoinLobbyOptions = new()
      {
        Player = GetPlayer()
      };


      Lobby lobby = await LobbyService.Instance.QuickJoinLobbyAsync(quickJoinLobbyOptions);
      joinedLobby = lobby;


      Debug.Log("Quick Joined Lobby");


      PrintPlayers(lobby);
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  Player GetPlayer()
  {
    return new Player
    {
      Data = new Dictionary<string, PlayerDataObject>
      {
        {keyPlayerName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
      }
    };
  }


  [ContextMenu("Print Players")]
  public void PrintPlayers()
  {
    PrintPlayers(joinedLobby);
  }


  void PrintPlayers(Lobby lobby)
  {
    Debug.Log("Players in Lobby: " + lobby.Name + "    Lobby Data GameMode: " + lobby.Data[keyGameMode].Value + "    Map: " + lobby.Data[keyMap].Value);


    foreach (Player player in lobby.Players)
    {
      Debug.Log("Payer Id: "       + player.Id +
            "   Player Name: " + player.Data[keyPlayerName].Value);
    }
  }


  //[Button("Update Lobby Game Mode To Hide And Seek")]
  public void UpdateLobbyGameModeToHideAndSeek()
  {
    UpdateLobbyGameMode("HideAndSeek");
  }


  async void UpdateLobbyGameMode(string gameMode)
  {
    try
    {
      hostLobby = await LobbyService.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
      {
        Data = new Dictionary<string, DataObject>
        {
          {keyGameMode, new DataObject(DataObject.VisibilityOptions.Public, gameMode)}
        }
      });


      joinedLobby = hostLobby;


      PrintPlayers(hostLobby);
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  [ContextMenu("Update Player Name")]
  public void UpdatePlayerNameButton()
  {
    UpdatePlayerName(playerNameTextField.text);
  }


  async void UpdatePlayerName(string newPlayerName)
  {
    try
    {
      playerName = newPlayerName;
      await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
      {
        Data = new Dictionary<string, PlayerDataObject>
        {
          {keyPlayerName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, newPlayerName)}
        }
      });
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  [ContextMenu("Leave Lobby")]
  public async void LeaveLobby()
  {
    try
    {
      await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);


      Debug.Log("Leave Lobby");
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  [ContextMenu("Kick Player")]
  public async void KickPlayer()
  {
    try
    {
      await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, joinedLobby.Players[1].Id);


      Debug.Log("Kick Player");
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  async void MigrateLobbyHost()
  {
    try
    {
      hostLobby = await LobbyService.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
      {
        HostId = joinedLobby.Players[1].Id
      });


      joinedLobby = hostLobby;


      PrintPlayers(hostLobby);
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  async void DeleteLobby()
  {
    try
    {
      await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);
      Debug.Log("Delete Lobby");
    }
    catch (LobbyServiceException e)
    {
      Debug.Log(e);
    }
  }


  [ContextMenu("Start Game")]
  public async void StartGame()
  {
    if (IsLobbyHost())
    {
      try
      {
        Debug.Log("Start Game");


        string relayCode = await RelayManager.instance.CreateRelay();


        Lobby lobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
        {
          Data = new Dictionary<string, DataObject>
          {
            {keyStartGameRelayCode, new DataObject(DataObject.VisibilityOptions.Member, relayCode)}
          }
        });


        joinedLobby = lobby;
      }
      catch (LobbyServiceException e)
      {
        Debug.Log(e);
      }
    }
  }


  bool IsLobbyHost()
  {
    if (hostLobby != null)
    {
      return hostLobby.HostId == AuthenticationService.Instance.PlayerId;
    }


    return false;
  }
}


