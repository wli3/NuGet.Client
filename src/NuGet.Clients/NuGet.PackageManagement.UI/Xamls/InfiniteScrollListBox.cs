using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class InfiniteScrollListBox : ListBox, INotifyPropertyChanged
    {
        public ReentrantSemaphore ItemsLock { get; set; }
        private readonly LoadingStatusIndicator _loadingStatusIndicator = new LoadingStatusIndicator();

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

        public InfiniteScrollListBox()
        {
            _loadingStatusIndicator.PropertyChanged += LoadingStatusIndicator_PropertyChanged;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #region Loading Status Indicator
        public event PropertyChangedEventHandler LoadingStatusIndicator_PropertyChanged;
        public LoadingStatus Status
        {
            get
            {
                return _loadingStatusIndicator.Status;
            }
            set
            {
                _loadingStatusIndicator.Status = value;
            }
        }


        public void SetError(string message)
        {
            _loadingStatusIndicator.SetError(message);
        }

        public void BeginLoadingIndeterminate()
        {
            lock (_loadingStatusIndicator)
            {
                // add Loading... indicator if not present
                if (!Items.Contains(_loadingStatusIndicator))
                {
                    Items.Add(_loadingStatusIndicator);
                }
            }
        }

        public void EndLoadingIndeterminate()
        {
            lock (_loadingStatusIndicator)
            {
                bool hasLoadingIndicator = Items.Contains(_loadingStatusIndicator);

                // Ideally, after a search, it should report its status, and
                // do not keep the LoadingStatus.Loading forever.
                // This is a workaround.
                int emptyListCount = hasLoadingIndicator ? 1 : 0;

                if (Items.Count == emptyListCount)
                {
                    _loadingStatusIndicator.Status = LoadingStatus.NoItemsFound;
                }
                else
                { 
                    Items.Remove(_loadingStatusIndicator);
                }
            }
        }
        #endregion
    }
}
