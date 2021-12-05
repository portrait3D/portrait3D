using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Fusion;

#nullable enable
namespace Portrait3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
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
        /// Precision factor for the reconstruction settings
        /// </summary>
        private int precisionFactor = 1;

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

            reconstructor = new Reconstructor(sensor, depthImageSize, 256, new Vector3(256 * precisionFactor, 128 * precisionFactor, 256 * precisionFactor));
            reconstructor.FrameProcessed += Reconstructor_FrameProcessed;
            reconstructor.ErrorEvent += Reconstructor_ErrorEvent;

            // Set the image we display to point to the bitmap where we'll put the image data
            Image.Source = reconstructor.ColorBitmap;

            // Add an event handler to be called whenever depth has new data
            sensor.DepthFrameReady += reconstructor.SensorFramesReady;
        }

        private void Reconstructor_ErrorEvent(object sender, Reconstructor.ErrorEventArgs e)
        {
            StatusBarText.Text = e.Message;
        }

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

            StartStopControl.Content = "Stop";
            isRunning = !isRunning;
        }

        private void StopSensor()
        {
            string? msg = sensor.Stop();
            if (msg != null)
            {
                StatusBarText.Text = msg;
                return;
            }

            fps.Stop();

            StartStopControl.Content = "Start";
            isRunning = !isRunning;
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

        /// <summary>
        /// Handles the user clicking the open export folder button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OpenExportFolder(object sender, RoutedEventArgs e)
        {
            Exporter.CreateExportFolderIfInexistant();
            Process.Start(Exporter.DirectoryPath);
        }

        /// <summary>
        /// Handles the change of value of the precision input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void changedValue(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            precisionFactor = (int)PrecisionSelector.Value;
        }
    }
}