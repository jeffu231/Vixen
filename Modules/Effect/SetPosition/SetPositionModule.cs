using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using Vixen.Data.Value;
using Vixen.Intent;
using Vixen.Module;
using Vixen.Module.Effect;
using Vixen.Sys;
using Vixen.Sys.Attribute;
using VixenModules.App.Curves;
using VixenModules.Effect.Effect;
using VixenModules.EffectEditor.EffectDescriptorAttributes;
using VixenModules.Property.Pan;
using VixenModules.Property.Tilt;
using ZedGraph;

namespace VixenModules.Effect.SetPosition
{
	public class SetPositionModule : BaseEffect
	{
		private SetPositionData _data;
		private EffectIntents _effectIntents;
		private bool _canTilt;
		private bool _canPan;

		public override IModuleDataModel ModuleData
		{
			get => _data;
			set => _data = (SetPositionData) value;
		}

		[Value]
		[ProviderCategory(@"Position", 2)]
		[ProviderDisplayName(@"Pan")]
		[ProviderDescription(@"Brightness")]
		public Curve Pan
		{
			get => _data.Pan;
			set
			{
				_data.Pan = value;
				IsDirty = true;
			}
		}

		[Value]
		[ProviderCategory(@"Position", 2)]
		[ProviderDisplayName(@"Tilt")]
		[ProviderDescription(@"Brightness")]
		public Curve Tilt
		{
			get => _data.Tilt;
			set
			{
				_data.Tilt = value;
				IsDirty = true;
			}
		}

		#region Information

		public override string Information
		{
			get { return "Visit the Vixen Lights website for more information on this effect."; }
		}

		public override string InformationLink
		{
			get { return "http://www.vixenlights.com/vixen-3-documentation/sequencer/effects/pulse/"; }
		}

		#endregion

		/// <summary>
		/// Updates the visibility of Pan / Tilt attributes.
		/// </summary>
		private void UpdateAttributes(bool refresh = true)
		{
			Dictionary<string, bool> propertyStates = new Dictionary<string, bool>(1)
			{
				{ nameof(Pan), _canPan},
				{ nameof(Tilt), _canTilt},
			};
			SetBrowsable(propertyStates);
			if (refresh)
			{
				TypeDescriptor.Refresh(this);
			}
		}

		protected override void _PreRender(CancellationTokenSource cancellationToken = null)
		{
			_effectIntents = new EffectIntents();

			if (_canPan)
			{
				RenderCurve(Pan, PositionType.Pan, cancellationToken);
			}

			if (_canTilt)
			{
				RenderCurve(Tilt, PositionType.Tilt, cancellationToken);
			}
			
		}

		/// <inheritdoc />
		protected override void TargetNodesChanged()
		{
			GetDeviceCapabilities();
			UpdateAttributes();
		}

		protected override EffectIntents _Render()
		{
			return _effectIntents;
		}

		private void RenderCurve(Curve c, PositionType positionType, CancellationTokenSource cancellationToken = null)
		{

			var nodes = GetRenderNodesForType(positionType);
			if (!nodes.Any()) return;
			c.Points.Sort();

			HashSet<double> points = new HashSet<double> { 0.0 };
			foreach (PointPair point in c.Points)
			{
				points.Add(point.X);
			}
			points.Add(100.0);
			var pointList = points.ToList();
			TimeSpan startTime = TimeSpan.Zero;
			for (int i = 1; i < points.Count; i++)
			{
				PositionValue startValue = new PositionValue(positionType, c.GetValue(pointList[i - 1]) / 100d);
				PositionValue endValue = new PositionValue(positionType, c.GetValue(pointList[i]) / 100d);

				TimeSpan timeSpan = TimeSpan.FromMilliseconds(TimeSpan.TotalMilliseconds * ((pointList[i] - pointList[i - 1])/100));
				PositionIntent intent = new PositionIntent(startValue, endValue, timeSpan);
				foreach (IElementNode node in nodes)
				{
					if (cancellationToken != null && cancellationToken.IsCancellationRequested)
						return;

					if (node != null)
					{
						_effectIntents.AddIntentForElement(node.Element.Id, intent, startTime);
					}
				}

				startTime = startTime + timeSpan;
			}
		}

		private void GetDeviceCapabilities()
		{
			var node = TargetNodes.FirstOrDefault();
			if (node != null)
			{
				_canPan = node.GetLeafEnumerator().Any(x => x != null && x.Properties.Contains(PanDescriptor._typeId));
				_canTilt = node.GetLeafEnumerator().Any(x => x != null && x.Properties.Contains(TiltDescriptor._typeId));
			}
		}

		private IEnumerable<IElementNode> GetRenderNodesForType(PositionType type)
		{
			var node = TargetNodes.FirstOrDefault();
			if (node == null) return Enumerable.Empty<IElementNode>();
			Guid descriptor = Guid.Empty;
			switch (type)
			{
				case (PositionType.Pan):
					descriptor = PanDescriptor._typeId;
					break;
				case (PositionType.Tilt):
					descriptor = TiltDescriptor._typeId;
					break;
			}

			return node.GetLeafEnumerator()
				.Where(x => x != null && x.Properties.Contains(descriptor));
		}



		#region Overrides of BaseEffect

		/// <inheritdoc />
		protected override EffectTypeModuleData EffectModuleData { get; }

		public override bool ForceGenerateVisualRepresentation { get { return true; } }

		public override void GenerateVisualRepresentation(Graphics g, Rectangle clipRectangle)
		{
			var showBoth = _canPan && _canTilt;
			var rect = new Rectangle(clipRectangle.X, clipRectangle.Y, clipRectangle.Width, showBoth ? (clipRectangle.Height-4) / 2 : clipRectangle.Height-4);
			var textRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height / 2);
			var f = Vixen.Common.Graphics.GetAdjustedFont(g, "Tilt", textRect, SystemFonts.MessageBoxFont.Name, 24, SystemFonts.MessageBoxFont);

			if (_canPan)
			{
				var tiltColor = Color.Green;
				var panCurve = Pan.GenerateGenericCurveImage(new Size(rect.Width, rect.Height),false, false, false, tiltColor);
				g.DrawImage(panCurve, rect.X, rect.Y+2);
				
				g.DrawString("Pan", f, new SolidBrush(tiltColor), rect.X, showBoth?-2:1);
			}

			
			if (_canTilt)
			{
				var panColor = Color.FromArgb(0, 128, 255);
				var panCurve = Tilt.GenerateGenericCurveImage(new Size(rect.Width, rect.Height), false, false, false, panColor);
				if (showBoth)
				{
					g.DrawImage(panCurve, rect.X, rect.Y + rect.Height+3);
					g.DrawString("Tilt", f, new SolidBrush(panColor), rect.X, rect.Y + rect.Height);
				}
				else
				{
					g.DrawImage(panCurve, rect.X, rect.Y+2);
					g.DrawString("Tilt", f, new SolidBrush(panColor), rect.X, rect.Y + 1);
				}
				
			}
		}

		#endregion

	}
}