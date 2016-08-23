using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.System.RemoteSystems;
using Windows.UI.Core;
using Share_Across_Devices.Controls;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI.Composition;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.ViewManagement;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage;
using System.IO;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using System.Collections.Generic;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Documents;
using Windows.ApplicationModel.Core;

namespace Share_Across_Devices.Views
{
    public sealed partial class LandingPage : Page
    {
        private RemoteSystemWatcher deviceWatcher;
        private Compositor _compositor;
        private RemoteDeviceObject selectedDevice;
        private bool sendOptionsHidden = true;
        private bool notificationsHidden = true;
        private bool mediaViewGridHidden = true;
        private bool mediaRetrievalGridHidden = true;
        private bool openInBrowser = false;
        private bool openInTubeCast = false;
        private bool openInMyTube = false;
        private bool transferFile = false;
        private bool sharingInitiated = false;
        private string fileName;
        private string textToCopy;
        private StorageFile file;

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

        ObservableCollection<RemoteDeviceObject> DeviceList = new ObservableCollection<RemoteDeviceObject>();
        ObservableCollection<Options> OptionsList = new ObservableCollection<Options>();

        public LandingPage()
        {
            this.InitializeComponent();
            this.setUpDevicesList();
            this.setUpOptionsList();
            this.setUpCompositor();
            this.setTitleBar();
            this.DeviceList.CollectionChanged += DeviceList_CollectionChanged;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter.GetType() == typeof(ShareOperation))
            {
                this.sharingInitiated = true;
                this.handleShare(e);
            }
            else
            {
                var protocolArgs = e.Parameter as ProtocolActivatedEventArgs;

                if (protocolArgs != null)
                {
                    this.SelectedDeviceIcon.Glyph = "\uE119";
                    this.SelectedDeviceName.Text = "Receiving!";
                    this.resetView();
                    this.animateDeviceChosen();

                    var queryStrings = new WwwFormUrlDecoder(protocolArgs.Uri.Query);
                    if (!protocolArgs.Uri.Query.StartsWith("?FileName="))
                    {
                        this.textToCopy = queryStrings.GetFirstValueByName("Text");
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
                                this.NotificationText.Text = "Copied!";
                                this.SelectedDeviceIcon.Glyph = "\uE166";
                                this.SelectedDeviceName.Text = "Received!";
                                this.animateShowNotification();
                            }
                        }
                        catch
                        {
                            this.NotificationText.Text = "Manual copy required, tap here to copy";
                            this.animateShowNotification();
                            this.NotificationText.Tapped += NotificationText_Tapped;
                        }

                    }
                    else
                    {
                        this.fileName = queryStrings.GetFirstValueByName("FileName");
                        this.beginListeningForFile();
                    }
                }
            }
        }

        #region Share Target
        private async void handleShare(NavigationEventArgs e)
        {
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
                        this.NotificationText.Text = "Failed GetWebLinkAsync - " + ex.Message;
                        this.animateShowNotification();
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
                        this.NotificationText.Text = "Failed GetApplicationLinkAsync - " + ex.Message;
                        this.animateShowNotification();
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
                        this.NotificationText.Text = "Failed GetTextAsync - " + ex.Message;
                        this.animateShowNotification();
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
                        this.NotificationText.Text = "Failed GetStorageItemsAsync - " + ex.Message;
                        this.animateShowNotification();
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
                        this.NotificationText.Text = "Failed GetTextAsync(" + dataFormatName + ") - " + ex.Message;
                        this.animateShowNotification();
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
                        this.NotificationText.Text = "Failed GetHtmlFormatAsync - " + ex.Message;
                        this.animateShowNotification();
                    }

                    try
                    {
                        this.sharedResourceMap = await this.shareOperation.Data.GetResourceMapAsync();
                    }
                    catch (Exception ex)
                    {
                        this.NotificationText.Text = "Failed GetResourceMapAsync - " + ex.Message;
                        this.animateShowNotification();
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
                        this.NotificationText.Text = "Failed GetBitmapAsync - " + ex.Message;
                        this.animateShowNotification();
                    }
                }

                // In this sample, we just display the shared data content.

                // Get back to the UI thread using the dispatcher.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    if (this.sharedWebLink != null)
                    {
                        this.MessageToSend.Text = this.sharedWebLink.AbsoluteUri;
                    }
                    if (this.sharedStorageItems != null && this.sharedStorageItems.Count == 1)
                    {
                        this.file = await StorageFile.GetFileFromPathAsync(this.sharedStorageItems[0].Path);
                        this.MessageToSend.Text = "attached file";
                        MediaView mediaView = new MediaView(file);
                        this.MediaSendViewGrid.Children.Clear();
                        this.MediaSendViewGrid.Children.Add(mediaView);
                        this.showMediaViewGrid();
                        this.transferFile = true;
                    }
                });
            });
        }
        #endregion

        #region File retrieval 
        private async void beginListeningForFile()
        {
            this.NotificationText.Text = "Receiving file...";
            this.animateShowNotification();
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
                this.NotificationText.Text = "Failed to receive file";
                this.animateShowNotification();
            }
        }
        private async void SocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            sender.ConnectionReceived -= SocketListener_ConnectionReceived;
            if (fileName != null)
            {
                try
                {
                    file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    //Read line from the remote client.
                    using (var fileStream = await file.OpenStreamForWriteAsync())
                    {
                        using (var inStream = args.Socket.InputStream.AsStreamForRead())
                        {
                            byte[] bytes;
                            DataReader dataReader = new DataReader(inStream.AsInputStream());
                            fileStream.Seek(0, SeekOrigin.Begin);
                            while (inStream.CanRead)
                            {
                                await dataReader.LoadAsync(sizeof(bool));
                                if (dataReader.ReadBoolean() == false)
                                {
                                    break;
                                }
                                await dataReader.LoadAsync(sizeof(Int32));
                                var byteSize = dataReader.ReadInt32();
                                bytes = new byte[byteSize];
                                await dataReader.LoadAsync(sizeof(Int32));
                                var percentComplete = dataReader.ReadInt32();
                                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    this.NotificationText.Text = percentComplete + "% transferred";
                                    this.animateShowNotification();
                                });
                                await dataReader.LoadAsync((uint)byteSize);
                                dataReader.ReadBytes(bytes);
                                await fileStream.WriteAsync(bytes, 0, byteSize);
                            }
                        }
                    }

                    //Send the line back to the remote client.
                    Stream outStream = args.Socket.OutputStream.AsStreamForWrite();
                    StreamWriter writer = new StreamWriter(outStream);
                    await writer.WriteLineAsync("File Received!");
                    await writer.FlushAsync();

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        this.SelectedDeviceIcon.Glyph = "\uE166";
                        this.SelectedDeviceName.Text = "Received!";
                        this.NotificationText.Text = "File received";
                        this.animateShowNotification();
                        TransferView transferView = new TransferView(this.file);
                        this.MediaRetrieveViewGrid.Children.Clear();
                        this.MediaRetrieveViewGrid.Children.Add(transferView);
                        this.showMediaRetrieveViewGrid();
                        transferView.CancelEvent += TransferView_CancelEvent;
                        transferView.SaveEvent += TransferView_SaveEvent;
                    });
                }
                catch
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        this.NotificationText.Text = "Transfer interrupted";
                        this.animateShowNotification();
                    });
                }
            }
        }
        #endregion

        #region Beauty and animations
        private void setTitleBar()
        {
            CoreApplicationView coreView = CoreApplication.GetCurrentView();
            CoreApplicationViewTitleBar coreTitleBar = coreView.TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            var appBlue = Color.FromArgb(255, 56, 118, 191);
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                ApplicationView AppView = ApplicationView.GetForCurrentView();
                AppView.TitleBar.BackgroundColor = appBlue;
                AppView.TitleBar.ButtonInactiveBackgroundColor = appBlue;
                AppView.TitleBar.ButtonInactiveForegroundColor = Colors.White;
                AppView.TitleBar.ButtonBackgroundColor = appBlue;
                AppView.TitleBar.ButtonForegroundColor = Colors.White;
                AppView.TitleBar.ButtonHoverBackgroundColor = appBlue;
                AppView.TitleBar.ButtonHoverForegroundColor = Colors.White;
                AppView.TitleBar.ButtonPressedBackgroundColor = appBlue;
                AppView.TitleBar.ButtonPressedForegroundColor = Colors.White;
                AppView.TitleBar.ForegroundColor = Colors.White;
                AppView.TitleBar.InactiveBackgroundColor = appBlue;
                AppView.TitleBar.InactiveForegroundColor = Colors.White;
            }
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundOpacity = 1;
                statusBar.BackgroundColor = appBlue;
                statusBar.ForegroundColor = Colors.White;
            }

            var _offSet = 0;

            InputPane.GetForCurrentView().Showing += (s, args) =>
            {
                _offSet = (int)args.OccludedRect.Height;
                args.EnsuredFocusedElementInView = true;
                var trans = new TranslateTransform();
                trans.Y = -_offSet;
                this.RenderTransform = trans;
            };

            InputPane.GetForCurrentView().Hiding += (s, args) =>
            {
                var trans = new TranslateTransform();
                trans.Y = 0;
                this.RenderTransform = trans;
                args.EnsuredFocusedElementInView = false;
            };
        }
        private void setUpCompositor()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var sendOptionsVisual = ElementCompositionPreview.GetElementVisual(this.SendOptionsPanel);
            sendOptionsVisual.Opacity = 0f;
            var devicePanelVisual = ElementCompositionPreview.GetElementVisual(this.DevicePanel);
            devicePanelVisual.Opacity = 0f;
            var notificationVisual = ElementCompositionPreview.GetElementVisual(this.NotificationPanel);
            notificationVisual.Opacity = 0f;
        }
        private void animateDeviceChosen()
        {
            var devicePanelVisual = ElementCompositionPreview.GetElementVisual(this.DevicePanel);

            Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, -100f, 0f));
            offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f));

            ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            fadeAnimation.InsertKeyFrame(0f, 0f);
            fadeAnimation.InsertKeyFrame(1f, 1f);

            devicePanelVisual.StartAnimation("Offset", offsetAnimation);
            devicePanelVisual.StartAnimation("Opacity", fadeAnimation);
        }
        private void animateHideNotification()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.NotificationPanel);

            if (!this.notificationsHidden)
            {
                this.notificationsHidden = true;
                Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 0f, 0f));
                offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, -100f, 0f));

                ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                fadeAnimation.InsertKeyFrame(0f, 1f);
                fadeAnimation.InsertKeyFrame(1f, 0f);

                itemVisual.StartAnimation("Offset", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
        }
        private void animateShowNotification()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.NotificationPanel);

            if (this.notificationsHidden)
            {
                this.notificationsHidden = false;
                Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, -100f, 0f));
                offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f));

                ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                fadeAnimation.InsertKeyFrame(0f, 0f);
                fadeAnimation.InsertKeyFrame(1f, 1f);

                itemVisual.StartAnimation("Offset", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
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
        private void hideSendOptionsPanel()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.SendOptionsPanel);

            if (!this.sendOptionsHidden)
            {
                this.sendOptionsHidden = true;
                Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 0f, 0f));
                offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 100f, 0f));

                ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                fadeAnimation.InsertKeyFrame(0f, 1f);
                fadeAnimation.InsertKeyFrame(1f, 0f);

                itemVisual.StartAnimation("Offset", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
            this.openInBrowser = false;
            this.openInMyTube = false;
            this.openInTubeCast = false;
        }

        private void showSendOptionsPanel()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.SendOptionsPanel);

            if (this.sendOptionsHidden)
            {
                this.sendOptionsHidden = false;
                Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 100f, 0f));
                offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f));

                ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                fadeAnimation.InsertKeyFrame(0f, 0f);
                fadeAnimation.InsertKeyFrame(1f, 1f);

                itemVisual.StartAnimation("Offset", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
        }
        private void showMediaViewGrid()
        {
            if (this.MediaSendViewGrid.Children[0] != null)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(this.MediaSendViewGrid.Children[0]);

                if (this.mediaViewGridHidden)
                {
                    this.mediaViewGridHidden = false;
                    Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                    offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 300f, 0f));
                    offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f));

                    ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    fadeAnimation.InsertKeyFrame(0f, 0f);
                    fadeAnimation.InsertKeyFrame(1f, 1f);

                    itemVisual.StartAnimation("Offset", offsetAnimation);
                    itemVisual.StartAnimation("Opacity", fadeAnimation);
                }
            }
        }
        private void showMediaRetrieveViewGrid()
        {
            if (this.MediaRetrieveViewGrid.Children[0] != null)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(this.MediaRetrieveViewGrid.Children[0]);

                if (this.mediaRetrievalGridHidden)
                {
                    this.mediaRetrievalGridHidden = false;
                    Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                    offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, -300f, 0f));
                    offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f));

                    ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    fadeAnimation.InsertKeyFrame(0f, 0f);
                    fadeAnimation.InsertKeyFrame(1f, 1f);

                    itemVisual.StartAnimation("Offset", offsetAnimation);
                    itemVisual.StartAnimation("Opacity", fadeAnimation);
                }
            }
        }
        private async void hideMediaViewGrid()
        {
            if (this.MediaSendViewGrid.Children[0] != null)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(this.MediaSendViewGrid.Children[0]);

                if (!this.mediaViewGridHidden)
                {
                    this.mediaViewGridHidden = true;
                    Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                    offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 0f, 0f));
                    offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 300f, 0f));

                    ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    fadeAnimation.InsertKeyFrame(0f, 1f);
                    fadeAnimation.InsertKeyFrame(1f, 0f);

                    itemVisual.StartAnimation("Offset", offsetAnimation);
                    itemVisual.StartAnimation("Opacity", fadeAnimation);
                    await Task.Delay(1000);
                    this.MediaSendViewGrid.Children.Clear();
                }
            }
        }
        private async void hideMediaRetrieveViewGrid()
        {
            if (this.MediaRetrieveViewGrid.Children[0] != null)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(this.MediaRetrieveViewGrid.Children[0]);

                if (!this.mediaRetrievalGridHidden)
                {
                    this.mediaRetrievalGridHidden = true;
                    Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                    offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 0f, 0f));
                    offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, -300f, 0f));

                    ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    fadeAnimation.InsertKeyFrame(0f, 1f);
                    fadeAnimation.InsertKeyFrame(1f, 0f);

                    itemVisual.StartAnimation("Offset", offsetAnimation);
                    itemVisual.StartAnimation("Opacity", fadeAnimation);

                    await Task.Delay(1000);
                    this.MediaRetrieveViewGrid.Children.Clear();
                    this.resetView();
                }
            }
        }
        private async void animateTutorialGridClosing()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(TutorialGrid);

            ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            fadeAnimation.InsertKeyFrame(0f, 1f);
            fadeAnimation.InsertKeyFrame(1f, 0f);

            itemVisual.StartAnimation("Opacity", fadeAnimation);

            await Task.Delay(1000);
            this.TutorialGrid.Visibility = Visibility.Collapsed;

            var rectVisual = ElementCompositionPreview.GetElementVisual(SelectionRectangle);
            rectVisual.StopAnimation("Opacity");
        }
        private void animateTutorialGridOpening()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(TutorialGrid);

            ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            fadeAnimation.InsertKeyFrame(0f, 0f);
            fadeAnimation.InsertKeyFrame(1f, 1f);

            itemVisual.StartAnimation("Opacity", fadeAnimation);

            var rectVisual = ElementCompositionPreview.GetElementVisual(SelectionRectangle);
            ScalarKeyFrameAnimation rectFadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            rectFadeAnimation.Duration = TimeSpan.FromMilliseconds(2000);
            rectFadeAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
            rectFadeAnimation.InsertKeyFrame(0f, 0f);
            rectFadeAnimation.InsertKeyFrame(0.5f, 1f);
            rectFadeAnimation.InsertKeyFrame(1f, 0f);
            rectVisual.StartAnimation("Opacity", rectFadeAnimation);
        }
        #endregion

        #region Remote system methods
        private void DeviceList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.HamburgerMenu.ItemsSource = DeviceList;
        }
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
                RemoteDeviceObject device = new RemoteDeviceObject(remoteSystem);
                this.DeviceList.Add(device);
            });
        }
        private async void DeviceWatcher_RemoteSystemUpdated(RemoteSystemWatcher sender, RemoteSystemUpdatedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (RemoteDeviceObject device in this.DeviceList)
                {
                    if (device.RemoteSystem.Id == args.RemoteSystem.Id)
                    {
                        device.RemoteSystem = args.RemoteSystem;
                        if (this.selectedDevice != null && this.selectedDevice.RemoteSystem.Id == args.RemoteSystem.Id)
                        {
                            this.selectedDevice.RemoteSystem = args.RemoteSystem;
                        }
                        this.validateTextAndButtons();
                        return;
                    }
                }
            });
        }
        private async void DeviceWatcher_RemoteSystemRemoved(RemoteSystemWatcher sender, RemoteSystemRemovedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (RemoteDeviceObject device in this.DeviceList)
                {
                    if (device.RemoteSystem.Id == args.RemoteSystemId)
                    {
                        this.DeviceList.Remove(device);
                        if (this.selectedDevice != null && this.selectedDevice.RemoteSystem.Id == args.RemoteSystemId)
                        {
                            this.selectedDevice = null;
                        }
                        this.validateTextAndButtons();
                        return;
                    }
                }
            });
        }
        #endregion     

        #region UI events
        private void TransferView_SaveEvent(object sender, EventArgs e)
        {
            this.hideMediaRetrieveViewGrid();
            this.NotificationText.Text = "File saved!";
            this.animateShowNotificationTimed();
        }
        private void CloseTutorialButton_Click(object sender, RoutedEventArgs e)
        {
            this.animateTutorialGridClosing();
        }
        private void HamburgerMenu_OptionsItemClick(object sender, ItemClickEventArgs e)
        {
            this.TutorialGrid.Visibility = Visibility.Visible;
            this.animateTutorialGridOpening();
        }
        private void NotificationText_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            DataPackage package = new DataPackage()
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            package.SetText(this.textToCopy);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            this.NotificationText.Tapped -= NotificationText_Tapped;
            this.NotificationText.Text = "Copied";
            this.animateShowNotificationTimed();
        }
        private void TransferView_CancelEvent(object sender, EventArgs e)
        {
            this.hideMediaRetrieveViewGrid();
        }
        private void MediaViewer_CancelEvent(object sender, EventArgs e)
        {
            if (this.sharingInitiated)
            {
                this.shareOperation.DismissUI();
            }
            else
            {
                this.resetView();
            }
        }

        private void MessageToSend_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.validateTextAndButtons();
        }
        private void Button_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var button = sender as Button;
            this.animateButtonEnabled(button);
        }
        private void HamburgerMenu_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedItem = e.ClickedItem as RemoteDeviceObject;
            this.selectedDevice = clickedItem;
            this.selectedDevice.NotifyEvent += SelectedDevice_NotifyEvent;
            this.SelectedDeviceIcon.Glyph = clickedItem.DeviceIcon;
            this.SelectedDeviceName.Text = clickedItem.DeviceName;
            if (this.VisualStateGroup.CurrentState == this.VisualStatePhone)
            {
                this.HamburgerMenu.IsPaneOpen = false;
            }
            if (this.file != null)
            {
                this.selectedDevice.SetFileToSend(this.file);
            }
            this.resetView();
            this.MessageToSend.IsEnabled = true;
            this.animateDeviceChosen();
            this.validateTextAndButtons();
        }
        private void SelectedDevice_NotifyEvent(object sender, MyEventArgs e)
        {
            var message = e.Message;
            this.NotificationText.Text = message;
            if (e.MessageType == MyEventArgs.messageType.Indefinite)
            {
                this.animateShowNotification();
            }
            else
            {
                this.animateShowNotificationTimed();
            }
        }
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.selectedDevice != null)
            {
                if (this.openInBrowser)
                {
                    this.selectedDevice.OpenLinkInBrowser(this.MessageToSend.Text);
                }
                else if (this.openInMyTube)
                {
                    this.selectedDevice.OpenLinkInMyTube(this.MessageToSend.Text);
                }
                else if (this.openInTubeCast)
                {
                    this.selectedDevice.OpenLinkInTubeCast(this.MessageToSend.Text);
                }
                else if (this.transferFile)
                {
                    this.selectedDevice.SendFile();
                }
                else
                {
                    this.selectedDevice.ShareMessage(this.MessageToSend.Text);
                }
            }
        }
        private async void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            var file = await this.selectedDevice.OpenFileToSend();
            if (file != null)
            {
                this.transferFile = true;
                this.openInBrowser = false;
                this.openInMyTube = false;
                this.openInTubeCast = false;
                this.SendButton.IsEnabled = true;
                this.hideSendOptionsPanel();
                var mediaViewer = new MediaView(file);
                mediaViewer.CancelEvent += MediaViewer_CancelEvent;
                this.MediaSendViewGrid.Children.Clear();
                this.MediaSendViewGrid.Children.Add(mediaViewer);
                this.showMediaViewGrid();
                this.MessageToSend.Text = "attached file";
            }
        }
        private void OpenInGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.OpenInGridView.SelectedIndex >= 0)
            {
                if (e.AddedItems[0] == this.OpenInBrowserButton)
                {
                    this.openInBrowser = true;
                    this.openInMyTube = false;
                    this.openInTubeCast = false;
                }
                else if (e.AddedItems[0] == this.OpenInMyTubeButton)
                {
                    this.openInMyTube = true;
                    this.openInBrowser = false;
                    this.openInTubeCast = false;
                }
                else if (e.AddedItems[0] == this.OpenInTubeCastButton)
                {
                    this.openInTubeCast = true;
                    this.openInBrowser = false;
                    this.openInMyTube = false;
                }
            }
        }
        private void OpenInButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender == this.OpenInBrowserButton)
            {
                if (this.openInBrowser != true)
                {
                    this.openInBrowser = true;
                    this.OpenInBrowserButton.BorderBrush = new SolidColorBrush(Colors.White);
                }
                else
                {
                    this.openInBrowser = false;
                    this.OpenInBrowserButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                }
                this.openInMyTube = false;
                this.openInTubeCast = false;
                this.OpenInMyTubeButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                this.OpenInTubeCastButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }
            else if (sender == this.OpenInMyTubeButton)
            {
                if (this.openInMyTube != true)
                {
                    this.openInMyTube = true;
                    this.OpenInMyTubeButton.BorderBrush = new SolidColorBrush(Colors.White);
                }
                else
                {
                    this.openInMyTube = false;
                    this.OpenInMyTubeButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                }
                this.openInBrowser = false;
                this.openInTubeCast = false;
                this.OpenInBrowserButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                this.OpenInTubeCastButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }
            else if (sender == this.OpenInTubeCastButton)
            {
                if (this.openInTubeCast != true)
                {
                    this.openInTubeCast = true;
                    this.OpenInTubeCastButton.BorderBrush = new SolidColorBrush(Colors.White);
                }
                else
                {
                    this.openInTubeCast = false;
                    this.OpenInTubeCastButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                }
                this.openInBrowser = false;
                this.openInMyTube = false;
                this.OpenInBrowserButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                this.OpenInMyTubeButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }
        }
        #endregion

        #region Helpers
        private async void animateShowNotificationTimed()
        {
            this.animateShowNotification();
            await Task.Delay(2000);
            this.animateHideNotification();
            await Task.Delay(1000);
            this.resetView();
            this.validateTextAndButtons();
            if (this.sharingInitiated)
            {
                this.shareOperation.DismissUI();
            }
        }

        private void setUpOptionsList()
        {
            Options helpOption = new Options("\uE11B", "Where are my devices?");
            this.OptionsList.Add(helpOption);
            this.HamburgerMenu.OptionsItemsSource = this.OptionsList;
        }
        private void validateTextAndButtons()
        {
            if (this.sharingInitiated)
            {
                this.MessageToSend.IsEnabled = false;
                this.AttachButton.IsEnabled = false;
                if (this.selectedDevice != null)
                {
                    if (this.remoteSystemIsLocal() && this.transferFile)
                    {
                        this.SendButton.IsEnabled = true;
                        return;
                    }
                    else if (this.transferFile)
                    {
                        this.SendButton.IsEnabled = false;
                        return;
                    }

                    if (this.MessageToSend.Text.Length > 0)
                    {
                        this.SendButton.IsEnabled = true;
                        this.checkIfWebLink();
                    }
                    else
                    {
                        this.SendButton.IsEnabled = false;
                        this.hideSendOptionsPanel();
                    }
                }
                else
                {
                    this.SendButton.IsEnabled = false;
                    this.hideSendOptionsPanel();
                }
            }
            else
            {
                if (this.selectedDevice != null)
                {
                    if (this.remoteSystemIsLocal())
                    {
                        this.AttachButton.IsEnabled = true;
                    }
                    else
                    {
                        this.AttachButton.IsEnabled = false;
                    }

                    if (this.MessageToSend.Text.Length > 0)
                    {
                        this.SendButton.IsEnabled = true;
                        this.checkIfWebLink();
                    }
                    else
                    {
                        this.SendButton.IsEnabled = false;
                        this.hideSendOptionsPanel();
                    }

                    if (this.transferFile)
                    {
                        this.MessageToSend.IsEnabled = false;
                    }
                    else
                    {
                        this.MessageToSend.IsEnabled = true;
                    }
                }
                else
                {
                    this.AttachButton.IsEnabled = false;
                    this.SendButton.IsEnabled = false;
                    this.hideSendOptionsPanel();
                    this.MessageToSend.IsEnabled = false;
                }
            }
        }
        private void checkIfWebLink()
        {
            if (this.MessageToSend.Text.ToLower().StartsWith("http://") || this.MessageToSend.Text.ToLower().StartsWith("https://"))
            {
                this.showSendOptionsPanel();
                this.OpenInBrowserButton.IsEnabled = true;
                if (this.MessageToSend.Text.ToLower().Contains("youtube.com/watch?"))
                {
                    this.OpenInMyTubeButton.IsEnabled = true;
                    this.OpenInTubeCastButton.IsEnabled = true;
                }
                else
                {
                    this.OpenInMyTubeButton.IsEnabled = false;
                    this.OpenInTubeCastButton.IsEnabled = false;
                }
            }
            else
            {
                this.OpenInBrowserButton.IsEnabled = false;
                this.OpenInMyTubeButton.IsEnabled = false;
                this.OpenInTubeCastButton.IsEnabled = false;
                this.hideSendOptionsPanel();
            }
        }    
        private bool remoteSystemIsLocal()
        {
            return this.selectedDevice.RemoteSystem.IsAvailableByProximity;
        }
        private void resetView()
        {
            this.MessageToSend.IsEnabled = false;
            if (!this.sharingInitiated)
            {
                this.MessageToSend.Text = "";
                this.openInBrowser = false;
                this.OpenInBrowserButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                this.openInMyTube = false;
                this.OpenInMyTubeButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                this.openInTubeCast = false;
                this.OpenInTubeCastButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                this.transferFile = false;
                this.hideMediaViewGrid();
                this.hideMediaRetrieveViewGrid();
                var notificationVisual = ElementCompositionPreview.GetElementVisual(this.NotificationPanel);
                notificationVisual.Opacity = 0f;
            }
        }
        #endregion
    }
}
