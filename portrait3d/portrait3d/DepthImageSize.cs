using Microsoft.Kinect;
using System;
using System.Windows;

namespace Portrait3D
{
    class DepthImageSize
    {
        /// <summary>
        /// The resolution of the depth image to be processed.
        /// </summary>
        public DepthImageFormat depthFormat { get; private set; }

        /// <summary>
        /// Image Width of depth frame
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Image height of depth frame
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// The resolution of the depth image to be processed.
        /// </summary>
        /// <param name="depthFormat">The depth image format.</param>
        public DepthImageSize(DepthImageFormat depthFormat)
        {
            this.depthFormat = depthFormat;
            UpdateWidthAndHeight();
        }

        /// <summary>
        /// Update width and height using depthFormat
        /// </summary>
        private void UpdateWidthAndHeight()
        {
            Width = (int)GetImageSize().Width;
            Height = (int)GetImageSize().Height;
        }

        /// <summary>
        /// Get the depth image size from the input depth image format.
        /// </summary>
        /// <returns>The widht and height of the input depth image format.</returns>
        private Size GetImageSize()
        {
            switch (depthFormat)
            {
                case DepthImageFormat.Resolution320x240Fps30:
                    return new Size(320, 240);

                case DepthImageFormat.Resolution640x480Fps30:
                    return new Size(640, 480);

                case DepthImageFormat.Resolution80x60Fps30:
                    return new Size(80, 60);
            }

            throw new ArgumentOutOfRangeException("imageFormat");
        }
    }
}
