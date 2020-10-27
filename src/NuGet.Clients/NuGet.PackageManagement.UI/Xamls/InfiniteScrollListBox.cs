using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
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
            //Style = Resources.FindName("loadingStatusIndicatorStyle") as Style;
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

        /// <summary>
        /// Resets the indicator status. If it's in the list, it's removed.
        /// </summary>
        /// <returns>Whether the indicator was in the list and was removed.</returns>
        public bool HideLoadingStatusIndicator()
        {
            lock (_loadingStatusIndicator)
            {
                _loadingStatusIndicator.Reset(string.Empty);
                
                //if (ObservableCollectionDataContext.Contains(_loadingStatusIndicator))
                //{
                //    //Items.Remove(_loadingStatusIndicator);
                //    return true;
                //}
                return false;
            }
        }

        /// <summary>
        /// Show the Loading Indicator in the list with the provided loading message.
        /// If the operation is complete, set the finalized state.
        /// </summary>
        /// <param name="loadingMessage">If provided, reset's the indicator to show the specified message.</param>
        /// <param name="operationComplete">When true, set a finalized state based on <c>Items</c> count.</param>
        //public void ShowLoadingIndicator(string loadingMessage = null, bool operationComplete = false)
        //{
        //    lock (_loadingStatusIndicator)
        //    {
        //        if (loadingMessage != null)
        //        {
        //            _loadingStatusIndicator.Reset(loadingMessage);
        //        }

        //        // add Loading... indicator if not present
        //        //if (!Items.Contains(_loadingStatusIndicator))
        //        //{
        //        //    Items.Add(_loadingStatusIndicator);
        //        //}

        //        if (operationComplete)
        //        {
        //            SetLoadingIndicatorBasedOnItemsCount();
        //        }
        //    }
        //}

        //private void SetLoadingIndicatorBasedOnItemsCount()
       //{
            // Ideally, after a search, it should report its status, and
            // do not keep the LoadingStatus.Loading forever.
            // This is a workaround.
            //if (Items.Count == 1) //Only contains the indicator itself.
            //{
            //    _loadingStatusIndicator.Status = LoadingStatus.NoItemsFound;
            //}
            //else //There are actual Items.
            //{ 
            //    Items.Remove(_loadingStatusIndicator);
            //}
        //}


        private bool _showingLoadingStatusIndicator;

        public void ShowLoadingIndicator(string loadingMessage = null, bool operationComplete = false, bool show = true)
        {

            //Border
            //>ScrollViewer
            //>>WrapPanel
            //>>>ItemsPresenter
            //>>>*Loading Indicator here*
            WrapPanel wrapPanel = (WrapPanel)Template.FindName("ListBoxWrapPanel", this);

            if (show)
            {
                lock (_loadingStatusIndicator)
                {
                    if (!_showingLoadingStatusIndicator)
                    {
                        wrapPanel.Children.Add(_loadingStatusIndicator);
                        _showingLoadingStatusIndicator = true;
                    }
                }                
            }
            else
            {
                lock (_loadingStatusIndicator)
                {
                    wrapPanel.Children.Remove(_loadingStatusIndicator);
                    _showingLoadingStatusIndicator = false;
                }
            }
        }
        #endregion
    }
}
