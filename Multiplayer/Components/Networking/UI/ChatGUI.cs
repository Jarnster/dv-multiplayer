using System;
using System.Collections.Generic;
using DV;
using DV.UI;
using DV.UI.Inventory;
using Multiplayer.Components.MainMenu;
using Multiplayer.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Multiplayer.Components.Networking.UI;

//[RequireComponent(typeof(Canvas))]
//[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(RectTransform))]
public class ChatGUI : MonoBehaviour
{
    private const float PANEL_LEFT_MARGIN = 20f;
    private const float PANEL_BOTTOM_MARGIN = 50f;

    private const float MESSAGE_INSET = 15f;
    private const int MAX_MESSAGES = 50;
    private const int MESSAGE_TIMEOUT = 10;

    private GameObject messagePrefab;

    public List<Message> messageList = new List<Message>();

    private TMP_InputField chatInputIF;
    private ScrollRect scrollRect;
    private RectTransform chatPanel;

    private GameObject panelGO;
    private GameObject textInputGO;
    private GameObject scrollViewGO;

    private bool isOpen = false;
    private bool showingMessage = false;

    private CustomFirstPersonController player;
    private HotbarController hotbarController;

    private float timeOut;

    private void Awake()
    {
        Debug.Log("ChatGUI Awake() called");

        SetupOverlay(); //sizes and positions panel

        BuildUI();      //Creates input fields and scroll area

        panelGO.SetActive(false); //We don't need this to be visible when the game launches

        //Find the player and toolbar so we can block input
        player = GameObject.FindObjectOfType<CustomFirstPersonController>();
        if(player == null)
        {
            Debug.Log("Failed to find CustomFirstPersonController");
        }

        hotbarController = GameObject.FindObjectOfType<HotbarController>();
        if (hotbarController == null)
        {
            Debug.Log("Failed to find HotbarController");
        }

    }

    private void OnEnable()
    {
        chatInputIF.onSubmit.AddListener(SendChat);
        
    }

    private void OnDisable()
    {
        chatInputIF.onSubmit.RemoveAllListeners();
    }

