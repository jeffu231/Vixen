using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VixenModules.Preview.VideoPreview;

namespace VixenModules.Preview.VixenPreview {
	public interface IDisplayForm : IDisposable {
		VideoPreviewData Data { get; set; }
		void Setup();
		void Close();
		void UpdatePreview();

		String DisplayName { get; set; }

		Guid InstanceId { get; set; }
	}
}
