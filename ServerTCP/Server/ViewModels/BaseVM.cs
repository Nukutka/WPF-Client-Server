using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Server.ViewModels
{
    /// <summary>
    /// Базовый функционал ViewModel
    /// </summary>
    public abstract class BaseVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }
}
