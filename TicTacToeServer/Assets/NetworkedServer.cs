using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    int playerWaitingForMatch = -1;

    LinkedList<GameSession> gameSessions;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccounts = new LinkedList<PlayerAccount> ();
        gameSessions = new LinkedList<GameSession> ();

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


        if (signifier == ClientToServerSignifiers.CreateAccount)
        {
            string name = csv[1];
            string pass = csv[1];

            bool usernameAlreadyInUse = false;

            foreach(PlayerAccount pa in playerAccounts)
            {
                if(pa.username == name)
                {
                    usernameAlreadyInUse = true;
                    break;
                }
            }

            if(!usernameAlreadyInUse)
            {
                PlayerAccount pa = new PlayerAccount(name, pass);
                playerAccounts.AddLast(pa);
                SendMessageToClient(ServerToClientSignifiers.accountSuccess + "", id);
            }
            else
            {
                SendMessageToClient(ServerToClientSignifiers.accountFail + "", id);
            }
           
        }
        else if (signifier == ClientToServerSignifiers.Login)
        {
            string name = csv[1];
            string pass = csv[1];

            bool usernameNotFound = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.username == name)
                {
                    usernameNotFound = true;
                   if(pa.password == pass)
                    {
                        SendMessageToClient(ServerToClientSignifiers.loginSuccess + "", id);
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.loginFail + "", id);
                    }
                }
            }
            if(!usernameNotFound)
            {
                SendMessageToClient(ServerToClientSignifiers.loginFail + "", id);
            }
        }


        else if(signifier == ClientToServerSignifiers.AddToGameRoomQueue)
        {
            Debug.Log("Waiting for match");
            if(playerWaitingForMatch == -1)
            {
                playerWaitingForMatch = id;
            }
            else
            {
                GameSession gs = new GameSession(playerWaitingForMatch, id); //GameSession = GameRoom
                gameSessions.AddLast(gs);

                SendMessageToClient(ServerToClientSignifiers.GameSessionStarted + "", id);
                SendMessageToClient(ServerToClientSignifiers.GameSessionStarted + "", playerWaitingForMatch);

                playerWaitingForMatch = -1;
            }


        }

        else if (signifier == ClientToServerSignifiers.TicTacToePlay)
        {
            Debug.Log("Joining game!");

           GameSession gs = FindGameSessionWithPlayerID(id);

            if(gs != null)
            {
                if (gs.playerID1 == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentTicTacToePlay + "", gs.playerID2);
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentTicTacToePlay + "", gs.playerID1);
                }
            }         

        }

    }

    private GameSession FindGameSessionWithPlayerID(int id)
    {
        foreach(GameSession gs in gameSessions)
        {
            if(gs.playerID1 == id || gs.playerID2 == id)
            {
                return gs;
            }        
        }
        return null;
    }
}

public class PlayerAccount
{
    public string username;
    public string password;

    public PlayerAccount(string username, string password)
    {
        this.username = username;
        this.password = password;
    }
}

public class GameSession
{
    //Hold 2 clients
    public int playerID1, playerID2;

    public GameSession(int playerID1, int playerID2)
    {
        this.playerID1 = playerID1;
        this.playerID2 = playerID2;
    }
}

public static class ClientToServerSignifiers
{
    public const int Login = 1;

    public const int CreateAccount = 2;

    public const int AddToGameRoomQueue = 3;
    public const int TicTacToePlay = 4;



}

public static class ServerToClientSignifiers
{
    //  public const int LoginResponse = 1;
   public const int loginSuccess = 1;
   public const int accountSuccess = 2;

   public const int loginFail = 3;
   public const int accountFail = 4;

    public const int GameSessionStarted = 5;

    public const int OpponentTicTacToePlay = 6;


}