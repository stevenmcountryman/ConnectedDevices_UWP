using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
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
using static Share_Across_Devices.MainPage;

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
        private ObservableCollection<RemoteSystem> deviceList;
        private RemoteSystemWatcher deviceWatcher;
        private Compositor _compositor;

        public ObservableCollection<RemoteSystem> DeviceList
        {
            get
            {
                return this.deviceList;
            }
        }

        public ShareWebLink()
        {
            this.InitializeComponent();
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
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

        private async void setUpDevicesList()
        {
            deviceList = new ObservableCollection<RemoteSystem>();
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
                        NotifyUser("Failed GetWebLinkAsync - " + ex.Message, NotifyType.ErrorMessage);
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
                        NotifyUser("Failed GetApplicationLinkAsync - " + ex.Message, NotifyType.ErrorMessage);
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
                        NotifyUser("Failed GetTextAsync - " + ex.Message, NotifyType.ErrorMessage);
                    }
                }
                if (this.shareOperation.Data.Contains(StandardDataFormats.StorageItems))
                {
                    try
                    {
                        this.sharedStorageItems = await this.shareOperation.Data.GetStorageItemsAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetStorageItemsAsync - " + ex.Message, NotifyType.ErrorMessage);
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
                        NotifyUser("Failed GetTextAsync(" + dataFormatName + ") - " + ex.Message, NotifyType.ErrorMessage);
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
                        NotifyUser("Failed GetHtmlFormatAsync - " + ex.Message, NotifyType.ErrorMessage);
                    }

                    try
                    {
                        this.sharedResourceMap = await this.shareOperation.Data.GetResourceMapAsync();
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("Failed GetResourceMapAsync - " + ex.Message, NotifyType.ErrorMessage);
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
                        NotifyUser("Failed GetBitmapAsync - " + ex.Message, NotifyType.ErrorMessage);
                    }
                }

                // In this sample, we just display the shared data content.

                // Get back to the UI thread using the dispatcher.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (this.sharedWebLink != null)
                    {
                        AddContentValue("", this.sharedWebLink.AbsoluteUri);
                    }
                });
            });
        }

        public void NotifyUser(string strMessage, NotifyType type)
        {
            StatusBlock.Text = strMessage;
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
        private async void openRemoteConnectionAsync(RemoteSystem remotesys)
        {
            if (remotesys != null)
            {
                // Create a remote system connection request.
                RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(remotesys);

                this.LoadingBar.IsEnabled = true;
                this.LoadingBar.Visibility = Visibility.Visible;
                NotifyUser("Sharing to " + remotesys.DisplayName + "...", NotifyType.StatusMessage);
                var status = await RemoteLauncher.LaunchUriAsync(connectionRequest, new Uri("share-app:?Text=" + this.ClipboardText.Text));
                NotifyUser(status.ToString(), NotifyType.StatusMessage);
                this.LoadingBar.IsEnabled = false;
                this.LoadingBar.Visibility = Visibility.Collapsed;
                this.shareOperation.DismissUI();
            }
            else
            {
                NotifyUser("Select a device for remote connection.", NotifyType.ErrorMessage);
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
                    this.shareOperation.DismissUI();
                }
            }
        }

        private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            RemoteSystem selectedDevice = this.DeviceListBox.SelectedItem as RemoteSystem;
            this.openRemoteConnectionAsync(selectedDevice);
        }
        private void ClipboardText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.ClipboardText.Text.Length > 0 && this.DeviceListBox.SelectedItem != null)
            {
                this.checkIfWebLink();
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
                this.checkIfWebLink();
                this.CopyToClipboardButton.IsEnabled = true;
            }
            else
            {
                this.LaunchInBrowserButton.IsEnabled = false;
                this.CopyToClipboardButton.IsEnabled = false;
            }
        }

        private void checkIfWebLink()
        {
            if (this.ClipboardText.Text.ToLower().StartsWith("http://") || this.ClipboardText.Text.ToLower().StartsWith("https://"))
            {
                this.LaunchInBrowserButton.IsEnabled = true;
                if (this.ClipboardText.Text.ToLower().StartsWith("https://www.youtube.com/watch?"))
                {
                    this.OpenInTubeCastButton.Visibility = Visibility.Visible;
                    this.OpenInTubeCastButton.IsEnabled = true;
                    this.OpenInMyTubeButton.Visibility = Visibility.Visible;
                    this.OpenInMyTubeButton.IsEnabled = true;
                }
                else
                {
                    this.OpenInTubeCastButton.IsEnabled = false;
                    this.OpenInMyTubeButton.IsEnabled = false;
                }
            }
            else
            {
                this.LaunchInBrowserButton.IsEnabled = false;
                this.OpenInTubeCastButton.IsEnabled = false;
                this.OpenInMyTubeButton.IsEnabled = false;
            }
        }

        private void LaunchInBrowserButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var button = sender as Button;
            this.animateButtonEnabled(button);
        }

        private void CopyToClipboardButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var button = sender as Button;
            this.animateButtonEnabled(button);
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

        private async void OpenInTubeCastButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = this.DeviceListBox.SelectedItem as RemoteSystem;

            if (selectedDevice != null)
            {
                Uri uri;
                if (Uri.TryCreate(this.convertYoutubeLinkToTubeCastUri(), UriKind.Absolute, out uri))
                {
                    this.LoadingBar.IsEnabled = true;
                    this.LoadingBar.Visibility = Visibility.Visible;
                    NotifyUser("Sharing to " + selectedDevice.DisplayName + "...", NotifyType.StatusMessage);
                    RemoteLaunchUriStatus launchUriStatus = await RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(selectedDevice), uri);
                    NotifyUser(launchUriStatus.ToString(), NotifyType.StatusMessage);
                    this.LoadingBar.IsEnabled = false;
                    this.LoadingBar.Visibility = Visibility.Collapsed;
                    this.shareOperation.DismissUI();
                }
            }
        }

        private async void OpenInMyTubeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = this.DeviceListBox.SelectedItem as RemoteSystem;

            if (selectedDevice != null)
            {
                Uri uri;
                if (Uri.TryCreate(this.convertYoutubeLinkToMyTubeUri(), UriKind.Absolute, out uri))
                {
                    this.LoadingBar.IsEnabled = true;
                    this.LoadingBar.Visibility = Visibility.Visible;
                    NotifyUser("Sharing to " + selectedDevice.DisplayName + "...", NotifyType.StatusMessage);
                    RemoteLaunchUriStatus launchUriStatus = await RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(selectedDevice), uri);
                    NotifyUser(launchUriStatus.ToString(), NotifyType.StatusMessage);
                    this.LoadingBar.IsEnabled = false;
                    this.LoadingBar.Visibility = Visibility.Collapsed;
                    this.shareOperation.DismissUI();
                }
            }
        }

        private string convertYoutubeLinkToMyTubeUri()
        {
            return "mytube:link=" + this.ClipboardText.Text;
        }

        private string convertYoutubeLinkToTubeCastUri()
        {
            var uri = new Uri(this.ClipboardText.Text);
            var queryStrings = new WwwFormUrlDecoder(uri.Query);
            string videoString = queryStrings.GetFirstValueByName("v");
            return "tubecast:VideoID=" + videoString;
        }

        private void OpenInTubeCastButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var button = sender as Button;
            this.animateLocalClipButton(button);
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

        private void OnBatchCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (!this.OpenInTubeCastButton.IsEnabled)
            {
                this.OpenInTubeCastButton.Visibility = Visibility.Collapsed;
            }
            if (!this.OpenInMyTubeButton.IsEnabled)
            {
                this.OpenInMyTubeButton.Visibility = Visibility.Collapsed;
            }
        }
    }
}