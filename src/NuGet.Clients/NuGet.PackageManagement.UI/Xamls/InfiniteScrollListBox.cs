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
        /// Renders or removes the LoadingStatusIndicator from the ListBox with any specified state information.
        /// </summary>
        /// <param name="show">When true, add the Loading Indicator to the ListBox with the provided loading message.
        /// When false, Resets the indicator status. If it's currently in the VisualTree, remove it.</param>
        /// <param name="status">Optional Status for the indicator, default <c>Unknown</c>.
        /// Ignored when <paramref name="operationComplete"/> is <c>true</c> and there's no Items in the list.</param>
        /// <param name="loadingMessage">Text to show in the loading indicator when <c>show</c> is <c>true</c>.</param>
        /// <param name="operationComplete">If the operation is complete, set the finalized state.</param>
        public void UpdateLoadingIndicator(bool show, LoadingStatus status = LoadingStatus.Unknown, string loadingMessage = null,
            bool operationComplete = false)
        {
            WrapPanel wrapPanel = (WrapPanel)Template.FindName("ListBoxWrapPanel", this);

            bool operationCompleteItemsShown = operationComplete && Items.Count > 0;

            //Render the indicator.
            if (show && !operationCompleteItemsShown)
            {
                lock (_loadingStatusIndicator)
                {
                    if (loadingMessage != null)
                    {
                        _loadingStatusIndicator.Reset(loadingMessage);
                    }

                    if (operationComplete && !operationCompleteItemsShown)
                    {
                        _loadingStatusIndicator.Status = LoadingStatus.NoItemsFound;
                    }
                    else if (status != LoadingStatus.Unknown)
                    {
                        _loadingStatusIndicator.Status = status;
                    }

                    if (!_showingLoadingStatusIndicator)
                    {
                        wrapPanel.Children.Add(_loadingStatusIndicator);
                        _showingLoadingStatusIndicator = true;
                    }
                }
            }
            else //Remove the indicator.
            {
                lock (_loadingStatusIndicator)
                {
                    _loadingStatusIndicator.Reset(string.Empty);
                    wrapPanel.Children.Remove(_loadingStatusIndicator);
                    _showingLoadingStatusIndicator = false;
                }
            }
        }
        #endregion
    }
}
