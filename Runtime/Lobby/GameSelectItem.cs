using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Azeesoft.Multiplayer
{
    public class GameSelectItem : MonoBehaviour
    {
        public TextMeshProUGUI sceneNameText;
        public Image background;
        public Button button;

        [Header("Colors")]
        public Color selectedColor;

        private Color regularColor;

        private void Awake()
        {
            regularColor = background.color;
        }

        void Update()
        {
            if (!SimpleLobbyUI.Instance)
            {
                return;
            }

            background.color = SimpleLobbyUI.Instance.SelectedGameSceneName == sceneNameText.text ? selectedColor : regularColor;
        }
    }
}
