using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

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
            this.setTitleBar();
        }

        private void setTitleBar()
        {

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                ApplicationView AppView = ApplicationView.GetForCurrentView();
                AppView.TitleBar.BackgroundColor = Colors.SlateGray;
                AppView.TitleBar.ButtonInactiveBackgroundColor = Colors.SlateGray;
                AppView.TitleBar.ButtonInactiveForegroundColor = Colors.White;
                AppView.TitleBar.ButtonBackgroundColor = Colors.SlateGray;
                AppView.TitleBar.ButtonForegroundColor = Colors.White;
                AppView.TitleBar.ButtonHoverBackgroundColor = Colors.SlateGray;
                AppView.TitleBar.ButtonHoverForegroundColor = Colors.White;
                AppView.TitleBar.ButtonPressedBackgroundColor = Colors.SlateGray;
                AppView.TitleBar.ButtonPressedForegroundColor = Colors.White;
                AppView.TitleBar.ForegroundColor = Colors.White;
                AppView.TitleBar.InactiveBackgroundColor = Colors.SlateGray;
                AppView.TitleBar.InactiveForegroundColor = Colors.White;
            }
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundOpacity = 1;
                statusBar.BackgroundColor = Colors.SlateGray;
                statusBar.ForegroundColor = Colors.White;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var protocolActivatedEventArgs = e.Parameter as ProtocolActivatedEventArgs;
            // Set the ProtocolForResultsOperation field.
            if (protocolActivatedEventArgs != null) {
                var uri = protocolActivatedEventArgs.Uri.Query.Remove(0, 6).Replace("%20", " ");
                this.ClipboardText.Text = uri;
                DataPackage package = new DataPackage()
                {
                    RequestedOperation = DataPackageOperation.Copy
                };
                package.SetText(uri);
                Clipboard.SetContent(package);
                NotifyUser("Copied!", NotifyType.StatusMessage);
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
        private async void openRemoteConnectionAsync(RemoteSystem remotesys)
        {
            if (remotesys != null)
            {
                // Create a remote system connection request.
                RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(remotesys);
                NotifyUser("Launching app on " + remotesys.DisplayName + "...", NotifyType.StatusMessage);
                var status = await RemoteLauncher.LaunchUriAsync(connectionRequest, new Uri("share-app:?Data=" + this.ClipboardText.Text));
                NotifyUser(status.ToString(), NotifyType.StatusMessage);
            }
            else
            {
                NotifyUser("Select a device for remote connection.", NotifyType.ErrorMessage);
            }
        }
        public void NotifyUser(string strMessage, NotifyType type)
        {
            StatusBlock.Text = strMessage;
        }

        private void ClipboardText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.ClipboardText.Text.Length > 0 && this.DeviceListBox.SelectedItem != null)
            {
                if (this.ClipboardText.Text.StartsWith("http://") || this.ClipboardText.Text.StartsWith("https://"))
                {
                    this.LaunchInBrowserButton.IsEnabled = true;
                }
                this.CopyToClipboardButton.IsEnabled = true;
            }
            else
            {
                this.LaunchInBrowserButton.IsEnabled = false;
                this.CopyToClipboardButton.IsEnabled = false;
            }
        }

        private void DeviceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.ClipboardText.Text.Length > 0 && this.DeviceListBox.SelectedItem != null)
            {
                if (this.ClipboardText.Text.StartsWith("http://") || this.ClipboardText.Text.StartsWith("https://"))
                {
                    this.LaunchInBrowserButton.IsEnabled = true;
                }
                this.CopyToClipboardButton.IsEnabled = true;
            }
            else
            {
                this.LaunchInBrowserButton.IsEnabled = false;
                this.CopyToClipboardButton.IsEnabled = false;
            }
        }

        private async void LaunchInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = this.deviceList[this.DeviceListBox.SelectedIndex];

            if (selectedDevice != null)
            {
                Uri uri;
                if (Uri.TryCreate(this.ClipboardText.Text, UriKind.Absolute, out uri))
                {
                    this.LoadingBar.IsEnabled = true;
                    this.LoadingBar.Visibility = Visibility.Visible;
                    NotifyUser("Sharing to " + selectedDevice.DisplayName + "...", NotifyType.StatusMessage);
                    RemoteLaunchUriStatus launchUriStatus = await RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(selectedDevice), uri);
                    NotifyUser(launchUriStatus.ToString(), NotifyType.StatusMessage);
                    this.LoadingBar.IsEnabled = false;
                    this.LoadingBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            RemoteSystem selectedDevice = this.DeviceListBox.SelectedItem as RemoteSystem;
            this.openRemoteConnectionAsync(selectedDevice);
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
