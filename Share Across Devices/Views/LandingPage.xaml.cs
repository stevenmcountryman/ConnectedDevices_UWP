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

namespace Share_Across_Devices.Views
{
    public sealed partial class LandingPage : Page
    {
        private RemoteSystemWatcher deviceWatcher;
        private Compositor _compositor;
        private RemoteDeviceObject selectedDevice;
        private bool sendOptionsHidden = true;
        private bool mediaGridHidden = true;
        private bool openInBrowser = false;
        private bool openInTubeCast = false;
        private bool openInMyTube = false;
        private bool transferFile = false;
        ObservableCollection<RemoteDeviceObject> DeviceList = new ObservableCollection<RemoteDeviceObject>();

        public LandingPage()
        {
            this.InitializeComponent();
            this.setUpDevicesList();
            this.setUpCompositor();
            this.DeviceList.CollectionChanged += DeviceList_CollectionChanged;
        }

        private void setUpCompositor()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var sendOptionsVisual = ElementCompositionPreview.GetElementVisual(this.SendOptionsPanel);
            sendOptionsVisual.Offset = new Vector3(0f, 100f, 0f);
            sendOptionsVisual.Opacity = 0f;
        }

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
            this.validateTextAndButtons();
        }

        private void SelectedDevice_NotifyEvent(object sender, MyEventArgs e)
        {
            var message = e.Message;
            this.Notification.Text = message;
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
        private void validateTextAndButtons()
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
            }
            else
            {
                this.AttachButton.IsEnabled = false;
                this.SendButton.IsEnabled = false;
                this.hideSendOptionsPanel();
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

        private void hideSendOptionsPanel()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.SendOptionsPanel);

            if (!this.sendOptionsHidden)
            {
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
                this.sendOptionsHidden = true;
            }
        }

        private void showSendOptionsPanel()
        {
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.SendOptionsPanel);

            if (this.sendOptionsHidden)
            {
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
                this.sendOptionsHidden = false;
            }
        }
        private void showMediaViewGrid()
        {
            if (this.MediaViewGrid.Children[0] != null)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(this.MediaViewGrid.Children[0] as MediaView);

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
        private void hideMediaViewGrid()
        {
            if (this.MediaViewGrid.Children[0] != null)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(this.MediaViewGrid.Children[0] as MediaView);

                Vector3KeyFrameAnimation offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 0f, 0f));
                offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 100f, 0f));

                ScalarKeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(1000);
                fadeAnimation.InsertKeyFrame(0f, 0f);
                fadeAnimation.InsertKeyFrame(1f, 1f);

                itemVisual.StartAnimation("Offset", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
        }

        private bool remoteSystemIsLocal()
        {
            return this.selectedDevice.RemoteSystem.IsAvailableByProximity;
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
                this.MessageToSend.IsEnabled = false;
                this.hideSendOptionsPanel();
                var mediaViewer = new MediaView(file);
                this.MediaViewGrid.Children.Clear();
                this.MediaViewGrid.Children.Add(mediaViewer);
                this.showMediaViewGrid();
            }
        }

        private void OpenInGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
}
