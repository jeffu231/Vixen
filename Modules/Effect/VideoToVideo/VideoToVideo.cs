using Common.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Vixen.Attributes;
using Vixen.Data.Value;
using Vixen.Intent;
using Vixen.Module;
using Vixen.Sys;
using Vixen.Sys.Attribute;
using VixenModules.Effect.Effect;
using VixenModules.EffectEditor.EffectDescriptorAttributes;
using NLog;
using VixenModules.Property.Video;

namespace VixenModules.Effect.VideoToVideo
{
	public class VideoToVideo : BaseEffect
	{
		private VideoToVideoData _data;
		private EffectIntents _elementData = null;
        private static NLog.Logger Logging = LogManager.GetCurrentClassLogger();
        private static readonly string VideoPath = VideoToVideoDescriptor.ModulePath;
        private static readonly string TempPath = Path.Combine(Path.GetTempPath(), "Vixen", "VideoToVideoEffect");
        private string _tempFilePath;
        private bool _videoFileDetected;
        private Size _frameSize;
        private List<string> _moviePicturesFileList;
        private int VideoLength;
        private bool _getNewVideoInfo;
        private int VideoQuality = 50;
        private bool _processVideo;
        double _currentMovieImageNum;


        public VideoToVideo()
		{
			_data = new VideoToVideoData();
            PopulateTempPath();
            _processVideo = true;
            //UpdateAttributes();
        }

		protected override void TargetNodesChanged()
		{
		}

        public override bool ForceGenerateVisualRepresentation { get { return true; } }

        //Is this actually used anywhere?  Can i remove it?
        protected void SetupRender()
        {
            //UpdateQualityAttribute();
            if (_data.FileName == "") return;

            if (_processVideo || !Directory.Exists(_tempFilePath)) ProcessMovie(_frameSize); // Check if directory exist is needed for when an effect is cloned.
            if (_videoFileDetected)
            {
                _currentMovieImageNum = 0;
            }
        }

        protected override void _PreRender(CancellationTokenSource tokenSource = null)
		{
			_elementData = new EffectIntents();
			
			foreach (ElementNode node in TargetNodes) {
				if (tokenSource != null && tokenSource.IsCancellationRequested)
					return;

				if (node != null)
				{
					_elementData = RenderNode(node);
					//_elementData = IntentBuilder.ConvertToStaticArrayIntents(_elementData, TimeSpan, false);					
				}
			}
		}

		protected override EffectIntents _Render()
		{
			return _elementData;
		}

		public override IModuleDataModel ModuleData
		{
			get { return _data; }
			set
			{
				_data = value as VideoToVideoData;
				IsDirty = true;
			}
		}

		protected override EffectTypeModuleData EffectModuleData
		{
			get { return _data; }
		}

        [Value]
        [ProviderCategory(@"Video Configuration", 2)]
        [ProviderDisplayName(@"Filename")]
        [ProviderDescription(@"Filename")]
        [PropertyEditor("VideoPathEditor")]
        [PropertyOrder(1)]
        public string FileName
        {
            get { return _data.FileName; }
            set
            {
                _data.FileName = ConvertPath(value);
                IsDirty = true;
                _getNewVideoInfo = true;
                _processVideo = true;
                OnPropertyChanged();
            }
        }
        
        #region Information

        public override string Information
		{
			get { return "Visit the Vixen Lights website for more information on this effect."; }
		}

		public override string InformationLink
		{
			get { return "http://www.vixenlights.com/vixen-3-documentation/sequencer/effects/"; }
		}

        #endregion

