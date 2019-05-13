using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using Client.Other;

namespace Client.Views
{
    /// <summary>
    /// Логика окна авторизации
    /// </summary>
    public partial class AuthorizationWindow : Window, IDataErrorInfo
    {
        public string Address { get;  set; } // IP адресс
        public string Port { get;  set; }    // Порт
        public string Login { get;  set; }   // Логин
        public ClientTCP ReadyClient { get; private set; } // Клиент

        public string Error => throw new NotImplementedException();

        /// <summary>
        /// Корректность ввода данных
        /// </summary>
        public string this[string columnName]
        {
            get
            {
                string error = string.Empty;
                switch (columnName)
                {
                    case "Address":
                        if (!IsValidIPAddress(Address))
                        {
                            error = "Invalid IP address";
                        }
                        break;
                    case "Port":
                        if (!IsValidPort(Port))
                        {
                            error = "Invalid port number";
                        }
                        break;
                    case "Login":
                        if (!IsValidLogin(Login))
                        {
                            error = "Invalid login";
                        }
                        break;
                }
                return error;
            }
        }

        /// <summary>
        /// Проверяет, соответствует ли адрес маске IPv4
        /// </summary>
        /// <param name="address">Адрес</param>
        private bool IsValidIPAddress(string address) => new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b").IsMatch(address);

        /// <summary>
        /// Проверяет, является ли порт допустимым двухбайтовым числом (1-65535)
        /// </summary>
        /// <param name="port">Порт</param>
        private bool IsValidPort(string port) => int.TryParse(Port, out var res) && res > 0 && res < 65536;

        /// <summary>
        /// Првоеряет валидность логина (пока только длину)
        /// </summary>
        /// <param name="login">Логин</param>
        private bool IsValidLogin(string login) => login.Length > 0;

        /// <summary>
        /// Инициализирует Адрес и порт значениями из app.config или по-умолчанию
        /// </summary>
        public AuthorizationWindow()
        {
            string tmpParameter = AppConfigManager.ReadConfigParameter("ip_address");
            Address = tmpParameter != "" ? tmpParameter : "127.0.0.1";
            tmpParameter = AppConfigManager.ReadConfigParameter("port");
            Port = tmpParameter != "" ? tmpParameter : "50000";
            tmpParameter = AppConfigManager.ReadConfigParameter("login");
            Login = tmpParameter != "" ? tmpParameter : "User";
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// Запуск клиента с сохранением введенных параметров
        /// </summary>
        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (IsValidIPAddress(Address) && IsValidPort(Port) && IsValidLogin(Login))
            {
                Address = addressTextBox.Text;
                Port = portTextBox.Text;
                Login = loginTextBox.Text;
                ReadyClient = new ClientTCP(Address, int.Parse(Port), Login);
                try
                {
                    await ReadyClient.ConnectAsync();
                    AppConfigManager.UpdateConfigParameter("ip_address", Address);
                    AppConfigManager.UpdateConfigParameter("port", Port);
                    AppConfigManager.UpdateConfigParameter("login", Login);
                    DialogResult = true;
                }
                catch 
                {
                    MessageBox.Show("Server is not available");
                }
            }
        }
    }
}
