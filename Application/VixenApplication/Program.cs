using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Catel.Logging;
using Vixen.Sys;

namespace VixenApplication
{
	internal static class Program
	{
		private static NLog.Logger Logging = NLog.LogManager.GetCurrentClassLogger();
		private const string ErrorMsg = "An application error occurred. Please contact the Vixen Dev Team " +
									"with the following information:\n\n";
		private static VixenApplication _app;
		internal static string LockFilePath = string.Empty;

		private static string[] LoaderPaths =
		{
			"Common",
			"Modules/App",
			"Modules/Analysis",
			"Modules/EffectEditor",
			"Modules/Effect",
			"Modules/Editor",
			"Modules/ModuleTemplate",
			"Modules/Input",
			"Modules/Controller",
			"Modules/RuntimeBehavior",
			"Modules/SequenceType",
			"Modules/Trigger",
			"Modules/Media",
			"Modules/MediaRenderer",
			"Modules/Timing",
			"Modules/Script",
			"Modules/Property",
			"Modules/Preview",
			"Modules/OutputFilter",
			"Modules/SequenceFilter",
			"Modules/SmartController",
			"Modules/Service"
		};
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		private static void Main()
		{
			try
			{
				AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
				Logging.Info("Vixen app starting.");
				LogManager.AddListener(new NLogListener());
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				Application.ThreadException += Application_ThreadException;
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);

				_app = new VixenApplication();
				Application.Run(_app);
			}
			catch (Exception ex)
			{
				LogMessageAndExit(ex);
			}
		}

		static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
			LogMessageAndExit(e.Exception);
			
		}

		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
		{ 
			var e = (Exception)args.ExceptionObject;
			LogMessageAndExit(e);
		}

		private static void LogMessageAndExit(Exception ex)
		{
			// Since we can't prevent the app from terminating, log this to the event log. 
			Logging.Fatal(ex, ErrorMsg);
			if (VixenSystem.IsSaving())
			{
				Logging.Fatal("Save was in progress during the fatal crash. Trying to pause 5 seconds to give it a chance to complete.");
				Thread.Sleep(5000);
			}
			if (_app != null)
			{
				_app.RemoveLockFile();
			}
			else 
			{
				//try the failsafe to clean up the lock file.
				VixenApplication.RemoveLockFile(LockFilePath);
			}
			Environment.Exit(1);
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			// Ignore missing resources
			if (args.Name.Contains(".resources"))
				return null;

			// check for assemblies already loaded
			Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
			if (assembly != null)
				return assembly;

			// Try to load by filename - split out the filename of the full assembly name
			// and append the base path of the original assembly (ie. look in the same dir)
			string filename = args.Name.Split(',')[0] + ".dll".ToLower();

			foreach (var loaderPath in LoaderPaths)
			{
				string asmFile = Path.Combine(@".\", loaderPath, filename);

				try
				{
					return Assembly.LoadFrom(asmFile);
				}
				catch (Exception ex)
				{
					Logging.Error(ex, $"Error loading assembly {args.Name}");
					return null;
				}
			}
			Logging.Error($"Could not find assembly {args.Name}");
			return null;

		}

	}
}