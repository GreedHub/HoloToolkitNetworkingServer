using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ServerClient {
    public int connectionId;
    public string playerName;
    public GameObject playerPrefab;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
}

public class PlayBox {
    public int boxId;
    public GameObject prefab;
}

public class NetworkServer : MonoBehaviour {

    [Header("Network Properties")]
    public string hostIp = "127.0.0.1";
    public int port = 3000;
    public GameObject playerPrefab;
    public GameObject boxToPlay;
    public int boxQuantity = 20;
    List<PlayBox> listOfBoxes = new List<PlayBox>();
    public float networkMessageSendRate = 0.05f;
    private float lastSentTime;

    private const int MAX_CONNECTIONS = 100;
    private int hostId;
    private int webHostId;

    private int reliableChannel;
    private int unreliableChannel;

    private int connectionId;

    private List<ServerClient> clientsList = new List<ServerClient>();

    private float connectionTime;
    private bool isConnected = false;
    private bool isStarted = false;
    private byte error;

    private string playerName;

    [Header("Lerping Properties")]
    public bool isLearpingPosition;
    public bool isLearpingRotation;
    public Vector3 realPosition;
    public Quaternion realRotation;
    public Vector3 lastRealPosition;
    public Quaternion lastRealRotation;
    public float timeStartedLearping;
    public float timeToLerp;


    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig connectionConfig = new ConnectionConfig();

        reliableChannel = connectionConfig.AddChannel(QosType.Reliable);
        unreliableChannel = connectionConfig.AddChannel(QosType.Unreliable);

        HostTopology networkTopology = new HostTopology(connectionConfig, MAX_CONNECTIONS);

        hostId = NetworkTransport.AddHost(networkTopology, port, null);
        connectionId = NetworkTransport.AddWebsocketHost(networkTopology, port, null);

        SpawnBoxes();

