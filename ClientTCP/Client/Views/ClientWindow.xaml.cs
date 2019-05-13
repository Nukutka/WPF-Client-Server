using System.Windows;
using Client.Views;
using Client.Other;
using System.Windows.Input;
using System;
using System.Windows.Threading;
using System.Collections.Generic;
using Microsoft.Win32;
using System.IO;

namespace Client
{
    /// <summary>
    /// Логика интерфейса ClientWindow
    /// </summary>
    public partial class MainWindow : Window
    {
        private ClientTCP client;
        private Dispatcher dispatcher;
        private List<UIElement> listUI; // Список ui элементов для смены их в grid


        /// <summary>
        /// Настройка окна ClientWindow 
        /// </summary>
        public MainWindow()
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            AppConfigManager.CreateConfigParameters("ip_address", "port", "login");
            Authorization();
            InitializeComponent();
            listUI = new List<UIElement>
            {
                sendMessageGrid,
                sendFileGrid,
                logTextBox
            };
            dataGrid.Children.Clear();
            dataGrid.Children.Add(listUI[0]);
            listViewMenu.SelectedIndex = 0;
        }

        /// <summary>
        /// Вызов модального окна для ввода IP-адресса, порта и логина
        /// </summary>
        private void Authorization()
        {
            AuthorizationWindow authorizationWindow = new AuthorizationWindow();
            if (authorizationWindow.ShowDialog() == true)
            {
                client = authorizationWindow.ReadyClient;
                client.StopServer += ServerStatus_Changed;
                client.AddToLog += LogList_AddData;
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        #region События

        private void LogList_AddData(object sender, string str)
        {
            string text = $"{DateTime.Now.ToLongTimeString()} {str}";
            dispatcher.BeginInvoke(new Action(() => logTextBox.Text += $"{text}\n"));
        }

        /// <summary>
        /// Обработка изменения статуса сервера
        /// </summary>
        private void ServerStatus_Changed(object sender, EventArgs e)
        {
            dispatcher.BeginInvoke(new Action(() => Authorization()));
        }

        /// <summary>
        /// Переключение по вкладкам меню
        /// </summary>
        private void ListViewMenu_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int index = listViewMenu.SelectedIndex;
            dataGrid.Children.Clear();
            dataGrid.Children.Add(listUI[index]);
        }

        /// <summary>
        /// Отправка сообщений по клавише Enter
        /// </summary>
        private async void MessageTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (messageTextBox.Text.Length > 0 && e.Key == Key.Enter)
            {
               await client.SendMessageAsync(messageTextBox.Text);
               messageTextBox.Text = "";
            }
        }

        /// <summary>
        /// Вызов окна выбора файла
        /// </summary>
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                filePathTextBox.Text = openFileDialog.FileName;
            }
        }

        /// <summary>
        /// Отправка файла на сервер
        /// </summary>
        private async void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (filePathTextBox.Text != "")
            {
                await client.SendFileAsync(filePathTextBox.Text);
            }
        }

       


        #endregion События
    }
}
