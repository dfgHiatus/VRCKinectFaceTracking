using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using ViveSR.anipal.Lip;
using VRCFaceTracking;

namespace Kinect
{
    public class KinectModule : ExtTrackingModule
    {
        private static CancellationTokenSource _cancellationToken;
        private MemoryMappedFile memMapFile;
        private MemoryMappedViewAccessor viewAccessor;
        private Process companionProcess;
        private FaceInfo faceInfo;
        public override (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            string exePath = Path.Combine("KinectCompanion.exe");
            if (!File.Exists(exePath))
            {
                Logger.Error("KinectCompanion executable wasn't found!");
                return (false, false);
            }
            companionProcess = new Process();
            companionProcess.StartInfo.FileName = exePath;
            companionProcess.Start();

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    memMapFile = MemoryMappedFile.OpenExisting("KinectFaceTracking");
                    viewAccessor = memMapFile.CreateViewAccessor();
                    Logger.Msg("Connected to Kinect 360 Sensor!");
                    return (true, true);
                }
                catch (FileNotFoundException)
                {
                    Logger.Warning($"Attempting to connect to the Kinect 360, attempt {i + 1}/5");
                }
                catch (Exception ex)
                {
                    Logger.Error("Could not open the mapped file: " + ex);
                    return (false, false);
                }
                Thread.Sleep(500);
            }

            return (false, false);
        }

        // This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
        public override Action GetUpdateThreadFunc()
        {
            _cancellationToken = new CancellationTokenSource();
            return () =>
            {
                while (true)
                {
                    Update();
                    Thread.Sleep(10);
                }
            };
        }

        public override void Update()
        {
            if (memMapFile == null) return;
            viewAccessor.Read(0, out faceInfo);

            UnifiedTrackingData.LatestEyeData.Left.Openness = 1f;
            UnifiedTrackingData.LatestEyeData.Right.Openness = 1f;
            UnifiedTrackingData.LatestEyeData.Combined.Openness = 1f;

            UnifiedTrackingData.LatestEyeData.Left.Widen = faceInfo.BrowRaiser;
            UnifiedTrackingData.LatestEyeData.Right.Widen = faceInfo.BrowRaiser;
            UnifiedTrackingData.LatestEyeData.Combined.Widen = faceInfo.BrowRaiser;

            UnifiedTrackingData.LatestEyeData.Left.Squeeze = faceInfo.BrowLower;
            UnifiedTrackingData.LatestEyeData.Right.Squeeze = faceInfo.BrowLower;
            UnifiedTrackingData.LatestEyeData.Combined.Squeeze = faceInfo.BrowLower;

            if (faceInfo.JawLower > 0.2f)
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.JawOpen] = faceInfo.JawLower;
            }
            else
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.JawOpen] = 0f;
            }
            
            // if (faceInfo.BrowLower > 0.1f && faceInfo.BrowRaiser > 0.05f)
            // {   // If the eyebrows are lowered, draw angry eyes
            // }
            // else if (faceInfo.LipStretcher < -0.1f && faceInfo.LipStretcher > 0.1f && faceInfo.LipCornerDepressor > 0.1f)
            // {   // If eyebrow up and mouth stretched, draw fearful eyes
            // }
            // else if (faceInfo.JawLower > 0.1f && faceInfo.LipStretcher < -0.1f)
            // {   // if eyebrow up and mouth open, draw big surprised eyes
            // }

            if ((faceInfo.LipStretcher - faceInfo.LipCornerDepressor) > 0.1f && faceInfo.LipCornerDepressor < 0)
            {   // If lips are stretched, assume smile and draw smily eyes
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSmileLeft] = Math.Min(faceInfo.LipStretcher - faceInfo.LipCornerDepressor, 1.0f) * 0.25f;
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSmileRight] = Math.Min(faceInfo.LipStretcher - faceInfo.LipCornerDepressor, 1.0f) * 0.25f;
            }
            else
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSmileLeft] = 0f;
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSmileRight] = 0f;
            }

            if ((faceInfo.LipStretcher - faceInfo.LipCornerDepressor) < 0 && faceInfo.BrowRaiser < -0.3f)
            {   // If lips low and eyebrow slanted up draw sad eyes
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSadLeft] = -faceInfo.LipCornerDepressor * 0.25f;
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSadRight] = -faceInfo.LipCornerDepressor * 0.25f;
            }
            else
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSadLeft] = 0f;
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthSadRight] = 0f;
            }

            if (faceInfo.LipRaiser >= 0)
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthUpperLeft] = faceInfo.LipRaiser;
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthUpperRight] = faceInfo.LipRaiser;
            }
            else
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthLowerLeft] = Math.Abs(faceInfo.LipRaiser);
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthLowerRight] = Math.Abs(faceInfo.LipRaiser);
            }

            if (faceInfo.LipCornerDepressor >= 0)
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthUpperUpLeft] = faceInfo.LipCornerDepressor;
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthUpperUpRight] = faceInfo.LipCornerDepressor;
            }
            else
            {
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthLowerDownLeft] = Math.Abs(faceInfo.LipCornerDepressor);
                UnifiedTrackingData.LatestLipShapes[LipShape_v2.MouthLowerDownRight] = Math.Abs(faceInfo.LipCornerDepressor);
            }
        }

        public override void Teardown()
        {
            _cancellationToken.Cancel();
            if (memMapFile == null) return;
            viewAccessor.Write(0, ref faceInfo);
            memMapFile.Dispose();
            companionProcess.Kill();
            _cancellationToken.Dispose();
        }

        public override (bool SupportsEye, bool SupportsLip) Supported => (true, true);
        public override (bool UtilizingEye, bool UtilizingLip) Utilizing { get; set; }
        public struct FaceInfo
        {
            public float LipRaiser;
            public float JawLower;
            public float LipStretcher;
            public float BrowLower;
            public float LipCornerDepressor;
            public float BrowRaiser;
        }
    }
}
