using Share_Across_Devices.Helpers;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI.Xaml;
using static Share_Across_Devices.Controls.MyEventArgs;

namespace Share_Across_Devices.Controls
{
    public class RemoteDeviceObject
    {
        private StorageFile fileToSend;
        private RemoteSystem remoteSystem;
        private DispatcherTimer timer;
        private DateTime lastUpdatedTime;
        private FileTransfer fileTransfer;

        public delegate void NotifyHandler(object sender, MyEventArgs e);
        public event NotifyHandler NotifyEvent;

        public RemoteSystem RemoteSystem
        {
            get
            {
                return this.remoteSystem;
            }
            set
            {
                this.remoteSystem = value;
            }
        }
        public string DeviceIcon
        {
            get
            {
                if (this.remoteSystem.Kind == RemoteSystemKinds.Phone)
                {
                    return "\uE8EA";
                }
                else if (this.remoteSystem.Kind == RemoteSystemKinds.Desktop)
                {
                    return "\uE212";
                }
                else if (this.remoteSystem.Kind == RemoteSystemKinds.Xbox)
                {
                    return "\uE7FC";
                }
                else if (this.remoteSystem.Kind == RemoteSystemKinds.Holographic)
                {
                    return "\uE1A6";
                }
                else if (this.remoteSystem.Kind == RemoteSystemKinds.Hub)
                {
                    return "\uE8AE";
                }
                else return null;
            }
        }
        public string DeviceName
        {
            get
            {
                return this.remoteSystem.DisplayName;
            }
        }
        public RemoteDeviceObject(RemoteSystem remoteSystem)
        {
            this.remoteSystem = remoteSystem;
            this.timer = new DispatcherTimer();
            this.timer.Interval = new TimeSpan(0, 0, 1);
        }

        private void Timer_Tick(object sender, object e)
        {
            var currentTime = DateTime.Now.ToUniversalTime();
            if (this.lastUpdatedTime != null && currentTime.Subtract(this.lastUpdatedTime).TotalSeconds >= 10)
            {
                this.NotifyEvent(this, new MyEventArgs("Network timeout. Check your connection and try again", messageType.Indefinite, false));
                this.timer.Stop();
            }
            else if (this.lastUpdatedTime != null && currentTime.Subtract(this.lastUpdatedTime).TotalSeconds == 5)
            {
                this.NotifyEvent(this, new MyEventArgs("This is taking longer than expected...", messageType.Indefinite, false));
            }
        }

        private void startTimer()
        {
            this.timer.Start();
            this.lastUpdatedTime = DateTime.Now.ToUniversalTime();
            this.timer.Tick += Timer_Tick;
        }

        private void stopTimer()
        {
            this.timer.Start();
            this.lastUpdatedTime = DateTime.Now.ToUniversalTime();
            this.timer.Tick -= Timer_Tick;
        }

        public async void ShareMessage(string message)
        {
            this.NotifyEvent(this, new MyEventArgs("Sending to remote clipboard...", messageType.Indefinite, false));
            this.startTimer();
            var status = await RemoteLaunch.TrySharetext(this.remoteSystem, message);
            this.stopTimer();
            if (status == RemoteLaunchUriStatus.Success)
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Timed, false));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Indefinite, false));
            }
        }

        public async void OpenLinkInBrowser(string url)
        {
            this.NotifyEvent(this, new MyEventArgs("Opening in remote browser...", messageType.Indefinite, false));
            this.startTimer();
            var status = await RemoteLaunch.TryShareURL(this.remoteSystem, url);
            this.stopTimer();
            if (status == RemoteLaunchUriStatus.Success)
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Timed, false));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Indefinite, false));
            }
        }
        public async void OpenLinkInTubeCast(string url)
        {
            this.NotifyEvent(this, new MyEventArgs("Opening in remote TubeCast...", messageType.Indefinite, false));
            this.startTimer();
            var status = await RemoteLaunch.TryShareURL(this.remoteSystem, RemoteLaunch.ParseYoutubeLinkToTubeCastUri(url));
            this.stopTimer();
            if (status == RemoteLaunchUriStatus.Success)
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Timed, false));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Indefinite, false));
            }
        }
        public async void OpenLinkInMyTube(string url)
        {
            this.NotifyEvent(this, new MyEventArgs("Opening in remote myTube!...", messageType.Indefinite, false));
            this.startTimer();
            var status = await RemoteLaunch.TryShareURL(this.remoteSystem, RemoteLaunch.ParseYoutubeLinkToMyTubeUri(url));
            this.stopTimer();
            if (status == RemoteLaunchUriStatus.Success)
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Timed, false));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs(status.ToString(), messageType.Indefinite, false));
            }
        }
        public async Task<StorageFile> OpenFileToSend()
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add("*");
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            var files = await openPicker.PickMultipleFilesAsync();            
            if (files != null)
            {
                if (files.Count == 1)
                {
                    this.fileToSend = files[0];
                    return this.fileToSend;
                }
                else
                {
                    StorageFile collectionZip = await ApplicationData.Current.LocalFolder.CreateFileAsync("FileCollection.zip", CreationCollisionOption.ReplaceExisting);
                    foreach (StorageFile item in files)
                    {
                        var newFile = await item.CopyAsync(ApplicationData.Current.LocalFolder, item.Name, NameCollisionOption.ReplaceExisting);
                        await Task.Run(() =>
                        {
                            using (FileStream stream = new FileStream(collectionZip.Path, FileMode.Open))
                            {
                                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update))
                                {
                                    archive.CreateEntryFromFile(newFile.Path, newFile.Name);
                                }
                            }
                        });
                        await newFile.DeleteAsync();
                    }
                    this.fileToSend = collectionZip;
                    return collectionZip;
                }
            }
            return null;
        }
        public void SetFileToSend(StorageFile file)
        {
            this.fileToSend = file;
        }

        public void SendFile()
        {
            if (this.fileToSend != null)
            {
                if (this.fileTransfer != null)
                {
                    this.fileTransfer.Dispose();
                }
                fileTransfer = new FileTransfer("1717", 7171, this.remoteSystem, this.fileToSend);
                fileTransfer.NotifyEvent += FileTransfer_NotifyEvent;
                fileTransfer.sendFile();
            }
        }

        private void FileTransfer_NotifyEvent(object sender, MyEventArgs e)
        {
            this.NotifyEvent(this, e);
        }
    }
}
