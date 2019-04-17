﻿using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Vixen.Sys.LayerMixing;
using WeifenLuo.WinFormsUI.Docking;

namespace VixenModules.Editor.TimedSequenceEditor.Forms.WPF.ElementFilterDocker
{
	public partial class ElementNodeFilterEditor : DockContent
	{
		private readonly Editor.LayerEditor.LayerEditorView _layerEditorView;
		
		public ElementNodeFilterEditor(SequenceLayers layers)
		{
		
			InitializeComponent();
			
			var host = new ElementHost { Dock = DockStyle.Fill };

			Controls.Add(host);

			_layerEditorView = new Editor.LayerEditor.LayerEditorView(layers);

			host.Child = _layerEditorView;

			_layerEditorView.CollectionChanged += LayerEditorViewCollectionChanged;
			_layerEditorView.LayerChanged += LayerEditorViewOnLayerChanged;
		}

		private void LayerEditorViewOnLayerChanged(object sender, EventArgs eventArgs)
		{
			if (LayersChanged != null)
				LayersChanged(this, eventArgs);
		}

		private void LayerEditorViewCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			OnMixingLayerFilterCollectionChanged(new EventArgs());
		}

		public event EventHandler<EventArgs> LayersChanged;
		protected virtual void OnMixingLayerFilterCollectionChanged(EventArgs e)
		{
			if (LayersChanged != null)
				LayersChanged(this, e);
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			if (_layerEditorView != null)
			{
				_layerEditorView.CollectionChanged -= LayerEditorViewCollectionChanged;
				_layerEditorView.LayerChanged -= LayerEditorViewOnLayerChanged;
			}
			base.Dispose(disposing);
		}
	}
}