        isStarted = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isStarted) return;

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
                Debug.Log("Player" + connectionId + " connected");
                OnClientConnection(connectionId);
                break;

            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                //Debug.Log("Player" + connectionId + " sent " + msg);

                string[] msgArray = msg.Split('|');

                switch (msgArray[0])
                {
                    case "PLYRNAME":

                        Debug.Log("Player" + connectionId + " sent " + msg);
                        SpawnClientPlayer(clientsList.Find(x => x.connectionId == connectionId), msgArray[1]);

                        //Send new player name to other players
                        msg = "ADDPLAYER|" + msgArray[1] + "|" + connectionId;
                        ResendMessageToOtherPlayers(msg, connectionId);
                       

                        break;

                    case "UPDCARTRANS":
                        Vector3 updatedPosition = new Vector3(ParseFloatUnit(msgArray[1]), ParseFloatUnit(msgArray[2]), ParseFloatUnit(msgArray[3]));
                        Quaternion updatedRotation = new Quaternion(ParseFloatUnit(msgArray[4]), ParseFloatUnit(msgArray[5]), ParseFloatUnit(msgArray[6]), ParseFloatUnit(msgArray[7]));
                        MoveClientPlayer(clientsList.Find(x => x.connectionId == connectionId), updatedPosition, updatedRotation);

                        msg = msg+ "|" + connectionId;
                        ResendMessageToOtherPlayers(msg, connectionId);
                        break;

                    case "UPDATEBOX":

                        Vector3 boxPosition = new Vector3(ParseFloatUnit(msgArray[1]), ParseFloatUnit(msgArray[2]), ParseFloatUnit(msgArray[3]));
                        Quaternion boxRotation = new Quaternion(ParseFloatUnit(msgArray[4]), ParseFloatUnit(msgArray[5]), ParseFloatUnit(msgArray[6]), ParseFloatUnit(msgArray[7]));
                        MoveBox(listOfBoxes.Find(x => x.boxId == int.Parse(msgArray[8])), boxPosition, boxRotation);

                        msg = msg + "|" + connectionId;
                        ResendMessageToOtherPlayers(msg, connectionId);
                        break;

                }

                break;

            case NetworkEventType.DisconnectEvent:
                Debug.Log("Player" + connectionId + " disconnected");
                var itemToRemove = clientsList.Single(x => x.connectionId == connectionId);
                Destroy(itemToRemove.playerPrefab);
                clientsList.Remove(itemToRemove);
                msg = "PLAYERDC|" + connectionId;
                ResendMessageToOtherPlayers(msg, connectionId);
                break;
        }

    }

    private void MoveBox(PlayBox box, Vector3 updatedPosition, Quaternion updatedRotation)
    {
        if (box.prefab != null)
        {
            box.prefab.transform.position = updatedPosition;
            box.prefab.transform.rotation = updatedRotation;
        }
    }

    private void SpawnBoxes()
    {        
        for (int i = 0; i < boxQuantity; i++)
        {
            PlayBox box = new PlayBox();
            box.boxId = i;
            Vector3 position = new Vector3(Random.Range(-21.0f, 21.0f), 0.548f, Random.Range(-21.0f, 21.0f));
            box.prefab = Instantiate(boxToPlay, position, Quaternion.identity);
            listOfBoxes.Add(box);
        }
    }



    private void ResendMessageToOtherPlayers(string msg, int senderConnectionId)
    {
        List<ServerClient> otherClients = new List<ServerClient>();

        foreach (ServerClient client_found in clientsList)
        {
            if (client_found.connectionId != senderConnectionId)
            {
                otherClients.Add(client_found);
            }
        }

        if (otherClients.Count > 0)
        {
            SendNetworkMessage(msg, reliableChannel, otherClients);
        }
    }

    private float ParseFloatUnit(string value)
    {
        float parsedFloat;

        if (float.TryParse(value, out parsedFloat))
        {
            return parsedFloat;
        }
        else
        {
            Debug.Log("cannot parse '" + value + "'");
            return 0;
        }

    }

    private void SpawnClientPlayer(ServerClient player, string name)
    {
        player.playerName = name;
        player.playerPrefab = Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
    }

    private void MoveClientPlayer(ServerClient player, Vector3 updatedPosition, Quaternion updatedRotation)
    {
        if (player.playerPrefab != null)
        {
            player.playerPrefab.transform.position = updatedPosition;
            player.playerPrefab.transform.rotation = updatedRotation;
        }
    }

    private void OnClientConnection(int connectionId)
    {
        //Save the player into a list
        ServerClient client = new ServerClient();
        client.connectionId = connectionId;
        client.playerName = "TEMP";
        clientsList.Add(client);

        //Tell the player his ID and the list of other people and ask the for his name
        string msg = "ASKNAME|" + connectionId + "|";
        foreach (ServerClient c in clientsList)
        {
            if(c.connectionId != connectionId)
            {
                msg += c.playerName + "%" + c.connectionId + "|";
            }
        }
        msg = msg.Trim('|');
        SendNetworkMessage(msg, reliableChannel, connectionId);

        foreach(PlayBox box in listOfBoxes)
        {
            msg = "SPAWNBOX|";            
            msg += box.prefab.transform.position.x + "|";
            msg += box.prefab.transform.position.y + "|";
            msg += box.prefab.transform.position.z + "|";
            msg += box.prefab.transform.rotation.x + "|";
            msg += box.prefab.transform.rotation.y + "|";
            msg += box.prefab.transform.rotation.z + "|";
            msg += box.prefab.transform.rotation.w + "|";
            msg += box.boxId;
            SendNetworkMessage(msg, reliableChannel, connectionId);
        }
        
    }

    private void SendNetworkMessage(string message, int channel, int connectionId)
    {
        List<ServerClient> client = new List<ServerClient>();
        client.Add(clientsList.Find(x => x.connectionId == connectionId));
        SendNetworkMessage(message, channel, client);
    }

    private void SendNetworkMessage(string message, int channel, List<ServerClient> _clientsList)
    {

        byte[] msg = Encoding.Unicode.GetBytes(message);

        foreach (ServerClient client in _clientsList)
        {
            Debug.Log("Sending '" + message + "' to " + client.playerName);
            NetworkTransport.Send(hostId, client.connectionId, channel, msg, msg.Length, out error);
        }
    }
}

