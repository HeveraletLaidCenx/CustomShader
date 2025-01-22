using Composition.WindowsRuntimeHelpers;
using System;
using System.Numerics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;

namespace CaptureSampleCore
{
    public class BasicSampleApplication : IDisposable
    {
        private Compositor compositor;
        private ContainerVisual root;

        private SpriteVisual content;
        private CompositionSurfaceBrush brush;

        private IDirect3DDevice device;
        private BasicCapture capture;

        public BasicSampleApplication(Compositor c)
        {
            compositor = c;
            device = Direct3D11Helper.CreateDevice();

            // Setup the root.
            root = compositor.CreateContainerVisual();
            root.RelativeSizeAdjustment = Vector2.One;

            // Setup the content.
            brush = compositor.CreateSurfaceBrush();
            brush.HorizontalAlignmentRatio = 0.5f;
            brush.VerticalAlignmentRatio = 0.5f;
            brush.Stretch = CompositionStretch.Uniform;

            var shadow = compositor.CreateDropShadow();
            shadow.Mask = brush;

            content = compositor.CreateSpriteVisual();
            content.AnchorPoint = new Vector2(0.5f);
            content.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
            content.RelativeSizeAdjustment = Vector2.One;
            //content.Size = new Vector2(-80, -80);
            content.Brush = brush;
            content.Shadow = shadow;
            root.Children.InsertAtTop(content);
        }

        public Visual Visual => root;

        public void Dispose()
        {
            StopCapture();
            compositor = null;
            root.Dispose();
            content.Dispose();
            brush.Dispose();
            device.Dispose();
        }

        public void StartCaptureFromItem(GraphicsCaptureItem item)
        {
            StopCapture();
            capture = new BasicCapture(device, item);

            var surface = capture.CreateSurface(compositor);
            brush.Surface = surface;

            capture.StartCapture();
        }

        public void StopCapture()
        {
            capture?.Dispose();
            brush.Surface = null;
        }
    }
}
