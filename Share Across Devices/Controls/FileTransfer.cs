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

        public delegate void NotifyHandler(object sender, MyEventArgs e);
        public event NotifyHandler NotifyEvent;
        
        public FileTransfer(string portNumber, int blockSize, RemoteSystem remoteSys, StorageFile file)
        {
            this.PortNumber = portNumber;
            this.BlockSize = blockSize;
            this.RemoteSystem = remoteSys;
            this.FileToSend = file;
        }

        public async void sendFile()
        {
            var sendAttempt = 1;
            while (sendAttempt <= 3)
            {
                try
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
                    //Create the StreamSocket and establish a connection to the echo server.
                    try
                    {
                        this.NotifyEvent(this, new MyEventArgs("Launching app on device....", messageType.Indefinite));
                        RemoteLaunch.TryBeginShareFile(this.RemoteSystem, this.FileToSend.Name, hostname?.CanonicalName);

                        this.NotifyEvent(this, new MyEventArgs("Waiting for connection....", messageType.Indefinite));
                        //Create a StreamSocketListener to start listening for TCP connections.
                        StreamSocketListener socketListener = new StreamSocketListener();

                        //Hook up an event handler to call when connections are received.
                        socketListener.ConnectionReceived += SocketListener_ConnectionReceived;

                        //Start listening for incoming TCP connections on the specified port. You can specify any port that' s not currently in use.
                        await socketListener.BindServiceNameAsync(this.PortNumber);

                        break;
                    }
                    catch (Exception e)
                    {
                        sendAttempt++;
                        this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3", messageType.Indefinite));
                    }
                }
                catch (Exception e)
                {
                    sendAttempt++;
                    this.NotifyEvent(this, new MyEventArgs("Failed. Retrying attempt " + sendAttempt + " of 3", messageType.Indefinite));
                }
            }
        }
        private async void SocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            sender.ConnectionReceived -= SocketListener_ConnectionReceived;
            this.NotifyEvent(this, new MyEventArgs("Connected! Sending file....", messageType.Indefinite));
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
                Stream streamIn = args.Socket.InputStream.AsStreamForRead();
                StreamReader reader = new StreamReader(streamIn);
                string response = await reader.ReadLineAsync();
                this.NotifyEvent(this, new MyEventArgs(response, messageType.Timed));
                return;
            }
            catch (Exception e)
            {
                this.NotifyEvent(this, new MyEventArgs("Transfer interrupted", messageType.Indefinite));
            }
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
