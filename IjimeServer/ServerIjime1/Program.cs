using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

using System.Security.Cryptography;

using MySql.Data.MySqlClient;

using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace ServerIjime1 {
    class Program {
        public static Hashtable clientList = new Hashtable();
        public static string cs = @"server=localhost;userid=super;password=12345;database=testijime";
        public static AesCryptoServiceProvider messageCP = new AesCryptoServiceProvider();
        static void Main(string[] args) {
            Console.WriteLine("Avvio Server Ijime");
            const int PORT = 10816;
            Socket server;
            Socket accClient = null;

            #region SOCKET BINDING
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, PORT);

            Console.WriteLine("Info: \nServer bind->" + localEndPoint.ToString());
            server.Bind(localEndPoint);
            server.Listen(100);

            #endregion

            #region MYSQL CONNECTION TEST
            MySqlConnection conn = null;

            try {
                conn = new MySqlConnection(cs);
                conn.Open();
                Console.WriteLine("MySQL Version: " + conn.ServerVersion + "\n" + "Database: " + conn.Database.ToString() + "\n" + "Stato: " + conn.State.ToString() + "\n---------------\n\n");
                conn.Close();
            } catch (MySqlException e) {
                Console.WriteLine("Error: " + e.ToString());
            }

            #endregion


            const string MSGKEY = "OGacb4z3VEwPqHdLIE1v8lU9r7wde5uitgDInqQKXBY=";
            const string MSGIV = "IRx3Af40/klYajPj8gBmuQ==";

            messageCP.BlockSize = 128;
            messageCP.KeySize = 256;
            messageCP.IV = Convert.FromBase64String(MSGIV);
            messageCP.Key = Convert.FromBase64String(MSGKEY);
            messageCP.Mode = CipherMode.CBC;
            messageCP.Padding = PaddingMode.PKCS7;
            messageSemder msgSender = new messageSemder();
            Thread senderTh = new Thread(msgSender.sending);
            senderTh.Start();

            while (true) {
                try {
                    accClient = server.Accept();
                    SocketCommunication sc = new SocketCommunication();
                    sc.client = accClient;
                    Thread th = new Thread(new ThreadStart(sc.tWork));
                    th.Start();
                } catch {
                    break;
                }
                Console.WriteLine("\n# NEW CONNECTION: Local Endpoint:" + accClient.LocalEndPoint.ToString() + " Remote Endpoint:" + accClient.RemoteEndPoint.ToString() + "\n");
            }
            server.Close();
            accClient.Close();
        }
    }




    class SocketCommunication {
        byte[] key;
        byte[] inVec;
        uint id = 0;
        public string username;
        int debug = 1;
        public AesCryptoServiceProvider cryptP = new AesCryptoServiceProvider();
        ICryptoTransform transformD;
        ICryptoTransform transformE;
        public Socket client;
        int request = 0;
        bool flag = true;
        public void tWork() {
            cryptSetup();

            cryptP.IV = inVec = fromClient();

            cryptP.Key = key = fromClient();

            transformD = cryptP.CreateDecryptor();
            transformE = cryptP.CreateEncryptor();
            Console.WriteLine("@ KEY RETRIVED: " + Convert.ToBase64String(key));
            try {
                while (flag) {
                    request = Int32.Parse(Encoding.ASCII.GetString(fromClient()));
                    Console.WriteLine("## Log: richiesta=" + request);
                    switch (request) {
                        case (1): //LOGIN
                            Console.WriteLine("\n -----------\nLogin Attempt");
                            login();
                            Console.WriteLine("Login Finish\n -----------");
                            break;
                        case (2): //LOGOUT
                            if (Program.clientList.ContainsKey(id)) {
                                Console.WriteLine("\n -----------\nLogout Attempt");
                                SocketCommunication x = Program.clientList[id] as SocketCommunication;
                                Console.WriteLine("IP: " + x.client.RemoteEndPoint.ToString());
                                Program.clientList.Remove(id);
                                updateConnected(id, 0);

                                Console.WriteLine("Logout Finish\n -----------");
                            }
                            flag = false;
                            break;
                        case (3): //GET PSY
                            getPsy();
                            break;
                        case (4): //GET MESSAGE
                            messageHandler();
                            break;
                        default:
                            flag = false;
                            break;
                    }
                }
                Console.WriteLine("Client Disconnected");
                client.Close();
            } catch (Exception e) {
                Console.WriteLine("********************\nTHREAD CRASH\n********************");
                Console.WriteLine("Error: " + e.ToString());
                if (Program.clientList.ContainsKey(id)) {
                    Program.clientList.Remove(id);
                    updateConnected(id, 0);
                }
                client.Close();
            }

        }

        private void cryptSetup() {
            cryptP.BlockSize = 128;
            cryptP.KeySize = 256;
            cryptP.Mode = CipherMode.CBC;
            cryptP.Padding = PaddingMode.PKCS7;
        }

        private void login() {
            string user;
            string psw;

            transformD.Dispose();
            transformD = cryptP.CreateDecryptor();
            byte[] usernameByte = fromClient();
            usernameByte = transformD.TransformFinalBlock(usernameByte, 0, usernameByte.Length); //Decrypt username
            user = ASCIIEncoding.ASCII.GetString(usernameByte);

            transformD.Dispose();
            transformD = cryptP.CreateDecryptor();

            byte[] pswByte = fromClient();
            pswByte = transformD.TransformFinalBlock(pswByte, 0, pswByte.Length); //Decrypt password
            psw = Encoding.ASCII.GetString(pswByte);

            using (var sqlCon = new MySqlConnection(Program.cs)) {

                sqlCon.Open();
                MySqlCommand cmd = sqlCon.CreateCommand();
                cmd.CommandText = "SELECT users.id_user, groups.name FROM groups, users WHERE users.cod_group = groups.id_group AND username = @user AND psw = @psw;";
                cmd.Parameters.AddWithValue("@user", user);
                cmd.Parameters.AddWithValue("@psw", psw);
                string groupname = "error";

                try {
                    MySqlDataReader dr = cmd.ExecuteReader();
                    if (dr.HasRows) {
                        dr.Read();
                        id = dr.GetUInt32(dr.GetOrdinal("id_user"));
                        groupname = dr.GetString(dr.GetOrdinal("name"));
                        Console.WriteLine("User: " + id + " (" + groupname + ")  has connected");
                        if (Program.clientList.ContainsKey(id))
                            Program.clientList.Remove(id);

                        Program.clientList.Add(id, this);
                        updateConnected(id, 1);
                        dr.Close();
                        username = user;
                        
                        using (var sqlUpdate = new MySqlConnection(Program.cs)) {
                            sqlUpdate.Open();
                            MySqlCommand comUpdate = sqlUpdate.CreateCommand();
                            jsonCounty jc = new jsonCounty();
                            string ip;
                            try {
                                string socket = client.RemoteEndPoint.ToString();
                                int chL = socket.IndexOf(":", StringComparison.Ordinal);
                                ip = socket.Substring(0, chL);

                                string info = new WebClient().DownloadString("http://ipinfo.io/" + ip);
                                jc = JsonConvert.DeserializeObject<jsonCounty>(info);
                            } catch {
                                jc = null;
                            }
                            if (jc!=null) {
                                comUpdate.CommandText = "UPDATE users SET last_ip = INET_ATON(@ip), last_country = @lcountry, last_city = @lcity WHERE id_user = @id;";
                                comUpdate.Parameters.AddWithValue("@ip", jc.ip);
                                comUpdate.Parameters.AddWithValue("@lcountry", jc.country);
                                comUpdate.Parameters.AddWithValue("@lcity", jc.city);
                                comUpdate.Parameters.AddWithValue("@id", id);
                                
                                try {
                                    comUpdate.ExecuteNonQuery();
                                } catch (MySqlException e){
                                    Console.WriteLine("Errore: " + e.ToString());
                                }
                            }
                        }

                    } else {
                        Console.WriteLine("Error: User not found");
                    }
                    byte[] msgId = BitConverter.GetBytes(id);
                    client.Send(msgId, 0, msgId.Length, 0);
                    byte[] msgGroup = Encoding.ASCII.GetBytes(groupname);
                    client.Send(msgGroup, 0, msgGroup.Length, 0);
                } catch (MySqlException e) {
                    Console.WriteLine("Mysql Error: " + e.ToString());
                }

                sqlCon.Close();
            }
        }

        void getPsy() {
            using (var sqlCon = new MySqlConnection(Program.cs)) {
                sqlCon.Open();
                MySqlCommand cmd = sqlCon.CreateCommand();
                cmd.CommandText = "SELECT r1.id_user, r1.username";
                cmd.CommandText += " FROM psicologi AS r1 JOIN";
                cmd.CommandText += " (SELECT CEIL(RAND() * (SELECT MAX(id_user) FROM psicologi)) AS id) AS r2";
                cmd.CommandText += " WHERE r1.id_user >= r2.id";
                cmd.CommandText += " ORDER BY r1.id_user ASC LIMIT 1; ";
                uint psyId = 0;
                string psyUser = "Error";
                try {
                    MySqlDataReader dr = cmd.ExecuteReader();
                    dr.Read();
                    psyId = dr.GetUInt32(dr.GetOrdinal("id_user"));
                    psyUser = dr.GetString(dr.GetOrdinal("username"));
                } catch {   }

                byte[] msgBuffer = Encoding.Default.GetBytes("1"); //SEND PSY
                client.Send(msgBuffer, 0, msgBuffer.Length, 0);

                byte[] msgId = BitConverter.GetBytes(psyId);
                client.Send(msgId, 0, msgId.Length, 0);
                transformE.Dispose();
                transformE = cryptP.CreateEncryptor();
                Thread.Sleep(400);
                byte[] msgUsername = Encoding.ASCII.GetBytes(psyUser);
                byte[] userCrypt = transformE.TransformFinalBlock(msgUsername, 0, msgUsername.Length);
                client.Send(userCrypt, 0, userCrypt.Length, 0);
            }
        }

        void messageHandler() {

            uint idFrom = BitConverter.ToUInt32(fromClient(), 0);
            uint idTo = BitConverter.ToUInt32(fromClient(), 0);
            transformD.Dispose();
            transformD = cryptP.CreateDecryptor();
            byte[] userByte = fromClient();
            userByte = transformD.TransformFinalBlock(userByte, 0, userByte.Length);
            string userFrom = Encoding.UTF8.GetString(userByte);
            transformD.Dispose();
            transformD = cryptP.CreateDecryptor();
            byte[] messageByte = fromClient();
            messageByte = transformD.TransformFinalBlock(messageByte, 0, messageByte.Length);
            string message = Encoding.UTF8.GetString(messageByte);
            Console.WriteLine("DEBUG " + (debug++) + ")");
            if (Program.clientList.ContainsKey(idTo)) {
                SocketCommunication x = Program.clientList[idTo] as SocketCommunication;
                Socket msgClient = x.client;
                AesCryptoServiceProvider cp = x.cryptP;
                sendMessage(msgClient, cp, idFrom, userFrom, message);
                regMessage(Program.messageCP, message, idFrom, idTo, 1);
            } else {
                regMessage(Program.messageCP, message, idFrom, idTo, 0);
            }
        }

        void updateConnected(uint id, ushort c) {
            using (var sqlCon = new MySqlConnection(Program.cs)) {
                sqlCon.Open();
                MySqlCommand cmd = sqlCon.CreateCommand();
                cmd.CommandText = "UPDATE users SET users.connected = @iscon  WHERE users.id_user = @id;";
                cmd.Parameters.AddWithValue("@iscon", c);
                cmd.Parameters.AddWithValue("@id", id);

                try {
                    MySqlDataReader dr = cmd.ExecuteReader();

                } catch (MySqlException e) {
                    Console.WriteLine("Mysql Error: " + e.ToString());
                }
            }
        }

        public static void sendMessage(Socket sock, AesCryptoServiceProvider cp, uint idFrom, string userFrom, string message) {
            ICryptoTransform enc;

            byte[] msgBuffer = Encoding.Default.GetBytes("2");
            sock.Send(msgBuffer, 0, msgBuffer.Length, 0);
            Console.WriteLine("Messaggio da: " + idFrom);
            msgBuffer = BitConverter.GetBytes(idFrom);
            sock.Send(msgBuffer, 0, msgBuffer.Length, 0);
            Thread.Sleep(250);
            enc = cp.CreateEncryptor();

            byte[] userName = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(userFrom), 0, Encoding.UTF8.GetBytes(userFrom).Length);
            sock.Send(userName, 0, userName.Length, 0);

            enc.Dispose();
            enc = cp.CreateEncryptor();
            Thread.Sleep(250);

            byte[] encMes = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(message), 0, Encoding.UTF8.GetBytes(message).Length);
            sock.Send(encMes, 0, encMes.Length, 0);
        }

        void regMessage(AesCryptoServiceProvider cp, string message, uint from, uint to, short sent) {
            ICryptoTransform enc;
            using (var sqlCon = new MySqlConnection(Program.cs)) {
                sqlCon.Open();
                MySqlCommand cmd = sqlCon.CreateCommand();
                enc = cp.CreateEncryptor();

                byte[] messageByte = Encoding.UTF8.GetBytes(message);
                messageByte = enc.TransformFinalBlock(messageByte, 0, messageByte.Length);
                string msgEnc = Convert.ToBase64String(messageByte);
                cmd.CommandText = "INSERT INTO messages(fromU, toU, message, sent) VALUES (@from, @to, @message, @sent);";
                cmd.Parameters.AddWithValue("@from", from);
                cmd.Parameters.AddWithValue("@to", to);
                cmd.Parameters.AddWithValue("@message", msgEnc);
                cmd.Parameters.AddWithValue("@sent", sent);
                try {
                    MySqlDataReader dr = cmd.ExecuteReader();
                } catch (MySqlException e) {
                    Console.WriteLine("Errore nel caricamento del messaggio: " + e.ToString());
                }
            }
        }

        byte[] fromClient() {
            byte[] buffer = new byte[client.SendBufferSize];
            int bytesRead = client.Receive(buffer);
            byte[] formatted = new byte[bytesRead];
            for (int i = 0; i < bytesRead; i++) formatted[i] = buffer[i];
            return formatted;
        }
    }

    public class messageSemder{
        public void sending() {
            while (true) {
                using (var sqlCon = new MySqlConnection(Program.cs)) {
                    sqlCon.Open();
                    MySqlCommand cmd = sqlCon.CreateCommand();
                    cmd.CommandText = "SELECT id_message, message, fromU, toU FROM messages WHERE sent = 0;";
                    try {
                        MySqlDataReader dr = cmd.ExecuteReader();
                        if (dr.HasRows) {
                            while (dr.Read()) {
                                UInt64 id_m = dr.GetUInt64(dr.GetOrdinal("id_message"));
                                string msg = dr.GetString(dr.GetOrdinal("message"));
                                uint idFrom = dr.GetUInt32(dr.GetOrdinal("fromU"));
                                uint idTo = dr.GetUInt32(dr.GetOrdinal("toU"));
                                if (Program.clientList.ContainsKey(idTo)) {
                                    using (var sqlConUser = new MySqlConnection(Program.cs)) {
                                        sqlConUser.Open();
                                        MySqlCommand cmdUser = sqlConUser.CreateCommand();
                                        cmdUser.CommandText = "SELECT username FROM users WHERE id_user = @id";
                                        cmdUser.Parameters.AddWithValue("@id", idFrom);
                                        SocketCommunication sc = Program.clientList[idTo] as SocketCommunication;
                                        ICryptoTransform dec;
                                        dec = Program.messageCP.CreateDecryptor();
                                        byte[] msgByte = Convert.FromBase64String(msg);
                                        msgByte = dec.TransformFinalBlock(msgByte, 0, msgByte.Length);
                                        msg = Encoding.UTF8.GetString(msgByte);
                                        try {
                                            MySqlDataReader drUser = cmdUser.ExecuteReader();
                                            drUser.Read();
                                            SocketCommunication.sendMessage(sc.client, sc.cryptP, idFrom, drUser.GetString(drUser.GetOrdinal("username")), msg);
                                        } catch (Exception e) {
                                            Console.WriteLine("Errore: " + e.ToString());
                                        }
                                    }
                                    using (var sqlConUpdate = new MySqlConnection(Program.cs)) {
                                        sqlConUpdate.Open();
                                        MySqlCommand cmdUpdate = sqlConUpdate.CreateCommand();
                                        cmdUpdate.CommandText = "UPDATE messages SET sent = 1 WHERE id_message = @id;";
                                        cmdUpdate.Parameters.AddWithValue("@id", id_m);
                                        cmdUpdate.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    } catch (MySqlException e) {
                        Console.WriteLine("Errore durante la lettura dei messaggi: " + e.ToString());
                    }

                }

                Thread.Sleep(5000);
            }
        }
    }

    public class jsonCounty {
        [JsonProperty("ip")]
        public string ip { get; set; }
        [JsonProperty("hostname")]
        public string hostname { get; set; }
        [JsonProperty("city")]
        public string city { get; set; }
        [JsonProperty("region")]
        public string region { get; set; }
        [JsonProperty("country")]
        public string country { get; set; }
        [JsonProperty("loc")]
        public string loc { get; set; }
        [JsonProperty("org")]
        public string org { get; set; }
        [JsonProperty("postal")]
        public string postal { get; set; }
    }
}

/*
Copyright 2017 Davide Balice

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/