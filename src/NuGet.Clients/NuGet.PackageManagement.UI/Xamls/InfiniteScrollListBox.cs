using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class InfiniteScrollListBox : ListBox, INotifyPropertyChanged
    {
        public ReentrantSemaphore ItemsLock { get; set; }

        private bool _checkBoxesEnabled;
        public bool CheckBoxesEnabled
        {
            get => _checkBoxesEnabled;
            set
            {
                if (_checkBoxesEnabled != value)
                {
                    _checkBoxesEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
