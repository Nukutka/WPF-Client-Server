using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;

namespace Client
{
    /// <summary>
    /// Модель клиента
    /// </summary>
    public class ClientTCP
    {
        private IPEndPoint ipEndPoint; // Адрес и порт сервера
        private string login;          // Логин клиента
        private TcpClient tcpClient;   // Связь с сервером
        private NetworkStream stream;  // Для передачи сообщений
        private CancellationTokenSource serverMessagingToken; // Остановка прослушивания сообщений от сервера
        private Mutex mutex;

        public event EventHandler StopServer;               // Событие остановки сервера
        public event EventHandler<string> AddToLog;         // Событие записи в лог

        private const int block = 1024;     // Передача по 1024 байта

        /// <summary>
        /// Инициализация настроек клиента
        /// </summary>
        /// <param name="ipAddress">IP-адрес сервера</param>
        /// <param name="port">Номер порта сервера</param>
        /// <param name="login">Логин пользователя</param>
        public ClientTCP(string ipAddress, int port, string login)
        {
            ipEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            this.login = login;
            tcpClient = new TcpClient();
            serverMessagingToken = new CancellationTokenSource();
            mutex = new Mutex();
        }

        /// <summary>
        /// Выполняет подключение к серверу
        /// </summary>
        public async Task ConnectAsync()
        {
            await tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
            stream = tcpClient.GetStream();
            await SendMessageAsync(login);
            Task.Run(() => ServerMessaging());
            AddToLog?.Invoke(this, "Conection successful");
        }

        /// <summary>
        /// Выполняет отключение от сервера
        /// </summary>
        public async void DisconnectAsync()
        {
            await SendMessageAsync("disconnect_request");
            serverMessagingToken.Cancel();
            stream.Close();
            tcpClient.Close();
            AddToLog(this, "Disconnected");
        }

        /// <summary>
        /// Отсылает сообщение на сервер через NetworkStream
        /// </summary>
        /// <param name="message">Текст сообщения</param>
        public async Task SendMessageAsync(string message)
        {
            byte[] buff = Encoding.Unicode.GetBytes(message);
            await stream.WriteAsync(buff, 0, buff.Length);
        }

        /// <summary>
        /// Отсылает сообщение в виде набора байтов на сервер через NetworkStream
        /// </summary>
        /// <param name="buff">Набор байтов</param>
        /// <returns></returns>
        public async Task SendBytesAsync(byte[] buff)
        {
            await stream.WriteAsync(buff, 0, buff.Length);
        }

        /// <summary>
        /// Принимает сообщение от сервера через NetworkStream
        /// </summary>
        public async Task<string> ReceiveMessageAsync()
        {
            StringBuilder str = new StringBuilder();
            if (tcpClient.Client.Poll(1000000, SelectMode.SelectRead))
            {
                byte[] buff = new byte[block];
                int bytes = 0;
                while (stream.DataAvailable)
                {
                    bytes = await stream.ReadAsync(buff, 0, buff.Length);
                    str.Append(Encoding.Unicode.GetString(buff, 0, bytes));
                }
            }
            return str.ToString() != string.Empty ? str.ToString() : "Receive message error";
        }

        public async Task SendFileAsync(string path)
        {
            mutex.WaitOne();
            Task.Run(() => serverMessagingToken.Cancel()).Wait(); // Отключаем обработку сообщений от сервера для синхронизации

            await SendMessageAsync("send_file_request");

            FileInfo file = new FileInfo(path);
            string fileName = file.Name;
            long fileLength = file.Length;

            await ReceiveMessageAsync();
            await SendMessageAsync(fileName);
            await ReceiveMessageAsync();
            await SendMessageAsync(fileLength.ToString());
            await ReceiveMessageAsync();

            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read)) // Передача пакетов по 1024 байта
            {
                int readBytes = 0, tmpBytes = 0;
                byte[] buff;
                while (readBytes < fileLength)
                {
                    buff = new byte[block];
                    tmpBytes = reader.Read(buff, 0, block);
                    buff = buff.Take(tmpBytes).ToArray();   
                    readBytes += tmpBytes;
                    await SendMessageAsync(buff.Length.ToString());
                    await ReceiveMessageAsync();
                    await SendBytesAsync(buff);
                    await ReceiveMessageAsync();
                }
            }

            serverMessagingToken = new CancellationTokenSource();
            Task.Run(() => ServerMessaging());
            mutex.ReleaseMutex();
        }

        /// <summary>
        /// Обработка сообщений от сервера
        /// </summary>
        private async void ServerMessaging()
        {
            string command = "";
            while (!serverMessagingToken.Token.IsCancellationRequested)
            {
                if (tcpClient.Client.Poll(100000, SelectMode.SelectRead)) // Изменение на сокете
                {
                    if (stream.DataAvailable) // Есть какие-то данные
                    {
                        if (!serverMessagingToken.Token.IsCancellationRequested) // Если в процессе проверки сокета не было отмены
                        {
                            command = await ReceiveMessageAsync();
                            switch (command)
                            {
                                case "stop_server":
                                    StopClient();
                                    break;
                                default:
                                    AddToLog(this, command);
                                    break;
                            }
                        }
                    }
                    else // Если нет, то произошел обрыв связи
                    {
                        StopClient();
                    }
                }
                Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Остановка клиента при остановке сервера
        /// </summary>
        private void StopClient()
        {
            serverMessagingToken.Cancel();
            stream.Close();
            tcpClient.Close();
            StopServer(this, new EventArgs());
            AddToLog(this, "Server is stopped");
        }
    }
}
