using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Server.Views;
using Server.ViewModels;
using Server.Other;

namespace Server
{
    /// <summary>
    /// Логика интерфейса ServerWindow.xaml
    /// </summary>
    public partial class ServerWindow : Window
    {
        List<UIElement> listUI; // Список ui элементов для смены их в grid

        /// <summary>
        /// Настройка окна ServerWindows
        /// </summary>
        public ServerWindow()
        {
            AppConfigManager.CreateConfigParameters("ip_address", "port");

            if (Authorization(out string address, out int port))
            {
                DataContext = new ServerVM(address, port);
            }
            else
            {
                Application.Current.Shutdown();
            }

            InitializeComponent();

            listUI = new List<UIElement>
            {
                clientsListBox,
                filesListBox,
                logTextBox
            };
            dataGrid.Children.Clear();
            dataGrid.Children.Add(listUI[0]);
            listViewMenu.SelectedIndex = 0;
        }

        

        /// <summary>
        /// Вызов модального окна для ввода IP-адресса и порта сервера
        /// </summary>
        public bool Authorization(out string address, out int port)
        {
            AuthorizationWindow authorizationWindow = new AuthorizationWindow();
            if (authorizationWindow.ShowDialog() == true)
            {
                address = authorizationWindow.Address;
                port = int.Parse(authorizationWindow.Port);
                return true;
            }
            else
            {
                address = "";
                port = -1;
                return false;
            }
        }

        /// <summary>
        /// Переключение по вкладкам меню
        /// </summary>
        private void ListViewMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = listViewMenu.SelectedIndex;
            dataGrid.Children.Clear();
            dataGrid.Children.Add(listUI[index]);
        }
    }
}
