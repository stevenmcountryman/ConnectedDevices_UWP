using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using Windows.System.RemoteSystems;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Share_Across_Devices
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<RemoteSystem> deviceList;
        private RemoteSystemWatcher deviceWatcher;

        public ObservableCollection<RemoteSystem> DeviceList
        {
            get
            {
                return this.deviceList;
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            deviceList = new ObservableCollection<RemoteSystem>();
            this.setUpDevicesList();
            this.setupClipboardText();
        }

        private async void setupClipboardText()
        {
            try
            {
                DataPackageView dataPackageView = Clipboard.GetContent();
                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    string text = await dataPackageView.GetTextAsync();

                    this.ClipboardText.Text = text;
                }
            }
            catch
            {

            }
        }

        private async void setUpDevicesList()
        {
            RemoteSystemAccessStatus accessStatus = await RemoteSystem.RequestAccessAsync();

            if (accessStatus == RemoteSystemAccessStatus.Allowed)
            {
                deviceWatcher = RemoteSystem.CreateWatcher();
                deviceWatcher.RemoteSystemAdded += DeviceWatcher_RemoteSystemAdded;
                deviceWatcher.Start();
            }
        }

        private async void DeviceWatcher_RemoteSystemAdded(RemoteSystemWatcher sender, RemoteSystemAddedEventArgs args)
        {
            var remoteSystem = args.RemoteSystem;
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (!this.deviceList.Contains(remoteSystem))
                {
                    this.deviceList.Add(remoteSystem);
                }
                this.DeviceListBox.ItemsSource = this.deviceList;
            });
        }

        private async void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            RemoteSystem selectedDevice = this.DeviceListBox.SelectedItem as RemoteSystem;
            this.openRemoteConnectionAsync(selectedDevice);
            
        }
        private async void openRemoteConnectionAsync(RemoteSystem remotesys)
        {
            AppServiceConnection connection = new AppServiceConnection
            {
                AppServiceName = "com.simplisidy.appservice",
                PackageFamilyName = "Simplisidy.UWP.ShareAcrossDevices.CS_wtkr3v20s86d8"
            };

            if (remotesys != null)
            {
                // Create a remote system connection request.
                RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(remotesys);

                NotifyUser("Opening connection to " + remotesys.DisplayName + "...", NotifyType.StatusMessage);
                AppServiceConnectionStatus status = await connection.OpenRemoteAsync(connectionRequest);

                if (status == AppServiceConnectionStatus.Success)
                {
                    NotifyUser("Successfully connected to " + remotesys.DisplayName + "...", NotifyType.StatusMessage);
                    await SendMessageToRemoteAppServiceAsync(connection);
                }
                else
                {
                    NotifyUser("Attempt to open a remote app service connection failed with error - " + status.ToString(), NotifyType.ErrorMessage);
                }
            }
            else
            {
                NotifyUser("Select a device for remote connection.", NotifyType.ErrorMessage);
            }
        }
        private async Task SendMessageToRemoteAppServiceAsync(AppServiceConnection connection)
        {
            // Send message if connection to the remote app service is open.
            if (connection != null)
            {
                //Set up the inputs and send a message to the service.
                ValueSet inputs = new ValueSet();
                inputs.Add("clipboard", this.ClipboardText.Text);
                NotifyUser("Sent clipboard to " + (this.DeviceListBox.SelectedItem as RemoteSystem).DisplayName + ". Waiting for a response...", NotifyType.StatusMessage);
                AppServiceResponse response = await connection.SendMessageAsync(inputs);

                if (response.Status == AppServiceResponseStatus.Success)
                {
                    if (response.Message.ContainsKey("result"))
                    {
                        string resultText = response.Message["result"].ToString();
                        if (string.IsNullOrEmpty(resultText))
                        {
                            NotifyUser("Remote app service did not respond with a result.", NotifyType.ErrorMessage);
                        }
                        else
                        {
                            NotifyUser("Result = " + resultText, NotifyType.StatusMessage);
                        }
                    }
                    else
                    {
                        NotifyUser("Response from remote app service does not contain a result.", NotifyType.ErrorMessage);
                    }
                }
                else
                {
                    NotifyUser("Sending message to remote app service failed with error - " + response.Status.ToString(), NotifyType.ErrorMessage);
                }
            }
            else
            {
                NotifyUser("Not connected to any app service. Select a device to open a connection.", NotifyType.ErrorMessage);
            }
        }
        public void NotifyUser(string strMessage, NotifyType type)
        {
            StatusBlock.Text = strMessage;
        }

        private void ClipboardText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.ClipboardText.Text.Length > 0)
            {
                this.ShareButton.IsEnabled = true;
            }
            else
            {
                this.ShareButton.IsEnabled = false;
            }
        }

        private void DeviceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DeviceListBox.SelectedIndex >= 0)
            {
                this.ShareButton.IsEnabled = true;
            }
        }
    }
    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

    sealed partial class App : Application
    {
        protected override void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        {
            var rootFrame = CreateRootFrame();
            rootFrame.Navigate(typeof(ShareWebLink), args.ShareOperation);
            Window.Current.Activate();
        }
    }
}
