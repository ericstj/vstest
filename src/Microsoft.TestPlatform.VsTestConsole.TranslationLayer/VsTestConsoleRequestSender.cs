// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.TestPlatform.VsTestConsole.TranslationLayer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.TestPlatform.VsTestConsole.TranslationLayer.Interfaces;
    using Microsoft.TestPlatform.VsTestConsole.TranslationLayer.Payloads;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;

    /// <summary>
    /// VstestConsoleRequestSender for sending requests to Vstest.console.exe
    /// </summary>
    internal class VsTestConsoleRequestSender : ITranslationLayerRequestSender
    {
        private ICommunicationManager communicationManager;

        private IDataSerializer dataSerializer;

        private ManualResetEvent handShakeComplete = new ManualResetEvent(false);

        private bool handShakeSuccessful = false;

        #region Constructor

        public VsTestConsoleRequestSender() : this(new SocketCommunicationManager(), JsonDataSerializer.Instance)
        {
        }

        internal VsTestConsoleRequestSender(ICommunicationManager communicationManager, IDataSerializer dataSerializer)
        {
            this.communicationManager = communicationManager;
            this.dataSerializer = dataSerializer;
        }

        #endregion

        #region ITranslationLayerRequestSender

        /// <summary>
        /// Initializes Communication with vstest.console.exe
        /// Hosts a communication channel and asynchronously connects to vstest.console.exe
        /// </summary>
        /// <returns>Port Number of hosted server on this side</returns>
        public int InitializeCommunication()
        {
            this.handShakeSuccessful = false;
            this.handShakeComplete.Reset();
            int port = -1;
            try
            {
                port = this.communicationManager.HostServer();
                this.communicationManager.AcceptClientAsync();

                Task.Run(() =>
                {
                    this.communicationManager.WaitForClientConnection(Timeout.Infinite);
                    handShakeSuccessful = HandShakeWithVsTestConsole();
                    this.handShakeComplete.Set();
                });
            }
            catch (Exception ex)
            {
                EqtTrace.Error("VsTestConsoleRequestSender: Error initializing communication with VstestConsole: {0}", ex);
                this.handShakeComplete.Set();
            }

            return port;
        }

        /// <summary>
        /// Waits for Vstest.console.exe Connection for a given timeout
        /// </summary>
        /// <param name="clientConnectionTimeout">Time to wait for the connection</param>
        /// <returns>True, if successful</returns>
        public bool WaitForRequestHandlerConnection(int clientConnectionTimeout)
        {
            var waitSucess = this.handShakeComplete.WaitOne(clientConnectionTimeout);
            return waitSucess && handShakeSuccessful;
        }

        /// <summary>
        /// Initializes the Extensions while probing additional extension paths 
        /// </summary>
        /// <param name="pathToAdditionalExtensions">Paths to check for additional extensions</param>
        public void InitializeExtensions(IEnumerable<string> pathToAdditionalExtensions)
        {
            this.communicationManager.SendMessage(MessageType.ExtensionsInitialize, pathToAdditionalExtensions);
        }

        /// <summary>
        /// Discover Tests using criteria and send events through eventHandler
        /// </summary>
        /// <param name="discoveryCriteria"></param>
        /// <param name="eventHandler"></param>
        public void DiscoverTests(IEnumerable<string> sources, string runSettings, ITestDiscoveryEventsHandler eventHandler)
        {
            this.communicationManager.SendMessage(MessageType.StartDiscovery, new DiscoveryRequestPayload() { Sources = sources, RunSettings = runSettings });
            this.ListenAndReportTestCases(eventHandler);
        }

        /// <summary>
        /// Starts the TestRun with given sources and criteria
        /// </summary>
        /// <param name="sources">Sources for test run</param>
        /// <param name="runSettings">RunSettings for test run</param>
        /// <param name="runEventsHandler">EventHandler for test run events</param>
        public void StartTestRun(IEnumerable<string> sources, string runSettings, ITestRunEventsHandler runEventsHandler)
        {
            this.communicationManager.SendMessage(MessageType.TestRunAllSourcesWithDefaultHost, 
                new TestRunRequestPayload() { Sources = sources.ToList(), RunSettings = runSettings });
            ListenAndReportTestResults(runEventsHandler, null);
        }

        /// <summary>
        /// Starts the TestRun with given test cases and criteria
        /// </summary>
        /// <param name="testCases">TestCases to run</param>
        /// <param name="runSettings">RunSettings for test run</param>
        /// <param name="runEventsHandler">EventHandler for test run events</param>
        public void StartTestRun(IEnumerable<TestCase> testCases, string runSettings, ITestRunEventsHandler runEventsHandler)
        {
            this.communicationManager.SendMessage(MessageType.TestRunSelectedTestCasesDefaultHost,
                new TestRunRequestPayload() { TestCases = testCases.ToList(), RunSettings = runSettings });
            ListenAndReportTestResults(runEventsHandler, null);
        }

        /// <summary>
        /// Starts the TestRun with given sources and criteria with custom test host
        /// </summary>
        /// <param name="sources">Sources for test run</param>
        /// <param name="runSettings">RunSettings for test run</param>
        /// <param name="runEventsHandler">EventHandler for test run events</param>
        public void StartTestRunWithCustomHost(IEnumerable<string> sources, string runSettings, ITestRunEventsHandler runEventsHandler, 
            ITestHostLauncher customHostLauncher)
        {
            this.communicationManager.SendMessage(MessageType.GetTestRunnerProcessStartInfoForRunAll,
                new TestRunRequestPayload() { Sources = sources.ToList(), RunSettings = runSettings, DebuggingEnabled = customHostLauncher.IsDebug });
            ListenAndReportTestResults(runEventsHandler, customHostLauncher);
        }

        /// <summary>
        /// Starts the TestRun with given test cases and criteria with custom test host
        /// </summary>
        /// <param name="testCases">TestCases to run</param>
        /// <param name="runSettings">RunSettings for test run</param>
        /// <param name="runEventsHandler">EventHandler for test run events</param>
        public void StartTestRunWithCustomHost(IEnumerable<TestCase> testCases, string runSettings, ITestRunEventsHandler runEventsHandler,
            ITestHostLauncher customHostLauncher)
        {
            this.communicationManager.SendMessage(MessageType.GetTestRunnerProcessStartInfoForRunSelected,
                new TestRunRequestPayload() { TestCases = testCases.ToList(), RunSettings = runSettings, DebuggingEnabled = customHostLauncher.IsDebug });
            ListenAndReportTestResults(runEventsHandler, customHostLauncher);
        }


        /// <summary>
        /// Send Cancel TestRun message
        /// </summary>
        public void CancelTestRun()
        {
            this.communicationManager.SendMessage(MessageType.CancelTestRun);
        }


        /// <summary>
        /// Send Abort TestRun message
        /// </summary>
        public void AbortTestRun()
        {
            this.communicationManager.SendMessage(MessageType.AbortTestRun);
        }

        public void Close()
        {
            this.Dispose();
        }

        public void EndSession()
        {
            this.communicationManager.SendMessage(MessageType.SessionEnd);
        }

        public void Dispose()
        {
            this.communicationManager?.StopServer();
        }

        #endregion

        private bool HandShakeWithVsTestConsole()
        {
            var success = false;
            var message = this.communicationManager.ReceiveMessage();
            if (message.MessageType == MessageType.SessionConnected)
            {
                this.communicationManager.SendMessage(MessageType.VersionCheck);
                message = this.communicationManager.ReceiveMessage();

                if (message.MessageType == MessageType.VersionCheck)
                {
                    var testPlatformVersion = this.dataSerializer.DeserializePayload<int>(message);
                    success = testPlatformVersion == 1;
                    if (!success)
                    {
                        EqtTrace.Error("VsTestConsoleRequestSender: VersionCheck Failed. TestPlatform Version: {0}", testPlatformVersion);
                    }
                }
                else
                {
                    EqtTrace.Error("VsTestConsoleRequestSender: VersionCheck Message Expected but different message received: Received MessageType: {0}", message.MessageType);
                }
            }
            else
            {
                EqtTrace.Error("VsTestConsoleRequestSender: SessionConnected Message Expected but different message received: Received MessageType: {0}", message.MessageType);
            }
            return success;
        }

        private void ListenAndReportTestCases(ITestDiscoveryEventsHandler eventHandler)
        {
            var isDiscoveryComplete = false;

            // Cycle through the messages that the vstest.console sends. 
            // Currently each of the operations are not separate tasks since they should not each take much time. 
            // This is just a notification.
            while (!isDiscoveryComplete)
            {
                try
                {
                    var message = this.communicationManager.ReceiveMessage();
                    if (string.Equals(MessageType.TestCasesFound, message.MessageType))
                    {
                        var testCases = this.dataSerializer.DeserializePayload<IEnumerable<TestCase>>(message);

                        eventHandler.HandleDiscoveredTests(testCases);
                    }
                    else if (string.Equals(MessageType.DiscoveryComplete, message.MessageType))
                    {
                        var discoveryCompletePayload = this.dataSerializer.DeserializePayload<DiscoveryCompletePayload>(message);

                        eventHandler.HandleDiscoveryComplete(discoveryCompletePayload.TotalTests, discoveryCompletePayload.LastDiscoveredTests, discoveryCompletePayload.IsAborted);
                        isDiscoveryComplete = true;
                    }
                    else if (string.Equals(MessageType.TestMessage, message.MessageType))
                    {
                        var testMessagePayload = this.dataSerializer.DeserializePayload<TestMessagePayload>(message);
                        eventHandler.HandleLogMessage(testMessagePayload.MessageLevel, testMessagePayload.Message);
                    }
                }
                catch (Exception ex)
                {
                    EqtTrace.Error("VsTestConsoleRequestSender: TestDiscovery: Message Deserialization failed with {0}", ex);

                    // Notify of a discovery complete and bail out.
                    eventHandler.HandleDiscoveryComplete(0, null, false);
                    isDiscoveryComplete = true;
                }
            }
        }

        private void ListenAndReportTestResults(ITestRunEventsHandler eventHandler, ITestHostLauncher customHostLauncher)
        {
            var isTestRunComplete = false;

            // Cycle through the messages that the testhost sends. 
            // Currently each of the operations are not separate tasks since they should not each take much time. This is just a notification.
            while (!isTestRunComplete)
            {
                try
                {
                    var message = this.communicationManager.ReceiveMessage();

                    if (string.Equals(MessageType.TestRunStatsChange, message.MessageType))
                    {
                        var testRunChangedArgs = this.dataSerializer.DeserializePayload<TestRunChangedEventArgs>(message);
                        eventHandler.HandleTestRunStatsChange(testRunChangedArgs);
                    }
                    else if (string.Equals(MessageType.ExecutionComplete, message.MessageType))
                    {
                        var testRunCompletePayload = this.dataSerializer.DeserializePayload<TestRunCompletePayload>(message);

                        eventHandler.HandleTestRunComplete(
                            testRunCompletePayload.TestRunCompleteArgs,
                            testRunCompletePayload.LastRunTests,
                            testRunCompletePayload.RunAttachments,
                            testRunCompletePayload.ExecutorUris);
                        isTestRunComplete = true;
                    }
                    else if (string.Equals(MessageType.TestMessage, message.MessageType))
                    {
                        var testMessagePayload = this.dataSerializer.DeserializePayload<TestMessagePayload>(message);
                        eventHandler.HandleLogMessage(testMessagePayload.MessageLevel, testMessagePayload.Message);
                    }
                    else if (string.Equals(MessageType.CustomTestHostLaunch, message.MessageType))
                    {
                        var testProcessStartInfo = this.dataSerializer.DeserializePayload<TestProcessStartInfo>(message);

                        int processId = (customHostLauncher != null) ? customHostLauncher.LaunchTestHost(testProcessStartInfo) : -1;
                        this.communicationManager.SendMessage(MessageType.CustomTestHostLaunchCallback, processId);
                    }
                }
                catch (Exception exception)
                {
                    EqtTrace.Error("VsTestConsoleRequestSender: TestExecution: Error Processing Request from DesignModeClient: {0}", exception);
                    // notify of a test run complete and bail out.
                    eventHandler.HandleTestRunComplete(new TestRunCompleteEventArgs(null, false, true, exception, null, TimeSpan.MinValue), null, null, null);
                    isTestRunComplete = true;
                }
            }
        }
    }
}
