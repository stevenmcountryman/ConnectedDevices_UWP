using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;

namespace Share_Across_Devices.Controls
{
    public sealed partial class MediaView : UserControl
    {
        public delegate void CancelHandler(object sender, EventArgs e);
        public event CancelHandler CancelEvent;
        private StorageFile file;
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

        public MediaView()
        {
            this.InitializeComponent();
            this.setUpCompositorStuff();
        }
        public MediaView(StorageFile file) : this()
        {
            this.DisplayFile(file);
        }

        private void setUpCompositorStuff()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var itemVisual = ElementCompositionPreview.GetElementVisual(this.PlayPauseGrid);
            itemVisual.Opacity = 0;
        }

        public async void DisplayFile(StorageFile file)
        {
            this.file = file;
            this.FileNameBlock.Text = this.file.Name;

            if (this.imageTypes.Contains(this.file.FileType))
            {
                using (var fileStream = await this.file.OpenReadAsync())
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.SetSource(fileStream);
                    this.ImageFileViewer.Source = bitmap;
                }
            }
            else if (this.mediaTypes.Contains(this.file.FileType))
            {
                this.VideoFileViewer.Visibility = Visibility.Visible;
                var fileStream = await this.file.OpenReadAsync();
                this.VideoFileViewer.SetSource(fileStream, this.file.ContentType);
                this.PlayPausePanel.Tapped += PlayPausePanel_Tapped;
                this.PlayPausePanel.PointerEntered += PlayPausePanel_PointerEntered;
                this.PlayPausePanel.PointerExited += PlayPausePanel_PointerExited;
            }
            else
            {
                this.setThumbnail();
            }
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.CancelEvent(this, new EventArgs());
        }
    }
}
