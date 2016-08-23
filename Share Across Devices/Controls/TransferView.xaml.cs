using System;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace Share_Across_Devices.Controls
{
    public sealed partial class TransferView : UserControl
    {
        private StorageFile file;

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
            this.file = file;
            this.MediaViewer.DisplayFile(file);
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
    }
}
