using System;
using System.IO;
using Microsoft.Win32;

namespace PlexServiceCommon {
	public static class PlexDirHelper {
		/// <summary>
		/// Returns the full path and filename of the plex media server executable
		/// </summary>
		/// <returns></returns>
		public static string GetPlexDataDir()
		{
			//set appDataFolder to the default user local app data folder
			var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			

			//check if the user has a custom path specified in the registry, if so, update the path to return this instead

			var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

			var architecture = is64Bit ? RegistryView.Registry64 : RegistryView.Registry32;

			try
			{
				using var pmsDataKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, architecture).OpenSubKey(@"Software\Plex, Inc.\Plex Media Server");

				if (pmsDataKey is not null)
					appDataFolder = Path.Combine((string)pmsDataKey.GetValue("LocalAppdataPath"), "Plex Media Server");
			}
			catch { }


			var path = Path.Combine(appDataFolder, "Plex Media Server");

			if (Directory.Exists(path))
				return path;

			return string.Empty;
		}
	}
}