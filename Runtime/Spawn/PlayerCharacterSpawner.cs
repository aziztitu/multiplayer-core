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

public class PlayerCharacterSpawner : NetworkBehaviour
{
    public static PlayerCharacterSpawner Instance { get; private set; }

    public NetworkObject playerCharacterPrefab;
    public PlayerCharacterSpawnGroup[] SpawnGroups;
    public bool AutoSpawn = true;

    private bool initializedHost = false;

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

        if (IsHost)
        {
            if (initializedHost)
            {
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            initializedHost = true;
        }

        NetworkManager.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (IsHost)
        {
            if (AutoSpawn)
            {
                SpawnPlayersForAllClients();
            }
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (AutoSpawn)
        {
            SpawnPlayerCharacter(clientId);
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
        if (!NetworkManager.Singleton.IsHost)
        {
            return;
        }

        foreach (var character in spawnedPlayerCharacters)
        {
            character.Value.Despawn();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsHost)
        {
            Debug.Log("Cleaning up level...");
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
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
