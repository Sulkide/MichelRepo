using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
	public static RelayManager instance;

	[SerializeField] TMP_InputField joinCodeTextField;

	void Awake()
	{
		instance = this;
	}

	[ContextMenu("Create Relay")]
	public void CreateRelayButton()
	{
		CreateRelay();
	}

	public async Task<string> CreateRelay()
	{
		try
		{
			// on mets 3 car on veut 3 clients l'host inclus de base donc on aura bien 4 joueur
			Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

			string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

			Debug.Log("Join Code: " + joinCode);

			// NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
			//     allocation.RelayServer.IpV4,
			//     (ushort)allocation.RelayServer.Port,
			//     allocation.AllocationIdBytes,
			//     allocation.Key,
			//     allocation.ConnectionData
			// );

			// RelayServerData relayServerData = new(allocation, "dtls");
			RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
			NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

			NetworkManager.Singleton.StartHost();

			return joinCode;
		}
		catch (RelayServiceException e)
		{
			Debug.Log(e);
			return null;
		}
	}

	public void JoinRelayButton()
	{
		JoinRelay(joinCodeTextField.text);
	}

	public async void JoinRelay(string joinCode)
	{
		try
		{
			Debug.Log("Joining Relay with " + joinCode);
			JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

			// NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
			//     joinAllocation.RelayServer.IpV4,
			//     (ushort)joinAllocation.RelayServer.Port,
			//     joinAllocation.AllocationIdBytes,
			//     joinAllocation.Key,
			//     joinAllocation.ConnectionData,
			//     joinAllocation.HostConnectionData
			// );

			// RelayServerData relayServerData = new(joinAllocation, "dtls");
			RelayServerData relayServerData = joinAllocation.ToRelayServerData("dtls");
			NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

			NetworkManager.Singleton.StartClient();
		}
		catch (RelayServiceException e)
		{
			Debug.Log(e);
		}
	}
}