using System;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace Share_Across_Devices.Controls
{
    public sealed partial class TransferView : UserControl
    {
        private StorageFile file;
        private IReadOnlyList<StorageFile> files;

        public delegate void CancelHandler(object sender, EventArgs e);
        public event CancelHandler CancelEvent;
        public delegate void SavedHandler(object sender, EventArgs e);
        public event SavedHandler SaveEvent;       

        public TransferView()
        { 
            this.InitializeComponent();
        }

        public TransferView(StorageFile file) : this()
        {
            if (file.FileType == ".zip")
            {
                this.openZip(file);
                this.SaveFileButton.Content = "save all";
            }
            else
            {
                this.file = file;
                MediaView mediaView = new MediaView(file);
                mediaView.DisplayFile(file);
                mediaView.CancelEvent += MediaViewer_CancelEvent;
                this.MediaFlip.Items.Add(mediaView);
            }
        }

        private async void openZip(StorageFile file)
        {
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("tempFiles", CreationCollisionOption.ReplaceExisting);
            await Task.Run(() =>
            {
                using (ZipArchive archive = ZipFile.OpenRead(file.Path))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        entry.ExtractToFile(Path.Combine(folder.Path, entry.Name));
                    }
                }
            });
            this.files = await folder.GetFilesAsync();
            this.file = this.files[0];
            foreach (var item in files)
            {
                MediaView mediaView = new MediaView(item);
                mediaView.DisplayFile(item);
                mediaView.CancelEvent += MediaViewer_CancelEvent;
                this.MediaFlip.Items.Add(mediaView);
            }
        }

        private void SaveFileButton_Click(object sender, RoutedEventArgs e)
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
            StorageApplicationPermissions.FutureAccessList.AddOrReplace("FileToSave", folder);

            if (folder != null)
            {
                if (this.files != null)
                {
                    foreach (var file in this.files)
                    {
                        await file.CopyAsync(folder, file.Name, NameCollisionOption.ReplaceExisting);
                    }
                    this.SaveEvent(this, new EventArgs());
                }
                else
                {
                    await this.file.CopyAsync(folder, this.file.Name, NameCollisionOption.ReplaceExisting);
                    this.SaveEvent(this, new EventArgs());
                }
            }
        }

        private void close()
        {
            this.CancelEvent(this, new EventArgs());
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

        private void MediaViewer_CancelEvent(object sender, EventArgs e)
        {
            this.close();
        }

        private void MediaFlip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.files != null)
            {
                this.file = this.files[this.MediaFlip.SelectedIndex];
            }
        }
    }
}
