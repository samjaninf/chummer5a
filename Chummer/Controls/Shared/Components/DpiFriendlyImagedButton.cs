/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Chummer
{
    public class DpiFriendlyImagedButton : Button
    {
        public void RefreshImage()
        {
            if (Utils.IsDesignerMode || Utils.IsRunningInVisualStudio)
                return;
            List<Image> lstImages = Images.ToList();
            switch (lstImages.Count)
            {
                case 0:
                    Image = null;
                    return;

                case 1:
                    Image = lstImages[0];
                    return;
            }

            Image objBestImage = null;

            if (AutoSize && string.IsNullOrWhiteSpace(Text))
            {
                // Image-only button that auto-sizes means we should scale things up only if we are large enough
                using (Graphics g = CreateGraphics())
                {
                    float fltScalingRatio = Convert.ToSingle(Math.Sqrt(g.DpiX * g.DpiY / (96.0d * 96.0d)));
                    if (fltScalingRatio >= 4.0f)
                        objBestImage = ImageDpi384;
                    if (objBestImage == null && fltScalingRatio >= 3.0f)
                        objBestImage = ImageDpi288;
                    if (objBestImage == null && fltScalingRatio >= 2.0f)
                        objBestImage = ImageDpi192;
                    if (objBestImage == null && fltScalingRatio >= 1.5f)
                        objBestImage = ImageDpi144;
                    if (objBestImage == null && fltScalingRatio >= 1.25f)
                        objBestImage = ImageDpi120;
                    if (objBestImage == null)
                        objBestImage = ImageDpi96;
                }
            }
            else
            {
                // Buttons contain both images and text, so we take the smallest of the two dimensions for the image and then assume that the image should be square-shaped
                int intWidth = Math.Max(Math.Min(PreferredSize.Width, PreferredSize.Height), Math.Min(Width, Height));
                int intHeight = intWidth;
                intWidth -= Padding.Left + Padding.Right;
                intHeight -= Padding.Top + Padding.Bottom;
                int intBestImageMetric = int.MaxValue;
                foreach (Image objLoopImage in lstImages)
                {
                    int intLoopMetric = (intHeight - objLoopImage.Height).Pow(2) +
                                        (intWidth - objLoopImage.Width).Pow(2);
                    // Small biasing so that in case of a tie, the image that gets picked is the one that would be scaled down, not scaled up
                    if (objLoopImage.Height >= intHeight)
                        --intLoopMetric;
                    if (objLoopImage.Width >= intWidth)
                        --intLoopMetric;
                    if (objBestImage == null || intLoopMetric < intBestImageMetric)
                    {
                        objBestImage = objLoopImage;
                        intBestImageMetric = intLoopMetric;
                    }
                }
            }

            Image = objBestImage;
        }

        public new Image Image
        {
            get => base.Image;
            set
            {
                if (Utils.IsDesignerMode || Utils.IsRunningInVisualStudio)
                    ImageDpi96 = value;
                base.Image = value;
            }
        }

        private IEnumerable<Image> Images
        {
            get
            {
                if (ImageDpi96 != null)
                    yield return ImageDpi96;
                if (ImageDpi120 != null)
                    yield return ImageDpi120;
                if (ImageDpi144 != null)
                    yield return ImageDpi144;
                if (ImageDpi192 != null)
                    yield return ImageDpi192;
                if (ImageDpi288 != null)
                    yield return ImageDpi288;
                if (ImageDpi384 != null)
                    yield return ImageDpi384;
            }
        }

        private Image _objImageDpi96;

        private Image _objImageDpi120;

        private Image _objImageDpi144;

        private Image _objImageDpi192;

        private Image _objImageDpi288;

        private Image _objImageDpi384;

        public Image ImageDpi96
        {
            get => _objImageDpi96;
            set
            {
                Image objOldImage = Interlocked.Exchange(ref _objImageDpi96, value);
                if (objOldImage == value)
                    return;
                if (Utils.IsDesignerMode || Utils.IsRunningInVisualStudio)
                    base.Image = value;
                else
                    UpdateImageIfBetterMatch(value, objOldImage, 1.0f);
            }
        }

        public Image ImageDpi120
        {
            get => _objImageDpi120;
            set
            {
                Image objOldImage = Interlocked.Exchange(ref _objImageDpi120, value);
                if (objOldImage == value)
                    return;
                UpdateImageIfBetterMatch(value, objOldImage, 1.25f);
            }
        }

        public Image ImageDpi144
        {
            get => _objImageDpi144;
            set
            {
                Image objOldImage = Interlocked.Exchange(ref _objImageDpi144, value);
                if (objOldImage == value)
                    return;
                UpdateImageIfBetterMatch(value, objOldImage, 1.5f);
            }
        }

        public Image ImageDpi192
        {
            get => _objImageDpi192;
            set
            {
                Image objOldImage = Interlocked.Exchange(ref _objImageDpi192, value);
                if (objOldImage == value)
                    return;
                UpdateImageIfBetterMatch(value, objOldImage, 2.0f);
            }
        }

        public Image ImageDpi288
        {
            get => _objImageDpi288;
            set
            {
                Image objOldImage = Interlocked.Exchange(ref _objImageDpi288, value);
                if (objOldImage == value)
                    return;
                UpdateImageIfBetterMatch(value, objOldImage, 3.0f);
            }
        }

        public Image ImageDpi384
        {
            get => _objImageDpi384;
            set
            {
                Image objOldImage = Interlocked.Exchange(ref _objImageDpi384, value);
                if (objOldImage == value)
                    return;
                UpdateImageIfBetterMatch(value, objOldImage, 4.0f);
            }
        }

        public void BatchSetImages(Image imgDpi96, Image imgDpi120, Image imgDpi144, Image imgDpi192, Image imgDpi288,
            Image imgDpi384)
        {
            _objImageDpi96 = imgDpi96;
            _objImageDpi120 = imgDpi120;
            _objImageDpi144 = imgDpi144;
            _objImageDpi192 = imgDpi192;
            _objImageDpi288 = imgDpi288;
            _objImageDpi384 = imgDpi384;

            if (Utils.IsDesignerMode || Utils.IsRunningInVisualStudio)
                base.Image = imgDpi96;
            else
                RefreshImage();
        }

        /// <summary>
        /// Checks a newly set image against the existing image of the button to see if it's a better fit than the current image.
        /// Only use this with images that are one of the ones set for this button!
        /// </summary>
        /// <param name="objNewImage"></param>
        /// <param name="objImageToReplace"></param>
        /// <param name="fltNewImageIntendedScaling"></param>
        private void UpdateImageIfBetterMatch(Image objNewImage, Image objImageToReplace, float fltNewImageIntendedScaling)
        {
            if (Utils.IsDesignerMode || Utils.IsRunningInVisualStudio)
                return;
            if (Image == objImageToReplace)
            {
                Image = objNewImage;
                return;
            }
            if (objNewImage == null)
                return;
            if (Image == null)
            {
                Image = objNewImage;
                return;
            }

            if (AutoSize && string.IsNullOrWhiteSpace(Text))
            {
                // Image-only button that auto-sizes means we should scale things up only if we are large enough
                using (Graphics g = CreateGraphics())
                {
                    float fltScalingRatio = Convert.ToSingle(Math.Sqrt(g.DpiX * g.DpiY / (96.0d * 96.0d)));
                    if (fltNewImageIntendedScaling > fltScalingRatio)
                        return;
                }
                float fltCurrentIntendedScaling;
                if (Image == ImageDpi384)
                    fltCurrentIntendedScaling = 4.0f;
                else if (Image == ImageDpi288)
                    fltCurrentIntendedScaling = 3.0f;
                else if (Image == ImageDpi192)
                    fltCurrentIntendedScaling = 3.0f;
                else if (Image == ImageDpi144)
                    fltCurrentIntendedScaling = 1.5f;
                else if (Image == ImageDpi120)
                    fltCurrentIntendedScaling = 1.25f;
                else
                    fltCurrentIntendedScaling = 1.0f;
                if (fltNewImageIntendedScaling > fltCurrentIntendedScaling)
                    Image = objNewImage;
                return;
            }

            // Toolstrip items contain both images and text, so we take the smallest of the two dimensions for the image and then assume that the image should be square-shaped
            int intWidth = Math.Max(Math.Min(PreferredSize.Width, PreferredSize.Height), Math.Min(Width, Height));
            int intHeight = intWidth;
            intWidth -= Padding.Left + Padding.Right;
            intHeight -= Padding.Top + Padding.Bottom;
            int intCurrentMetric = (intHeight - Image.Height).Pow(2) +
                                   (intWidth - Image.Width).Pow(2);
            int intNewMetric = (intHeight - objNewImage.Height).Pow(2) +
                               (intWidth - objNewImage.Width).Pow(2);
            if (intNewMetric < intCurrentMetric)
                Image = objNewImage;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RefreshImage();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            RefreshImage();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RefreshImage();
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            RefreshImage();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            if (AutoSize && string.IsNullOrWhiteSpace(Text))
                RefreshImage();
        }
    }
}
