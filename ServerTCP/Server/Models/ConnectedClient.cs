using System.Threading;
using System.Net.Sockets;

namespace Server
{
    /// <summary>
    /// Модель клиента
    /// </summary>
    public class ConnectedClient
    {
        public TcpClient TcpClient { get; set; }
        public string Login { get; set; }
        public CancellationTokenSource ClientToken { get; set; }

        /// <summary>
        /// Инициализая подключенного клиента
        /// </summary>
        /// <param name="tcpClient">tcpClient, через который установлена связь с клиентом</param>
        /// <param name="login">Логин подключенного клиента</param>
        /// <param name="token">Токен для завершения задачи обработки сообщений</param>
        public ConnectedClient(TcpClient tcpClient, string login, CancellationTokenSource token)
        {
            TcpClient = tcpClient;
            //TcpClient.GetStream().ReadTimeout = 1000;
            Login = login;
            ClientToken = token;
        }
    }
}
