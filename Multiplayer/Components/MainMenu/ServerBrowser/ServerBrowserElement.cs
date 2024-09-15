using DV.UIFramework;
using Multiplayer.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Multiplayer.Components.MainMenu.ServerBrowser
{
    public class ServerBrowserElement : AViewElement<IServerBrowserGameDetails>
    {
        private TextMeshProUGUI networkName;
        private TextMeshProUGUI playerCount;
        private TextMeshProUGUI ping;
        private GameObject goIcon;
        private Image icon;
        private IServerBrowserGameDetails data;

        private const int PING_WIDTH = 124; // Adjusted width for the ping text
        private const int PING_POS_X = 650; // X position for the ping text

        protected override void Awake()
        {
            // Find and assign TextMeshProUGUI components for displaying server details
            networkName = this.FindChildByName("name [noloc]").GetComponent<TextMeshProUGUI>();
            playerCount = this.FindChildByName("date [noloc]").GetComponent<TextMeshProUGUI>();
            ping = this.FindChildByName("time [noloc]").GetComponent<TextMeshProUGUI>();
            goIcon = this.FindChildByName("autosave icon");
            icon = goIcon.GetComponent<Image>();

            // Fix alignment of the player count text relative to the network name text
            Vector3 namePos = networkName.transform.position;
            Vector2 nameSize = networkName.rectTransform.sizeDelta;
            playerCount.transform.position = new Vector3(namePos.x + nameSize.x, namePos.y, namePos.z);

            // Adjust the size and position of the ping text
            Vector2 rowSize = transform.GetComponentInParent<RectTransform>().sizeDelta;
            Vector3 pingPos = ping.transform.position;
            Vector2 pingSize = ping.rectTransform.sizeDelta;

            ping.rectTransform.sizeDelta = new Vector2(PING_WIDTH, pingSize.y);
            ping.transform.position = new Vector3(PING_POS_X, pingPos.y, pingPos.z);
            ping.alignment = TextAlignmentOptions.Right;

            // Set change icon
            icon.sprite = Multiplayer.AssetIndex.lockIcon;
        }

        public override void SetData(IServerBrowserGameDetails data, AGridView<IServerBrowserGameDetails> _)
        {
            // Clear existing data
            if (this.data != null)
            {
                this.data = null;
            }
            // Set new data
            if (data != null)
            {
                this.data = data;
            }
            // Update the view with the new data
            UpdateView();
        }

        public void UpdateView()
        {
            // Update the text fields with the data from the server
            networkName.text = data.Name;
            playerCount.text = $"{data.CurrentPlayers} / {data.MaxPlayers}";
            ping.text = $"<color={GetColourForPing(data.Ping)}>{(data.Ping < 0 ? "?" : data.Ping)} ms</color>";

            // Hide the icon if the server does not have a password
            if (!data.HasPassword)
            {
                goIcon.SetActive(false);
            }
        }

        private string GetColourForPing(int ping)
        {
            switch (ping)
            {
                case -1:
                    return "#808080";  // Mid-range gray for unknown
                case < 60:
                    return "#00ff00";  // Bright green for excellent ping
                case < 100:
                    return "#ffa500";  // Orange for good ping
                case < 150:
                    return "#ff4500";  // OrangeRed for high ping
                default:
                    return "#ff0000";  // Red for don't even bother
            }
        }
    }
}
