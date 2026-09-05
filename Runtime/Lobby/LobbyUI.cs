using AZUtils;
using Netcode.Transports.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if !DISABLESTEAMWORKS
using HeathenEngineering.SteamworksIntegration.API;
using Netcode.Transports;
#endif

namespace Azeesoft.Multiplayer
{
    public class LobbyUI : SingletonMonoBehaviour<LobbyUI>
    {
        public enum TransportType
        {
            Unity,
            Steam,
            WebRTC
        }

        [Header("Connect Screen")]
        [SerializeField] private GameObject ConnectScreen;
        [SerializeField] private TMP_InputField CodeInput;
        [SerializeField] private TMP_Dropdown TransportSelectDropdown;

        [Header("Lobby Screen")]
        [SerializeField] private NetworkObject LobbyManagerPrefab;
        [SerializeField] private GameObject LobbyScreen;
        [SerializeField] private TMP_InputField LobbyCodeText;
        [SerializeField] private GameObject StartButton;
        [SerializeField] private GameObject WaitingMessage;
        [SerializeField] private TextMeshProUGUI TotalPlayersText;
        [SerializeField] private GameObject GameListContainer;
        [SerializeField] private GameSelectItem GameSelectItemPrefab;
        [SerializeField] private GameObject GameListInputBlocker;
        [SerializeField] private Button StartGameBtn;
        [SerializeField] private TMP_InputField PlayerNameInput;

        [Header("Transport Selection")]
        [SerializeField] private TransportType defaultTransportTypeInEditor = TransportType.Steam;
        [SerializeField] private TransportType defaultTransportTypeInDesktop = TransportType.Steam;
        [SerializeField] private TransportType defaultTransportTypeInMobile = TransportType.WebRTC;

        [Header("Misc")]
        [FormerlySerializedAs("testGameSceneNames")]
        [SerializeField] private string[] gameSceneNames;

        public string SelectedGameSceneName
        {
            get
            {
                if (SimpleLobbyManager.Instance)
                {
                    return SimpleLobbyManager.Instance.SelectedGameSceneName.Value.ToString();
                }
                return "";
            }
            private set
            {
                if (SimpleLobbyManager.Instance)
                {
                    SimpleLobbyManager.Instance.SelectedGameSceneName.Value = value;
                }
            }
        }

        private TransportType DefaultTransportType
        {
            get
            {
                if (Application.isEditor)
                {
                    return defaultTransportTypeInEditor;
                }

                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    return TransportType.WebRTC;
                }

                if (Application.platform == RuntimePlatform.WindowsPlayer
                    || Application.platform == RuntimePlatform.OSXPlayer
                    || Application.platform == RuntimePlatform.LinuxPlayer)
                {
                    return defaultTransportTypeInDesktop;
                }

                if (Application.platform == RuntimePlatform.Android
                    || Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    return defaultTransportTypeInMobile;
                }

                return TransportType.Unity;
            }
        }

        private TransportType UseTransportType => (TransportType)TransportSelectDropdown.value;

        private bool ShowGamePicker => gameSceneNames != null && gameSceneNames.Length > 1;

        new void Awake()
        {
            base.Awake();

            InitTransformSelectDropdown();
            InitGameList();
        }

        void Start()
        {
            Time.timeScale = 1.0f;
            HelperUtilities.UpdateCursorLock(false);

            if (CustomNetworkManager.Instance.CurrentNetworkTransport is WebRTCTransport)
            {
                var webRTCTransport = CustomNetworkManager.Instance.GetTransport<WebRTCTransport>();
                CodeInput.text = webRTCTransport.roomId;
            }

            NetworkManager.Singleton.OnServerStarted += SpawnLobbyManager;
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
            }

            if (NetworkPlayerIdentities.TryGet(NetworkManager.Singleton.LocalClientId, out var playerNetworkIdentity))
            {
                PlayerNameInput.text = playerNetworkIdentity.PlayerName;
            }

