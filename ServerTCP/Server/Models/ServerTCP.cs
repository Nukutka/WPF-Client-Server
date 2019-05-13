using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.IO;

namespace Server
{
    /// <summary>
    /// Модель сервера
    /// </summary>
    class ServerTCP
    {
        private readonly Dispatcher dispatcher;  // Для изменения ObservableCollection из других потоков
        private readonly IPEndPoint ipEndPoint;  // Адрес и порт сервера
        private readonly TcpListener tcpListener; // Слушает входящие соединения
        private readonly CancellationTokenSource listenToken; // Отмена прослушивания 

        public ObservableCollection<ConnectedClient> ClientList { get; set; } // Список клиентов
        public ObservableCollection<FileInfo> FileList { get; set; }         // Список файлов 

        public event EventHandler<string> AddToLog; // Событие записи в лог

        private const int block = 1024; // Размер буффера при передаче
        private const string filePath = "files\\";

        /// <summary>
        /// Инициализация настроек сервера
        /// </summary>
        public ServerTCP(string ipAddress, int port)
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ipEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            tcpListener = new TcpListener(ipEndPoint);
            listenToken = new CancellationTokenSource();
            ClientList = new ObservableCollection<ConnectedClient>();
            FileList = new ObservableCollection<FileInfo>();
        }

        /// <summary>
        /// Запуск прослушивания входящих соединений
        /// </summary>
        public void Start()
        {
            tcpListener.Start();
            Task.Run(() => ListeningIncomingConnetions());
            Directory.CreateDirectory(filePath);
            SearchFiles();
            AddToLog(this, "Server is running");
        }

        /// <summary>
        /// Остановка сервера 
        /// </summary>
        public Task Stop()
        {
            return Task.Run(() =>
            {
                listenToken.Cancel();
                foreach (var client in ClientList)
                {
                    CloseClient(client);
                }
                ClientList.Clear();
                FileList.Clear();
                tcpListener.Stop();
            });
        }

        /// <summary>
        /// Отсылает сообщение клиенту
        /// </summary>
        /// <param name="client">Клиент, которому отправляется сообщение</param>
        /// <param name="message">Текст сообщения</param>
        private void SendMessage(TcpClient client, string message)
        {
            byte[] buff = Encoding.Unicode.GetBytes(message);
            client.GetStream().Write(buff, 0, buff.Length);
        }

        /// <summary>
        /// Принимает сообщение от клиента
        /// </summary>
        /// <param name="client">Клиент, сообщение от которого принимается</param>
        private string ReceiveMessage(TcpClient client)
        {
            StringBuilder str = new StringBuilder();
            if (client.Client.Poll(1000000, SelectMode.SelectRead)) // Изменение на сокете
            {
                byte[] buff = new byte[block];
                int bytes = 0;
                while (client.GetStream().DataAvailable)
                {
                    bytes = client.GetStream().Read(buff, 0, buff.Length);
                    str.Append(Encoding.Unicode.GetString(buff, 0, bytes));
                }
            }
            return str.ToString() != "" ? str.ToString() : "-1";
        }

        /// <summary>
        /// Принимает сообщение в виде набора байтов от клиента
        /// </summary>
        /// <param name="client">Клиент, сообщение от которого принимается</param>
        private byte[] ReceiveBytes(TcpClient client)
        {
            int buffLength = int.Parse(ReceiveMessage(client));
            SendMessage(client, "0");

            byte[] buff = new byte[buffLength];
            int bytes = client.GetStream().Read(buff, 0, buffLength);
            return buff;
        }

