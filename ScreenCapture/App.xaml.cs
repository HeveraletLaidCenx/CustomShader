using Composition.WindowsRuntimeHelpers;
using System.Windows;
using Windows.System;

namespace WPFCaptureSample
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            _controller = CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();
        }

        private DispatcherQueueController _controller;
    }
}
