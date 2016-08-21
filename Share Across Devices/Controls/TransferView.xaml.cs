using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
using System.Numerics;

namespace Share_Across_Devices.Controls
{
    public sealed partial class TransferView : UserControl
    {
        private StorageFile file;

        public delegate void CancelHandler(object sender, EventArgs e);
        public event CancelHandler CancelEvent;
        public delegate void SavedHandler(object sender, EventArgs e);
        public event SavedHandler SaveEvent;

        private List<string> imageTypes = new List<string>()
        {
            ".tif",
            ".tiff",
            ".gif",
            ".jpeg",
            ".jpg",
            ".png",
            ".bmp",
            ".ico"
        };
        private List<string> mediaTypes = new List<string>()
        {
            ".mp4",
            ".wmv",
            ".wma",
            ".mp3",
            ".m4a"
        };
        private bool mediaPaused = true;
        private bool pointerEntered = false;
        private string pauseIcon = "\uE103";
        private string playIcon = "\uE102";
        private Compositor _compositor;

        public TransferView()
        {
            this.InitializeComponent();
            this.setUpCompositorStuff();
        }

        private void setUpCompositorStuff()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.PlayPauseGrid);
            itemVisual.Opacity = 0;
        }

        public TransferView(StorageFile file) : this()
        {
            this.file = file;
            this.DisplayFile(file);
        }

        public void DisplayFile(StorageFile file)
        {
            this.file = file;
            this.FileNameBlock.Text = this.file.Name;

            if (this.imageTypes.Contains(this.file.FileType))
            {
                this.ImageFileViewer.Source = new BitmapImage(new Uri(file.Path));
            }
            else if (this.mediaTypes.Contains(this.file.FileType))
            {
                this.setThumbnail();
                this.VideoFileViewer.Visibility = Visibility.Visible;
                this.VideoFileViewer.Source = new Uri(this.file.Path);
                this.PlayPausePanel.Tapped += PlayPausePanel_Tapped;
                this.PlayPausePanel.PointerEntered += PlayPausePanel_PointerEntered;
                this.PlayPausePanel.PointerExited += PlayPausePanel_PointerExited;
            }
            else
            {
                this.setThumbnail();
            }
        }

        private void PlayPausePanel_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.pointerEntered = false;
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.PlayPauseGrid);

            ScalarKeyFrameAnimation opacityAnimation = this._compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            opacityAnimation.InsertKeyFrame(0f, 1f);
            opacityAnimation.InsertKeyFrame(1f, 0f);

            itemVisual.StartAnimation("Opacity", opacityAnimation);
        }

        private void PlayPausePanel_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.pointerEntered = true;
            if (mediaPaused)
            {
                this.PlayPauseButton.Text = this.playIcon;
            }
            else
            {
                this.PlayPauseButton.Text = this.pauseIcon;
            }

            var itemVisual = ElementCompositionPreview.GetElementVisual(this.PlayPauseGrid);

            ScalarKeyFrameAnimation opacityAnimation = this._compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            opacityAnimation.InsertKeyFrame(0f, 0f);
            opacityAnimation.InsertKeyFrame(1f, 1f);

            itemVisual.StartAnimation("Opacity", opacityAnimation);
        }

        private void PlayPausePanel_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            this.togglePlayPause();
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.PlayPauseGrid);
            float width = (float)this.PlayPauseGrid.RenderSize.Width;
            float height = (float)this.PlayPauseGrid.RenderSize.Height;
            itemVisual.CenterPoint = new Vector3(width / 2, height / 2, 0f);

            ScalarKeyFrameAnimation opacityAnimation = this._compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(500);
            opacityAnimation.InsertKeyFrame(0f, 1f);
            if (!this.pointerEntered)
            {
                opacityAnimation.InsertKeyFrame(1f, 0f);
            }

            Vector3KeyFrameAnimation scaleAnimation = this._compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);
            scaleAnimation.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));
            scaleAnimation.InsertKeyFrame(0.2f, new Vector3(1.1f, 1.1f, 1.1f));
            scaleAnimation.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f));

            itemVisual.StartAnimation("Opacity", opacityAnimation);
            itemVisual.StartAnimation("Scale", scaleAnimation);
        }

        private async void setThumbnail()
        {
            try
            {
                var thumb = await this.file.GetThumbnailAsync(ThumbnailMode.DocumentsView, (uint)300, ThumbnailOptions.ResizeThumbnail);
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(thumb);
                this.ImageFileViewer.Source = bitmap;
            }
            catch
            {
            }
        }

        private void SaveFileButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.saveFile();
        }

        private async void saveFile()
        {
            var savePicker = new FolderPicker();
            savePicker.FileTypeFilter.Add("*");
            savePicker.ViewMode = PickerViewMode.List;
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            StorageFolder folder = await savePicker.PickSingleFolderAsync();
            if (folder != null)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("FileToSave", folder);
                await this.file.CopyAsync(folder, this.file.Name, NameCollisionOption.ReplaceExisting);
                this.SaveEvent(this, new EventArgs());
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.close();
        }

        private void close()
        {
            this.VideoFileViewer.Stop();
            this.mediaPaused = true;
            this.CancelEvent(this, new EventArgs());
            this.PlayPausePanel.Tapped -= PlayPausePanel_Tapped;
            this.PlayPausePanel.PointerEntered -= PlayPausePanel_PointerEntered;
            this.PlayPausePanel.PointerExited -= PlayPausePanel_PointerExited;
        }

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (file != null)
            {
                // Set the option to show the picker
                var options = new Windows.System.LauncherOptions();
                options.DisplayApplicationPicker = false;

                // Launch the retrieved file
                await Windows.System.Launcher.LaunchFileAsync(this.file, options);
            }
            else
            {
                // Could not find file
            }

        }

        private void togglePlayPause()
        {
            if (mediaPaused)
            {
                this.VideoFileViewer.Play();
                this.mediaPaused = false;
                this.PlayPauseButton.Text = this.playIcon;
            }
            else
            {
                this.VideoFileViewer.Pause();
                this.mediaPaused = true;
                this.PlayPauseButton.Text = this.pauseIcon;
            }
        }
    }
}
