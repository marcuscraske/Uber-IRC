using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace UberLib.IRC
{
    public class Connection
    {
        #region "Events"
        // Core
        public delegate void _Event_Core_Connected();
        public event _Event_Core_Connected Event_Core_Connected;
        public delegate void _Event_Core_FailedToConnect(SocketException exception);
        public event _Event_Core_FailedToConnect Event_Core_FailedToConnect;
        public delegate void _Event_Core_Disconnected();
        public event _Event_Core_Disconnected Event_Core_Disconnected;
        public delegate void _Event_Core_MessageRecieved(string data, List<string> Attribs);
        public event _Event_Core_MessageRecieved Event_Core_MessageRecieved;
        // Channels
        // Users
        // Server
        #endregion

        #region "Properties"
        // Variables storing properties
        private int _Settings_Port = 6667;
        //private string _Settings_IP = "irc.freenode.org";
        private string _Settings_IP = "irc.freenode.org";
        private string _Settings_Nickname = "lemmings-cat";
        private string _Settings_Username = "lemmings-cat";
        private string _Settings_Name = "Anonymous User";
        private string _Settings_Connection_Password = "";
        private bool _Settings_RespondToPing = true;
        private bool _Settings_CTCP_Handled = true;
        private bool _Settings_CTCP_Ping = true;
        private string _Settings_CTCP_Version = "UberLib IRC Library";
        private bool _Settings_CTCP_Time = true;
        private string _Settings_CTCP_Finger = "Not supported.";
        // Property accessors
        /// <summary>
        /// Specifies the port of the IRC server; changes will require a reconnect.
        /// </summary>
        public int Settings_Port
        {
            get
            {
                return _Settings_Port;
            }
            set
            {
                _Settings_Port = value;
            }
        }
        /// <summary>
        /// Specifies the IP or host-name of the IRC server; changes will require a reconnect.
        /// </summary>
        public string Settings_IP
        {
            get
            {
                return _Settings_IP;
            }
            set
            {
                _Settings_IP = value;
            }
        }
        /// <summary>
        /// Specifies the nickname used by the connection; changes will be propagated.
        /// </summary>
        public string Settings_Nickname
        {
            get
            {
                return _Settings_Nickname;
            }
            set
            {
                _Settings_Nickname = value;
                if(sock != null && sock.Connected) Cmd_User_ChangeNick(_Settings_Nickname);
            }
        }
        /// <summary>
        /// Specifies the username used at the start of the connection; changes require a connect.
        /// </summary>
        public string Settings_Username
        {
            get
            {
                return _Settings_Username;
            }
            set
            {
                _Settings_Username = value;
            }
        }
        /// <summary>
        /// Specifies the name used at the start of the connection; changes require a reconnect.
        /// </summary>
        public string Settings_Name
        {
            get
            {
                return _Settings_Name;
            }
            set
            {
                _Settings_Name = value;
            }
        }
        /// <summary>
        /// Specifies the password used to connect to the server at the start of the connection.
        /// </summary>
        public string Settings_Connection_Password
        {
            get
            {
                return _Settings_Connection_Password;
            }
            set
            {
                _Settings_Connection_Password = value;
            }
        }
        /// <summary>
        /// Specifies if the connection should automatically respond to ping-requests; message-events for pings will still be raised.
        /// </summary>
        public bool Settings_RespondToPing
        {
            get
            {
                return _Settings_RespondToPing;
            }
            set
            {
                _Settings_RespondToPing = value;
            }
        }
        /// <summary>
        /// States if CTCP protocol is handled at all; this will override all other CTCP booleans to false if false.
        /// </summary>
        public bool Settings_CTCP
        {
            get
            {
                return _Settings_CTCP_Handled;
            }
            set
            {
                _Settings_CTCP_Handled = value;
            }
        }
        /// <summary>
        /// If true, CTCP ping-requests will recieve a reply.
        /// </summary>
        public bool Settings_CTCP_RespondToPing
        {
            get
            {
                return _Settings_CTCP_Ping;
            }
            set
            {
                _Settings_CTCP_Ping = value;
            }
        }
        /// <summary>
        /// If not an empty string, this will be the reply to CTCP version requests.
        /// </summary>
        public string Settings_CTCP_Version
        {
            get
            {
                return _Settings_CTCP_Version;
            }
            set
            {
                _Settings_CTCP_Version = value;
            }
        }
        /// <summary>
        /// If true, CTCP time requests will recieve a reply.
        /// </summary>
        public bool Settings_CTCP_Time
        {
            get
            {
                return _Settings_CTCP_Time;
            }
            set
            {
                _Settings_CTCP_Time = value;
            }
        }
        /// <summary>
        /// If not an empty string, this will be the reply to CTCP finger requests.
        /// </summary>
        public string Settings_CTCP_Finger
        {
            get
            {
                return _Settings_CTCP_Finger;
            }
            set
            {
                _Settings_Connection_Password = value;
            }
        }
        #endregion

        #region "Variables"
        Thread _Reciever = null;
        Thread _Checker = null;
        public TcpClient sock = null;
        StreamReader sr = null;
        StreamWriter sw = null;
        private string _Current_Server = "";
        #endregion

        public void Connect()
        {
            try
            {
                sock = new TcpClient(_Settings_IP, _Settings_Port);
                System.Diagnostics.Debug.WriteLine("Connecting...");
                sock.Connect(_Settings_IP, _Settings_Port);
            }
            catch(SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to connect: " + ex.SocketErrorCode.ToString());
                    sock = null;
                    if (Event_Core_FailedToConnect != null) Event_Core_FailedToConnect(ex);
                }
            }
            if (sock != null)
            {
                if (Event_Core_Connected != null) Event_Core_Connected();
                sw = new StreamWriter(sock.GetStream());
                sr = new StreamReader(sock.GetStream());
                if (_Settings_Connection_Password.Length > 0)
                {
                    Send("PASS " + _Settings_Connection_Password);
                    System.Diagnostics.Debug.WriteLine("Sent connection password...");
                }
                System.Diagnostics.Debug.WriteLine("Sending nickname and user information...");
                Send("NICK " + _Settings_Nickname);
                Send("USER " + _Settings_Nickname + " 8 * : " + _Settings_Name);
                System.Diagnostics.Debug.WriteLine("Listening for further instructions...");
                // Begin listening for packet data
                _Reciever = new Thread(new ParameterizedThreadStart(Recieve));
                _Reciever.Start(this);
                // Launch checker to ensure connection is always valid
                _Checker = new Thread(new ParameterizedThreadStart(CheckConnState));
                _Checker.Start(this);
            }
        }
        public void Disconnect()
        {
            System.Diagnostics.Debug.WriteLine("Disconnecting...");
            try
            {
                if (_Reciever != null)
                {
                    if (_Reciever.ThreadState == ThreadState.Running) _Reciever.Abort();
                    _Reciever = null;
                }
            }
            catch { }
            try
            {
                if (_Checker != null)
                {
                    if (_Checker.ThreadState == ThreadState.Running) _Checker.Abort();
                    _Checker = null;
                }
            }
            catch { }
            try
            {
                sw.Flush();
                sw.Close();
            }
            catch { }
            sw = null;
            try
            {
                sr.Close();
            }
            catch { }
            sr = null;
            try
            {
                sock.Client.Disconnect(true);
            }
            catch { }
            sock = null;
            if (Event_Core_Disconnected != null) Event_Core_Disconnected();
            System.Diagnostics.Debug.WriteLine("Disconnected.");
        }
        public void Send(string data)
        {
            try
            {
                sw.WriteLine(data);
                sw.Flush();
            }
            catch
            {
                Disconnect();
            }
        }
        public static void Recieve(object conn)
        {
            Connection c = (Connection)conn;
            try
            {
                List<string> Attribs;
                string data;
                string t;
                string[] t2;
                while (true)
                {
                    data = c.sr.ReadLine();
                    if (data != null)
                    {
                        // Read and organize data
                        t = data.IndexOf(':', 1) != -1 ? data.Substring(data.IndexOf(':', 1)) : String.Empty;
                        t2 = data.Substring(0, data.IndexOf(':', 1) != -1 ? data.IndexOf(':', 1) : data.Length).Split(' ');
                        Attribs = new List<string>();
                        foreach (string s in t2) if (s.Length != 0) Attribs.Add(s[0].Equals(':') ? s.Remove(0, 1) : s);
                        if (t.Length != 0) Attribs.Add(t[0].Equals(':') ? t.Remove(0, 1) : t);
                        // Check if the server name has been set
                        if (c._Current_Server.Length == 0) c._Current_Server = Attribs[0];
                        // CTCP test
                        if(c._Settings_CTCP_Handled && Attribs.Count == 4 && Attribs[1].Equals("PRIVMSG") && Attribs[3][0].Equals('\u0001') && Attribs[3][Attribs[3].Length - 1].Equals('\u0001'))
                        {
                            try
                            {
                                string cmd = Attribs[3].Remove(0, 1).Remove(Attribs[3].Length - 2, 1);
                                string usr = Attribs[0].Split('!')[0];
                                if (c._Settings_CTCP_Ping && cmd.StartsWith("PING ")) c.Send("NOTICE " + usr + " \u0001PING " + cmd.Substring(cmd.IndexOf(' ')) + "\u0001");
                                else if (c._Settings_CTCP_Version.Length != 0 && cmd.Equals("VERSION")) c.Send("NOTICE " + usr + " \u0001VERSION " + c._Settings_CTCP_Version + "\u0001");
                                else if (c._Settings_CTCP_Time && cmd.Equals("TIME")) c.Send("NOTICE " + usr + " \u0001TIME " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "\u0001");
                                else if (c._Settings_CTCP_Finger.Length != 0 && cmd.Equals("FINGER")) c.Send("NOTICE " + usr + " \u0001FINGER " + c._Settings_CTCP_Finger + "\u0001");
                            }
                            catch { }
                            System.Diagnostics.Debug.WriteLine("Recieved and handled ctcp command.");
                        }
                        // Handle data
                        if (Attribs[0].Equals("PING") && Attribs.Count > 1)
                        {
                            c.Send("PONG " + Attribs[1]);
                            System.Diagnostics.Debug.WriteLine("Responded to ping from '" + Attribs[1] + "'");
                        }
#if DEBUG
                        string o = "# ";
                        foreach (string a in Attribs) o += "'" + a + "',";
                        System.Diagnostics.Debug.WriteLine(o.Remove(o.Length - 1, 1));
#endif
                    }
                }
            }
            catch
            {
                c.Disconnect();
            }
        }
        public static void CheckConnState(object conn)
        {
            Connection c = (Connection)conn;
            bool wasConnected = false;
            while (true)
            {
                lock (c)
                {
                    if (c.sock != null && c.sock.Connected)
                    {
                        if (c.sock.Client.Poll(0, SelectMode.SelectWrite) && c.sock.Client.Poll(0, SelectMode.SelectRead))
                        {
                            byte[] t = new byte[1];
                            if (c.sock.Client.Receive(t, SocketFlags.Peek) == 0) c.Disconnect();
                            else wasConnected = true;
                        }
                    }
                    else if (wasConnected) c.Disconnect();
                }
                System.Threading.Thread.Sleep(2500);
            }
        }

        #region "Commands & Queries"
        #region "Channels"
        public void Cmd_Channels_Join(string channel)
        {
            Send("JOIN " + (channel.StartsWith("#") ? channel : "#" + channel));
        }
        public void Cmd_Channels_Part(string channel)
        {
            Send("PART " + (channel.StartsWith("#") ? channel : "#" + channel));
        }
        public void Cmd_Channels_Msg(string channel, string message)
        {
            Send("PRIVMSG " + (channel.StartsWith("#") ? channel : "#" + channel) + " :" + message);
        }
        public void Cmd_Channels_KickUser(string channel, string user, string reason)
        {
            Send("KICK " + (channel.StartsWith("#") ? channel : "#" + channel) + " " + user + " " + reason);
        }
        public void Cmd_Channels_Topic(string channel, string topic)
        {
            Send("TOPIC " + (channel.StartsWith("#") ? channel : "#" + channel) + " " + topic);
        }
        public void Query_Channels_UserList(string channel)
        {
            Send("NAMES " + (channel.StartsWith("#") ? channel : "#" + channel));
        }
        #endregion
        #region "User"
        public void Cmd_User_Msg(string name, string message)
        {
            Send("PRIVMSG " + name + " :" + message);
        }
        public void Cmd_User_ChangeNick(string new_nickanme)
        {
            Send("NICK " + new_nickanme);
        }
        public void Cmd_User_Operator_Authenticate(string username, string password)
        {
            Send("OPER " + username + " " + password);
        }
        #endregion
        #region "Server"
        public void Cmd_Server_KickUser(string user, string reason)
        {
            Send("KILL " + user + " " + reason);
        }
        public void Query_Server_Channels()
        {
            Send("LIST");
        }
        public void Query_Server_UserStats()
        {
            Send("LUSERS");
        }
        public void Query_Server_MOTD()
        {
            Send("MOTD");
        }
        public void Query_Server_Ping()
        {
            Send("PING " + _Current_Server);
        }
        public void Query_Server_Ping(string server)
        {
            Send("PING " + server);
        }
        public void Query_Server_Stats()
        {
            Send("STATS");
        }
        public void Query_Server_Stats(string server)
        {
            Send("STATS " + server);
        }
        public void Query_Server_Time()
        {
            Send("TIME");
        }
        public void Query_Server_Time(string server)
        {
            Send("TIME " + server);
        }
        public void Query_Server_Version()
        {
            Send("VERSION");
        }
        public void Query_Server_Version(string server)
        {
            Send("VERSION " + server);
        }
        public void Cmd_Server_Pong()
        {
            Send("PONG " + _Current_Server);
        }
        public void Cmd_Server_Pong(string server)
        {
            Send("PONG " + server);
        }
        public void Cmd_Server_Quit(string reason)
        {
            Send("QUIT :" + (reason.Length > 0 ? reason : "not specified"));
            Disconnect();
        }
        public void Cmd_Server_Restart()
        {
            Send("RESTART");
        }
        #endregion
        #endregion
    }
}
