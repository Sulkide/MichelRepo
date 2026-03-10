
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using Random = System.Random;

public class MultiUi : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    Button disconnectButton;
    private Material playerMat;

    void Awake()
    {
        playerMat = GetComponent<Renderer>().material;
    }
    private void Start()
    {
        Button hostButton = uiDocument.rootVisualElement.Q<Button>("host");
        Button clientButton = uiDocument.rootVisualElement.Q<Button>("Client");
        Button serverButton = uiDocument.rootVisualElement.Q<Button>("server");
        Button discButton = uiDocument.rootVisualElement.Q<Button>("disco");

        hostButton.clicked += OnHostButton;
        clientButton.clicked += OnClientClicked;
        serverButton.clicked += OnServerClicked;
        discButton.clicked += OnDisconnectClicked;
    }

    private void OnHostButton()
    {
        NetworkManager.Singleton.StartHost();
    }

    private void OnClientClicked()
    {
        NetworkManager.Singleton.StartClient();
    }

    private void OnServerClicked()
    {
        NetworkManager.Singleton.StartServer();
    }
    
    private void OnDisconnectClicked()
    {
        NetworkManager.Singleton.Shutdown();
    }
    
}