        // renders the given node to the internal ElementData dictionary. If the given node is
        // not a element, will recursively descend until we render its elements.
        private EffectIntents RenderNode(ElementNode node)
            {
            EffectIntents effectIntents = new EffectIntents();

            foreach (ElementNode elementNode in node.GetLeafEnumerator())
            {
                //Get the property for this node to use the size dimensions.
                _frameSize = getElementVideoSize(elementNode);                
                if (!_frameSize.IsEmpty)  //check if element has a video property
                {
                    StaticArrayIntent<BitmapValue> intent;

                    //load the Video file and create frame images 
                    ProcessMovie(_frameSize);
                    //Create the intents
                    intent = CreateIntents();
                    //Create Intent for node
                    effectIntents.AddIntentForElement(elementNode.Element.Id, intent, TimeSpan.Zero);
                }
            }
            return effectIntents;
            }

        StaticArrayIntent<BitmapValue> CreateIntents()
        {
            var updateInterval = VixenSystem.DefaultUpdateTimeSpan;
            var intervals = (int)(TimeSpan.TotalMilliseconds / updateInterval.TotalMilliseconds);
            var values = new BitmapValue[intervals + 1];
            var valueIdx = 0;

            foreach(string file in _moviePicturesFileList)
            {
                var image = LoadImage(file, _frameSize);
                values[valueIdx] = new BitmapValue(new Bitmap(image));               
                image.Dispose();
                valueIdx++;
            }
            RemoveTempFiles();
            return new StaticArrayIntent<BitmapValue>(updateInterval, values, TimeSpan);
        }

        private string ConvertPath(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return path;
                }
                if (Path.IsPathRooted(path))
                {
                    return CopyLocal(path);
                }
                return path;
            }

        private string CopyLocal(string path)
        {
            string name = Path.GetFileName(path);
            var destPath = Path.Combine(VideoToVideoDescriptor.ModulePath, name);
            if (path != destPath)
            {
                File.Copy(path, destPath, true);
            }
            return name;
        }

