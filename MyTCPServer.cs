using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.Diagnostics;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronSockets;

namespace AisleTCPLibrary
{
    public class MyTCPServer
    {
        public EventHandler DataReceivedEvent;
        TCPServer tcpServer;
        private string addressToAccept = "0.0.0.0"; // Accepts Connection from any client.
        private int port;                           // Port Number.    
        private int buffSize;                       // Buffer Size.
        private int maxConnections;                 // Maximum number of connected Clients.
        private string rcvString;                   // String received from the client.
        private string delimiter;                   // Lets us know when we gathered all of the data

        public string RcvString
        {
            get { return rcvString; }
            set { rcvString = value; }
        }

        public MyTCPServer(int port, int buffSize, int maxConnections, string delimiter)
        {
            this.port = port;
            this.buffSize = buffSize;
            this.maxConnections = maxConnections;
            this.delimiter = delimiter;

            tcpServer = new TCPServer(addressToAccept, port, buffSize, EthernetAdapterType.EthernetLANAdapter, maxConnections);
            tcpServer.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(server_SocketStatusChange);
        }

        public void ListenForConnection()
        {
            SocketErrorCodes error = tcpServer.WaitForConnectionAsync(ServerConnectedCallback);
            CrestronConsole.PrintLine("WaitForConnectionAsync return: " + error);
        }

        private void ServerConnectedCallback(TCPServer myTCPServer, uint clientIndex)
        {
            if (clientIndex != 0) {
                CrestronConsole.PrintLine("Server listening on port #" + myTCPServer.PortNumber);
                CrestronConsole.PrintLine("Connected: client #" + clientIndex);

                /* Starts listeing for the specific client index. */
                myTCPServer.ReceiveDataAsync(clientIndex, ServerDataReceivedCallback);

                if (myTCPServer.MaxNumberOfClientSupported == myTCPServer.NumberOfClientsConnected) {
                    CrestronConsole.PrintLine("Client limit reached. Server will stop listening.");
                    myTCPServer.Stop();
                    CrestronConsole.PrintLine("Server State: " + myTCPServer.State);
                }
            }
                /* A clientIndex of 0 could mean that the server is no longer listening, or that the TLS handshake failed when a client tried to connect.
                 * In the case of a TLS handshake failure, wait for another connection so that other clients can still connect. */
            else {
                CrestronConsole.Print("Error in ServerConnectedCallback: ");
                if ((myTCPServer.State & ServerState.SERVER_NOT_LISTENING) > 0) {
                    CrestronConsole.PrintLine("Server is no longer listening.");
                } else {
                    CrestronConsole.PrintLine("Unable to make connection with client.");
                    /* This connection failed, but keep waiting for another. */
                    myTCPServer.WaitForConnectionAsync(ServerConnectedCallback);
                }
            }

        }

        private void ServerDataReceivedCallback(TCPServer myTCPServer, uint clientIndex, int totalBytesRcv)
        {
            /* 0 or negative byte count indicates the connection has been closed. */
            if (totalBytesRcv <= 0) {
                CrestronConsole.PrintLine("Error: server's connection with client " + clientIndex + " has been closed.");
                myTCPServer.Disconnect(clientIndex);

                /* If server is not listening because of max number of clients, It will start listening again because a client disconnected. */
                if ((myTCPServer.State & ServerState.SERVER_NOT_LISTENING) > 0)
                    myTCPServer.WaitForConnectionAsync(ServerConnectedCallback);
            } else {
                byte[] rcvBytes = new byte[totalBytesRcv];
                Array.Copy(myTCPServer.GetIncomingDataBufferForSpecificClient(clientIndex), rcvBytes, totalBytesRcv);
                rcvString += Encoding.UTF8.GetString(rcvBytes, 0, totalBytesRcv);

                /* Use this block of code to see the data comming into the server on the CrestronConsole*/
                //CrestronConsole.PrintLine("----------- incoming message -----------\n\r" + rcvString + "\n\r---------- end of message ----------"); 

                /* Event will be called if the expected delimiter is found in the string. Clears the rcvString. */
                if (DataReceivedEvent != null && rcvString.IndexOf(delimiter) >= 0) {
                    DataReceivedEvent.Invoke(this, EventArgs.Empty);
                    rcvString = "";
                }

                /* Begin waiting for another message from that same client. */
                myTCPServer.ReceiveDataAsync(clientIndex, ServerDataReceivedCallback);
            }
        }

        private void server_SocketStatusChange(TCPServer myTCPServer, uint clientIndex, SocketStatus socketStatus)
        {
            /* Lets us know if the connection was succesfull or not. It also lets us know which specific client had a succesfull connection. */
            if (socketStatus == SocketStatus.SOCKET_STATUS_CONNECTED) {
                CrestronConsole.PrintLine("Client #" + clientIndex + "connected");
            } else {
                CrestronConsole.PrintLine("Client #" + clientIndex + ": " + socketStatus + ".");
            }
        }
    }
}

