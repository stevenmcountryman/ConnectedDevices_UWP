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
        public string DeviceFriendlyName
        {
            get
            {
                return this.DeviceName.Text;
            }
        }
        public string DeviceFontIcon
        {
            get
            {
                if (this.remoteDevice.Kind == RemoteSystemKinds.Phone)
                {
                    return "\uE8EA";
                }
                else if (this.remoteDevice.Kind == RemoteSystemKinds.Desktop)
                {
                    return "\uE212";
                }
                else if (this.remoteDevice.Kind == RemoteSystemKinds.Xbox)
                {
                    return "\uE7FC";
                }
                else if (this.remoteDevice.Kind == RemoteSystemKinds.Holographic)
                {
                    return "\uE1A6";
                }
                else if (this.remoteDevice.Kind == RemoteSystemKinds.Hub)
                {
                    return "\uE8AE";
                }
                else return null;
            }
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
        public void SetDevice(RemoteSystem system)
        {
            this.remoteDevice = system;
        }
    }
}
