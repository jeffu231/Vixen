using NSch;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Threading;
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

namespace VixenModules.Effect.ImageToVideo
{
	public class ImageToVideo : BaseEffect
	{
		private ImageToVideoData _data;
		private EffectIntents _elementData = null;
        private static NLog.Logger Logging = LogManager.GetCurrentClassLogger();

        public ImageToVideo()
		{
			_data = new ImageToVideoData();
		}

		protected override void TargetNodesChanged()
		{
		}

        public override bool ForceGenerateVisualRepresentation { get { return true; } }

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
				_data = value as ImageToVideoData;
				IsDirty = true;
			}
		}

		protected override EffectTypeModuleData EffectModuleData
		{
			get { return _data; }
		}

        [Value]
        [ProviderCategory(@"Config", 2)]
        [ProviderDisplayName(@"Filename")]
        [ProviderDescription(@"Filename")]
        [PropertyEditor("ImagePathEditor")]
        [PropertyOrder(1)]
        public string FileName
        {
            get { return _data.FileName; }
            set
            {
                _data.FileName = ConvertPath(value);
                IsDirty = true;
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
                //bool containsVideoProperty = elementNode.Properties.Contains(VideoDescriptor.ModuleId);
                if (elementNode.Properties.Contains(VideoDescriptor.ModuleId))  //check if element has a video property
                {
                    StaticArrayIntent<BitmapValue> intent;

                    //Get the property for this node to use the size dimensions.
                    VideoModule videoProperty = elementNode?.Properties.Get(VideoDescriptor.ModuleId) as VideoModule;
                    //load the image file & Create the intents
                    var image = LoadImage(videoProperty.Width, videoProperty.Height);

					intent = CreateBitmapIntent(image, TimeSpan);
					image.Dispose();
                    effectIntents.AddIntentForElement(elementNode.Element.Id, intent, TimeSpan.Zero);
                }
            }
            return effectIntents;
            }

        // Probably should move this to IntentBuilder.cs
        StaticArrayIntent<BitmapValue> CreateBitmapIntent(Bitmap image,TimeSpan duration)
        {
            var interval = VixenSystem.DefaultUpdateTimeSpan;
            var intervals = (int)(duration.TotalMilliseconds / interval.TotalMilliseconds);
            var values = new BitmapValue[intervals + 1];

            for (int i = 0; i < intervals + 1; i++)
            {
                values[i] = new BitmapValue(new Bitmap(image));
            }

            return new StaticArrayIntent<BitmapValue>(interval, values, duration);
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
            var destPath = Path.Combine(ImageToVideoDescriptor.ModulePath, name);
            if (path != destPath)
            {
                File.Copy(path, destPath, true);
            }
            return name;
        }

        private Bitmap LoadImage(int width,int height)
        {
            Image image = null;
            var filePath = Path.Combine(ImageToVideoDescriptor.ModulePath, FileName);
            if (File.Exists(filePath))
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var ms = new MemoryStream();
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    image = Image.FromStream(ms);
                }
            }
            else
            {
                Logging.Error("File is missing or invalid path. {0}", filePath);
                FileName = "";       
            }
            return new Bitmap(image,width,height);
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
                Logging.Error(e, "Exception rendering the visualization for the ImageToVideo effect.");
            }
        }
    }
}