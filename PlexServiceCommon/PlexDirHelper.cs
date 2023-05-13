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
			var userDefinedPath = PlexRegistryHelper.ReadUserRegistryValue("LocalAppDataPath");
			appDataFolder = string.IsNullOrEmpty(userDefinedPath) ? appDataFolder : userDefinedPath;

			var path = Path.Combine(appDataFolder, "Plex Media Server");

			if (Directory.Exists(path))
				return path;

			return string.Empty;
		}
	}
}