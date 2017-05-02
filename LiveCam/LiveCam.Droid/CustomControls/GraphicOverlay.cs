
/* 
 * Ported to C# from https://github.com/googlesamples/android-vision/tree/master/visionSamples/FaceTracker
 * Ported by Nish Anil (Nish@microsoft.com)
 * 
 * Copyright (C) The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Gms.Vision;
using Android.Util;
using Android.Graphics;

namespace LiveCam.Droid
{
    /**
     * A view which renders a series of custom graphics to be overlayed on top of an associated preview
     * (i.e., the camera preview).  The creator can add graphics objects, update the objects, and remove
     * them, triggering the appropriate drawing and invalidation within the view.<p>
     *
     * Supports scaling and mirroring of the graphics relative the camera's preview properties.  The
     * idea is that detection items are expressed in terms of a preview size, but need to be scaled up
     * to the full view size, and also mirrored in the case of the front-facing camera.<p>
     *
     * Associated {@link Graphic} items should use the following methods to convert to view coordinates
     * for the graphics that are drawn:
     * <ol>
     * <li>{@link Graphic#scaleX(float)} and {@link Graphic#scaleY(float)} adjust the size of the
     * supplied value from the preview scale to the view scale.</li>
     * <li>{@link Graphic#translateX(float)} and {@link Graphic#translateY(float)} adjust the coordinate
     * from the preview's coordinate system to the view coordinate system.</li>
     * </ol>
     */
    public class GraphicOverlay : View
    {
        private Object mLock = new Object();
        private int mPreviewWidth;
        private float mWidthScaleFactor = 1.0f;
        private int mPreviewHeight;
        private float mHeightScaleFactor = 1.0f;
        private CameraFacing mFacing = CameraFacing.Front;
        private HashSet<Graphic> mGraphics = new HashSet<Graphic>();

        public int PreviewWidth { get => mPreviewWidth; set => mPreviewWidth = value; }
        public float WidthScaleFactor { get => mWidthScaleFactor; set => mWidthScaleFactor = value; }
        public int PreviewHeight { get => mPreviewHeight; set => mPreviewHeight = value; }
        public float HeightScaleFactor { get => mHeightScaleFactor; set => mHeightScaleFactor = value; }
        public CameraFacing CameraFacing { get => mFacing; set => mFacing = value; }

        public GraphicOverlay(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            
        }

        /// <summary>
        /// Removes all graphics from the overlay.
        /// </summary>
        public void Clear()
        {
            lock(mLock) {
                mGraphics.Clear();
            }
            PostInvalidate();
        }

        /// <summary>
        /// Adds a graphic to the overlay.
        /// </summary>
        /// <param name="graphic"></param>
        public void Add(Graphic graphic)
        {
            lock(mLock) {
                mGraphics.Add(graphic);
            }
            PostInvalidate();
        }

        /// <summary>
        /// Removes a graphic from the overlay.
        /// </summary>
        /// <param name="graphic"></param>
        public void Remove(Graphic graphic)
        {
            lock(mLock) {
                mGraphics.Remove(graphic);
            }
            PostInvalidate();
        }
       
        /// <summary>
        ///  Sets the camera attributes for size and facing direction, which informs how to transform image coordinates later.
        /// </summary>
        /// <param name="previewWidth"></param>
        /// <param name="previewHeight"></param>
        /// <param name="facing"></param>
        public void SetCameraInfo(int previewWidth, int previewHeight, CameraFacing facing)
        {
            lock(mLock) {
                PreviewWidth = previewWidth;
                PreviewHeight = previewHeight;
                CameraFacing = facing;
            }
            PostInvalidate();
        }

        public override void Draw(Canvas canvas)
        {
            base.Draw(canvas);
            lock(mLock) {
                if ((PreviewWidth != 0) && (PreviewHeight != 0))
                {
                    WidthScaleFactor = (float)canvas.Width / (float)PreviewWidth;
                    HeightScaleFactor = (float)canvas.Height / (float)PreviewHeight;
                }

                foreach (Graphic graphic in mGraphics)
                {
                    graphic.Draw(canvas);
                }
            }
        }
    }

    /**
     * Base class for a custom graphics object to be rendered within the graphic overlay.  Subclass
     * this and implement the {@link Graphic#draw(Canvas)} method to define the
     * graphics element.  Add instances to the overlay using {@link GraphicOverlay#add(Graphic)}.
     */
    public abstract class Graphic
    {
        private GraphicOverlay mOverlay;

        public Graphic(GraphicOverlay overlay)
        {
            mOverlay = overlay;
        }

        /**
         * Draw the graphic on the supplied canvas.  Drawing should use the following methods to
         * convert to view coordinates for the graphics that are drawn:
         * <ol>
         * <li>{@link Graphic#scaleX(float)} and {@link Graphic#scaleY(float)} adjust the size of
         * the supplied value from the preview scale to the view scale.</li>
         * <li>{@link Graphic#translateX(float)} and {@link Graphic#translateY(float)} adjust the
         * coordinate from the preview's coordinate system to the view coordinate system.</li>
         * </ol>
         *
         * @param canvas drawing canvas
         */
        public abstract void Draw(Canvas canvas);

        /**
         * Adjusts a horizontal value of the supplied value from the preview scale to the view
         * scale.
         */
        public float ScaleX(float horizontal)
        {
            return horizontal * mOverlay.WidthScaleFactor;
        }

        /**
         * Adjusts a vertical value of the supplied value from the preview scale to the view scale.
         */
        public float ScaleY(float vertical)
        {
            return vertical * mOverlay.HeightScaleFactor;
        }

        /**
         * Adjusts the x coordinate from the preview's coordinate system to the view coordinate
         * system.
         */
        public float TranslateX(float x)
        {
            if (mOverlay.CameraFacing == CameraFacing.Front)
            {
                return mOverlay.Width - ScaleX(x);
            }
            else
            {
                return ScaleX(x);
            }
        }

        /**
         * Adjusts the y coordinate from the preview's coordinate system to the view coordinate
         * system.
         */
        public float TranslateY(float y)
        {
            return ScaleY(y);
        }

        public void PostInvalidate()
        {
            mOverlay.PostInvalidate();
        }
    }
}