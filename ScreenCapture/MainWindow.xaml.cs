using CaptureSampleCore;
using Composition.WindowsRuntimeHelpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.UI.Composition;

namespace WPFCaptureSample
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Process> processes;

        // for Window click through and info
        private const uint WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int GWL_EXSTYLE = (-20);

        [DllImport("user32", EntryPoint = "GetWindowLong")]
        private static extern uint GetWindowLong(IntPtr hWndm, int nIndex);

        [DllImport("user32", EntryPoint = "SetWindowLong")]
        private static extern uint SetWindowLong(IntPtr hWndm, int nIndex, uint dwNewLong);

        // scale factor
        public double scaleFactor = 1.0;

        [DllImport("user32", EntryPoint = "GetDpiForSystem")]
        private static extern uint GetDpiForSystem();

        public void UpdateSystemScaleFactor()
        {
            scaleFactor = (double)GetDpiForSystem() / 96.0;
            Debug.WriteLine($"scaleFactor: {scaleFactor}");
        }

        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // a list of sub WPF Window created by code
        private ObservableCollection<Window> list_Windows;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // binding data for WPF filter window combobox
            list_Windows = new ObservableCollection<Window>();
            FilterComboBox.ItemsSource = list_Windows;

            InitWindowList();
        }

        // extend a class FilterWindow from Window
        public class FilterWindow : Window
        {
            private WindowInteropHelper interopWindow;
            public IntPtr hwnd;
            public IntPtr hwnd_process;
            private PresentationSource presentationSource;

            public Compositor compositor;
            public CompositionTarget target;
            public ContainerVisual root;
            public BasicSampleApplication sample;

            public int X;
            public int Y;
            public int W;
            public int H;
            public int ActualW;
            public int ActualH;

            private uint originalEx = 0;

            public FilterWindow()
            {
                // get the window
                var window = (Window)this;

                // set Window as no border
                window.WindowStyle = WindowStyle.None;
                window.ResizeMode = ResizeMode.NoResize;
                window.AllowsTransparency = true;
                window.Opacity = 0; // which it only affect the opacity of the window background, not affect the content rendered by SharpDX

                Loaded += Filter_Window_Loaded;
            }

            private void Filter_Window_Loaded(object sender, RoutedEventArgs e)
            {
                // get the window
                var window = (Window)sender;

                interopWindow = new WindowInteropHelper(window);
                hwnd = interopWindow.Handle;
                presentationSource = PresentationSource.FromVisual(this);

                // set Window click through
                originalEx = GetWindowLong(hwnd, GWL_EXSTYLE);
                uint _result = SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);

                this.Topmost = true;

                try
                {
                    InitComposition();
                    StartHwndCapture();
                }
                catch (Exception fwex)
                {
                    Debug.WriteLine($"!!! FilterWindow exception: {fwex.Message}");
                }
            }

            private void InitComposition()
            {
                // Create the compositor.
                compositor = new Compositor();

                // Create a target for the window.
                target = compositor.CreateDesktopWindowTarget(hwnd, true);

                // Attach the root visual.
                root = compositor.CreateContainerVisual();
                root.RelativeSizeAdjustment = Vector2.One;
                //root.Size = new Vector2(-W, 0);
                //root.Offset = new Vector3(W, 0, 0);
                target.Root = root;

                // Setup the rest of the sample application.
                sample = new BasicSampleApplication(compositor);
                root.Children.InsertAtTop(sample.Visual);
            }

            private void StartHwndCapture()
            {
                GraphicsCaptureItem item = CaptureHelper.CreateItemForWindow(this.hwnd_process);
                if (item != null)
                {
                    sample.StartCaptureFromItem(item);
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            //StopCapture();
            if (FilterComboBox.SelectedItem != null)
            {
                var filterWindow = (FilterWindow)FilterComboBox.SelectedItem;
                // close filter window
                filterWindow.Close();
            }
            WindowComboBox.SelectedIndex = -1;
        }

        private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var process = (Process)comboBox.SelectedItem;

            if (process != null)
            {
                //StopCapture();
                var hwnd_process = process.MainWindowHandle;
                
                UpdateSystemScaleFactor();

                // add a new WPF Window to list_Windows
                var filterWindow = new FilterWindow();
                list_Windows.Add(filterWindow);
                filterWindow.hwnd_process = hwnd_process;

                // try get process window's screen rect
                RECT rect = new RECT();
                if (GetWindowRect(hwnd_process, out rect))
                {
                    filterWindow.X = rect.left;
                    filterWindow.Y = rect.top;
                    filterWindow.W = rect.right - rect.left;
                    filterWindow.H = rect.bottom - rect.top;
                    filterWindow.ActualW = (int)(filterWindow.W / scaleFactor);
                    filterWindow.ActualH = (int)(filterWindow.H / scaleFactor);
                    Debug.WriteLine($"Hwnd 0x{hwnd_process.ToInt32():X8} rect: X={filterWindow.X}, Y={filterWindow.Y}, W={filterWindow.W}, H={filterWindow.H}");
                }

                filterWindow.Left = filterWindow.X + 1;
                filterWindow.Top = filterWindow.Y;
                filterWindow.Width = filterWindow.ActualW;
                filterWindow.Height = filterWindow.ActualH - 7;

                // set Window title as "Filter of " + process name
                filterWindow.Title = "Filter of " + process.ProcessName;

                // when the window close, remove it from list_Windows
                filterWindow.Closing += (nw_sender, nw_e) => {
                    list_Windows.Remove(filterWindow);
                    if (list_Windows.Count > 0)
                    {
                        FilterComboBox.SelectedIndex = list_Windows.Count - 1;
                    } else
                    {
                        FilterComboBox.IsEnabled = false;
                    }
                };

                filterWindow.Show();

                // update FilterListBox selected index
                FilterComboBox.SelectedIndex = list_Windows.Count - 1;
            }
        }

        private void InitWindowList()
        {
            if (ApiInformation.IsApiContractPresent(
                typeof(Windows.Foundation.UniversalApiContract).FullName, 8
            ))
            {
                var processesWithWindows = from p in Process.GetProcesses()
                    where !string.IsNullOrWhiteSpace(p.MainWindowTitle) && WindowEnumerationHelper.IsWindowValidForCapture(p.MainWindowHandle)
                    select p;
                processes = new ObservableCollection<Process>(processesWithWindows);
                WindowComboBox.ItemsSource = processes;
            }
            else
            {
                WindowComboBox.IsEnabled = false;
            }
        }

        //private void StopCapture()
        //{
        //    sample.StopCapture();
        //}
    }
}