            PlayerNameInput.onValueChanged.AddListener((playerName) =>
            {
                if (!NetworkManager.Singleton)
                {
                    return;
                }

                if (NetworkPlayerIdentities.TryGet(NetworkManager.Singleton.LocalClientId, out var identity))
                {
                    identity.PlayerName = playerName;
                }
            });
        }

        void Update()
        {
            if (!NetworkManager.Singleton)
            {
                return;
            }

            var isInLobby = NetworkManager.Singleton.IsConnectedClient;
            ConnectScreen.SetActive(!isInLobby);
            LobbyScreen.SetActive(isInLobby);

            if (isInLobby)
            {
                var isHost = NetworkManager.Singleton.IsHost;
                StartButton.SetActive(isHost);
                WaitingMessage.SetActive(!isHost);
                if (GameListInputBlocker)
                {
                    GameListInputBlocker.SetActive(!isHost && ShowGamePicker);
                }

                var showLobbyCode = UseTransportType != TransportType.Unity;
                LobbyCodeText.gameObject.SetActive(showLobbyCode);

                if (showLobbyCode)
                {
                    var transport = CustomNetworkManager.Instance.CurrentNetworkTransport;
#if !DISABLESTEAMWORKS
                    if (transport is SteamNetworkingSocketsTransport steamTransport)
                    {
                        var serverSteamId = isHost ? User.Client.Id.SteamId : steamTransport.ConnectToSteamID;
                        LobbyCodeText.text = $"{serverSteamId}";
                    }
#endif
                    if (transport is WebRTCTransport webRTCTransport)
                    {
                        LobbyCodeText.text = $"{webRTCTransport.roomId}";
                    }
                }

                TotalPlayersText.text = $"Total Players: {NetworkManager.Singleton.ConnectedClients.Count}";
                StartGameBtn.interactable = SelectedGameSceneName != "";
            }
            else
            {
                var showCodeInput = UseTransportType != TransportType.Unity;
                CodeInput.gameObject.SetActive(showCodeInput);
            }
        }

        private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                SpawnLobbyManager();
            }
        }

        void InitGameList()
        {
            if (GameListContainer)
            {
                GameListContainer.SetActive(ShowGamePicker);
                GameListContainer.transform.DestroyAllChildren(true);
            }

            if (GameListInputBlocker && !ShowGamePicker)
            {
                GameListInputBlocker.SetActive(false);
            }

            if (!ShowGamePicker)
            {
                return;
            }

            foreach (var sceneName in gameSceneNames)
            {
                var gameSelectItem = Instantiate(GameSelectItemPrefab, GameListContainer.transform);
                gameSelectItem.sceneNameText.text = sceneName;
                gameSelectItem.button.onClick.AddListener(() =>
                {
                    if (NetworkManager.Singleton.IsHost)
                    {
                        SelectedGameSceneName = sceneName;
                    }
                });
            }
        }

        public void Host()
        {
            SetupNetworkTransport();
            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("Could not start the Host");
            }
        }

        public void Join()
        {
            SetupNetworkTransport();
            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("Could not connect to the Host");
            }
        }

        void InitTransformSelectDropdown()
        {
            List<TMP_Dropdown.OptionData> options = Enum.GetNames(typeof(TransportType)).Select(name =>
            {
                return new TMP_Dropdown.OptionData()
                {
                    text = name,
                };
            }).ToList();

            TransportSelectDropdown.ClearOptions();
            TransportSelectDropdown.AddOptions(options);

            if (CustomNetworkManager.Instance.IsConnectedClient)
            {
                TransportType currentTransportType = TransportType.Unity;
                if (CustomNetworkManager.Instance.CurrentNetworkTransport is UnityTransport)
                {
                    currentTransportType = TransportType.Unity;
                }
#if !DISABLESTEAMWORKS
                else if (CustomNetworkManager.Instance.CurrentNetworkTransport is SteamNetworkingSocketsTransport)
                {
                    currentTransportType = TransportType.Steam;
                }
#endif
                else if (CustomNetworkManager.Instance.CurrentNetworkTransport is WebRTCTransport)
                {
                    currentTransportType = TransportType.WebRTC;
                }
                TransportSelectDropdown.value = (int)currentTransportType;
            }
            else
            {
                TransportSelectDropdown.value = (int)DefaultTransportType;
            }
        }

        void SpawnLobbyManager()
        {
            if (!NetworkManager.Singleton.IsHost || SimpleLobbyManager.Instance != null)
            {
                return;
            }

            var lobbyManager = Instantiate(LobbyManagerPrefab);
            lobbyManager.Spawn(true);

            if (gameSceneNames != null && gameSceneNames.Length > 0)
            {
                SelectedGameSceneName = gameSceneNames[0];
            }
        }

        void SetupNetworkTransport()
        {
            if (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }

            switch (UseTransportType)
            {
                case TransportType.Unity:
                    CustomNetworkManager.Instance.UseTransport<UnityTransport>();
                    break;
                case TransportType.Steam:
#if !DISABLESTEAMWORKS
                    var steamTransport = CustomNetworkManager.Instance.UseTransport<SteamNetworkingSocketsTransport>();
                    if (ulong.TryParse(CodeInput.text, out var code))
                    {
                        steamTransport.ConnectToSteamID = code;
                    }
                    break;
#else
                    Debug.LogWarning("Trying to use Steam, but Steamworks is disabled");
                    break;
#endif
                case TransportType.WebRTC:
                    var webRTCTransport = CustomNetworkManager.Instance.UseTransport<WebRTCTransport>();
                    webRTCTransport.roomId = CodeInput.text;
                    break;
            }
        }

        public void LeaveLobby()
        {
            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.LogError("You are not in a Lobby");
                return;
            }

            NetworkManager.Singleton.Shutdown();
        }

        public void StartGame()
        {
            if (SelectedGameSceneName == "")
            {
                return;
            }

            var status = NetworkManager.Singleton.SceneManager.LoadScene(SelectedGameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"Failed to load {SelectedGameSceneName} " +
                      $"with a {nameof(SceneEventProgressStatus)}: {status}");
            }
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
            }
            NetworkManager.Singleton.OnServerStarted -= SpawnLobbyManager;
        }

        public void Exit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
