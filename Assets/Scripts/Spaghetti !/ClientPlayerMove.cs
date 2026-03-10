using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ClientPlayerMove : NetworkBehaviour
{
    [SerializeField] private GameObject camera;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private RadialCursor8Way radialCursor;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        camera.SetActive(false);
        playerInput.enabled = false;
        playerMovement.enabled = false;
        radialCursor.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            camera.SetActive(true);
            playerInput.enabled = true;
            playerMovement.enabled = true;
            radialCursor.enabled = true;
        }
    }
}
