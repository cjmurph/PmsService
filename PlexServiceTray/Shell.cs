using System;
using System.Diagnostics;
using System.IO;

namespace PlexServiceTray
{
    /// <summary>
    /// Opens links, files and folders. Values often come from the service, so only safe targets are opened.
    /// </summary>
    internal static class Shell
    {
        public static bool IsWebUrl(string value)
        {
            return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Opens a http(s) url in the default browser
        /// </summary>
        public static bool OpenUrl(string url)
        {
            if (!IsWebUrl(url)) return false;
            Process.Start(new ProcessStartInfo(url.Trim()) { UseShellExecute = true });
            return true;
        }

        /// <summary>
        /// Opens a folder in explorer
        /// </summary>
        public static bool OpenFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = false });
            return true;
        }

        /// <summary>
        /// Opens a file with the default application, restricted to the given extensions
        /// </summary>
        public static bool OpenFile(string path, params string[] allowedExtensions)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var extension = Path.GetExtension(path);
            if (Array.FindIndex(allowedExtensions, e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)) < 0) return false;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
    }
}
