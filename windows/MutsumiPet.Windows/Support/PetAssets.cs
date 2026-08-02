using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MutsumiPet.Models;

namespace MutsumiPet.Support
{
    /// Decodes the shared PNG artwork that is embedded into the executable, and
    /// slices the three lifestyle sprite sheets into their four frames.
    public static class PetAssets
    {
        private const int FrameCount = 4;

        private static readonly Dictionary<PetPose, BitmapSource> Images =
            new Dictionary<PetPose, BitmapSource>();

        private static readonly Dictionary<PetActivity, BitmapSource[]> ActivityFrames =
            new Dictionary<PetActivity, BitmapSource[]>();

        static PetAssets()
        {
            foreach (PetPose pose in PetPoses.AllCases)
            {
                Images[pose] = Load(PetPoses.AssetName(pose));
            }

            foreach (PetActivity activity in PetActivities.Animated)
            {
                ActivityFrames[activity] = LoadStripFrames(PetActivities.StripAssetName(activity));
            }
        }

        public static BitmapSource Character(PetPose pose)
        {
            BitmapSource image;
            if (Images.TryGetValue(pose, out image)) return image;
            if (Images.TryGetValue(PetPose.Idle, out image)) return image;
            return Empty();
        }

        public static BitmapSource Character(PetActivity activity, int frame, PetPose fallback)
        {
            BitmapSource[] frames;
            if (ActivityFrames.TryGetValue(activity, out frames) == false || frames.Length == 0)
            {
                return Character(fallback);
            }

            int index = frame;
            if (index < 0) index = 0;
            if (index > frames.Length - 1) index = frames.Length - 1;
            return frames[index];
        }

        private static BitmapSource Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return Empty();

            try
            {
                using (Stream stream = typeof(PetAssets).Assembly.GetManifestResourceStream(name + ".png"))
                {
                    if (stream == null) return Empty();
                    BitmapFrame decoded = BitmapFrame.Create(
                        stream,
                        BitmapCreateOptions.None,
                        BitmapCacheOption.OnLoad);
                    decoded.Freeze();
                    return decoded;
                }
            }
            catch (NotSupportedException)
            {
                return Empty();
            }
            catch (IOException)
            {
                return Empty();
            }
        }

        private static BitmapSource[] LoadStripFrames(string name)
        {
            BitmapSource strip = Load(name);
            int frameWidth = strip.PixelWidth / FrameCount;
            if (frameWidth <= 0) return new BitmapSource[0];

            var frames = new List<BitmapSource>(FrameCount);
            for (int index = 0; index < FrameCount; index++)
            {
                var rectangle = new Int32Rect(index * frameWidth, 0, frameWidth, strip.PixelHeight);
                var cropped = new CroppedBitmap(strip, rectangle);
                cropped.Freeze();
                frames.Add(cropped);
            }
            return frames.ToArray();
        }

        private static BitmapSource Empty()
        {
            BitmapSource empty = BitmapSource.Create(
                1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 0 }, 4);
            empty.Freeze();
            return empty;
        }
    }
}
