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
        public readonly object Lock = new object();
        private readonly LoadingStatusIndicator _loadingStatusIndicator = new LoadingStatusIndicator();
        private Panel _loadingStatusIndicatorParent;

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

        /// <summary>
        /// The <see cref="LoadingStatusIndicator"/> by default reflects the state of loaded Package Items (<see cref="PackageItemListViewModel"/>)
        /// without waiting for Task completion of properties like <see cref="PackageItemListViewModel.Status"/>.
        /// Setting as <c>true</c> means to continue showing the <see cref="LoadingStatusIndicator"/> until all lazy-loaded <see cref="PackageItemListViewModel"/> properties have completed.
        /// </summary>
        public bool ShowLoadingIndicatorForBackgroundWork { get; set; }

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
            Loaded += InfiniteScrollListBox_Loaded;
        }

        private void InfiniteScrollListBox_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void ScrollToHome()
        {
            ScrollViewer scrollViewer = (ScrollViewer)Template.FindName("ListBoxScrollViewer", this);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToHome();
            }
        }

        #region Loading Status Indicator
        public event PropertyChangedEventHandler LoadingStatusIndicator_PropertyChanged;

        public void SetError(string message)
        {
            _loadingStatusIndicator.SetError(message);
        }

        private bool _showingLoadingStatusIndicator;

        public void MoveLoadingStatusIndicator(Panel parent)
        {
            if (_loadingStatusIndicatorParent == null)
            {
                //TODO: validate?
                _loadingStatusIndicatorParent = parent;
            }
            else
            {
                lock (_loadingStatusIndicator)
                {
                    _loadingStatusIndicatorParent.Children.Remove(_loadingStatusIndicator);

                    //TODO: validate?
                    _loadingStatusIndicatorParent = parent;

                    _loadingStatusIndicatorParent.Children.Add(_loadingStatusIndicator);
                }
            }
        }

        /// <summary>
        /// Adds or removes the LoadingStatusIndicator from the ListBox's VisualTree with any specified state information.
        /// </summary>
        /// <param name="status">Status for the indicator.</param>
        /// <param name="loadingMessage">Text to show in the loading indicator when <c>show</c> is <c>true</c>.
        /// If not provided, the previous text persists.</param>
        /// <param name="itemsCount">Number of items that have been loaded.</param>
        public void UpdateLoadingIndicator(LoadingStatus status, string loadingMessage = null, int itemsCount = 0)
        {
            if (_loadingStatusIndicatorParent == null)
            {
                throw new NullReferenceException();
            }
            bool show = false;

            if (status != LoadingStatus.Unknown)
            {
                bool itemsComplete = LoadingStatus.CompletedItems.HasFlag(status);

                // A completed stay may need Indicator to be visible to display "No packages found".
                if (itemsComplete)
                {
                    var noItemsFound = itemsCount == 0;
                    if (noItemsFound)
                    {
                        show = true;
                        status = LoadingStatus.NoItemsFound;
                    }
                    else
                    {
                        if (ShowLoadingIndicatorForBackgroundWork && LoadingStatus.PendingBackgroundWork.HasFlag(status))
                        {
                            status = LoadingStatus.PendingBackgroundWork;
                        }
                        else
                        {
                            status = LoadingStatus.NoMoreItems;
                        }
                    }
                }

                if (status == LoadingStatus.Loading || status == LoadingStatus.PendingBackgroundWork || status == LoadingStatus.Ready)
                {
                    show = true;
                }
            }

            lock (_loadingStatusIndicator)
            {
                if (loadingMessage != null)
                {
                    _loadingStatusIndicator.LoadingMessage = loadingMessage;
                }
                _loadingStatusIndicator.Status = status;

                // Render the indicator.
                if (show)
                {
                    if (!_showingLoadingStatusIndicator)
                    {
                        _loadingStatusIndicatorParent.Children.Add(_loadingStatusIndicator);
                        _showingLoadingStatusIndicator = true;
                    }
                }
                else // Remove the indicator.
                {
                    _loadingStatusIndicatorParent.Children.Remove(_loadingStatusIndicator);
                    _showingLoadingStatusIndicator = false;
                }
            }
        }
        #endregion
    }
}
