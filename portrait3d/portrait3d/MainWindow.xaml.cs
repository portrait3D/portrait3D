using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Fusion;

#nullable enable
namespace Portrait3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable, INotifyPropertyChanged
    {
        /// <summary>
        /// The resolution of the depth image to be processed.
        /// </summary>
        private DepthImageSize depthImageSize = new DepthImageSize(DepthImageFormat.Resolution640x480Fps30);

        /// <summary>
        /// Used to reconstruct what the sensor sees
        /// </summary>
        private Reconstructor reconstructor;

        /// <summary>
        /// Used to calculate the number of frames processed per second
        /// </summary>
        private FPS fps = new FPS(5);

        private Sensor sensor;

        /// <summary>
        /// Track whether Dispose has been called
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Tracks if the sensor is currently running
        /// </summary>
        private bool isRunning = false;

        /// <summary>
        /// The reconstruction volume voxel density in voxels per meter (vpm)
        /// 1000mm / 256vpm = ~3.9mm/voxel
        /// </summary>
        private int voxelsPerMeter = 640;

        /// <summary>
        /// The reconstruction volume voxel resolution in the X axis
        /// At a setting of 256vpm the volume is 512 / 256 = 2m wide
        /// </summary>
        private int voxelsX = 640;

        /// <summary>
        /// The reconstruction volume voxel resolution in the Y axis
        /// At a setting of 256vpm the volume is 384 / 256 = 1.5m high
        /// </summary>
        private int voxelsY = 512;

        /// <summary>
        /// The reconstruction volume voxel resolution in the Z axis
        /// At a setting of 256vpm the volume is 512 / 256 = 2m deep
        /// </summary>
        private int voxelsZ = 640;

        /// <summary>
        /// Property change event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Binding property to voxels per meter slider
        /// </summary>
        public double VoxelsPerMeter
        {
            get
            {
                return (double)this.voxelsPerMeter;
            }

            set
            {
                this.voxelsPerMeter = (int)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsPerMeter"));
                }
            }
        }

        /// <summary>
        /// Binding property to X-axis volume resolution slider
        /// </summary>
        public double VoxelsX
        {
            get
            {
                return (double)this.voxelsX;
            }

            set
            {
                this.voxelsX = (int)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsX"));
                }
            }
        }

        /// <summary>
        /// Binding property to Y-axis volume resolution slider
        /// </summary>
        public double VoxelsY
        {
            get
            {
                return (double)this.voxelsY;
            }

            set
            {
                this.voxelsY = (int)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsY"));
                }
            }
        }

        /// <summary>
        /// Binding property to Z-axis volume resolution slider
        /// </summary>
        public double VoxelsZ
        {
            get
            {
                return (double)this.voxelsZ;
            }

            set
            {
                this.voxelsZ = (int)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsZ"));
                }
            }
        }

        /// <summary>
        /// Binding property to Z-axis volume resolution slider
        /// </summary>
        public Boolean NotIsRunning
        {
            get
            {
                return !this.isRunning;
            }

            set
            {
                this.isRunning = !value;
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("NotIsRunning"));
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Finalizes an instance of the MainWindow class.
        /// This destructor will run only if the Dispose method does not get called.
        /// </summary>
        ~MainWindow()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose the allocated frame buffers and reconstruction.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees all memory associated with the FusionImageFrame.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                reconstructor?.Dispose();
                disposed = true;
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Set default blank image for Image component in case we don't have a Kinect so it doesn't show the background
            Image.Source = new WriteableBitmap(
                depthImageSize.Width,
                depthImageSize.Height,
                96.0,
                96.0,
                PixelFormats.Bgr32,
                null);

            string msg = Reconstructor.VerifySuitableDirect11CompatibleHardwareExists();
            if (msg != string.Empty)
            {
                StatusBarText.Text = msg;
                return;
            }

            sensor = new Sensor(depthImageSize);
            if (!sensor.SensorConnected())
            {
                StatusBarText.Text = Properties.Resources.NoKinectReady;
                return;
            }

            initiliazeReconstructor();
        }

        /// <summary>
        /// Initialize the reconstructor and event handlers and link the output image to the application Image
        /// </summary>
        private void initiliazeReconstructor()
        {
            if (reconstructor != null)
            {
                reconstructor.FrameProcessed -= Reconstructor_FrameProcessed;
                reconstructor.ErrorEvent -= Reconstructor_ErrorEvent;
                sensor.DepthFrameReady -= reconstructor.SensorFramesReady;
                reconstructor.Dispose();
            }
            reconstructor = null;
            reconstructor = new Reconstructor(sensor, depthImageSize, voxelsPerMeter, new Vector3(voxelsX, voxelsY, voxelsZ));
            reconstructor.FrameProcessed += Reconstructor_FrameProcessed;
            reconstructor.ErrorEvent += Reconstructor_ErrorEvent;

            // Set the image we display to point to the bitmap where we'll put the image data
            Image.Source = reconstructor.ColorBitmap;

            // Add an event handler to be called whenever depth has new data
            sensor.DepthFrameReady += reconstructor.SensorFramesReady;
        }

        /// <summary>
        /// When an error happens, show the message in the status bar
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reconstructor_ErrorEvent(object sender, Reconstructor.ErrorEventArgs e)
        {
            StatusBarText.Text = e.Message;
        }

        /// <summary>
        /// When a frame is processed, increment the FPS manager's frame count to update fps
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reconstructor_FrameProcessed(object sender, EventArgs e)
        {
            // The input frame was processed successfully, increase the processed frame count
            fps.AddFrame();
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            fps.Stop();
            sensor.Stop();
        }

        /// <summary>
        /// Reset the reconstruction to initial value
        /// </summary>
        private void ResetReconstruction()
        {
            reconstructor.ResetReconstruction();
            fps.Restart();
        }

        /// <summary>
        /// Handles the user clicking on the reset reconstruction button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ButtonResetReconstructionClick(object sender, RoutedEventArgs e)
        {
            if (!sensor.SensorConnected())
            {
                StatusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            // reset the reconstruction and update the status text
            ResetReconstruction();
            StatusBarText.Text = Properties.Resources.ResetReconstruction;

            ButtonExport.IsEnabled = false;
        }

        /// <summary>
        /// Handles fps value changed
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Fps_FPSChanged(object sender, EventArgs e)
        {
            StatusBarText.Text = fps.ToString();
        }

        /// <summary>
        /// Starts the sensor, FPS manager and update button manageability and text
        /// Shows the error message if an error occurs during the start process
        /// </summary>
        private void StartSensor()
        {
            string? msg = sensor.Start();
            if (msg != null)
            {
                StatusBarText.Text = msg;
                return;
            }

            fps.FPSChanged += Fps_FPSChanged;
            fps.Start();

           ButtonExport.IsEnabled = false;

            StartStopControl.Content = "Arrêter";
            NotIsRunning = false;
        }

        /// <summary>
        /// Stops the sensor, FPS manager and update button manageability and text
        /// Show the error message if an error occus during the stop process
        /// </summary>
        private void StopSensor()
        {
            string? msg = sensor.Stop();
            if (msg != null)
            {
                StatusBarText.Text = msg;
                return;
            }

            fps.Stop();

            ButtonExport.IsEnabled = true;

            StartStopControl.Content = "Démarrer";
            NotIsRunning = true;
        }

        /// <summary>
        /// Handles the user clicking on the start/stop button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void StartStopToggle(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                StopSensor();
            }
            else
            {
                StartSensor();
            }

            ButtonExport.IsEnabled = true;
        }

        /// <summary>
        /// Handles the user clicking on the export model button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Export(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() => Exporter.ExportMeshToFile(reconstructor.Volume.CalculateMesh(1)));
        }

        private void changedValue(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

        }

        /// <summary>
        /// Handles the user clicking the open export folder button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OpenExportFolder(object sender, RoutedEventArgs e)
        {
            Exporter.OpenExportFolder();
        }

        /// <summary>
        /// Handler for volume setting changing event
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event argument</param>
        private void VolumeSettingsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            if (reconstructor != null)
            {
                initiliazeReconstructor();
            }
        }
    }
}