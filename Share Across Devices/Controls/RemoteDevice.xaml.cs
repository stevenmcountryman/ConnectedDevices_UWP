using Windows.System.RemoteSystems;
using Windows.UI.Xaml.Controls;

namespace Share_Across_Devices.Controls
{
    public sealed partial class RemoteDevice : UserControl
    {
        private RemoteSystem remoteDevice;

        public RemoteDevice()
        {
            this.InitializeComponent();
        }
        public RemoteDevice(RemoteSystem device) : this()
        {
            this.remoteDevice = device;
            this.DeviceName.Text = device.DisplayName;
            if (device.Kind == RemoteSystemKinds.Phone)
            {
                this.DeviceIcon.Text = "\uE8EA";
            }
            else if (device.Kind == RemoteSystemKinds.Desktop)
            {
                this.DeviceIcon.Text = "\uE212";
            }
            else if (device.Kind == RemoteSystemKinds.Xbox)
            {
                this.DeviceIcon.Text = "\uE7FC";
            }
            else if (device.Kind == RemoteSystemKinds.Holographic)
            {
                this.DeviceIcon.Text = "\uE1A6";
            }
            else if (device.Kind == RemoteSystemKinds.Hub)
            {
                this.DeviceIcon.Text = "\uE8AE";
            }
        }
        public RemoteSystem GetDevice()
        {
            return this.remoteDevice;
        }
    }
}
