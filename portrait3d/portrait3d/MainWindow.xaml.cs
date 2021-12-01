//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
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

        private bool isRunning = false;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
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
            string? msg = Reconstructor.VerifySuitableDirect11CompatibleHardwareExists();
            if (msg != null)
            {
                statusBarText.Text = msg;
                return;
            }

            sensor = new Sensor(depthImageSize);
            if (!sensor.SensorConnected())
            {
                statusBarText.Text = Properties.Resources.NoKinectReady;
                return;
            }

            reconstructor = new Reconstructor(sensor, depthImageSize, 256, new Vector3(256, 128, 256));
            reconstructor.FrameProcessed += Reconstructor_FrameProcessed;
            reconstructor.ErrorEvent += Reconstructor_ErrorEvent;

            // Set the image we display to point to the bitmap where we'll put the image data
            Image.Source = reconstructor.ColorBitmap;

            // Add an event handler to be called whenever depth has new data
            sensor.DepthFrameReady += reconstructor.SensorFramesReady;
        }

        private void Reconstructor_ErrorEvent(object sender, Reconstructor.ErrorEventArgs e)
        {
            statusBarText.Text = e.Message;
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
                statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            // reset the reconstruction and update the status text
            ResetReconstruction();
            statusBarText.Text = Properties.Resources.ResetReconstruction;
        }

        /// <summary>
        /// Handles fps value changed
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Fps_FPSChanged(object sender, EventArgs e)
        {
            statusBarText.Text = fps.ToString();
        }

        private void StartSensor()
        {
            string? msg = sensor.Start();
            if (msg != null)
            {
                statusBarText.Text = msg;
                return;
            }

            fps.FPSChanged += Fps_FPSChanged;
            fps.Start();

            Control.Content = "Stop";
            isRunning = !isRunning;
        }

        private void StopSensor()
        {
            string? msg = sensor.Stop();
            if (msg != null)
            {
                statusBarText.Text = msg;
                return;
            }

            fps.Stop();

            Control.Content = "Start";
            isRunning = !isRunning;
        }

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
        }
    }
}
