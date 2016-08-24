﻿using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.System;
using Windows.UI.Xaml;

namespace ShareAcrossAppService
{
    public sealed class AppService : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private AppServiceConnection appServiceconnection;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.backgroundTaskDeferral = taskInstance.GetDeferral(); // Get a deferral so that the service isn't terminated.
            taskInstance.Canceled += OnTaskCanceled; // Associate a cancellation handler with the background task.

            // Retrieve the app service connection and set up a listener for incoming app service requests.
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            appServiceconnection = details.AppServiceConnection;
            appServiceconnection.RequestReceived += OnRequestReceived;
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
            {
                this.backgroundTaskDeferral.Complete();
                this.backgroundTaskDeferral = null;
            }
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            // Get a deferral because we use an awaitable API below to respond to the message
            // and we don't want this call to get cancelled while we are waiting.
            var messageDeferral = args.GetDeferral();

            ValueSet returnData = new ValueSet();
            var icp = NetworkInformation.GetInternetConnectionProfile();

            if (icp?.NetworkAdapter == null) return;
            var hostnames = NetworkInformation.GetHostNames();
            HostName hostname = null;
            foreach (var name in hostnames)
            {
                if (name.IPInformation?.NetworkAdapter != null && name.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId)
                {
                    hostname = name;
                    break;
                }
            }

            // the ip address
            returnData.Add("result", hostname?.CanonicalName);

            await args.Request.SendResponseAsync(returnData);// Return the data to the caller.

            this.backgroundTaskDeferral.Complete();// Complete the deferral so that the platform knows that we're done responding to the app service call.
        }
    }
}
