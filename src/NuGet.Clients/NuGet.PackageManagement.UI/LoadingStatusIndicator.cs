// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Windows.Controls;
using NuGet.PackageManagement.VisualStudio;
using Resx = NuGet.PackageManagement.UI.Resources;

namespace NuGet.PackageManagement.UI
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1501:AvoidExcessiveInheritance",
        Justification = "Needs to be capable of adding/removing from ListBox Children")]
    internal class LoadingStatusIndicator : ContentControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private LoadingStatus _status = LoadingStatus.Unknown;
        private string _errorMessage;
        private string _loadingMessage;

        public LoadingStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string LoadingMessage
        {
            get
            {
                return _loadingMessage;
            }
            set
            {
                if (_loadingMessage != value)
                {
                    _loadingMessage = value;
                    OnPropertyChanged(nameof(LoadingMessage));
                }
            }
        }

        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        public LoadingStatusIndicator()
        {
            DataContext = this;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        public void Reset(string loadingMessage)
        {
            Status = LoadingStatus.Unknown;
            LoadingMessage = loadingMessage;
        }

        public void SetError(string message)
        {
            Status = LoadingStatus.ErrorOccurred;
            ErrorMessage = message;
        }

        internal string LocalizedStatus
        {
            get
            {
                switch (Status)
                {
                    case LoadingStatus.Loading:
                        return LoadingMessage;

                    case LoadingStatus.NoItemsFound:
                        return Resx.Text_NoPackagesFound;

                    case LoadingStatus.Cancelled:
                        return Resx.Status_Canceled;

                    case LoadingStatus.ErrorOccurred:
                        return Resx.Status_ErrorOccurred;

                    case LoadingStatus.NoMoreItems:
                        return Resx.Status_NoMoreItems;

                    case LoadingStatus.Ready:
                        return Resx.Status_Ready;

                    default:
                        return string.Empty;
                }
            }
        }
    }
}
