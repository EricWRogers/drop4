using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ServerClient
{
	public int connectionId;
	public string playerName;
	public Vector3 position;
	public Quaternion rotation;
}

public class Server : MonoBehaviour 
{
	private const int MAX_CONNECTION = 100;

	private int port = 7777;

	private int hostId;
	//private int webHostId;

	private int reliableChannel;
	private int unrealiableChannel;

	private bool isStarted = false;
	private byte error = 0;

	private List<ServerClient> clients = new List<ServerClient>();

	private float lastMovementUpdate;
	public float movementUpdateRate = 0.1f;

	private void Start()
	{

		NetworkTransport.Init ();
		ConnectionConfig cc = new ConnectionConfig ();


		reliableChannel = cc.AddChannel (QosType.Reliable);
		unrealiableChannel = cc.AddChannel (QosType.Unreliable);

		HostTopology topo = new HostTopology (cc, MAX_CONNECTION);

		hostId = NetworkTransport.AddHost (topo, port, null);
		//WEBSupport
		//webHostId = NetworkTransport.AddWebsocketHost(topo,port,null);

		isStarted = true;
	}

	private void Update()
	{
		if (!isStarted)
			return;

		int recHostId; 
		int connectionId; 
		int channelId; 
		byte[] recBuffer = new byte[1024]; 
		int bufferSize = 1024;
		int dataSize;
		//byte error;
		NetworkEventType recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);
		switch (recData)
		{
		case NetworkEventType.Nothing:         //1
			break;
		case NetworkEventType.ConnectEvent:    //2
			Debug.Log("Player" + connectionId + "has connected");
			OnConnection(connectionId);
			break;
		case NetworkEventType.DataEvent:       //3
			string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
			Debug.Log("Receiving from " + connectionId + ": " + msg);

			//this is cool shit
			string[] splitData = msg.Split('|');

			switch(splitData[0])
			{
				case "NAMEIS":
					OnNameIs(connectionId, splitData[1]);
					break;
				case "MYPOSITION":
				OnMyPosition(connectionId,float.Parse(splitData[1]),float.Parse(splitData[2]),float.Parse(splitData[3]),float.Parse(splitData[4]),float.Parse(splitData[5]),float.Parse(splitData[6]));
					break;
				case "MESSAGETOSERVER":
						OnGetMessage(splitData[1],splitData[2]);
					break;

				default:
					Debug.Log("Inalid message : " + msg);
					break;
			}
			break;
			//break;
		case NetworkEventType.DisconnectEvent: //4
			Debug.Log("Player" + connectionId + "has disconnected");
			OnDisconnection(connectionId);
			break;
		}

		//Ask player for their position
		if(Time.time - lastMovementUpdate > movementUpdateRate)
		{
			lastMovementUpdate = Time.time;
			string m = "ASKPOSITION|";
			foreach (ServerClient sc in clients)
				m += sc.connectionId.ToString() + '%' + sc.position.x.ToString() + '%' + sc.position.y.ToString() + '%' + sc.position.z.ToString() + '%' + sc.rotation.x.ToString() + '%' + sc.rotation.y.ToString() + '%' + sc.rotation.z.ToString() + '|';

			m = m.Trim('|');
			Send(m, unrealiableChannel, clients);
		}
	}
	private void OnGetMessage(string htifrom, string msg)
	{
		string c = "ChatFromServer|";
		foreach (ServerClient sc in clients)
			c += htifrom + ": " + msg + '|';
		c += c.Trim('|');
		Send(c, unrealiableChannel, clients);
	}
	private void OnConnection(int cnnId)
	{
		//Add him to a list
		ServerClient c = new ServerClient();
		c.connectionId = cnnId;
		c.playerName = "TEMP";
		clients.Add(c);

		//When the player joins give him a id

		//Request his name
		string msg = "ASKNAME|" + cnnId + "|"; 
		//Also send the name of all the other players
		foreach (ServerClient sc in clients)
			msg += sc.playerName + '%' + sc.connectionId + '|';
		//trimming the last pipe
		msg = msg.Trim('|');
		//Now to send the message
		Send(msg,reliableChannel,cnnId);

	}
	private void OnDisconnection(int cnnId)
	{
		// Remove this player from our client list
		clients.Remove(clients.Find(x => x.connectionId == cnnId));
		// Tell everyone that somebody else has disconnected
		Send("DC|" + cnnId, reliableChannel, clients);
	}
	private void OnMyPosition(int cnnId, float x, float y, float z, float xr, float yr, float zr)
	{
		clients.Find(c => c.connectionId == cnnId).position = new Vector3(x,y,z);
		clients.Find (r => r.connectionId == cnnId).rotation = new Quaternion (xr, yr, zr, 1);
	}
	private void OnNameIs(int cnnId, string playerName)
	{
		//Link the name to the connection Id
		clients.Find(x => x.connectionId == cnnId).playerName = playerName;
		//Tell everybody that a new player has connected
		Send("CNN|" + playerName + '|' + cnnId, reliableChannel,clients);
	}
	private void Send(string message, int channelId, int cnnId)
	{
		List<ServerClient> c = new List<ServerClient>();
		c.Add(clients.Find(x => x.connectionId == cnnId));
		Send(message, channelId, c);
	}
	//overloading
	private void Send(string message, int channelId, List<ServerClient> c)
	{
		Debug.Log("Sending : " + message);
		byte[] msg = Encoding.Unicode.GetBytes(message);
		foreach(ServerClient sc in c)
		{
			NetworkTransport.Send(hostId,sc.connectionId,channelId,msg,message.Length * sizeof(char), out error);
		}
	}
}
