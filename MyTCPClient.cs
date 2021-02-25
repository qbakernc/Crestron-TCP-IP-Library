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
    public class MyTCPClient
    {
        public EventHandler DataReceivedEvent;
        private TCPClient tcpClient;
        private string IpAddress;     // IpAddress.
        private int port;             // Port Number.    
        private int buffSize;         // Buffer Size.
        private Byte[] sendBytes;     // Data to Send
        private string rcvString;     // String received from the Server
        private string delimiter;     // This is what we will be looking for in the response from the server. Lets us know when we gathered all of the data

        public string RcvString
        {
            get { return rcvString; }
            set { rcvString = value; }
        }
        public MyTCPClient(string IpAddress, int port, int buffSize, string delimiter)
        {
            this.IpAddress = IpAddress;
            this.port = port;
            this.buffSize = buffSize;
            this.delimiter = delimiter;

            tcpClient = new TCPClient(this.IpAddress, this.port, this.buffSize);
            tcpClient.SocketStatusChange += new TCPClientSocketStatusChangeEventHandler(masterConfigList_SocketStatusChange);
        }

        public void ConnectToServer(Byte[] sendBytes)
        {
            this.sendBytes = sendBytes;

            try {
                /* ClientConnectCallback is called once the client connects succesfully. */
                SocketErrorCodes error = tcpClient.ConnectToServerAsync(ClientConnectCallback);
            }
            catch (Exception e) {
                CrestronConsole.PrintLine("Error connecting to server: " + e.Message);
            }
        }

        private void ClientConnectCallback(TCPClient myTCPClient)
        {
            if (myTCPClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED) {
                /* Once connected, begin waiting for packets from the server.
                 * This call to ReceiveDataAsync is necessary to receive the FIN packet from the server in the event
                 * that the TLS handshake fails and the connection cannot be made. If you do not call ReceiveDataAsync here,
                 * the client will remain "connected" from its perspective, though no connection has been made. */
                myTCPClient.ReceiveDataAsync(ClientReceiveCallback);
                myTCPClient.SendData(sendBytes, sendBytes.Length);
            } else {
                CrestronConsole.PrintLine("clientConnectCallback: No connection could be made with the server.");
            }
        }

        public void ClientReceiveCallback(TCPClient myTCPClient, int totalBytesRcv)
        {
            /* 0 or negative byte count indicates the connection has been closed. */
            if (totalBytesRcv <= 0) {
                CrestronConsole.PrintLine("clientReceiveCallback: Could not receive message- connection closed");
            } else {
                try {
                    byte[] rcvBytes = new byte[totalBytesRcv];
                    Array.Copy(myTCPClient.IncomingDataBuffer, rcvBytes, totalBytesRcv);

                    /* Gathers the data into a string variable. */
                    rcvString += Encoding.UTF8.GetString(rcvBytes, 0, totalBytesRcv);

                    /* Event will be called if the delimiter is found in the string. Lets us know we gathered all of the data. */
                    if (DataReceivedEvent != null && rcvString.IndexOf(delimiter) >= 0) {
                        DataReceivedEvent.Invoke(this, EventArgs.Empty);
                        rcvString = "";
                    } else {
                        myTCPClient.ReceiveDataAsync(ClientReceiveCallback);
                    }
                }
                catch (Exception e) {
                    CrestronConsole.PrintLine("Exception in clientReceiveCallback: " + e.Message);
                }
            }
        }

        public void Disconnect()
        {
            /* Disconnect from the server once we gathered all of the data. */
            tcpClient.DisconnectFromServer();
        }

        public void masterConfigList_SocketStatusChange(TCPClient myTCPClient, SocketStatus clientSocketStatus)
        {
            CrestronConsole.PrintLine("Socket State: " + clientSocketStatus);
        }
    }
}