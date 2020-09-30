// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.VisualStudio;
using StreamJsonRpc;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    [Collection(MockedVS.Collection)]
    public class NuGetUITests : IDisposable
    {
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly TestDirectory _testDirectory;

        public NuGetUITests()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            _joinableTaskContext = new JoinableTaskContext(Thread.CurrentThread, SynchronizationContext.Current);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(_joinableTaskContext.Factory);

            _testDirectory = TestDirectory.Create();
        }

        public void Dispose()
        {
            _joinableTaskContext?.Dispose();
            _testDirectory.Dispose();
        }

        [Fact]
        public void ShowError_WhenArgumentIsSignatureExceptionWithNullResults_DoesNotThrow()
        {
            var exception = new SignatureException(message: "a");

            Assert.Null(exception.Results);

            using (NuGetUI ui = CreateNuGetUI())
            {
                ui.ShowError(exception);
            }
        }

        [Fact]
        public void ShowError_WhenArgumentIsRemoteInvocationException_ShowsError()
        {
            var exception = new RemoteInvocationException(message: "a", errorCode: 0, errorData: null);
            var defaultLogger = new Mock<INuGetUILogger>();
            var projectLogger = new Mock<INuGetUILogger>();

            defaultLogger.Setup(
                x => x.ReportError(
                    It.Is<ILogMessage>(
                        logMessage => logMessage.Level == LogLevel.Error
                        && logMessage.Message == exception.Message)));
            projectLogger.Setup(
                x => x.Log(
                    It.Is<ILogMessage>(
                        logMessage => logMessage.Level == LogLevel.Error
                        && logMessage.Message == exception.Message)));

            using (NuGetUI ui = CreateNuGetUI(defaultLogger.Object, projectLogger.Object))
            {
                ui.ShowError(exception);

                defaultLogger.VerifyAll();
                projectLogger.VerifyAll();
            }
        }

        [Fact]
        public void ShowError_WhenArgumentIsNotRemoteInvocationException_ShowsError()
        {
            var exception = new DivideByZeroException();
            var defaultLogger = new Mock<INuGetUILogger>();
            var projectLogger = new Mock<INuGetUILogger>();
            const bool indent = false;

            defaultLogger.Setup(
                x => x.ReportError(
                    It.Is<ILogMessage>(
                        logMessage => logMessage.Level == LogLevel.Error
                        && logMessage.Message == ExceptionUtilities.DisplayMessage(exception, indent))));
            projectLogger.Setup(
                x => x.Log(
                    It.Is<ILogMessage>(
                        logMessage => logMessage.Level == LogLevel.Error
                        && logMessage.Message == exception.ToString())));

            using (NuGetUI ui = CreateNuGetUI(defaultLogger.Object, projectLogger.Object))
            {
                ui.ShowError(exception);

                defaultLogger.VerifyAll();
                projectLogger.VerifyAll();
            }
        }

        private NuGetUI CreateNuGetUI()
        {
            return CreateNuGetUI(Mock.Of<INuGetUILogger>(), Mock.Of<INuGetUILogger>());
        }

        private NuGetUI CreateNuGetUI(INuGetUILogger defaultLogger, INuGetUILogger projectLogger)
        {
            var uiContext = CreateNuGetUIContext();

            return new NuGetUI(
                Mock.Of<ICommonOperations>(),
                new NuGetUIProjectContext(
                    Mock.Of<ICommonOperations>(),
                    projectLogger,
                    Mock.Of<ISourceControlManagerProvider>()),
                defaultLogger,
                uiContext);
        }

        private NuGetUIContext CreateNuGetUIContext()
        {
            var sourceRepositoryProvider = Mock.Of<ISourceRepositoryProvider>();
            var packageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                Mock.Of<ISettings>(),
                _testDirectory.Path);

            return new NuGetUIContext(
                sourceRepositoryProvider,
                Mock.Of<IServiceBroker>(),
                Mock.Of<IVsSolutionManager>(),
                new NuGetSolutionManagerServiceWrapper(),
                packageManager,
                new UIActionEngine(
                    sourceRepositoryProvider,
                    packageManager,
                    Mock.Of<INuGetLockService>()),
                Mock.Of<IPackageRestoreManager>(),
                Mock.Of<IOptionsPageActivator>(),
                Mock.Of<IUserSettingsManager>(),
                Enumerable.Empty<IVsPackageManagerProvider>());
        }
    }
}
