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
			var result = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var path = Path.Combine(result, "Plex Media Server");
			if (Directory.Exists(path)) {
				return path;
			}
			result = String.Empty;
			//work out the os type	 (32 or 64) and set the registry view to suit. this is only a reliable check when this project is compiled to x86.
			var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

			var architecture = RegistryView.Registry32;
			if (is64Bit)
			{
				architecture = RegistryView.Registry64;
			}

			using var pmsDataKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, architecture).OpenSubKey(@"Software\Plex, Inc.\Plex Media Server");
			if (pmsDataKey == null) {
				return result;
			}

			path = (string) pmsDataKey.GetValue("LocalAppdataPath");
			result = path;

			return result;
		}
	}
}