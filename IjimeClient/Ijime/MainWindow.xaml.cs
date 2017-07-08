using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.Controls;

using System.Net;
using System.Net.Sockets;

using System.Security.Cryptography;

using System.Threading;

namespace Ijime {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : MetroWindow {
        const int PORT = 10816;
        public Socket server;
        IPEndPoint toIP;
        
        AesCryptoServiceProvider cryptP = new AesCryptoServiceProvider();
        ICryptoTransform transformD;
        ICryptoTransform transformE;


        uint id;
        string group;

        Receiver r;
        Thread th;

        public MainWindow() {
            InitializeComponent();
            

            btnSend.IsEnabled = false;
            chatBox.IsReadOnly = true;
            chatBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            string pathImg = AppDomain.CurrentDomain.BaseDirectory + "Ijime.png";
            image.Source = new BitmapImage(new Uri(pathImg, UriKind.Absolute));
            flyout.IsOpen = true;
            flyout.Height = Window.Height - 2;

            cryptP.BlockSize = 128;
            cryptP.KeySize = 256;
            cryptP.GenerateIV();
            cryptP.GenerateKey();
            cryptP.Mode = CipherMode.CBC;
            cryptP.Padding = PaddingMode.PKCS7;

            transformD = cryptP.CreateDecryptor();
            transformE = cryptP.CreateEncryptor();

            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            toIP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), PORT); //here your server ip
            try {
                server.Connect(toIP);
                
                byte[] msgBuffer;
                msgBuffer = cryptP.IV;
                server.Send(msgBuffer, 0, msgBuffer.Length, 0);
                msgBuffer = cryptP.Key;
                server.Send(msgBuffer, 0, msgBuffer.Length, 0);
            } catch {
                lblErrorLogin.Visibility = Visibility.Visible;
                lblErrorLogin.Content = "Server non disponibile";
                btnLogin.IsEnabled = false;
            }

        }

        private void btnLogin_Click(object sender, RoutedEventArgs e) {
            loginRing.IsActive = true;
            string user = txtUser.Text;
            string psw = createMD5(pswBox.Password).ToLower();
            byte[] msgBuffer;
            msgBuffer = Encoding.Default.GetBytes("1");
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);

            msgBuffer = transformE.TransformFinalBlock(Encoding.UTF8.GetBytes(user), 0, user.Length);
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);

            transformE.Dispose();
            transformE = cryptP.CreateEncryptor();

            Thread.Sleep(400);

            byte[] pswBuffer = transformE.TransformFinalBlock(Encoding.UTF8.GetBytes(psw), 0, psw.Length);

            server.Send(pswBuffer, 0, pswBuffer.Length, 0);


            byte[] logBuff = new byte[server.SendBufferSize];
            int bytesRead = server.Receive(logBuff);
            byte[] formatted = new byte[bytesRead];
            for (int i = 0; i < bytesRead; i++) formatted[i] = logBuff[i];
            lblErrorLogin.Content = bytesRead.ToString();

            id = BitConverter.ToUInt32(formatted, 0);

            logBuff = new byte[server.SendBufferSize];
            bytesRead = server.Receive(logBuff);
            formatted = new byte[bytesRead];
            for (int i = 0; i < bytesRead; i++) formatted[i] = logBuff[i];
            group = ASCIIEncoding.ASCII.GetString(formatted);

            if (id == 0) {
                lblErrorLogin.Visibility = Visibility.Visible;
                lblErrorLogin.Content = "Credenziali errate! Riprova.";
                txtUser.Text = "";
                pswBox.Password = "";
            } else {
                lblUsername.Content = txtUser.Text;
                lblId.Content = id.ToString();
                lblContattoID.Visibility = Visibility.Visible;
                if (group == "psicologo") {
                    btnHelp.Visibility = Visibility.Hidden;
                }
                if(group != "admin") {
                    txtDebug.Visibility = Visibility.Hidden;
                    lblContattoID.Visibility = Visibility.Hidden;
                }
                flyout.IsOpen = false;

                r = new Receiver();
                r.server = server;
                r.cryptTh = cryptP;
                th = new Thread(new ThreadStart(r.doStart));
                th.Start();
            }

        }

        public static string createMD5(string input) {
            using (MD5 md5 = MD5.Create()) {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++) {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            try {
                byte[] msgBuffer = Encoding.Default.GetBytes("2");
                server.Send(msgBuffer, 0, msgBuffer.Length, 0);
                th.Abort();
            } catch { }
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e) {
            byte[] msgBuffer = Encoding.Default.GetBytes("3");
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);
        }

        private void contactList_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (contactList.SelectedItem != null) {
                string str = contactList.SelectedItem.ToString();
                int chL = str.IndexOf("#", StringComparison.Ordinal);
                string user = str.Substring(0, chL);
                string id = str.Substring(chL + 1);
                lblContatto.Content = user;
                lblContattoID.Content = id;
                btnSend.IsEnabled = true;
            }
        }

        private void btnSend_Click(object sender, RoutedEventArgs e) {
            if (!(txtWrite.Text == null || txtWrite.Text == "")) {
                uint id = Convert.ToUInt32(lblContattoID.Content.ToString(), 10);
                string user = lblContatto.Content.ToString();
                string time = "[" + DateTime.Now.ToShortTimeString() + "] ";
                writeToChat(chatBox, time, Brushes.Black);
                writeToChat(chatBox, "Tu: ", Brushes.Blue);
                writeToChat(chatBox, (txtWrite.Text + Environment.NewLine), Brushes.Black);
                MessageSender ms = new MessageSender(Convert.ToUInt32(lblId.Content.ToString(), 10), id, lblUsername.Content.ToString(), txtWrite.Text, cryptP, server);
                Thread sendTh = new Thread(ms.sending);
                sendTh.Start();
                txtWrite.Text = "";
            }
        }


        public void writeToChat(RichTextBox ric, string text, SolidColorBrush color) {
            TextRange tr = new TextRange(ric.Document.ContentEnd, ric.Document.ContentEnd);
            tr.Text = text;
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter && btnSend.IsEnabled) {
                btnSend_Click(null, null);
            }
        }

        private void button_Click(object sender, RoutedEventArgs e) {
            System.Diagnostics.Process.Start("http://didotto.com/#four");
        }
    }


    public class Receiver {
        public Socket server;
        public AesCryptoServiceProvider cryptTh;
        bool flag = true;
        ICryptoTransform transformD;
        ICryptoTransform transformE;

        public void doStart() {
            transformD = cryptTh.CreateDecryptor();
            transformE = cryptTh.CreateEncryptor();

            while (flag) {
                try {
                    int response;
                    response = Int32.Parse(Encoding.ASCII.GetString(fromServer()));
                    switch (response) {
                        case (1): //psy response
                            psyResponse();
                            break;
                        case (2): //GET MESSAGE
                            messageHandler();
                            break;
                        default:
                            flag = false;
                            break;
                    }
                } catch (Exception e) {
                    server.Close();
                }
            }
        }

        private void psyResponse() {
            uint id;
            string username;
            id = BitConverter.ToUInt32(fromServer(), 0);

            transformD.Dispose();
            transformD = cryptTh.CreateDecryptor();
            byte[] userByte = fromServer();
            userByte = transformD.TransformFinalBlock(userByte, 0, userByte.Length);
            username = Encoding.ASCII.GetString(userByte);
            if(id!=0){
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate () {
                    var myWin = (MainWindow)Application.Current.MainWindow;
                    myWin.lblErrorGeneral.Visibility = Visibility.Hidden;
                    string str = username + "#" + id.ToString();
                    if (!myWin.contactList.Items.Contains(str)) {
                        myWin.contactList.Items.Add(str);
                    }
                }));
            }else {
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate () {
                    var myWin = (MainWindow)Application.Current.MainWindow;
                    myWin.lblErrorGeneral.Visibility = Visibility.Visible;
                    myWin.lblErrorGeneral.Content = "Non ci sono psicologi online!";
                }));
            }
            
        }

        private void messageHandler() {
            uint id;
            string username;
            string message;
            
            id = BitConverter.ToUInt32(fromServer(), 0);
            transformD.Dispose();
            transformD = cryptTh.CreateDecryptor();
            byte[] userByte = fromServer();
            userByte = transformD.TransformFinalBlock(userByte, 0, userByte.Length);
            username = Encoding.ASCII.GetString(userByte);
            transformD.Dispose();
            transformD = cryptTh.CreateDecryptor();

            byte[] messageByte = fromServer();
            messageByte = transformD.TransformFinalBlock(messageByte, 0, messageByte.Length);
            message = Encoding.ASCII.GetString(messageByte);
            Application.Current.Dispatcher.Invoke(new Action(delegate () {
                var myWin = (MainWindow)Application.Current.MainWindow;
                string str = username + "#" + id.ToString();
               
                if (!myWin.contactList.Items.Contains(str)) {
                    myWin.contactList.Items.Add(str);
                }
                
                string time = "[" + DateTime.Now.ToShortTimeString() + "] ";
                myWin.writeToChat(myWin.chatBox, time, Brushes.Black);
                myWin.writeToChat(myWin.chatBox, (username + ": "), Brushes.Red);
                myWin.writeToChat(myWin.chatBox, (message + Environment.NewLine), Brushes.Black);


            }));
        }

        private byte[] fromServer() {
            byte[] buffer = new byte[server.SendBufferSize];
            int bytesRead = server.Receive(buffer);
            byte[] formatted = new byte[bytesRead];
            for (int i = 0; i < bytesRead; i++) formatted[i] = buffer[i];
            return formatted;
        }

    }

    public class MessageSender {
        public uint fromId;
        public uint toId;
        public string fromUser;
        public string message;
        public AesCryptoServiceProvider cryptSendTh;
        public Socket server;

        ICryptoTransform transformE;
        public void sending() {
            Application.Current.Dispatcher.BeginInvoke(new Action(delegate () {
                var myWin = (MainWindow)Application.Current.MainWindow;
                myWin.btnSend.IsEnabled = false;
            }));
            byte[] msgBuffer = Encoding.Default.GetBytes("4"); // SENDING MESSAGE (4, fromId, toId, fromUser, message) 
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);

            msgBuffer = BitConverter.GetBytes(fromId);
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);
            Thread.Sleep(250);
            msgBuffer = BitConverter.GetBytes(toId);
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);

            Application.Current.Dispatcher.Invoke(new Action(delegate () {
                var myWin = (MainWindow)Application.Current.MainWindow;
                myWin.txtDebug.Text = fromId.ToString();
            }));
            transformE = cryptSendTh.CreateEncryptor();
            Thread.Sleep(250);
            msgBuffer = transformE.TransformFinalBlock(Encoding.UTF8.GetBytes(fromUser), 0, fromUser.Length);
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);

            transformE.Dispose();
            transformE = cryptSendTh.CreateEncryptor();
            Thread.Sleep(250);

            msgBuffer = transformE.TransformFinalBlock(Encoding.UTF8.GetBytes(message), 0, message.Length);
            server.Send(msgBuffer, 0, msgBuffer.Length, 0);
            Application.Current.Dispatcher.BeginInvoke(new Action(delegate () {
                var myWin = (MainWindow)Application.Current.MainWindow;
                myWin.btnSend.IsEnabled = true;
            }));

        }

        public MessageSender(uint fI, uint tI, string fU, string m, AesCryptoServiceProvider cp, Socket s) {
            fromId = fI;
            toId = tI;
            fromUser = fU;
            message = m;
            cryptSendTh = cp;
            server = s;
        }

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

