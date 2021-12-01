using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Fusion;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Portrait3D
{
    /// <summary>
    /// Used to reconstruct what the sensor sees
    /// </summary>
    class Reconstructor
    {

        public class ErrorEventArgs : EventArgs
        {
            public ErrorEventArgs(string message)
            {
                Message = message;
            }

            public string Message { get; set; }
        }

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        public event EventHandler<ErrorEventArgs> ErrorEvent;

        /// <summary>
        /// Event fired when a frame has been processed
        /// </summary>
        public event EventHandler FrameProcessed;

        /// <summary>
        /// The reconstruction volume processor type. This parameter sets whether AMP or CPU processing
        /// is used. Note that CPU processing will likely be too slow for real-time processing.
        /// </summary>
        private const ReconstructionProcessor ProcessorType = ReconstructionProcessor.Amp;

        /// <summary>
        /// The zero-based device index to choose for reconstruction processing if the 
        /// ReconstructionProcessor AMP options are selected.
        /// Here we automatically choose a device to use for processing by passing -1, 
        /// </summary>
        private const int DeviceToUse = -1;

        /// <summary>
        /// The reconstruction volume voxel density in voxels per meter (vpm)
        /// Example with 256: 1000mm / 256vpm = ~3.9mm/voxel
        /// </summary>
        private int voxelsPerMeter;

        /// <summary>
        /// Parameter to translate the reconstruction based on the minimum depth setting. When set to
        /// false, the reconstruction volume +Z axis starts at the camera lens and extends into the scene.
        /// Setting this true in the constructor will move the volume forward along +Z away from the
        /// camera by the minimum depth threshold to enable capture of very small reconstruction volumes
        /// by setting a non-identity world-volume transformation in the ResetReconstruction call.
        /// Small volumes should be shifted, as the Kinect hardware has a minimum sensing limit of ~0.35m,
        /// inside which no valid depth is returned, hence it is difficult to initialize and track robustly  
        /// when the majority of a small volume is inside this distance.
        /// </summary>
        private bool translateResetPoseByMinDepthThreshold = true;

        /// <summary>
        /// Minimum depth distance threshold in meters. Depth pixels below this value will be
        /// returned as invalid (0). Min depth must be positive or 0.
        /// </summary>
        private float minDepthClip = FusionDepthProcessor.DefaultMinimumDepth;

        /// <summary>
        /// Maximum depth distance threshold in meters. Depth pixels above this value will be
        /// returned as invalid (0). Max depth must be greater than 0.
        /// </summary>
        private float maxDepthClip = FusionDepthProcessor.DefaultMaximumDepth;

        /// <summary>
        /// The transformation between the world and camera view coordinate system
        /// </summary>
        private Matrix4 worldToCameraTransform;

        /// <summary>
        /// The default transformation between the world and volume coordinate system
        /// </summary>
        private Matrix4 defaultWorldToVolumeTransform;

        /// <summary>
        /// The Kinect Fusion volume, enabling color reconstruction
        /// </summary>
        public Reconstruction Volume { get; private set; }

        /// <summary>
        /// The count of the depth frames to be processed
        /// </summary>
        private bool processingFrame = false;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        public WriteableBitmap ColorBitmap { get; private set; }

        /// <summary>
        /// Intermediate storage for the extended depth data received from the camera in the current frame
        /// </summary>
        private DepthImagePixel[] depthImagePixels;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private int[] colorPixels;

        /// <summary>
        /// Intermediate storage for the depth float data converted from depth image frame
        /// </summary>
        private FusionFloatImageFrame depthFloatBuffer;

        /// <summary>
        /// Intermediate storage for the point cloud data converted from depth float image frame
        /// </summary>
        private FusionPointCloudImageFrame pointCloudBuffer;

        /// <summary>
        /// Raycast shaded surface image
        /// </summary>
        private FusionColorImageFrame shadedSurfaceColorFrame;

        /// <summary>
        /// Contains data used for the ReconstructionVolume
        /// </summary>
        /// <param name="sensor">The sensor</param>
        /// <param name="depthImageSize">The depth image size</param>
        /// <param name="voxelsPerMeter">The reconstruction volume voxel density in voxels per meter (vpm)</param>
        /// <param name="voxelResolution">
        /// The reconstruction volume voxel resolution
        /// X: wide
        /// Y: high
        /// Z: deep
        /// Depends on the value set for VoxelsPerMeter (vpm)
        /// If vpm = 256 and X = 512, then 2m wide
        /// </param>
        public Reconstructor(Sensor sensor, DepthImageSize depthImageSize, int voxelsPerMeter, Vector3 voxelResolution)
        {
            this.voxelsPerMeter = voxelsPerMeter;

            // Allocate space to put the color pixels we'll create
            colorPixels = new int[sensor.FrameDataLength];

            // This is the bitmap we'll display on-screen
            ColorBitmap = new WriteableBitmap(
                depthImageSize.Width,
                depthImageSize.Height,
                96.0,
                96.0,
                PixelFormats.Bgr32,
                null);

            var volParam = new ReconstructionParameters(voxelsPerMeter, (int)voxelResolution.X, (int)voxelResolution.Y, (int)voxelResolution.Z);

            // Set the world-view transform to identity, so the world origin is the initial camera location.
            worldToCameraTransform = Matrix4.Identity;

            try
            {
                // This creates a volume cube with the Kinect at center of near plane, and volume directly
                // in front of Kinect.
                Volume = Reconstruction.FusionCreateReconstruction(volParam, ProcessorType, DeviceToUse, worldToCameraTransform);

                defaultWorldToVolumeTransform = Volume.GetCurrentWorldToVolumeTransform();

                if (translateResetPoseByMinDepthThreshold)
                {
                    // Reset the reconstruction if we need to add a custom world-volume transformation
                    ResetReconstruction();
                }
            }
            catch (InvalidOperationException ex)
            {
                OnError(new ErrorEventArgs(ex.Message));
                return;
            }
            catch (DllNotFoundException)
            {
                OnError(new ErrorEventArgs(Properties.Resources.NoDirectX11CompatibleDeviceOrInvalidDeviceIndex));
                return;
            }

            // Depth frames generated from the depth input
            depthFloatBuffer = new FusionFloatImageFrame(depthImageSize.Width, depthImageSize.Height);

            // Point cloud frames generated from the depth float input
            pointCloudBuffer = new FusionPointCloudImageFrame(depthImageSize.Width, depthImageSize.Height);

            // Create images to raycast the Reconstruction Volume
            shadedSurfaceColorFrame = new FusionColorImageFrame(depthImageSize.Width, depthImageSize.Height);

            int depthImageArraySize = depthImageSize.Width * depthImageSize.Height;

            // Create local depth pixels buffer
            depthImagePixels = new DepthImagePixel[depthImageArraySize];
        }

        /// <summary>
        /// Check to ensure suitable DirectX11 compatible hardware exists before initializing Kinect Fusion
        /// </summary>
        /// <returns>Empty string if found, error message otherwise</returns>
        public static string VerifySuitableDirect11CompatibleHardwareExists()
        {
            try
            {
                string deviceDescription = string.Empty;
                string deviceInstancePath = string.Empty;
                int deviceMemory = 0;

                FusionDepthProcessor.GetDeviceInfo(ProcessorType, DeviceToUse, out deviceDescription, out deviceInstancePath, out deviceMemory);
            }
            catch (IndexOutOfRangeException)
            {
                // Thrown when index is out of range for processor type or there is no DirectX11 capable device installed.
                // As we set -1 (auto-select default) for the DeviceToUse above, this indicates that there is no DirectX11 
                // capable device. The options for users in this case are to either install a DirectX11 capable device 
                // (see documentation for recommended GPUs) or to switch to non-real-time CPU based reconstruction by 
                // changing ProcessorType to ReconstructionProcessor.Cpu
                return Properties.Resources.NoDirectX11CompatibleDeviceOrInvalidDeviceIndex;
            }
            catch (DllNotFoundException)
            {
                return Properties.Resources.NoDirectX11CompatibleDeviceOrInvalidDeviceIndex;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }

            return string.Empty;
        }

        /// <summary>
        /// Reset the reconstruction to initial value
        /// </summary>
        public void ResetReconstruction()
        {
            // Set the world-view transform to identity, so the world origin is the initial camera location.
            worldToCameraTransform = Matrix4.Identity;

            if (Volume != null)
            {
                // Translate the reconstruction volume location away from the world origin by an amount equal
                // to the minimum depth threshold. This ensures that some depth signal falls inside the volume.
                // If set false, the default world origin is set to the center of the front face of the 
                // volume, which has the effect of locating the volume directly in front of the initial camera
                // position with the +Z axis into the volume along the initial camera direction of view.
                if (translateResetPoseByMinDepthThreshold)
                {
                    Matrix4 worldToVolumeTransform = defaultWorldToVolumeTransform;

                    // Translate the volume in the Z axis by the minDepthThreshold distance
                    float minDist = (minDepthClip < maxDepthClip) ? minDepthClip : maxDepthClip;
                    worldToVolumeTransform.M43 -= minDist * voxelsPerMeter;

                    Volume.ResetReconstruction(worldToCameraTransform, worldToVolumeTransform);
                }
                else
                {
                    Volume.ResetReconstruction(worldToCameraTransform);
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's AllFramesReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        public void SensorFramesReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            // Here we will drop a frame if we are still processing the last one
            if (!processingFrame)
            {
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    if (depthFrame != null)
                    {
                        // Copy the depth pixel data from the image to a buffer
                        depthFrame.CopyDepthImagePixelDataTo(depthImagePixels);

                        // Mark that one frame will be processed
                        processingFrame = true;

                        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => ProcessDepthData()));
                    }
                }
            }
        }

        /// <summary>
        /// Process the depth input
        /// </summary>
        /// <returns>null if everything is fine, an error message if error</returns>
        private void ProcessDepthData()
        {
            Debug.Assert(Volume != null, Properties.Resources.VolumeNull);
            Debug.Assert(shadedSurfaceColorFrame != null, Properties.Resources.ShadedSurfaceNull);
            Debug.Assert(ColorBitmap != null, Properties.Resources.ColorBitmapNull);

            try
            {
                // Convert the depth image frame to depth float image frame
                Volume.DepthToDepthFloatFrame(
                    depthImagePixels,
                    depthFloatBuffer,
                    FusionDepthProcessor.DefaultMinimumDepth,
                    FusionDepthProcessor.DefaultMaximumDepth,
                    false);

                // Use this to smooth each frame (check last 2 params)
                // volume.SmoothDepthFloatFrame(depthFloatBuffer, depthFloatBuffer, 1, 0.1f);

                // ProcessFrame will first calculate the camera pose and then integrate
                // if tracking is successful
                bool trackingSucceeded = false;
                trackingSucceeded = Volume.ProcessFrame(
                    depthFloatBuffer,
                    FusionDepthProcessor.DefaultAlignIterationCount,
                    FusionDepthProcessor.DefaultIntegrationWeight,
                    Volume.GetCurrentWorldToCameraTransform());

                // If camera tracking failed, no data integration or raycast for reference
                // point cloud will have taken place, and the internal camera pose
                // will be unchanged.
                if (!trackingSucceeded)
                {
                    OnError(new ErrorEventArgs(Properties.Resources.CameraTrackingFailed));
                }
                else
                {
                    Matrix4 calculatedCameraPose = Volume.GetCurrentWorldToCameraTransform();

                    // Set the camera pose and reset tracking errors
                    worldToCameraTransform = calculatedCameraPose;
                }
                // Calculate the point cloud
                Volume.CalculatePointCloud(pointCloudBuffer, worldToCameraTransform);

                // Shade point cloud and render
                FusionDepthProcessor.ShadePointCloud(
                    pointCloudBuffer, worldToCameraTransform, shadedSurfaceColorFrame, null);

                shadedSurfaceColorFrame.CopyPixelDataTo(colorPixels);

                // Write the pixel data into our bitmap
                ColorBitmap.WritePixels(
                    new Int32Rect(0, 0, ColorBitmap.PixelWidth, ColorBitmap.PixelHeight),
                    colorPixels,
                    ColorBitmap.PixelWidth * sizeof(int),
                    0);

                OnFrameProcessed(new EventArgs());
            }
            catch (InvalidOperationException ex)
            {
                OnError(new ErrorEventArgs(ex.Message));
            }
            finally
            {
                processingFrame = false;
            }
        }

        public void Dispose()
        {
            depthFloatBuffer?.Dispose();
            pointCloudBuffer?.Dispose();
            shadedSurfaceColorFrame?.Dispose();
            Volume?.Dispose();
        }

        protected virtual void OnError(ErrorEventArgs e)
        {
            ErrorEvent?.Invoke(this, e);
        }

        protected virtual void OnFrameProcessed(EventArgs e)
        {
            FrameProcessed?.Invoke(this, e);
        }
    }
}