        /// <summary>
        /// Принимает файл от клиента
        /// </summary>
        /// <param name="client">Клиент, файл от которого принимается</param>
        private void ReceiveFile(ConnectedClient client)
        {
            SendMessage(client.TcpClient, "0");
            string fileName = ReceiveMessage(client.TcpClient);
            SendMessage(client.TcpClient, "0");
            int fileLength = int.Parse(ReceiveMessage(client.TcpClient));
            SendMessage(client.TcpClient, "0");

            Directory.CreateDirectory(filePath);
            using (var writer = new FileStream($"{filePath}{fileName}", FileMode.Create, FileAccess.Write))
            {
                int bytes = 0;
                byte[] buff = new byte[block];
                while (bytes < fileLength)
                {
                    buff = ReceiveBytes(client.TcpClient);
                    writer.Write(buff, 0, buff.Length);
                    SendMessage(client.TcpClient, "0");
                    bytes += block;
                }
            }

            SearchFiles();
            AddToLog(this, $"Receive file: {fileName} {fileLength} bytes");
        }


        /// <summary>
        /// Прослушивание входящих соединений
        /// </summary>
        private void ListeningIncomingConnetions()
        {
            TcpClient incomingConnection;
            while (!listenToken.Token.IsCancellationRequested)
            {
                Thread.Sleep(50);
                if (!tcpListener.Pending()) continue; // Попытка соединения
                incomingConnection = tcpListener.AcceptTcpClient();

                string clientName = ReceiveMessage(incomingConnection); // Получаем имя клиента и добавляем его в список
                ConnectedClient client = new ConnectedClient(incomingConnection, clientName, new CancellationTokenSource());
                dispatcher.BeginInvoke(new Action(() => ClientList.Add(client)));

                Task.Run(() => ClientMessaging(client));
                AddToLog(this, $"{client.Login} successful connected");
            }
        }

        /// <summary>
        /// Обработка сообщений от клиента
        /// </summary>
        /// <param name="client">Клиент, сообщения которого обрабатываются</param>
        private void ClientMessaging(ConnectedClient client)
        {
            string command = "";
            while (!client.ClientToken.Token.IsCancellationRequested) // Обрабатываем, пока клиент не отключится
            {
                if (client.TcpClient.Client.Poll(1000000, SelectMode.SelectRead)) // Изменение на сокете
                {
                    if (client.TcpClient.GetStream().DataAvailable) // Есть какие-то данные
                    {
                        command = ReceiveMessage(client.TcpClient);
                        switch (command)
                        {
                            case "disconnect_request":
                                ClientDisconnection(client);
                                break;
                            case "send_file_request":
                                ReceiveFile(client);
                                break;
                            default:
                                AddToLog(this, $"{client.Login}: {command}");
                                break;
                        }
                    }
                    else // Если нет, то произошел обрыв связи
                    {
                        ClientDisconnection(client);
                    }
                }
                Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Закрытие соединения с клиентом по инициативе клиента
        /// </summary>
        /// <param name="client">Клиент, соединение с которым закрывается</param>
        private void ClientDisconnection(ConnectedClient client)
        {
            client.TcpClient.GetStream().Close();
            client.TcpClient.Close();
            client.ClientToken.Cancel();
            dispatcher.BeginInvoke(new Action(() => ClientList.Remove(client)));
            AddToLog(this, $"{client.Login}: disconnected");
        }

        /// <summary>
        /// Закрытие соединения с клиентом по инициативе сервера
        /// </summary>
        /// <param name="client">Клиент, соединение с которым закрывается</param>
        private void CloseClient(ConnectedClient client)
        {
            client.ClientToken.Cancel();
            SendMessage(client.TcpClient, "stop_server");
            client.TcpClient.GetStream().Close();
            client.TcpClient.Close();
            dispatcher.BeginInvoke(new Action(() => ClientList.Remove(client)));
            AddToLog(this, $"{client.Login}: disconnected");
        }

        /// <summary>
        /// Поиск загруженных файлов при старте сервера
        /// </summary>
        private void SearchFiles()
        {
            dispatcher.BeginInvoke(new Action(() => FileList.Clear()));
            foreach (var i in Directory.GetFiles(filePath))
            {
                dispatcher.BeginInvoke(new Action(() => FileList.Add(new FileInfo($"{filePath}{i}"))));
            }
        }
    }
}
