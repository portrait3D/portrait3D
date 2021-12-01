using Microsoft.Kinect;
using System;
using System.IO;

namespace Portrait3D
{

    #nullable enable
    /// <summary>
    /// Used to interact with a KinectSensor
    /// </summary>
    class Sensor
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor? sensor;

        /// <summary>
        /// The sensor depth frame data length
        /// </summary>
        public int FrameDataLength { get; private set; }

        public Sensor(DepthImageSize depthImageSize)
        {
            GetSensor(depthImageSize);
        }

        /// <summary>
        /// Look through all sensors and start the first connected one.
        /// This requires that a Kinect is connected at the time of app startup.
        /// </summary>
        private void GetSensor(DepthImageSize depthImageSize)
        {
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }

            if (sensor != null)
            {
                // Turn on the depth stream to receive frames
                sensor.DepthStream.Enable(depthImageSize.depthFormat);
                FrameDataLength = sensor.DepthStream.FramePixelDataLength;
                sensor.DepthStream.Range = DepthRange.Default;
            }
        }

        /// <summary>
        /// Verify a sensor is connected
        /// </summary>
        /// <returns>true if a sensor is connected, false otherwise</returns>
        public bool SensorConnected()
        {
            return sensor != null;
        }

        /// <summary>
        /// Passthrough to the KinectSensor's DepthFrameReady
        /// </summary>
        public event EventHandler<DepthImageFrameReadyEventArgs> DepthFrameReady
        {
            add { sensor.DepthFrameReady += value; }
            remove { sensor.DepthFrameReady -= value; }
        }

        /// <summary>
        /// Start the sensor
        /// </summary>
        /// <returns>null if everything is fine, an error message if error</returns>
        public string? Start()
        {
            // Start the sensor
            try
            {
                if (sensor == null)
                {
                    return Properties.Resources.NoKinectReady;
                }
                sensor.Start();
                sensor.ElevationAngle = 10;
            }
            catch (IOException ex)
            {
                // Device is in use
                sensor = null;
                return ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                // Device is not valid, not supported or hardware feature unavailable
                sensor = null;
                return ex.Message;
            }

            return null;
        }

        /// <summary>
        /// Stop the sensor
        /// </summary>
        /// <returns>null if everything is fine, an error message if error</returns>
        public string? Stop()
        {
            // Stop the sensor
            try
            {
                if (sensor != null)
                {
                    sensor.Stop();
                }
            }
            catch (IOException ex)
            {
                // Device is in use
                sensor = null;
                return ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                // Device is not valid, not supported or hardware feature unavailable
                sensor = null;
                return ex.Message;
            }

            return null;
        }
    }
}