        private Bitmap LoadImage(string path, Size imageSize)
        {
            Image image = null;
            if (File.Exists(path))
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var ms = new MemoryStream();
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    image = Image.FromStream(ms);
                }
            }
            else
            {
                Logging.Error("File is missing or invalid path. {0}", path);
                FileName = "";       
            }
            return new Bitmap(image,imageSize.Width,imageSize.Height);
        }

        private Size getElementVideoSize(ElementNode node)
        {
            if (node.Properties.Contains(VideoDescriptor.ModuleId))
            {
                VideoModule videoProperty = node?.Properties.Get(VideoDescriptor.ModuleId) as VideoModule;
                return new Size(videoProperty.Width, videoProperty.Height);
            }
            return Size.Empty;
        }

        public override void GenerateVisualRepresentation(Graphics g, Rectangle clipRectangle)
        {
            try
            {
                string effectDisplayText = _data.FileName.ToString();

                Font adjustedFont = Vixen.Common.Graphics.GetAdjustedFont(g, effectDisplayText, clipRectangle, "Vixen.Fonts.DigitalDream.ttf", 48);
                using (var stringBrush = new SolidBrush(Color.White))
                {
                    using (var backgroundBrush = new SolidBrush(Color.DarkGray))
                        { g.FillRectangle(backgroundBrush, clipRectangle); }
                    g.DrawString(effectDisplayText, adjustedFont, stringBrush, clipRectangle.X + 2, 2);
                }

            }
            catch (Exception e)
            {
                Logging.Error("Exception rendering the visualization for the VideoToVideo effect.", e);
            }
        }
        private void ProcessMovie(Size frameSize)
        {
            EstablishTempFolder();

            string videoFilename = Path.Combine(VideoPath, _data.FileName);
            try
            {
                if (_getNewVideoInfo) GetVideoInformation();

                string cropVideo = "";

                int renderWidth = frameSize.Width;
                int renderHeight = frameSize.Height;

                //Maybe Move these vars back to effect class level
                //I put them in here just to make the copied code work with minimum mods
                bool MaintainAspect = true;
                int RotateVideo = 0;
                

                ffmpeg.ffmpeg converter = new ffmpeg.ffmpeg(videoFilename);
                _currentMovieImageNum = 0;
                // Height and Width needs to be evenly divisible to work or ffmpeg complains.
                if (renderHeight % 2 != 0) renderHeight++;
                if (renderWidth % 2 != 0) renderWidth++;
                int frameRate = 1000 / (int)VixenSystem.DefaultUpdateTimeSpan.TotalMilliseconds;
                converter.MakeScaledThumbNails(_tempFilePath, 0, TimeSpan.TotalSeconds,
                    renderWidth, renderHeight, MaintainAspect, RotateVideo, cropVideo,frameRate);
                _moviePicturesFileList = Directory.GetFiles(_tempFilePath).OrderBy(f => f).ToList();

                _videoFileDetected = true;
            }
            catch (Exception ex)
            {
                Logging.Error(ex, $"There was a problem converting {videoFilename}");
                var messageBox = new MessageBoxForm("There was a problem converting " + videoFilename + ": " + ex.Message,
                    "Error Converting Video", MessageBoxButtons.OK, SystemIcons.Error);
                messageBox.ShowDialog();
                _videoFileDetected = false;
            }
        }

        private void PopulateTempPath()
        {
            _tempFilePath = TempPath + Path.PathSeparator + InstanceId;
        }

        private void EstablishTempFolder()
        {
            //Delete old path and create new path for processed video
            RemoveTempFiles();

            _tempFilePath = Path.Combine(TempPath, InstanceId.ToString());
            Directory.CreateDirectory(_tempFilePath);
        }

        private void RemoveTempFiles()
        {
            if (Directory.Exists(_tempFilePath))
            {
                try
                { Directory.Delete(_tempFilePath, true); }
                catch (Exception e)
                { Logging.Error(e, "Unable to delete all the video temp files."); }
            }
        }

        private void GetVideoInformation()
        {
            // This is only done each time a Video file is changed.
            // No point doing this every time it needs to render.
            // So once a user adds a video file to the effect this code will no longer be used.
            string videoFilename = Path.Combine(VideoPath, _data.FileName);
            try
            {
                VideoQuality = 50; // Set quality to 50% when a new file is opened.
                                   // Delete old path and create new path for processed video
                EstablishTempFolder();
                // Gets Video length and Frame rate will continue if users start position is less then the video length.
                ffmpeg.ffmpeg videoLengthInfo = new ffmpeg.ffmpeg(videoFilename);
                string result = videoLengthInfo.GetVideoInfo(_tempFilePath);
                // Get Video Length
                int durationIndex = result.IndexOf("Duration: ");
                string videoInfo = result.Substring(durationIndex + 10, 8);
                string[] words = videoInfo.Split(':');
                TimeSpan videoTimeSpan = new TimeSpan(Int32.Parse(words[0]), Int32.Parse(words[1]), Int32.Parse(words[2]));
                VideoLength = (int)videoTimeSpan.TotalSeconds;

                // Saves one frame from video then grabs it to determine Video size.
                // This was to replace the way Accord did it as it makes sense to do the Video size
                // conversion when generating all the images. This can reduces each bitmap file size significantly.
                ffmpeg.ffmpeg videoSizeInfo = new ffmpeg.ffmpeg(videoFilename);
                videoSizeInfo.GetVideoSize(_tempFilePath + "\\Temp.bmp");
                var image = Image.FromFile(_tempFilePath + "\\Temp.bmp");

                // Saves the Video info to data store.
                _data.VideoSize = new Size(image.Width, image.Height);

                image.Dispose();
                _getNewVideoInfo = false;
            }
            catch (Exception ex)
            {
                var messageBox = new MessageBoxForm("There was a problem converting " + videoFilename + ": " + ex.Message,
                    "Error Converting Video", MessageBoxButtons.OK, SystemIcons.Error);
                messageBox.ShowDialog();
                _videoFileDetected = false;
            }

        }
    }
}