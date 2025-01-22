using Composition.WindowsRuntimeHelpers;
using System;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;
// ---- add pixel shader
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology;
using System.Diagnostics;

namespace CaptureSampleCore
{
    public class BasicCapture : IDisposable
    {
        private GraphicsCaptureItem item;
        private Direct3D11CaptureFramePool framePool;
        private GraphicsCaptureSession session;
        private SizeInt32 lastSize;

        private IDirect3DDevice device;
        private Device d3dDevice;
        private SwapChain1 swapChain;
        private DeviceContext context;

        // ---- add pixel shader
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        private InputLayout inputLayout;
        private Buffer vertexBuffer;
        private ShaderResourceView textureView;
        private SamplerState sampler;
        private RenderTargetView renderTargetView;

        public BasicCapture(IDirect3DDevice d, GraphicsCaptureItem i)
        {
            item = i;
            device = d;
            d3dDevice = Direct3D11Helper.CreateSharpDXDevice(device);
            context = d3dDevice.ImmediateContext;

            var dxgiFactory = new Factory2();
            var description = new SwapChainDescription1()
            {
                Width = item.Size.Width,
                Height = item.Size.Height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            };
            swapChain = new SwapChain1(dxgiFactory, d3dDevice, ref description);

            framePool = Direct3D11CaptureFramePool.Create(
                device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                i.Size);
            session = framePool.CreateCaptureSession(i);
            lastSize = i.Size;

            InitializeShaders(); // ---- add pixel shader

            framePool.FrameArrived += OnFrameArrived;
        }

        private void InitializeShaders()
        {
            try
            {
                // ---- add pixel shader
                var vertexShaderBytecode = ShaderBytecode.CompileFromFile("Shader/VertexShader.hlsl", "VS", "vs_5_0", ShaderFlags.None, EffectFlags.None);
                vertexShader = new VertexShader(d3dDevice, vertexShaderBytecode);
                var pixelShaderBytecode = ShaderBytecode.CompileFromFile("Shader/PixelShader.hlsl", "PS", "ps_5_0", ShaderFlags.None, EffectFlags.None);
                if (pixelShaderBytecode == null || pixelShaderBytecode.Bytecode == null)
                {
                    throw new InvalidOperationException("!!! Failed to compile pixel shader");
                }
                pixelShader = new PixelShader(d3dDevice, pixelShaderBytecode);

                // define input layout (position and texture coordinates)
                var inputElements = new[]
                {
                new InputElement("POSITION", 0, Format.R32G32_Float, 0, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 8, 0)
            };
                inputLayout = new InputLayout(d3dDevice, ShaderSignature.GetInputSignature(vertexShaderBytecode), inputElements);

                // create vertex buffer (quad)
                var vertices = new[]
                {
                // Position    // Texture coordinates
                -1.0f,  1.0f,  0.0f, 0.0f,
                 1.0f,  1.0f,  1.0f, 0.0f,
                -1.0f, -1.0f,  0.0f, 1.0f,
                 1.0f, -1.0f,  1.0f, 1.0f
            };
                vertexBuffer = Buffer.Create(d3dDevice, BindFlags.VertexBuffer, vertices);

                // create sampler state
                var samplerDescription = new SamplerStateDescription
                {
                    Filter = Filter.MinMagMipLinear,
                    AddressU = TextureAddressMode.Wrap,
                    AddressV = TextureAddressMode.Wrap,
                    AddressW = TextureAddressMode.Wrap,
                    ComparisonFunction = Comparison.Never,
                    BorderColor = Color.Black,
                    MinimumLod = 0,
                    MaximumLod = float.MaxValue
                };
                sampler = new SamplerState(d3dDevice, samplerDescription);

                // set up render target
                using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
                {
                    renderTargetView = new RenderTargetView(d3dDevice, backBuffer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"!!! Failed to initialize shaders: {ex.Message}");
            }
        }

        public void Dispose()
        {
            session?.Dispose();
            framePool?.Dispose();
            swapChain?.Dispose();
            d3dDevice?.Dispose();
            // ---- add pixel shader
            vertexShader?.Dispose();
            pixelShader?.Dispose();
            inputLayout?.Dispose();
            vertexBuffer?.Dispose();
            textureView?.Dispose();
            sampler?.Dispose();
            renderTargetView?.Dispose();
        }

        public void StartCapture()
        {
            session.StartCapture();
        }

        public ICompositionSurface CreateSurface(Compositor compositor)
        {
            return compositor.CreateCompositionSurfaceForSwapChain(swapChain);
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            try
            {
                var newSize = false;

                using (var frame = sender.TryGetNextFrame())
                {
                    if (frame == null)
                    {
                        Debug.WriteLine("!!! Invalid frame or surface");
                        return;
                    }
                    if (frame.ContentSize.Width != lastSize.Width ||
                        frame.ContentSize.Height != lastSize.Height)
                    {
                        // The thing we have been capturing has changed size.
                        // We need to resize the swap chain first, then blit the pixels.
                        // After we do that, retire the frame and then recreate the frame pool.
                        newSize = true;
                        lastSize = frame.ContentSize;
                        swapChain.ResizeBuffers(
                            2,
                            lastSize.Width,
                            lastSize.Height,
                            Format.B8G8R8A8_UNorm,
                            SwapChainFlags.None
                        );
                    }

                    using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
                    using (var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface))
                    {
                        // ---- add pixel shader
                        // copy the frame to a shader resource
                        //if (textureView != null)
                        //{
                        //    context.CopyResource(bitmap, textureView.Resource);
                        //}
                        //else
                        //{
                        //    textureView = new ShaderResourceView(d3dDevice, bitmap);
                        //}
                        textureView = new ShaderResourceView(d3dDevice, bitmap);

                        // set up the pipeline
                        context.OutputMerger.SetRenderTargets(renderTargetView);
                        context.Rasterizer.SetViewport(new Viewport(0, 0, lastSize.Width, lastSize.Height));

                        context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, 16, 0));
                        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
                        context.InputAssembler.InputLayout = inputLayout;

                        context.VertexShader.Set(vertexShader);
                        context.PixelShader.Set(pixelShader);
                        context.PixelShader.SetShaderResource(0, textureView);
                        context.PixelShader.SetSampler(0, sampler);

                        // draw the quad
                        context.ClearRenderTargetView(renderTargetView, new Color4(0, 0, 0, 1)); // Clear to black
                        context.Draw(4, 0);
                    }

                } // Retire the frame.

                swapChain.Present(0, PresentFlags.None);

                if (newSize)
                {
                    Debug.WriteLine("! newSize triggered");
                    framePool.Recreate(
                        device,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        lastSize);
                }
            }
            catch (SharpDXException oex)
            {
                Debug.WriteLine($"!!! Failed to render frame: {oex.Message}");

                var reason = d3dDevice.DeviceRemovedReason;
                if (reason != null)
                {
                    Debug.WriteLine($"Device removed reason: {reason}");
                }

                Debug.WriteLine($"SharpDX ResultCode: {oex.ResultCode}");
            }
        }
    }
}
