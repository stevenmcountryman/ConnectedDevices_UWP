using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

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
            ".mp3"
        };
        private bool mediaPaused = false;

        public TransferView()
        {
            this.InitializeComponent();
        }

        public TransferView(StorageFile file) : this()
        {
            this.file = file;
            this.DisplayFile(file);
        }

        public async void DisplayFile(StorageFile file)
        {
            this.file = file;
            this.FileNameBlock.Text = this.file.Name;

            if (this.imageTypes.Contains(this.file.FileType))
            {
                this.ImageFileViewer.Visibility = Visibility.Visible;
                this.ImageFileViewer.Source = new BitmapImage(new Uri(file.Path));
            }
            else if (this.mediaTypes.Contains(this.file.FileType))
            {
                this.VideoFileViewer.Visibility = Visibility.Visible;
                this.VideoFileViewer.Source = new Uri(this.file.Path);
            }
            else
            {
                try
                {
                    var thumb = await this.file.GetThumbnailAsync(ThumbnailMode.DocumentsView, (uint)300, ThumbnailOptions.ResizeThumbnail);
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.SetSource(thumb);
                    this.ImageFileViewer.Visibility = Visibility.Visible;
                    this.ImageFileViewer.Source = bitmap;
                }
                catch
                {
                }
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

        private void CancelButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.CancelEvent(sender, new EventArgs());
        }

        private async void OpenFileButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
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
        private void VideoFileViewer_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (mediaPaused)
            {
                this.VideoFileViewer.Play();
                this.mediaPaused = false; 
            }
            else
            {
                this.VideoFileViewer.Pause();
                this.mediaPaused = true;
            }
        }
    }
}
