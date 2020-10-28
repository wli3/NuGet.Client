using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class InfiniteScrollListBox : ListBox, INotifyPropertyChanged
    {
        public ReentrantSemaphore ItemsLock { get; set; }
        private readonly LoadingStatusIndicator _loadingStatusIndicator = new LoadingStatusIndicator();

        public Style LoadingStatusIndicatorStyle
        {
            get
            {
                return _loadingStatusIndicator.Style;
            }
            set
            {
                _loadingStatusIndicator.Style = value;
            }
        }

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

        public ObservableCollection<PackageItemListViewModel> ObservableCollectionDataContext
        {
            get
            {
                return DataContext as ObservableCollection<PackageItemListViewModel>;
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

        public void SetError(string message)
        {
            _loadingStatusIndicator.SetError(message);
        }

        private bool _showingLoadingStatusIndicator;

        /// <summary>
        /// Adds or removes the LoadingStatusIndicator from the ListBox's VisualTree with any specified state information.
        /// </summary>
        /// <param name="status">Status for the indicator.
        /// <param name="loadingMessage">Text to show in the loading indicator when <c>show</c> is <c>true</c>.
        /// If not provided, the previous text persists.</param>
        public void UpdateLoadingIndicator(LoadingStatus status, string loadingMessage = null)
        {
            WrapPanel wrapPanel = (WrapPanel)Template.FindName("ListBoxWrapPanel", this);

            bool show = false;

            if (status != LoadingStatus.Unknown)
            {
                bool operationComplete = LoadingStatus.Completed.HasFlag(status);

                //NoItemsFound (can be shown or hidden)
                if (operationComplete)
                {
                    int itemsCount = (ItemsSource as ObservableCollection<object>).Count;
                    show = itemsCount == 0; //Indicator needs to be visible to display NoItemsFound (No packages found).
                }

                if (status == LoadingStatus.Loading || status == LoadingStatus.Ready)
                {
                    show = true;
                }
            }

            //Render the indicator.
            lock (_loadingStatusIndicator)
            {
                if (loadingMessage != null)
                {
                    _loadingStatusIndicator.LoadingMessage = loadingMessage;
                }
                _loadingStatusIndicator.Status = status;

                if (show)
                {
                    if (!_showingLoadingStatusIndicator)
                    {
                        wrapPanel.Children.Add(_loadingStatusIndicator);
                        _showingLoadingStatusIndicator = true;
                    }
                }
                else //Remove the indicator.
                {
                    wrapPanel.Children.Remove(_loadingStatusIndicator);
                    _showingLoadingStatusIndicator = false;
                }
            }
        }
        #endregion
    }
}
