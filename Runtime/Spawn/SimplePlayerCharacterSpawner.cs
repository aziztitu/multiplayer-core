using AZUtils;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Azeesoft.Multiplayer
{
public class SpawnPoint
{
    public Vector3 Position;
    public Quaternion Rotation;
}

[Serializable]
public class PlayerCharacterSpawnGroup
{
    [SerializeField] public NetworkObject OverridePlayerCharacterPrefab;
    [SerializeField] public Transform SpawnPointsHolder;

    public int GroupIndex { get; private set; }

    private Randomizer<Transform> spawnPoints = new(new List<Transform>());

    public HashSet<ulong> SpawnedClients
    {
        get; private set;
    } = new HashSet<ulong>();

    public void Init(int index)
    {
        GroupIndex = index;
        spawnPoints.items.Clear();
        foreach (Transform spawnPoint in SpawnPointsHolder)
        {
            spawnPoints.items.Add(spawnPoint);
        }
        spawnPoints.Shuffle();
    }

    public SpawnPoint GetNextSpawnPoint()
    {
        if (spawnPoints.items.Count == 0)
        {
            return null;
        }

        var spawnPoint = spawnPoints.GetRandomItem();
        return new SpawnPoint()
        {
            Position = spawnPoint.position,
            Rotation = spawnPoint.rotation,
        };
    }
}

public class SimplePlayerCharacterSpawner : NetworkBehaviour
{
    public static SimplePlayerCharacterSpawner Instance { get; private set; }

    public NetworkObject playerCharacterPrefab;
    public PlayerCharacterSpawnGroup[] SpawnGroups;
    public bool AutoSpawn = true;

    [Tooltip("With AutoSpawn on, also spawn a character for clients that connect after the level has loaded. Turn off for matches that should not accept players mid-game.")]
    public bool SpawnLateJoiners = true;

    private bool initializedServer = false;

    private int nextSpawnGroupIndex = 0;
    private Dictionary<ulong, NetworkObject> spawnedPlayerCharacters = new();
    private Dictionary<ulong, int> playerGroupMap = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Init();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void Init()
    {
        for (int i = 0; i < SpawnGroups.Length; i++)
        {
            SpawnGroups[i].Init(i);
        }

        // IsServer covers both a player-hosted session and a dedicated (headless) server
        if (IsServer)
        {
            if (initializedServer)
            {
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            initializedServer = true;
        }

        NetworkManager.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (IsServer)
        {
            if (AutoSpawn)
            {
                SpawnPlayersForAllClients();
            }
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (AutoSpawn && SpawnLateJoiners)
        {
            SpawnPlayerCharacter(clientId);
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        // NGO destroys the character with its owner; forget it here so the client can be spawned again if it rejoins
        ForgetClient(clientId);
    }

    private void ForgetClient(ulong clientId)
    {
        spawnedPlayerCharacters.Remove(clientId);

        if (playerGroupMap.TryGetValue(clientId, out var groupIndex))
        {
            if (groupIndex >= 0 && groupIndex < SpawnGroups.Length)
            {
                SpawnGroups[groupIndex].SpawnedClients.Remove(clientId);
            }

            playerGroupMap.Remove(clientId);
        }
    }

    public void SpawnPlayersForAllClients()
    {
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerCharacter(clientId);
        }
    }

    public void SpawnPlayerCharacter(ulong clientId, int groupIndex = -1, SpawnPoint spawnPoint = null)
    {
        if (spawnedPlayerCharacters.ContainsKey(clientId))
        {
            return;
        }

        PlayerCharacterSpawnGroup spawnGroup;
        if (groupIndex < 0)
        {
            spawnGroup = GetNextSpawnGroup();
        }
        else
        {
            if (groupIndex < SpawnGroups.Length)
            {
                spawnGroup = SpawnGroups[groupIndex];
            }
            else
            {
                Debug.LogError("Invalid Group Index: " + groupIndex);
                return;
            }
        }

        if (spawnPoint == null)
        {
            spawnPoint = spawnGroup.GetNextSpawnPoint();
            spawnPoint ??= new SpawnPoint()
                {
                    Position = new Vector3(0, 1, 0),
                    Rotation = Quaternion.identity,
                };
        }

        var prefab = playerCharacterPrefab;
        if (spawnGroup.OverridePlayerCharacterPrefab != null)
        {
            prefab = spawnGroup.OverridePlayerCharacterPrefab;
        }

        var playerCharacter = Instantiate(
            prefab,
            spawnPoint.Position,
            spawnPoint.Rotation
        );
        playerCharacter.SpawnAsPlayerObject(clientId, true);

        spawnedPlayerCharacters.Add(clientId, playerCharacter);
        spawnGroup.SpawnedClients.Add(clientId);
        playerGroupMap.Add(clientId, spawnGroup.GroupIndex);
    }

    public PlayerCharacterSpawnGroup GetNextSpawnGroup()
    {
        if (SpawnGroups.Length == 0)
        {
            return null;
        }

        var spawnGroup = SpawnGroups[nextSpawnGroupIndex];
        nextSpawnGroupIndex++;
        if (nextSpawnGroupIndex >= SpawnGroups.Length)
        {
            nextSpawnGroupIndex = 0;
        }
        return spawnGroup;
    }

    public void DestroyAllSpawnedCharacters()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (var character in spawnedPlayerCharacters)
        {
            if (character.Value != null && character.Value.IsSpawned)
            {
                character.Value.Despawn();
            }
        }

        // clear the bookkeeping too, otherwise SpawnPlayerCharacter would refuse to respawn these clients
        spawnedPlayerCharacters.Clear();
        playerGroupMap.Clear();
        foreach (var group in SpawnGroups)
        {
            group.SpawnedClients.Clear();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (initializedServer)
        {
            Debug.Log("Cleaning up level...");
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            initializedServer = false;
        }

        NetworkManager.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDestroy();
    }
}
}