    private void Update()
    {
   
        //Handle keypresses to open/close the chat window
        if (!isOpen && Input.GetKeyDown(KeyCode.Return))
        {
            isOpen = true;              //whole panel is open
            showingMessage = false;     //We don't want to time out

            panelGO.SetActive(isOpen);
            textInputGO.SetActive(isOpen);

            BlockInput(true);
        }
        else if (isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return)))
        {
            isOpen = false;
            if (showingMessage)
            {
                textInputGO.SetActive(isOpen);
            }
            else
            {
                panelGO.SetActive(isOpen);
            }

            BlockInput(false);
        }

        //Maintain focus on the text input field
        if(isOpen && !chatInputIF.isFocused)
        {
            chatInputIF.ActivateInputField();
        }

        //After a message is sent/received, keep displaying it for the timeout period
        //Would be nice to add a fadeout in future
        if (showingMessage)
        {
            timeOut += Time.deltaTime;

            if (timeOut >= MESSAGE_TIMEOUT)
            {
                showingMessage = false ;
                panelGO.SetActive(false);
            } 
        }
    }

    public void SendChat(string text)
    {
        if (text.Trim().Length > 0)
        {
            if (messageList.Count > MAX_MESSAGES)
            {
                messageList.RemoveAt(0);
            }

            Message newMessage = new Message(text);
            messageList.Add(newMessage);

            GameObject messageObj = Instantiate(messagePrefab, chatPanel);
            messageObj.GetComponent<TextMeshProUGUI>().text = text;

        }

        chatInputIF.text = "";
        timeOut = 0;
        showingMessage = true;
        textInputGO.SetActive(false);
        BlockInput(false);

        return;
    }

    public void ReceiveMessage(Message received)
    {

    }


    #region UI

    private void SetupOverlay()
    {
        //Setup the host object
        RectTransform myRT = this.transform.GetComponent<RectTransform>();
        myRT.sizeDelta = new Vector2(Screen.width, Screen.height);
        myRT.anchorMin = Vector2.zero;
        myRT.anchorMax = Vector2.zero;
        myRT.pivot = Vector2.zero;
        myRT.anchoredPosition = Vector2.zero;


        // Create a Panel
        panelGO = new GameObject("OverlayPanel");
        panelGO.transform.SetParent(this.transform, false);
        RectTransform rectTransform = panelGO.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(Screen.width * 0.25f, Screen.height * 0.25f);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        rectTransform.anchoredPosition = new Vector2(PANEL_LEFT_MARGIN, PANEL_BOTTOM_MARGIN);
    }

    private void BuildUI()
    {
        GameObject scrollViewPrefab = null;
        GameObject inputPrefab;

        //get prefabs
        PopupNotificationReferences popup = GameObject.FindObjectOfType<PopupNotificationReferences>();
        SaveLoadController saveLoad = GameObject.FindObjectOfType<SaveLoadController>();

        if (popup == null)
        {
            Debug.Log("Could not find PopupNotificationReferences");
            return;
        }
        else
        {
            inputPrefab = popup.popupTextInput.FindChildByName("TextFieldTextIcon");//MainMenuThingsAndStuff.Instance.renamePopupPrefab.gameObject.FindChildByName("TextFieldTextIcon");
        }

        if (saveLoad == null)
        {
            Debug.Log("Could not find SaveLoadController, attempting to instanciate");
            AppUtil.Instance.PauseGame();

            Debug.Log("Paused");

            saveLoad = FindObjectOfType<PauseMenuController>().saveLoadController;

            if (saveLoad == null)
            {
                Debug.Log("Failed to get SaveLoadController");
            }
            else
            {
                Debug.Log("Made a SaveLoadController!");
                scrollViewPrefab = saveLoad.FindChildByName("Scroll View");

                if (scrollViewPrefab == null)
                {
                    Debug.Log("Could not find scrollViewPrefab");
                    
                }
                else
                {
                    scrollViewPrefab = Instantiate(scrollViewPrefab);
                }
            }

            AppUtil.Instance.UnpauseGame();
        }
        else
        {
            scrollViewPrefab = saveLoad.FindChildByName("Scroll View");
        }


        if (inputPrefab == null)
        {
            Debug.Log("Could not find inputPrefab");
            return;
        }
        if (scrollViewPrefab == null)
        {
            Debug.Log("Could not find scrollViewPrefab");
            return;
        }


        //Add an input box
        textInputGO = Instantiate(inputPrefab);
        textInputGO.name = "Chat Input";
        textInputGO.transform.SetParent(panelGO.transform, false);

        //Remove redundant components
        GameObject.Destroy(textInputGO.FindChildByName("icon"));
        GameObject.Destroy(textInputGO.FindChildByName("image select"));
        GameObject.Destroy(textInputGO.FindChildByName("image hover"));
        GameObject.Destroy(textInputGO.FindChildByName("image click"));

        //Position input
        RectTransform textInputRT = textInputGO.GetComponent<RectTransform>();
        textInputRT.pivot = Vector3.zero;
        textInputRT.anchorMin = Vector2.zero;
        textInputRT.anchorMax = new Vector2(1f, 0);

        textInputRT.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 0, 20f);
        textInputRT.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 1f);

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        textInputRT.sizeDelta = new Vector2 (panelRT.rect.width, 40f);

        //Setup input
        chatInputIF = textInputGO.GetComponent<TMP_InputField>();
        chatInputIF.onFocusSelectAll = false;
        textInputGO.FindChildByName("text [noloc]").GetComponent<TMP_Text>().fontSize = 18;
        chatInputIF.placeholder.GetComponent<TMP_Text>().text = "Type a message and press Enter!";




        //Add a new scroll pane
        scrollViewGO = Instantiate(scrollViewPrefab);
        scrollViewGO.name = "Chat Scroll";
        scrollViewGO.transform.SetParent(panelGO.transform, false);

        //Position scroll pane
        RectTransform scrollViewRT = scrollViewGO.GetComponent<RectTransform>();
        scrollViewRT.pivot = Vector3.zero;
        scrollViewRT.anchorMin = Vector2.zero;
        scrollViewRT.anchorMax = new Vector2(1f, 0);

        scrollViewRT.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, textInputRT.rect.height, 20f);
        scrollViewRT.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 1f);

        scrollViewRT.sizeDelta = new Vector2(panelRT.rect.width, panelRT.rect.height - textInputRT.rect.height);


        //Setup scroll pane
        GameObject viewport = scrollViewGO.FindChildByName("Viewport");
        RectTransform viewportRT = viewport.GetComponent<RectTransform>();
        ScrollRect scrollRect = scrollViewGO.GetComponent<ScrollRect>();

        viewportRT.pivot = new Vector2(0.5f, 0.5f);
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one; 
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        scrollRect.viewport = scrollViewRT;

        //set up content
        GameObject.Destroy(scrollViewGO.FindChildByName("GRID VIEW").gameObject);
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter), typeof(VerticalLayoutGroup));
        content.transform.SetParent(viewport.transform, false);

        ContentSizeFitter contentSF = content.GetComponent<ContentSizeFitter>();
        contentSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup contentVLG = content.GetComponent<VerticalLayoutGroup>();
        contentVLG.childAlignment = TextAnchor.LowerLeft;
        contentVLG.childControlWidth = false;
        contentVLG.childControlHeight = true;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;

        chatPanel = content.GetComponent<RectTransform>();
        chatPanel.pivot = Vector2.zero;
        chatPanel.anchorMin = Vector2.zero;
        chatPanel.anchorMax = new Vector2(1f, 0f);
        chatPanel.offsetMin = Vector2.zero;
        chatPanel.offsetMax = Vector2.zero;
        scrollRect.content = chatPanel;

        chatPanel.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, MESSAGE_INSET, chatPanel.rect.width - MESSAGE_INSET);

        //Realign vertical scroll bar
        RectTransform scrollBarRT = scrollRect.verticalScrollbar.transform.GetComponent<RectTransform>();
        Vector3 origPos = scrollBarRT.localPosition;

        scrollBarRT.localPosition = new Vector3(origPos.x, viewportRT.rect.height, origPos.z);
        scrollBarRT.sizeDelta = new Vector2(scrollBarRT.sizeDelta.x, viewportRT.rect.height);




        //Build message prefab
        messagePrefab = new GameObject("Message Text", typeof(TextMeshProUGUI));

        RectTransform messagePrefabRT = messagePrefab.GetComponent<RectTransform>();
        messagePrefabRT.pivot = new Vector2(0.5f, 0.5f);
        messagePrefabRT.anchorMin = new Vector2(0f, 1f);
        messagePrefabRT.anchorMax = new Vector2(0f, 1f);
        messagePrefabRT.offsetMin = new Vector2(0f, 0f);
        messagePrefabRT.offsetMax = Vector2.zero;
        messagePrefabRT.sizeDelta = new Vector2(chatPanel.rect.width, messagePrefabRT.rect.height);
      
        TextMeshProUGUI messageTM = messagePrefab.GetComponent<TextMeshProUGUI>();
        messageTM.textWrappingMode = TextWrappingModes.Normal;
        messageTM.fontSize = 18;
        messageTM.text = "Morm: Hurry up!";
    }

    private void BlockInput(bool block)
    {
        player.Locomotion.inputEnabled = !block;
        hotbarController.enabled = !block;
    }


    #endregion
}

public class Message
{
    public string text;
    public GameObject message;

    public Message(string text) {
        this.text = text;
    }
}
