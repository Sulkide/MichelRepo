using System;
using Unity.Netcode;
using UnityEngine;

public class TriggerCubeInteraction : NetworkBehaviour
{
    private Material _cubeMaterial;
    private NetworkVariable<Color> _cubeColor = new NetworkVariable<Color>(new Color(255, 0, 0));

    private void Awake()
    {
        _cubeMaterial = GetComponent<Material>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        _cubeColor.OnValueChanged += OnColorChange;
        if (IsServer)
        {
            _cubeColor.Value = Color.red;
        }
        _cubeMaterial.color = _cubeColor.Value;
    }

    private void OnColorChange(Color oldColor, Color newColor)
    {
        _cubeMaterial.color = newColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsServer)
        {
            _cubeColor.Value = other.GetComponent<Renderer>().material.color;
        }
    }
}