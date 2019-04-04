using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class NetworkClient : MonoBehaviour
{
    [Header("Network Properties")]
    public string hostIp = "127.0.0.1";
    public int port = 3000;
    public GameObject playerPrefab;
    public float networkMessageSendRate = 0.1f;
    private float lastSentTime;


    private const int MAX_CONNECTIONS = 100;
    private int hostId;
    private int webHostId;

    private int reliableChannel;
    private int unreliableChannel;

    private int connectionId;

    private float connectionTime;
    private bool isConnected = false;
    private bool isStarted = false;
    private byte error;


    private string playerName;

    public void Connect()
    {
        playerName = "player_a";

        NetworkTransport.Init();
        ConnectionConfig connectionConfig = new ConnectionConfig();

        reliableChannel = connectionConfig.AddChannel(QosType.Reliable);
        unreliableChannel = connectionConfig.AddChannel(QosType.Unreliable);

        HostTopology networkTopology = new HostTopology(connectionConfig, MAX_CONNECTIONS);

        hostId = NetworkTransport.AddHost(networkTopology, 0);
        connectionId = NetworkTransport.Connect(hostId, hostIp, port, 0, out error);

        connectionTime = Time.time;
        isConnected = true;

    }

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        Connect();
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        if (!isConnected)
        {
            return;
        }

        int recHostId;
        int connectionId;
        int channelId;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error;

        NetworkEventType recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);

        switch (recData)
        {
            case NetworkEventType.Nothing:
                break;

            case NetworkEventType.ConnectEvent:
                Debug.Log("I've connected");
                break;

            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                Debug.Log("Recived: " + msg);
                string[] msgArray = msg.Split('|');

                switch (msgArray[0])
                {
                    case "ASKNAME":
                        string playerNameObtained = playerName;
                        SendNetworkMessage("PLYRNAME|" + playerNameObtained, reliableChannel, connectionId);

                        break;
                }



                break;

            case NetworkEventType.DisconnectEvent:
                break;
        }
        if ((Time.time - lastSentTime) > networkMessageSendRate)
        {
            //  Debug.Log(Time.time - lastSentTime);
            UpdateServerCar();
            lastSentTime = Time.time;
        }
    }

    private void SendNetworkMessage(string message, int channel, int connectionId)
    {

        //Debug.Log("Sending: " + message);
        byte[] msg = Encoding.Unicode.GetBytes(message);
        NetworkTransport.Send(hostId, connectionId, channel, msg, msg.Length * sizeof(char), out error);

    }

    private void UpdateServerCar()
    {
        string msg = "UPDCARTRANS|";
        msg += playerPrefab.transform.position.x + "|";
        msg += playerPrefab.transform.position.y + "|";
        msg += playerPrefab.transform.position.z + "|";
        msg += playerPrefab.transform.rotation.x + "|";
        msg += playerPrefab.transform.rotation.y + "|";
        msg += playerPrefab.transform.rotation.z + "|";
        msg += playerPrefab.transform.rotation.w + "|";

        SendNetworkMessage(msg, unreliableChannel, connectionId);
    }
}
