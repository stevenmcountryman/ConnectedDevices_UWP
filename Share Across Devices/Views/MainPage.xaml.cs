using System;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI.Xaml.Media;
using Share_Across_Devices.Helpers;
using Share_Across_Devices.Controls;
using Windows.ApplicationModel.AppService;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using System.IO;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Threading;
using Windows.Storage.Streams;
using System.Text;
using System.Runtime.Serialization;

namespace Share_Across_Devices
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private RemoteSystemWatcher deviceWatcher;
        private Compositor _compositor;
        private string fileName;
        private StorageFile file;

        public MainPage()
        {
            this.InitializeComponent();
            this.setUpCompositorStuff();
            this.setUpDevicesList();
            this.setTitleBar();
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;
        }

        private void setUpCompositorStuff()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.StatusPanel);
            itemVisual.Opacity = 0;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var protocolArgs = e.Parameter as ProtocolActivatedEventArgs;


            // Set the ProtocolForResultsOperation field.
            if (protocolArgs != null)
            {
                var queryStrings = new WwwFormUrlDecoder(protocolArgs.Uri.Query);
                if (!protocolArgs.Uri.Query.StartsWith("?FileName="))
                {
                    string textToCopy = queryStrings.GetFirstValueByName("Text");
                    try
                    {
                        if (textToCopy.Length > 0)
                        {
                            DataPackage package = new DataPackage()
                            {
                                RequestedOperation = DataPackageOperation.Copy
                            };
                            package.SetText(textToCopy);
                            Clipboard.SetContent(package);
                            Clipboard.Flush();
                            NotifyUser("Copied!");
                        }
                    }
                    catch
                    {
                        NotifyUser("Manual copy required");
                        this.ClipboardText.Text = textToCopy;
                        this.CopyToLocalClipboardButton.Visibility = Visibility.Visible;
                        this.CopyToLocalClipboardButton.IsEnabled = true;
                    }

                }
                else
                {
                    fileName = queryStrings.GetFirstValueByName("FileName");
                    this.beginListeningForFile();
                }
            }
        }

        private async void beginListeningForFile()
        {
            NotifyUser("Receiving file...");
            try
            {
                //Create a StreamSocketListener to start listening for TCP connections.
                StreamSocketListener socketListener = new StreamSocketListener();

                //Hook up an event handler to call when connections are received.
                socketListener.ConnectionReceived += SocketListener_ConnectionReceived;

                //Start listening for incoming TCP connections on the specified port. You can specify any port that' s not currently in use.
                await socketListener.BindServiceNameAsync("1717");
            }
            catch (Exception e)
            {
                NotifyUser("Failed to receive file");
            }
        }

        private async void SocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            if (fileName != null)
            {
                file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                //Read line from the remote client.
                using (var fileStream = await file.OpenStreamForWriteAsync())
                {
                    using (var inStream = args.Socket.InputStream.AsStreamForRead())
                    {

                    }
                }

                //Send the line back to the remote client.
                Stream outStream = args.Socket.OutputStream.AsStreamForWrite();
                StreamWriter writer = new StreamWriter(outStream);
                await writer.WriteLineAsync("File Received!");
                await writer.FlushAsync();

                NotifyUser("File received");

                this.saveFile();
            }
        }

        private async void saveFile()
        {
            FolderPicker saver = new FolderPicker();
            saver.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            var folder = await saver.PickSingleFolderAsync();
            await file.CopyAsync(folder);
            NotifyUser("File Saved");
        }

        #region Beautification
        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            var trans = new TranslateTransform();
            trans.Y = 0;
            this.RenderTransform = trans;
            args.EnsuredFocusedElementInView = false;
        }
        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            var _offSet = (int)args.OccludedRect.Height;
            args.EnsuredFocusedElementInView = true;
            var trans = new TranslateTransform();
            trans.Y = -_offSet;
            this.RenderTransform = trans;
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
        #endregion

        #region Device List Methods
        private async void setUpDevicesList()
        {
            RemoteSystemAccessStatus accessStatus = await RemoteSystem.RequestAccessAsync();

            if (accessStatus == RemoteSystemAccessStatus.Allowed)
            {
                deviceWatcher = RemoteSystem.CreateWatcher();
                deviceWatcher.RemoteSystemAdded += DeviceWatcher_RemoteSystemAdded;
                deviceWatcher.RemoteSystemUpdated += DeviceWatcher_RemoteSystemUpdated;
                deviceWatcher.RemoteSystemRemoved += DeviceWatcher_RemoteSystemRemoved;
                deviceWatcher.Start();
            }
        }

        private async void DeviceWatcher_RemoteSystemAdded(RemoteSystemWatcher sender, RemoteSystemAddedEventArgs args)
        {
            var remoteSystem = args.RemoteSystem;
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RemoteDevice device = new RemoteDevice(remoteSystem);
                this.DeviceGrid.Items.Add(device);
            });
        }
        private async void DeviceWatcher_RemoteSystemUpdated(RemoteSystemWatcher sender, RemoteSystemUpdatedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (RemoteDevice device in this.DeviceGrid.Items)
                {
                    if (device.GetDevice().Id == args.RemoteSystem.Id)
                    {
                        device.SetDevice(args.RemoteSystem);
                        this.validTextAndButtons();
                        return;
                    }
                }
            });
        }
        private async void DeviceWatcher_RemoteSystemRemoved(RemoteSystemWatcher sender, RemoteSystemRemovedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (RemoteDevice device in this.DeviceGrid.Items)
                {
                    if (device.GetDevice().Id == args.RemoteSystemId)
                    {
                        this.DeviceGrid.Items.Remove(device);
                        this.validTextAndButtons();
                        return;
                    }
                }
            });
        }
        #endregion

        #region UI Change Events
        private void ClipboardText_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.validTextAndButtons();
        }
        private void DeviceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.validTextAndButtons();
        }
        private void Button_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var button = sender as Button;
            this.animateButtonEnabled(button);
        }

        private void CopyToLocalClipboardButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var button = sender as Button;
            this.animateLocalClipButton(button);
        }
        #endregion

        #region UI Helpers
        public void NotifyUser(string strMessage)
        {
            StatusBlock.Text = strMessage;
            this.animateStatusContinuous();
        }

        private void showShareLoading(string deviceName)
        {
            this.LoadingBar.IsEnabled = true;
            this.LoadingBar.Visibility = Visibility.Visible;
            NotifyUser("Sharing to " + deviceName + "...");
        }
        private void showShareComplete(RemoteLaunchUriStatus status)
        {
            NotifyUser(status.ToString());
            this.LoadingBar.IsEnabled = false;
            this.LoadingBar.Visibility = Visibility.Collapsed;
        }
        private void showYoutubeButtons()
        {
            this.OpenInTubeCastButton.IsEnabled = true;
            this.OpenInMyTubeButton.IsEnabled = true;
        }
        private void hideYoutubeButtons()
        {
            this.OpenInTubeCastButton.IsEnabled = false;
            this.OpenInMyTubeButton.IsEnabled = false;
        }
        private void validTextAndButtons()
        {
            if (this.DeviceGrid.SelectedItem != null)
            {
                if (this.remoteSystemIsLocal())
                {
                    this.OpenFileButton.IsEnabled = true;
                }
                else
                {
                    this.OpenFileButton.IsEnabled = false;
                }

                if (this.ClipboardText.Text.Length > 0)
                {
                    this.CopyToClipboardButton.IsEnabled = true;
                    this.checkIfWebLink();
                }
                else
                {
                    this.CopyToClipboardButton.IsEnabled = false;
                    this.LaunchInBrowserButton.IsEnabled = false;
                    this.hideYoutubeButtons();
                }
            }
            else
            {
                this.LaunchInBrowserButton.IsEnabled = false;
                this.OpenFileButton.IsEnabled = false;
                this.CopyToClipboardButton.IsEnabled = false;
                this.hideYoutubeButtons();
            }
        }
        private bool remoteSystemIsLocal()
        {
            return (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice().IsAvailableByProximity;
        }
        private void checkIfWebLink()
        {
            if (this.ClipboardText.Text.ToLower().StartsWith("http://") || this.ClipboardText.Text.ToLower().StartsWith("https://"))
            {
                this.LaunchInBrowserButton.IsEnabled = true;
                if (this.ClipboardText.Text.ToLower().Contains("youtube.com/watch?"))
                {
                    this.showYoutubeButtons();
                }
                else
                {
                    this.hideYoutubeButtons();
                }
            }
            else
            {
                this.LaunchInBrowserButton.IsEnabled = false;
                this.hideYoutubeButtons();
            }
        }
        #endregion

        #region Button Click Events
        private async void LaunchInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TryShareURL(selectedDevice, this.ClipboardText.Text);
                this.showShareComplete(status);
            }
        }
        private async void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TrySharetext(selectedDevice, this.ClipboardText.Text);
                this.showShareComplete(status);
            }
        }
        private async void OpenInTubeCastButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TryShareURL(selectedDevice, RemoteLaunch.ParseYoutubeLinkToTubeCastUri(this.ClipboardText.Text));
                this.showShareComplete(status);
            }
        }
        private async void OpenInMyTubeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TryShareURL(selectedDevice, RemoteLaunch.ParseYoutubeLinkToMyTubeUri(this.ClipboardText.Text));
                this.showShareComplete(status);
            }
        }
        private void CopyToLocalClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage package = new DataPackage()
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            package.SetText(this.ClipboardText.Text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            NotifyUser("Copied!");
            this.CopyToLocalClipboardButton.IsEnabled = false;
        }
        #endregion

        #region Animations
        private void animateStatusContinuous()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.StatusPanel);

            ScalarKeyFrameAnimation opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.IterationBehavior = AnimationIterationBehavior.Count;
            opacityAnimation.IterationCount = 5;
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(1500);
            opacityAnimation.InsertKeyFrame(0f, 0f);
            opacityAnimation.InsertKeyFrame(0.5f, 1f);
            opacityAnimation.InsertKeyFrame(1f, 0f);

            itemVisual.StartAnimation("Opacity", opacityAnimation);
        }

        private void animateLocalClipButton(Button button)
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(button);
            float width = (float)button.RenderSize.Width;
            float height = (float)button.RenderSize.Height;
            itemVisual.CenterPoint = new Vector3(width / 2, height / 2, 0f);

            Vector3KeyFrameAnimation scaleAnimation = _compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);

            ScalarKeyFrameAnimation opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(500);

            if (button.IsEnabled)
            {
                itemVisual.Opacity = 0f;
                scaleAnimation.InsertKeyFrame(0f, new Vector3(0f, 0f, 0f));
                scaleAnimation.InsertKeyFrame(0.1f, new Vector3(1f, 1.1f, 1.1f));
                scaleAnimation.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f));

                opacityAnimation.InsertKeyFrame(1f, 1f);
            }
            else
            {
                itemVisual.Opacity = 1f;
                scaleAnimation.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));
                scaleAnimation.InsertKeyFrame(0.1f, new Vector3(1f, 1.1f, 1.1f));
                scaleAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f));

                opacityAnimation.InsertKeyFrame(1f, 0f);
            }

            CompositionScopedBatch myScopedBatch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            myScopedBatch.Completed += OnBatchCompleted;
            itemVisual.StartAnimation("Scale", scaleAnimation);
            itemVisual.StartAnimation("Opacity", opacityAnimation);
            myScopedBatch.End();
        }
        private void animateButtonEnabled(Button button)
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(button);
            float width = (float)button.RenderSize.Width;
            float height = (float)button.RenderSize.Height;
            itemVisual.CenterPoint = new Vector3(width / 2, height / 2, 0f);

            Vector3KeyFrameAnimation scaleAnimation = _compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            scaleAnimation.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));

            if (button.IsEnabled)
            {
                scaleAnimation.InsertKeyFrame(0.1f, new Vector3(1.1f, 1.1f, 1.1f));
            }
            else
            {
                scaleAnimation.InsertKeyFrame(0.1f, new Vector3(0.9f, 0.9f, 0.9f));
            }

            scaleAnimation.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f));
            itemVisual.StartAnimation("Scale", scaleAnimation);
        }

        private void OnBatchCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (!this.CopyToLocalClipboardButton.IsEnabled)
            {
                this.CopyToLocalClipboardButton.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region File/App Service Stuff
        private async void openRemoteConnectionAsync(RemoteSystem remotesys)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add("*");
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            file = await openPicker.PickSingleFileAsync();

            if (file != null)
            {
                AppServiceConnection connection = new AppServiceConnection
                {
                    AppServiceName = "simplisidy.appservice",
                    PackageFamilyName = "34507Simplisidy.ShareAcrossDevices_wtkr3v20s86d8"
                };

                if (remotesys != null)
                {
                    // Create a remote system connection request.
                    RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(remotesys);

                    NotifyUser("Requesting connection to " + remotesys.DisplayName + "...");
                    AppServiceConnectionStatus status = await connection.OpenRemoteAsync(connectionRequest);

                    if (status == AppServiceConnectionStatus.Success)
                    {
                        NotifyUser("Successfully connected to " + remotesys.DisplayName + "...");
                        await RequestIPAddress(connection);
                    }
                    else
                    {
                        NotifyUser("Attempt to open a remote app service connection failed with error - " + status.ToString());
                    }
                }
                else
                {
                    NotifyUser("Select a device for remote connection.");
                }
            }
        }
        private async Task RequestIPAddress(AppServiceConnection connection)
        {
            // Send message if connection to the remote app service is open.
            if (connection != null)
            {
                //Set up the inputs and send a message to the service.
                ValueSet inputs = new ValueSet();
                NotifyUser("Requesting IP address....");
                AppServiceResponse response = await connection.SendMessageAsync(inputs);

                if (response.Status == AppServiceResponseStatus.Success)
                {
                    if (response.Message.ContainsKey("result"))
                    {
                        string ipAddress = response.Message["result"].ToString();
                        if (string.IsNullOrEmpty(ipAddress))
                        {
                            NotifyUser("Remote app service did not respond with a result.");
                        }
                        else
                        {
                            this.beginConnection(ipAddress);
                        }
                    }
                    else
                    {
                        NotifyUser("Response from remote app service does not contain a result.");
                    }
                }
                else
                {
                    NotifyUser("Sending message to remote app service failed with error - " + response.Status.ToString());
                }
            }
            else
            {
                NotifyUser("Not connected to any app service. Select a device to open a connection.");
            }
        }
        private async void beginConnection(string ipAddress)
        {
            try
            {
                NotifyUser("Launching app on device....");
                var selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();
                var status = await RemoteLaunch.TryBeginShareFile(selectedDevice, file.Name);

                if (status == RemoteLaunchUriStatus.Success)
                {
                    //Create the StreamSocket and establish a connection to the echo server.
                    StreamSocket socket = new StreamSocket();

                    //The server hostname that we will be establishing a connection to. We will be running the server and client locally,
                    //so we will use localhost as the hostname.
                    Windows.Networking.HostName serverHost = new Windows.Networking.HostName(ipAddress);

                    //Every protocol typically has a standard port number. For example HTTP is typically 80, FTP is 20 and 21, etc.
                    //For the echo server/client application we will use a random port 1337.
                    NotifyUser("Opening connection....");
                    string serverPort = "1717";
                    await socket.ConnectAsync(serverHost, serverPort);

                    NotifyUser("Creating file stream....");
                    //Write data to the echo server.
                    using (Stream streamOut = socket.OutputStream.AsStreamForWrite())
                    {
                        using (var fileStream = await file.OpenStreamForReadAsync())
                        {

                        }
                    }

                    
                    //Read data from the echo server.
                    Stream streamIn = socket.InputStream.AsStreamForRead();
                    StreamReader reader = new StreamReader(streamIn);
                    string response = await reader.ReadLineAsync();
                    NotifyUser(response);
                }
            }
            catch (Exception e)
            {
                NotifyUser("Connection failed. Network destination not allowed.");
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            this.openRemoteConnectionAsync(selectedDevice);
        }
        #endregion
    }

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
