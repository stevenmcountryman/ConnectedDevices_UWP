using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Share_Across_Devices
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "Share target C# sample";

        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title="Welcome", ClassType=typeof(Welcome)},
        };
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
    
    sealed partial class App : Application
    {
        protected override void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        {
            var rootFrame = CreateRootFrame();
            rootFrame.Navigate(typeof(ShareWebLink), args.ShareOperation);
            Window.Current.Activate();
        }
    }
}
