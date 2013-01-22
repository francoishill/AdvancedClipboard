using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using SharedClasses;

namespace AdvancedClipboard
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			System.Windows.Forms.Application.EnableVisualStyles();

			Dictionary<string, string> userPrivilages;
			if (!LicensingInterop_Client.Client_ValidateLicense(out userPrivilages, err => UserMessages.ShowErrorMessage(err)))
				Environment.Exit(LicensingInterop_Client.cApplicationExitCodeIfLicenseFailedValidation);

			AutoUpdating.CheckForUpdates_ExceptionHandler(null);

			AdvancedClipboard.MainWindow win = new MainWindow();
			win.ShowDialog();
		}
	}
}
