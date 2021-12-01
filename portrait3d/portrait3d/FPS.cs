using System;
using System.Windows.Threading;

namespace Portrait3D
{
    /// <summary>
    /// Class used to calculate fps
    /// </summary>
    public class FPS
    {
        /// <summary>
        /// The interval in seconds to calculate FPS
        /// </summary>
        private readonly int interval;

        /// <summary>
        /// The timer to calculate FPS
        /// </summary>
        private DispatcherTimer timer;

        /// <summary>
        /// Timestamp of last computation of FPS
        /// </summary>
        private DateTime lastFPSTimestamp;

        /// <summary>
        /// The count of the frames in the FPS interval
        /// </summary>
        private double frameCount = 0d;

        /// <summary>
        /// The count of the frames in the FPS interval
        /// </summary>
        private double frameRate = 0d;

        /// <summary>
        /// Frame rate changed handler
        /// </summary>
        public event EventHandler FPSChanged;

        /// <summary>
        /// Count frames in an interval
        /// </summary>
        /// <param name="interval">The interval in seconds to calculate FPS</param>
        public FPS(int interval) => this.interval = interval;

        /// <summary>
        /// Start to calculate fps
        /// </summary>
        public void Start()
        {
            ResetFrameCounter();

            // Initialize and start the FPS timer
            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(FpsTimerTick);
            timer.Interval = new TimeSpan(0, 0, interval);

            timer.Start();
        }

        /// <summary>
        /// Stop to calculate fps
        /// </summary>
        public void Stop()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            lastFPSTimestamp = DateTime.MinValue;
            frameRate = 0d;
        }

        /// <summary>
        /// Restart by calling Stop() then Start()
        /// </summary>
        public void Restart()
        {
            Stop();
            Start();
        }

        /// <summary>
        /// Add frame to count
        /// </summary>
        public void AddFrame()
        {
            frameCount++;
        }

        /// <summary>
        /// Invoke handler on fps changed
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnFPSChanged(EventArgs e)
        {
            FPSChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Return the frame rate formated
        /// </summary>
        public override string ToString() => string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                Properties.Resources.Fps,
                frameRate);

        /// <summary>
        /// Handler for FPS timer tick
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void FpsTimerTick(object sender, EventArgs e)
        {
            // Calculate time span from last calculation of FPS
            double intervalSeconds = (DateTime.UtcNow - lastFPSTimestamp).TotalSeconds;
            frameRate = frameCount / intervalSeconds;

            OnFPSChanged(new EventArgs());
            ResetFrameCounter();
        }

        /// <summary>
        /// Reset frame counter
        /// </summary>
        private void ResetFrameCounter()
        {
            frameCount = 0d;
            lastFPSTimestamp = DateTime.UtcNow;
        }
    }
}
