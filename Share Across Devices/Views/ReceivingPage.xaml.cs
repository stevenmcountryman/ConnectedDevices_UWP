using Share_Across_Devices.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Share_Across_Devices.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ReceivingPage : Page
    {
        private Compositor _compositor;
        SpriteVisual _hostSprite;
        private bool notificationsHidden = true;
        private bool mediaRetrievalGridHidden = true;
        private string fileName;
        private string ipAddress;
        private string textToCopy;
        private StorageFile file;

        public ReceivingPage()
        {
            this.InitializeComponent();

            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var notificationVisual = ElementCompositionPreview.GetElementVisual(this.NotificationPanel);
            notificationVisual.Opacity = 0f;
            this.applyAcrylicAccent(this.Blur);
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var parameter = e.Parameter;

            if (parameter != null)
            {
                String[] message = parameter as String[];
                if (message.Count() == 1)
                {
                    this.textToCopy = message[0];
                    this.copyText();
                }
                else if (message.Count() == 2)
                {
                    this.fileName = message[0];
                    this.ipAddress = message[1];
                    this.beginListeningForFile();
                }
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_hostSprite != null)
                _hostSprite.Size = e.NewSize.ToVector2();
        }

        private async void returnToLandingPage()
        {
            await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
            this.Frame.Navigate(typeof(LandingPage), null);
        }

        private async void copyText()
        {
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
                    await this.animateShowNotificationTimed();
                    this.returnToLandingPage();
                }
            }
            catch
            {
                this.NotificationText.Text = "Manual copy required, tap here to copy";
                this.animateShowNotification();
                this.NotificationText.Tapped += NotificationText_Tapped;
            }
        }
        private async void NotificationPanel_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (this.NotificationText.Text == "File sharing unlikely - tap for more info")
            {
                MessageDialog helpDialogue = new MessageDialog("Due to firewalls and other network restrictions, it is likely that a file transfer will not succeed between these two devices. It is recommended to only attempt file transfers between devices on the same local wireless or wired networks.\n\nYou can still try to send files if you'd like, but there is no guarantee that it will succeed.", "Why can't I send files?");
                helpDialogue.Commands.Add(new UICommand("close"));
                helpDialogue.CancelCommandIndex = 0;
                helpDialogue.DefaultCommandIndex = 0;
                await helpDialogue.ShowAsync();
            }
        }
        private async void NotificationText_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
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
            await this.animateShowNotificationTimed();
            this.returnToLandingPage();
        }

        private async void beginListeningForFile()
        {
            try
            {
                using (var socket = new StreamSocket())
                {

                    //The server hostname that we will be establishing a connection to. We will be running the server and client locally,
                    //so we will use localhost as the hostname.
                    HostName serverHost = new HostName(this.ipAddress);
                    this.NotificationText.Text = "Opening connection....";
                    this.animateShowNotification();
                    await socket.ConnectAsync(serverHost, "1717");

                    if (fileName != null)
                    {
                        file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                        this.NotificationText.Text = "Receiving file...";
                        this.animateShowNotification();

                        using (var fileStream = await file.OpenStreamForWriteAsync())
                        {
                            using (var inStream = socket.InputStream.AsStreamForRead())
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
                                    await CoreApplication.GetCurrentView().CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                    }
                }

                await CoreApplication.GetCurrentView().CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.NotificationText.Text = "File received";
                    this.animateShowNotificationTimed();

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
                this.NotificationText.Text = "Something is blocking me :(";
                this.animateShowNotification();
            }
        }

        private void applyAcrylicAccent(Panel e)
        {
            _hostSprite = _compositor.CreateSpriteVisual();
            _hostSprite.Size = new Vector2((float)e.ActualWidth, (float)e.ActualHeight);

            ElementCompositionPreview.SetElementChildVisual(e, _hostSprite);
            _hostSprite.Brush = _compositor.CreateHostBackdropBrush();
        }
        private async void TransferView_CancelEvent(object sender, EventArgs e)
        {
            await this.hideMediaRetrieveViewGrid();
            this.returnToLandingPage();
        }
        private async void TransferView_SaveEvent(object sender, EventArgs e)
        {
            await this.hideMediaRetrieveViewGrid();
            this.NotificationText.Text = "File saved!";
            await this.animateShowNotificationTimed();
            this.returnToLandingPage();
        }
        private async Task animateShowNotificationTimed()
        {
            this.animateShowNotification();
            await Task.Delay(2000);
            this.animateHideNotification();
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
                offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 100f, 0f));

                ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                fadeAnimation.InsertKeyFrame(0f, 1f);
                fadeAnimation.InsertKeyFrame(1f, 0f);

                itemVisual.StartAnimation("Offset", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
        }
        private async Task hideMediaRetrieveViewGrid()
        {
            if (this.MediaRetrieveViewGrid.Children[0] != null)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(this.MediaRetrieveViewGrid.Children[0]);
                var x = (float)this.MediaRetrieveViewGrid.ActualWidth / 2;
                var y = (float)this.MediaRetrieveViewGrid.ActualHeight / 2;
                itemVisual.CenterPoint = new Vector3(x, y, 0f);

                if (!this.mediaRetrievalGridHidden)
                {
                    this.mediaRetrievalGridHidden = true;
                    ScalarKeyFrameAnimation scaleAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    scaleAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    scaleAnimation.InsertKeyFrame(0f, 1f);
                    scaleAnimation.InsertKeyFrame(1f, 0.8f);

                    ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    fadeAnimation.InsertKeyFrame(0f, 1f);
                    fadeAnimation.InsertKeyFrame(1f, 0f);

                    itemVisual.StartAnimation("Scale.X", scaleAnimation);
                    itemVisual.StartAnimation("Scale.Y", scaleAnimation);
                    itemVisual.StartAnimation("Opacity", fadeAnimation);

                    await Task.Delay(1000);
                    this.MediaRetrieveViewGrid.Children.Clear();
                }
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
                offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 100f, 0f));
                offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f));

                ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                fadeAnimation.InsertKeyFrame(0f, 0f);
                fadeAnimation.InsertKeyFrame(1f, 1f);

                itemVisual.StartAnimation("Offset", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
            else
            {
                itemVisual.Opacity = 1f;
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
                    ScalarKeyFrameAnimation scaleAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    scaleAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    scaleAnimation.InsertKeyFrame(0f, 0.8f);
                    scaleAnimation.InsertKeyFrame(1f, 1f);

                    ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                    fadeAnimation.InsertKeyFrame(0f, 0f);
                    fadeAnimation.InsertKeyFrame(1f, 1f);

                    itemVisual.StartAnimation("Scale.X", scaleAnimation);
                    itemVisual.StartAnimation("Scale.Y", scaleAnimation);
                    itemVisual.StartAnimation("Opacity", fadeAnimation);
                }
            }
        }
    }
}
