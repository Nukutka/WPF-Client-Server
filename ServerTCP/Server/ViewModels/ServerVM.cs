using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Threading;
using System.IO;
using System.Text;
using System.Threading;

namespace Server.ViewModels
{
    /// <summary>
    /// Модель представления, связывающая MainView и ServerTCP
    /// </summary>
    public class ServerVM : BaseVM
    {
        private Mutex clientMutex; // Для синхронизации 
        private Mutex fileMutex; // Для синхронизации 
        private Dispatcher dispatcher; // Для обновления свойств, на которые есть Binding
        private ServerTCP server; // Сервер

        public ObservableCollection<ConnectedClient> ClientList { get; set; } // Список клиентов для ServerWindow
        public ObservableCollection<FileInfo> FileList { get; set;  }         // Список файлов для ServerWindow
        public string Log { get; set; } // Лог для ServerWindow

        /// <summary>
        /// Инициализация необходимых для MainView данных
        /// </summary>
        public ServerVM(string ipAddress, int port)
        {
            File.Delete("Log.txt");
            dispatcher = Dispatcher.CurrentDispatcher;
            clientMutex = new Mutex();
            fileMutex = new Mutex();
            server = new ServerTCP(ipAddress, port);
            server.ClientList.CollectionChanged += ClientList_CollectionChanged;
            server.FileList.CollectionChanged += FileList_CollectionChanged;
            server.AddToLog += LogList_AddData;
            server.Start();
        }

        #region События

        /// <summary>
        /// Обновление свойства-коллекции ClientList => обновление данных в MainView
        /// </summary>
        private void ClientList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            clientMutex.WaitOne();
            ClientList = server.ClientList;
            OnPropertyChanged("ClientList");
            clientMutex.ReleaseMutex();
        }

        /// <summary>
        /// Обновление свойства-коллекции FileList => обновление данных в MainView
        /// </summary>
        private void FileList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            fileMutex.WaitOne();
            FileList = server.FileList;
            OnPropertyChanged("FileList");
            fileMutex.ReleaseMutex();
        }

        /// <summary>
        /// Обновление свойства Log => обновление данных в MainView
        /// </summary>
        private void LogList_AddData(object sender, string str)
        {
            clientMutex.WaitOne();
            string text = $"{DateTime.Now.ToLongTimeString()} {str}";
            using (StreamWriter sw = new StreamWriter("Log.txt", true, Encoding.Default))
            {
                sw.WriteLine(text);
            }
            dispatcher.BeginInvoke(new Action(() => Log += $"{text}\n"));
            OnPropertyChanged("Log");
            clientMutex.ReleaseMutex();
        }

        #endregion События

        #region Команды

        private BaseCommand startServerCommand;
        public BaseCommand StartServerCommand => startServerCommand ??
                    (startServerCommand = new BaseCommand(obj => server.Start()));

        #endregion Команды
    }
}
