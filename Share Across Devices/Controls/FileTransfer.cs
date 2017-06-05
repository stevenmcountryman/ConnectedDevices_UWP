using Share_Across_Devices.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Connectivity;
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
        private StreamSocketListener socketListener;

        public delegate void NotifyHandler(object sender, MyEventArgs e);
        public event NotifyHandler NotifyEvent;
        
        public FileTransfer(string portNumber, int blockSize, RemoteSystem remoteSys, StorageFile file)
        {
            this.PortNumber = portNumber;
            this.BlockSize = blockSize;
            this.RemoteSystem = remoteSys;
            this.FileToSend = file;
        }

        private async Task OpenStore()
        {
            this.NotifyEvent(this, new MyEventArgs("App not installed on target device..", messageType.Indefinite, false));
            var status = await RemoteLaunch.TryOpenStoreToApp(this.RemoteSystem, RemoteLaunch.MYAPP_STORE);
        }

        public async void sendFile()
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();

            if (icp?.NetworkAdapter == null) return;
            var hostnames = NetworkInformation.GetHostNames();
            HostName hostname = null;
            foreach (var name in hostnames)
            {
                if (name.IPInformation?.NetworkAdapter != null && name.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId)
                {
                    hostname = name;
                    break;
                }
            }
            this.NotifyEvent(this, new MyEventArgs("Launching app on device....", messageType.Indefinite, false));
            var status = await RemoteLaunch.TryBeginShareFile(this.RemoteSystem, this.FileToSend.Name, hostname?.CanonicalName);

            if (status == RemoteLaunchUriStatus.ProtocolUnavailable)
            {
                await this.OpenStore();
                return;
            }

            this.NotifyEvent(this, new MyEventArgs("Waiting for connection....", messageType.Indefinite, false));
            //Create a StreamSocketListener to start listening for TCP connections.
            if (this.socketListener != null)
            {
                this.socketListener.Dispose();
            }
            this.socketListener = new StreamSocketListener();

            //Hook up an event handler to call when connections are received.
            socketListener.ConnectionReceived += SocketListener_ConnectionReceived;

            //Start listening for incoming TCP connections on the specified port. You can specify any port that' s not currently in use.
            await socketListener.BindServiceNameAsync(this.PortNumber);
        }
        private async void SocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            sender.ConnectionReceived -= SocketListener_ConnectionReceived;
            this.NotifyEvent(this, new MyEventArgs("Connected! Sending file....", messageType.Indefinite, true));
            try
            {
                //Write data to the echo server.
                using (Stream streamOut = args.Socket.OutputStream.AsStreamForWrite())
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
                            this.NotifyEvent(this, new MyEventArgs(Convert.ToInt32(percentage) + "% transferred", messageType.Indefinite, true));
                            await fileStream.ReadAsync(bytes, 0, bytes.Length);
                            dataWriter.WriteBytes(bytes);
                            await dataWriter.StoreAsync();
                        }
                        this.NotifyEvent(this, new MyEventArgs("File sent!", messageType.Timed, true));
                        dataWriter.WriteBoolean(false);
                        await dataWriter.StoreAsync();
                    }
                }
                this.socketListener.Dispose();
            }
            catch (Exception e)
            {
                this.NotifyEvent(this, new MyEventArgs("Transfer interrupted", messageType.Indefinite, true));
            }
        }

        internal void Dispose()
        {
            this.socketListener.Dispose();
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
        public bool Marshalled
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
        public MyEventArgs(string message, messageType type, bool marshalled)
        {
            this.Message = message;
            this.MessageType = type;
            this.Marshalled = marshalled;
        }
    }
}
