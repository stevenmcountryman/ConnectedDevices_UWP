using Share_Across_Devices.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI.Xaml;
using static Share_Across_Devices.Controls.MyEventArgs;

namespace Share_Across_Devices.Controls
{
    public class FileTransfer
    {
        private string PortNumber;
        private int BlockSize;
        private RemoteSystem RemoteSystem;
        private StorageFile FileToSend;
        private string AppServiceName;
        private string PackageFamilyName;
        private DispatcherTimer timer;
        private DateTime lastUpdatedTime;

        public delegate void NotifyHandler(object sender, MyEventArgs e);
        public event NotifyHandler NotifyEvent;
        
        public FileTransfer(string portNumber, int blockSize, RemoteSystem remoteSys, StorageFile file, string appServiceName, string packageFamilyName)
        {
            this.PortNumber = portNumber;
            this.BlockSize = blockSize;
            this.RemoteSystem = remoteSys;
            this.FileToSend = file;
            this.AppServiceName = appServiceName;
            this.PackageFamilyName = packageFamilyName;
            this.timer = new DispatcherTimer();
            this.timer.Interval = new TimeSpan(0, 0, 1);
        }

        private void Timer_Tick(object sender, object e)
        {
            var currentTime = DateTime.Now.ToUniversalTime();
            if (this.lastUpdatedTime != null && currentTime.Subtract(this.lastUpdatedTime).TotalSeconds >= 10)
            {
                this.NotifyEvent(this, new MyEventArgs("Network timeout. Check your connection and try again", messageType.Indefinite));
                this.stopTimer();
            }
            else if (this.lastUpdatedTime != null && currentTime.Subtract(this.lastUpdatedTime).TotalSeconds == 5)
            {
                this.NotifyEvent(this, new MyEventArgs("This is taking longer than expected...", messageType.Indefinite));
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

        public async void sendFile()
        {
            var sendAttempt = 1;
            AppServiceConnectionStatus status = AppServiceConnectionStatus.Unknown;
            if (this.FileToSend != null && this.RemoteSystem != null)
            {
                while (sendAttempt <= 3)
                {
                    using (var connection = new AppServiceConnection
                    {
                        AppServiceName = this.AppServiceName,
                        PackageFamilyName = this.PackageFamilyName
                    })
                    {
                        // Create a remote system connection request.
                        RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(this.RemoteSystem);

                        this.NotifyEvent(this, new MyEventArgs("Requesting connection to " + this.RemoteSystem.DisplayName + "...", messageType.Indefinite));

                        this.startTimer();
                        status = await connection.OpenRemoteAsync(connectionRequest);
                        this.stopTimer();
                        if (status == AppServiceConnectionStatus.Success)
                        {
                            this.NotifyEvent(this, new MyEventArgs("Successfully connected to " + this.RemoteSystem.DisplayName + "...", messageType.Indefinite));
                            await this.RequestIPAddress(connection);
                            return;
                        }
                        else
                        {
                            sendAttempt++;
                            this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3", messageType.Indefinite));
                        }
                    }
                }
                this.NotifyEvent(this, new MyEventArgs("Attempt to open a remote app service connection failed with error - " + status.ToString(), messageType.Indefinite));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs("Select a device for remote connection.", messageType.Indefinite));
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
                    this.NotifyEvent(this, new MyEventArgs("Requesting IP address....", messageType.Indefinite));
                    this.startTimer();
                    response = await connection.SendMessageAsync(inputs);
                    this.stopTimer();
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
                                this.NotifyEvent(this, new MyEventArgs("Remote app service did not respond with a result.", messageType.Indefinite));
                            }
                            break;
                        }
                        else
                        {
                            this.NotifyEvent(this, new MyEventArgs("Response from remote app service does not contain a result.", messageType.Indefinite));
                        }
                    }
                    else
                    {
                        sendAttempt++;
                        this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3", messageType.Indefinite));
                    }
                }
                this.NotifyEvent(this, new MyEventArgs("Sending message to remote app service failed with error - " + response.Status.ToString(), messageType.Indefinite));
            }
            else
            {
                this.NotifyEvent(this, new MyEventArgs("Not connected to any app service. Select a device to open a connection.", messageType.Indefinite));
            }
        }
        private async void beginConnection(string ipAddress)
        {
            var sendAttempt = 1;
            while (sendAttempt <= 3)
            {
                try
                {
                    this.NotifyEvent(this, new MyEventArgs("Launching app on device....", messageType.Indefinite));
                    var status = await RemoteLaunch.TryBeginShareFile(this.RemoteSystem, this.FileToSend.Name);

                    if (status == RemoteLaunchUriStatus.Success)
                    {
                        //Create the StreamSocket and establish a connection to the echo server.
                        using (var socket = new StreamSocket())
                        {

                            //The server hostname that we will be establishing a connection to. We will be running the server and client locally,
                            //so we will use localhost as the hostname.
                            Windows.Networking.HostName serverHost = new Windows.Networking.HostName(ipAddress);

                            //Every protocol typically has a standard port number. For example HTTP is typically 80, FTP is 20 and 21, etc.
                            //For the echo server/client application we will use a random port 1337.
                            this.NotifyEvent(this, new MyEventArgs("Opening connection....", messageType.Indefinite));
                            string serverPort = this.PortNumber;

                            this.startTimer();
                            await socket.ConnectAsync(serverHost, serverPort);
                            this.stopTimer();

                            //Write data to the echo server.
                            using (Stream streamOut = socket.OutputStream.AsStreamForWrite())
                            {
                                using (var fileStream = await this.FileToSend.OpenStreamForReadAsync())
                                {
                                    byte[] bytes;
                                    DataWriter dataWriter = new DataWriter(streamOut.AsOutputStream());
                                    fileStream.Seek(0, SeekOrigin.Begin);
                                    while (fileStream.Position < fileStream.Length)
                                    {
                                        if (fileStream.Length - fileStream.Position >= this.BlockSize)
                                        {
                                            bytes = new byte[this.BlockSize];
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
                                        this.NotifyEvent(this, new MyEventArgs(Convert.ToInt32(percentage) + "% transferred", messageType.Indefinite));
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
                            this.NotifyEvent(this, new MyEventArgs(response, messageType.Timed));
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    sendAttempt++;
                    this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3", messageType.Indefinite));
                }
            }
            this.NotifyEvent(this, new MyEventArgs("Connection failed. Network destination not allowed.", messageType.Indefinite));
        }
    }

    public partial class MyEventArgs : EventArgs
    {
        public string Message
        {
            get;
            set;
        }
        public messageType MessageType
        {
            get;
            set;
        }
        public enum messageType
        {
            Indefinite,
            Timed
        }

        public MyEventArgs()
        {

        }
        public MyEventArgs(string message, messageType type)
        {
            this.Message = message;
            this.MessageType = type;
        }
    }
}
