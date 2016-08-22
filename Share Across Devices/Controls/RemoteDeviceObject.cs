using Share_Across_Devices.Helpers;
using System;
using System.IO;
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

namespace Share_Across_Devices.Controls
{
    public class RemoteDeviceObject
    {
        private StorageFile fileToSend;
        private AppServiceConnection connection;
        private StreamSocket socket;
        private RemoteSystem remoteSystem;

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
        }

        public async void ShareMessage(string message)
        {
            this.NotifyEvent(this, new MyEventArgs("Sending to remote clipboard..."));
            var status = await RemoteLaunch.TrySharetext(this.remoteSystem, message);
            this.NotifyEvent(this, new MyEventArgs(status.ToString()));
        }

        public async void OpenLinkInBrowser(string url)
        {
            this.NotifyEvent(this, new MyEventArgs("Opening in remote browser..."));
            var status = await RemoteLaunch.TryShareURL(this.remoteSystem, url);
            this.NotifyEvent(this, new MyEventArgs(status.ToString()));
        }
        public async void OpenLinkInTubeCast(string url)
        {
            this.NotifyEvent(this, new MyEventArgs("Opening in remote browser..."));
            var status = await RemoteLaunch.TryShareURL(this.remoteSystem, RemoteLaunch.ParseYoutubeLinkToTubeCastUri(url));
            this.NotifyEvent(this, new MyEventArgs(status.ToString()));
        }
        public async void OpenLinkInMyTube(string url)
        {
            this.NotifyEvent(this, new MyEventArgs("Opening in remote browser..."));
            var status = await RemoteLaunch.TryShareURL(this.remoteSystem, RemoteLaunch.ParseYoutubeLinkToMyTubeUri(url));
            this.NotifyEvent(this, new MyEventArgs(status.ToString()));
        }
        public async Task<StorageFile> OpenFileToSend()
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add("*");
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            this.fileToSend = await openPicker.PickSingleFileAsync();
            StorageApplicationPermissions.FutureAccessList.Add(this.fileToSend);
            return this.fileToSend;
        }

        public void SendFile()
        {
            if (this.fileToSend != null)
            {
                this.openRemoteConnectionAsync();
            }
        }

        private async void openRemoteConnectionAsync()
        {
            var sendAttempt = 1;
            AppServiceConnectionStatus status = AppServiceConnectionStatus.Unknown;
            if (this.fileToSend != null && this.remoteSystem != null)
            {
                while (sendAttempt <= 3)
                {
                    using (this.connection = new AppServiceConnection
                    {
                        AppServiceName = "simplisidy.appservice",
                        PackageFamilyName = "34507Simplisidy.ShareAcrossDevices_wtkr3v20s86d8"
                    })
                    {
                        // Create a remote system connection request.
                        RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(this.remoteSystem);

                        this.NotifyEvent(this, new MyEventArgs("Requesting connection to " + this.remoteSystem.DisplayName + "..."));
                        status = await this.connection.OpenRemoteAsync(connectionRequest);
                        if (status == AppServiceConnectionStatus.Success)
                        {
                            this.NotifyEvent(this, new MyEventArgs("Successfully connected to " + this.remoteSystem.DisplayName + "..."));
                            await this.RequestIPAddress(connection);
                            return;
                        }
                        else
                        {
                            sendAttempt++;
                            this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3"));
                        }
                    }
                }
                this.NotifyEvent(this, new MyEventArgs("Attempt to open a remote app service connection failed with error - " + status.ToString()));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs("Select a device for remote connection."));
            }
        }
        private async Task RequestIPAddress(AppServiceConnection connection)
        {
            var sendAttempt = 1;
            AppServiceResponse response = null;
            // Send message if connection to the remote app service is open.
            if (connection != null)
            {
                while (sendAttempt <= 3)
                {
                    //Set up the inputs and send a message to the service.
                    ValueSet inputs = new ValueSet();
                    this.NotifyEvent(this, new MyEventArgs("Requesting IP address...."));
                    response = await connection.SendMessageAsync(inputs);

                    if (response.Status == AppServiceResponseStatus.Success)
                    {
                        if (response.Message.ContainsKey("result"))
                        {
                            string ipAddress = response.Message["result"].ToString();
                            if (!string.IsNullOrEmpty(ipAddress))
                            {
                                this.beginConnection(ipAddress);
                                return;
                            }
                            else
                            {
                                this.NotifyEvent(this, new MyEventArgs("Remote app service did not respond with a result."));
                            }
                            break;
                        }
                        else
                        {
                            this.NotifyEvent(this, new MyEventArgs("Response from remote app service does not contain a result."));
                        }
                    }
                    else
                    {
                        sendAttempt++;
                        this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3"));
                    }
                }
                this.NotifyEvent(this, new MyEventArgs("Sending message to remote app service failed with error - " + response.Status.ToString()));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs("Not connected to any app service. Select a device to open a connection."));
            }
        }
        private async void beginConnection(string ipAddress)
        {
            var sendAttempt = 1;
            while (sendAttempt <= 3)
            {
                try
                {
                    this.NotifyEvent(this, new MyEventArgs("Launching app on device...."));
                    var status = await RemoteLaunch.TryBeginShareFile(this.remoteSystem, this.fileToSend.Name);

                    if (status == RemoteLaunchUriStatus.Success)
                    {
                        //Create the StreamSocket and establish a connection to the echo server.
                        using (this.socket = new StreamSocket())
                        {

                            //The server hostname that we will be establishing a connection to. We will be running the server and client locally,
                            //so we will use localhost as the hostname.
                            Windows.Networking.HostName serverHost = new Windows.Networking.HostName(ipAddress);

                            //Every protocol typically has a standard port number. For example HTTP is typically 80, FTP is 20 and 21, etc.
                            //For the echo server/client application we will use a random port 1337.
                            this.NotifyEvent(this, new MyEventArgs("Opening connection...."));
                            string serverPort = "1717";
                            await socket.ConnectAsync(serverHost, serverPort);
                            
                            //Write data to the echo server.
                            using (Stream streamOut = socket.OutputStream.AsStreamForWrite())
                            {
                                using (var fileStream = await this.fileToSend.OpenStreamForReadAsync())
                                {
                                    byte[] bytes;
                                    DataWriter dataWriter = new DataWriter(streamOut.AsOutputStream());
                                    fileStream.Seek(0, SeekOrigin.Begin);
                                    while (fileStream.Position < fileStream.Length)
                                    {
                                        if (fileStream.Length - fileStream.Position >= 7171)
                                        {
                                            bytes = new byte[7171];
                                        }
                                        else
                                        {
                                            bytes = new byte[fileStream.Length - fileStream.Position];
                                        }
                                        dataWriter.WriteBoolean(true);
                                        await dataWriter.StoreAsync();
                                        dataWriter.WriteInt32(bytes.Length);
                                        await dataWriter.StoreAsync();
                                        var percentage = ((double)fileStream.Position / (double)fileStream.Length) * 100.0;
                                        dataWriter.WriteInt32(Convert.ToInt32(percentage));
                                        await dataWriter.StoreAsync();
                                        this.NotifyEvent(this, new MyEventArgs(Convert.ToInt32(percentage) + "% transferred"));
                                        await fileStream.ReadAsync(bytes, 0, bytes.Length);
                                        dataWriter.WriteBytes(bytes);
                                        await dataWriter.StoreAsync();
                                    }
                                    dataWriter.WriteBoolean(false);
                                    await dataWriter.StoreAsync();
                                }
                            }

                            //Read data from the echo server.
                            Stream streamIn = socket.InputStream.AsStreamForRead();
                            StreamReader reader = new StreamReader(streamIn);
                            string response = await reader.ReadLineAsync();
                            this.NotifyEvent(this, new MyEventArgs(response));
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    sendAttempt++;
                    this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3"));
                }
            }
            this.NotifyEvent(this, new MyEventArgs("Connection failed. Network destination not allowed."));
        }
    }

    public partial class MyEventArgs : EventArgs
    {
        public string Message
        {
            get;
            set;
        }
        public MyEventArgs()
        {

        }
        public MyEventArgs(string message)
        {
            this.Message = message;
        }
    }
}
