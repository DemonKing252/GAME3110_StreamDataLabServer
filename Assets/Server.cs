using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

[System.Serializable]
public class SharingRoom
{
    public string name;

    public List<int> connectionIds = new List<int>();
    public SharingRoom()
    {

    }

}
public static class ClientToServerSignifiers
{
    public const int JoinSharingRoom = 1;

    public const int PartyDataTransferStart = 101;
    public const int PartyDataTransfer = 102;
    public const int PartyDataTransferEnd = 103;

}
public static class ServerToClientSignifiers
{
    public const int RecievedPartys = 102;

}


public class Server : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    public List<SharingRoom> sharingRooms;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        sharingRooms = new List<SharingRoom>();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                PlayerDisconnected(recConnectionID);
                break;
        }

    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);
        
        if (signifier == ClientToServerSignifiers.JoinSharingRoom)
        {
            string roomToJoin = csv[1];

            SharingRoom foundSharingRoom = null;

            // If we find a sharing room with this name, add our connection id to that 
            foreach(SharingRoom sr in sharingRooms)
            {
                if (sr.name == roomToJoin)
                {
                    foundSharingRoom = sr;
                    
                    sr.connectionIds.Add(id);

                    break;
                }
            }
            if (foundSharingRoom == null)
            {
                foundSharingRoom = new SharingRoom();
                foundSharingRoom.name = roomToJoin;
                foundSharingRoom.connectionIds.Add(id);
                sharingRooms.Add(foundSharingRoom);
            }
        }
        else if (signifier == ClientToServerSignifiers.PartyDataTransfer)
        {
            SharingRoom sr = GetSharingRoom(id);
            if (sr != null)
            {
                Debug.Log("we made it");
                foreach(int pId in sr.connectionIds)
                {
                    if (pId != id)  // dont send back to ourselves
                        SendMessageToClient(msg, pId);
                }
            }
        }
    
    }
    private void PlayerDisconnected(int id)
    {
        SharingRoom foundSR = null;
        foreach(SharingRoom sr in sharingRooms)
        {
            foreach(int pId in sr.connectionIds)
            {
                if (pId == id)
                {
                    foundSR = sr;
                    
                    break;
                }
            }
        }
        if (foundSR != null)
        {
            foundSR.connectionIds.Remove(id);

            if (foundSR.connectionIds.Count == 0)
                sharingRooms.Remove(foundSR);
        }

    }
    private SharingRoom GetSharingRoom(int id)
    {
        foreach(SharingRoom sr in sharingRooms)
        {
            foreach(int pId in sr.connectionIds)
            {
                if (pId == id)
                {
                    return sr;
                }
            }
        }
        return null;
    }

}