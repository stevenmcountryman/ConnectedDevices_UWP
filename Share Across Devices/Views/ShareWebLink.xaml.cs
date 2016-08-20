using Share_Across_Devices.Controls;
using Share_Across_Devices.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Share_Across_Devices
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ShareWebLink : Page
    {
        ShareOperation shareOperation;
        private string sharedDataTitle;
        private string sharedDataDescription;
        private string sharedDataPackageFamilyName;
        private Uri sharedDataContentSourceWebLink;
        private Uri sharedDataContentSourceApplicationLink;
        private Color sharedDataLogoBackgroundColor;
        private IRandomAccessStreamReference sharedDataSquare30x30Logo;
        private string shareQuickLinkId;
        private string sharedText;
        private Uri sharedWebLink;
        private Uri sharedApplicationLink;
        private IReadOnlyList<IStorageItem> sharedStorageItems;
        private string sharedCustomData;
        private string sharedHtmlFormat;
        private IReadOnlyDictionary<string, RandomAccessStreamReference> sharedResourceMap;
        private IRandomAccessStreamReference sharedBitmapStreamRef;
        private IRandomAccessStreamReference sharedThumbnailStreamRef;
        private const string dataFormatName = "http://schema.org/Book";
        private RemoteSystemWatcher deviceWatcher;
        private Compositor _compositor;
        StorageFile file;
        private bool cancelAttempted = false;
        private RemoteSystem selectedDevice;
        AppServiceConnection connection;
        StreamSocket socket;

        public ShareWebLink()
        {
            this.InitializeComponent();
            this.setUpCompositorStuff();
            this.setUpDevicesList();
            this.setTitleBar();
        }

        private void setUpCompositorStuff()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.StatusPanel);
            itemVisual.Opacity = 0;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // It is recommended to only retrieve the ShareOperation object in the activation handler, return as
            // quickly as possible, and retrieve all data from the share target asynchronously.

            this.shareOperation = (ShareOperation)e.Parameter;

            await Task.Factory.StartNew(async () =>
            {
                // Retrieve the data package properties.
                this.sharedDataTitle = this.shareOperation.Data.Properties.Title;
                this.sharedDataDescription = this.shareOperation.Data.Properties.Description;
                this.sharedDataPackageFamilyName = this.shareOperation.Data.Properties.PackageFamilyName;
                this.sharedDataContentSourceWebLink = this.shareOperation.Data.Properties.ContentSourceWebLink;
                this.sharedDataContentSourceApplicationLink = this.shareOperation.Data.Properties.ContentSourceApplicationLink;
                this.sharedDataLogoBackgroundColor = this.shareOperation.Data.Properties.LogoBackgroundColor;
                this.sharedDataSquare30x30Logo = this.shareOperation.Data.Properties.Square30x30Logo;
                this.sharedThumbnailStreamRef = this.shareOperation.Data.Properties.Thumbnail;
                this.shareQuickLinkId = this.shareOperation.QuickLinkId;

                // Retrieve the data package content.
                // The GetWebLinkAsync(), GetTextAsync(), GetStorageItemsAsync(), etc. APIs will throw if there was an error retrieving the data from the source app.
                // In this sample, we just display the error. It is recommended that a share target app handles these in a way appropriate for that particular app.
                if (this.shareOperation.Data.Contains(StandardDataFormats.WebLink))
                {
                    try
                    {
                        this.sharedWebLink = await this.shareOperation.Data.GetWebLinkAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetWebLinkAsync - " + ex.Message);
                    }
                }
                if (this.shareOperation.Data.Contains(StandardDataFormats.ApplicationLink))
                {
                    try
                    {
                        this.sharedApplicationLink = await this.shareOperation.Data.GetApplicationLinkAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetApplicationLinkAsync - " + ex.Message);
                    }
                }
                if (this.shareOperation.Data.Contains(StandardDataFormats.Text))
                {
                    try
                    {
                        this.sharedText = await this.shareOperation.Data.GetTextAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetTextAsync - " + ex.Message);
                    }
                }
                if (this.shareOperation.Data.Contains(StandardDataFormats.StorageItems))
                {
                    try
                    {
                        this.sharedStorageItems = await this.shareOperation.Data.GetStorageItemsAsync();
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("FileToSend", this.sharedStorageItems[0]);
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetStorageItemsAsync - " + ex.Message);
                    }
                }
                if (this.shareOperation.Data.Contains(dataFormatName))
                {
                    try
                    {
                        this.sharedCustomData = await this.shareOperation.Data.GetTextAsync(dataFormatName);
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetTextAsync(" + dataFormatName + ") - " + ex.Message);
                    }
                }
                if (this.shareOperation.Data.Contains(StandardDataFormats.Html))
                {
                    try
                    {
                        this.sharedHtmlFormat = await this.shareOperation.Data.GetHtmlFormatAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetHtmlFormatAsync - " + ex.Message);
                    }

                    try
                    {
                        this.sharedResourceMap = await this.shareOperation.Data.GetResourceMapAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetResourceMapAsync - " + ex.Message);
                    }
                }
                if (this.shareOperation.Data.Contains(StandardDataFormats.Bitmap))
                {
                    try
                    {
                        this.sharedBitmapStreamRef = await this.shareOperation.Data.GetBitmapAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetBitmapAsync - " + ex.Message);
                    }
                }

                // In this sample, we just display the shared data content.

                // Get back to the UI thread using the dispatcher.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    if (this.sharedWebLink != null)
                    {
                        AddContentValue("", this.sharedWebLink.AbsoluteUri);
                    }
                    if (this.sharedStorageItems != null && this.sharedStorageItems.Count == 1)
                    {
                        this.file = await StorageFile.GetFileFromPathAsync(this.sharedStorageItems[0].Path);
                        this.ClipboardText.Text = file.Name;
                    }
                });
            });
        }

        private void AddContentValue(string title, string description = null)
        {
            Run contentType = new Run();
            contentType.Foreground = new SolidColorBrush(Colors.White);
            contentType.FontSize = 18;
            contentType.FontWeight = FontWeights.Bold;
            contentType.Text = title;
            ClipboardText.Inlines.Add(contentType);

            if (description != null)
            {
                Run contentValue = new Run();
                contentValue.Text = description + Environment.NewLine;
                ClipboardText.Inlines.Add(contentValue);
            }
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
            var appGray = Color.FromArgb(255, 83, 83, 83);
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                ApplicationView AppView = ApplicationView.GetForCurrentView();
                AppView.TitleBar.BackgroundColor = appGray;
                AppView.TitleBar.ButtonInactiveBackgroundColor = appGray;
                AppView.TitleBar.ButtonInactiveForegroundColor = Colors.White;
                AppView.TitleBar.ButtonBackgroundColor = appGray;
                AppView.TitleBar.ButtonForegroundColor = Colors.White;
                AppView.TitleBar.ButtonHoverBackgroundColor = appGray;
                AppView.TitleBar.ButtonHoverForegroundColor = Colors.White;
                AppView.TitleBar.ButtonPressedBackgroundColor = appGray;
                AppView.TitleBar.ButtonPressedForegroundColor = Colors.White;
                AppView.TitleBar.ForegroundColor = Colors.White;
                AppView.TitleBar.InactiveBackgroundColor = appGray;
                AppView.TitleBar.InactiveForegroundColor = Colors.White;
            }
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundOpacity = 1;
                statusBar.BackgroundColor = appGray;
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
            if (this.ClipboardText.Text.Length > 0 && this.DeviceGrid.SelectedItem != null)
            {
                this.checkIfWebLink();
                this.checkIfFile();
                this.CopyToClipboardButton.IsEnabled = true;
            }
            else
            {
                this.LaunchInBrowserButton.IsEnabled = false;
                this.CopyToClipboardButton.IsEnabled = false;
                this.hideYoutubeButtons();
                this.OpenFileButton.IsEnabled = false;
            }
        }
        private void checkIfFile()
        {
            if (this.file != null)
            {
                this.OpenFileButton.IsEnabled = true;
            }
            else
            {
                this.OpenFileButton.IsEnabled = false;
            }
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
            this.selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TryShareURL(selectedDevice, this.ClipboardText.Text);
                this.showShareComplete(status);
            }
        }
        private async void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            this.selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TrySharetext(selectedDevice, this.ClipboardText.Text);
                this.showShareComplete(status);
            }
        }
        private async void OpenInTubeCastButton_Click(object sender, RoutedEventArgs e)
        {
            this.selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TryShareURL(selectedDevice, RemoteLaunch.ParseYoutubeLinkToTubeCastUri(this.ClipboardText.Text));
                this.showShareComplete(status);
            }
        }
        private async void OpenInMyTubeButton_Click(object sender, RoutedEventArgs e)
        {
            this.selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();

            if (selectedDevice != null)
            {
                this.showShareLoading(selectedDevice.DisplayName);
                var status = await RemoteLaunch.TryShareURL(selectedDevice, RemoteLaunch.ParseYoutubeLinkToMyTubeUri(this.ClipboardText.Text));
                this.showShareComplete(status);
            }
        }
        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.OpenFileButton.Content.ToString() == "file")
            {
                this.selectedDevice = (this.DeviceGrid.SelectedItem as RemoteDevice).GetDevice();
                this.openRemoteConnectionAsync(selectedDevice);
            }
            else
            {
                this.NotifyUser("Attempting to cancel transfer");
                try
                {
                    this.connection.Dispose();
                    this.socket.Dispose();
                }
                catch
                {

                }
                this.cancelAttempted = true;
                this.OpenFileButton.IsEnabled = false;
            }
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
        #endregion


        private void handleCancel()
        {
            this.NotifyUser("File transfer cancelled");
            this.cancelAttempted = false;
            this.OpenFileButton.IsEnabled = true;
            this.OpenFileButton.Content = "file";
        }
        private async void openRemoteConnectionAsync(RemoteSystem remotesys)
        {
            if (this.cancelAttempted)
            {
                this.handleCancel();
                return;
            }
            var sendAttempt = 1;
            AppServiceConnectionStatus status = AppServiceConnectionStatus.Unknown;
            if (file != null && remotesys != null)
            {
                while (sendAttempt <= 3)
                {
                    if (this.cancelAttempted)
                    {
                        this.handleCancel();
                        return;
                    }
                    using (this.connection = new AppServiceConnection
                    {
                        AppServiceName = "simplisidy.appservice",
                        PackageFamilyName = "34507Simplisidy.ShareAcrossDevices_wtkr3v20s86d8"
                    })
                    {
                        // Create a remote system connection request.
                        RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(remotesys);

                        NotifyUser("Requesting connection to " + remotesys.DisplayName + "...");
                        status = await this.connection.OpenRemoteAsync(connectionRequest);
                        if (status == AppServiceConnectionStatus.Success)
                        {
                            NotifyUser("Successfully connected to " + remotesys.DisplayName + "...");
                            await this.RequestIPAddress(connection);
                            return;
                        }
                        else
                        {
                            sendAttempt++;
                            NotifyUser("Failed. Retrying attempt " + sendAttempt + " of 3");
                        }
                    }
                }
                NotifyUser("Attempt to open a remote app service connection failed with error - " + status.ToString());
            }
            else
            {
                NotifyUser("Select a device for remote connection.");
            }
        }
        private async Task RequestIPAddress(AppServiceConnection connection)
        {
            if (this.cancelAttempted)
            {
                this.handleCancel();
                return;
            }
            var sendAttempt = 1;
            AppServiceResponse response = null;
            // Send message if connection to the remote app service is open.
            if (connection != null)
            {
                while (sendAttempt <= 3)
                {
                    if (this.cancelAttempted)
                    {
                        this.handleCancel();
                        return;
                    }
                    //Set up the inputs and send a message to the service.
                    ValueSet inputs = new ValueSet();
                    NotifyUser("Requesting IP address....");
                    response = await connection.SendMessageAsync(inputs);

                    if (response.Status == AppServiceResponseStatus.Success)
                    {
                        if (response.Message.ContainsKey("result"))
                        {
                            string ipAddress = response.Message["result"].ToString();
                            if (!string.IsNullOrEmpty(ipAddress))
                            {
                                this.beginConnection(ipAddress);
                                return;
                            }
                            else
                            {
                                NotifyUser("Remote app service did not respond with a result.");
                            }
                            break;
                        }
                        else
                        {
                            NotifyUser("Response from remote app service does not contain a result.");
                        }
                    }
                    else
                    {
                        sendAttempt++;
                        NotifyUser("Failed. Retrying attempt " + sendAttempt + " of 3");
                    }
                }
                NotifyUser("Sending message to remote app service failed with error - " + response.Status.ToString());
            }
            else
            {
                NotifyUser("Not connected to any app service. Select a device to open a connection.");
            }
        }
        private async void beginConnection(string ipAddress)
        {
            if (this.cancelAttempted)
            {
                this.handleCancel();
                return;
            }
            var sendAttempt = 1;
            while (sendAttempt <= 3)
            {
                if (this.cancelAttempted)
                {
                    this.handleCancel();
                    return;
                }
                try
                {
                    NotifyUser("Launching app on device....");
                    var status = await RemoteLaunch.TryBeginShareFile(this.selectedDevice, file.Name);

                    if (status == RemoteLaunchUriStatus.Success)
                    {
                        //Create the StreamSocket and establish a connection to the echo server.
                        using (this.socket = new StreamSocket())
                        {

                            //The server hostname that we will be establishing a connection to. We will be running the server and client locally,
                            //so we will use localhost as the hostname.
                            Windows.Networking.HostName serverHost = new Windows.Networking.HostName(ipAddress);

                            //Every protocol typically has a standard port number. For example HTTP is typically 80, FTP is 20 and 21, etc.
                            //For the echo server/client application we will use a random port 1337.
                            NotifyUser("Opening connection....");
                            string serverPort = "1717";
                            await socket.ConnectAsync(serverHost, serverPort);

                            var itemVisual = ElementCompositionPreview.GetElementVisual(this.StatusPanel);
                            itemVisual.Opacity = 1f;
                            this.StatusBlock.Text = "Sending file...";
                            //Write data to the echo server.
                            using (Stream streamOut = socket.OutputStream.AsStreamForWrite())
                            {
                                using (var fileStream = await file.OpenStreamForReadAsync())
                                {
                                    byte[] bytes;
                                    DataWriter dataWriter = new DataWriter(streamOut.AsOutputStream());
                                    fileStream.Seek(0, SeekOrigin.Begin);
                                    while (fileStream.Position < fileStream.Length)
                                    {
                                        if (this.cancelAttempted)
                                        {
                                            this.handleCancel();
                                            return;
                                        }
                                        if (fileStream.Length - fileStream.Position >= 7171)
                                        {
                                            bytes = new byte[7171];
                                        }
                                        else
                                        {
                                            bytes = new byte[fileStream.Length - fileStream.Position];
                                        }
                                        dataWriter.WriteBoolean(true);
                                        await dataWriter.StoreAsync();
                                        dataWriter.WriteInt32(bytes.Length);
                                        await dataWriter.StoreAsync();
                                        var percentage = ((double)fileStream.Position / (double)fileStream.Length) * 100.0;
                                        dataWriter.WriteInt32(Convert.ToInt32(percentage));
                                        await dataWriter.StoreAsync();
                                        this.StatusBlock.Text = Convert.ToInt32(percentage) + "% transferred";
                                        await fileStream.ReadAsync(bytes, 0, bytes.Length);
                                        dataWriter.WriteBytes(bytes);
                                        await dataWriter.StoreAsync();
                                    }
                                    dataWriter.WriteBoolean(false);
                                    await dataWriter.StoreAsync();
                                }
                            }

                            //Read data from the echo server.
                            Stream streamIn = socket.InputStream.AsStreamForRead();
                            StreamReader reader = new StreamReader(streamIn);
                            string response = await reader.ReadLineAsync();
                            NotifyUser(response);
                            this.OpenFileButton.Content = "file";
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    sendAttempt++;
                    NotifyUser("Failed. Retrying attempt " + sendAttempt + " of 3");
                }
            }
            NotifyUser("Connection failed. Network destination not allowed.");
        }
    }
}