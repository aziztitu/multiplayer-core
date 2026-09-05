using AZUtils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Azeesoft.Multiplayer
{
public class PauseMenu : SingletonMonoBehaviour<PauseMenu>
{
    public GameObject menuRoot;
    public string mainMenuScene;
    public GameObject restartButton;

    public InputAction pauseAction;

    [HideInInspector] public bool isPaused = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (NetworkManager.Singleton != null)
        {
            restartButton.SetActive(NetworkManager.Singleton.IsHost);
        }
    }

    private void OnEnable()
    {
        pauseAction.Enable();
        pauseAction.performed += OnPause;
    }

    private void OnDisable()
    {
        pauseAction.performed -= OnPause;
        pauseAction.Disable();
    }

    public void ShowPauseScreen()
    {
        menuRoot.SetActive(true);
        HelperUtilities.UpdateCursorLock(false);
        isPaused = true;
    }

    public void Resume()
    {
        menuRoot.SetActive(false);
        HelperUtilities.UpdateCursorLock(true);
        isPaused = false;
    }

    public void Restart()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            var sceneName = SceneManager.GetActiveScene().name;
            var status = NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"Failed to load {sceneName} " +
                      $"with a {nameof(SceneEventProgressStatus)}: {status}");
            }
        }
    }

    public void Leave()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            //PlayerCharacterSpawner.Instance.DestroyAllSpawnedCharacters();

            var status = NetworkManager.Singleton.SceneManager.LoadScene(mainMenuScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"Failed to load {mainMenuScene} " +
                      $"with a {nameof(SceneEventProgressStatus)}: {status}");
            }
        }
        else
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(mainMenuScene);
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            Resume();
        }
        else
        {
            ShowPauseScreen();
        }
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        TogglePause();
    }
}
}
