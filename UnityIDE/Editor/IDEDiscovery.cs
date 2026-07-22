using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Packages.IDE.Editor.Util;
using Unity.CodeEditor;
using OperatingSystemFamily = UnityEngine.OperatingSystemFamily;

namespace Packages.IDE.Editor
{
	internal interface IDiscovery
	{
		CodeEditor.Installation[] PathCallback();
	}

	internal class IDEDiscovery : IDiscovery
	{
		private static readonly string[] EditorNames = { "code", "cursor", "antigravity", "zed" };

		private static readonly string[] EditorDisplayNames = {
			"Visual Studio Code", "Cursor", "Antigravity IDE", "Zed"
		};

		private static readonly Dictionary<string, string> MacAppToDisplayName = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "Visual Studio Code", "Visual Studio Code" },
			{ "Visual Studio Code - Insiders", "VS Code Insiders" },
			{ "Cursor", "Cursor" },
			{ "Antigravity IDE", "Antigravity IDE" },
			{ "Zed", "Zed" },
		};

		public CodeEditor.Installation[] PathCallback()
		{
			var installations = new List<CodeEditor.Installation>();
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			for (int i = 0; i < EditorNames.Length; i++)
			{
				var editorName = EditorNames[i];
				var displayName = EditorDisplayNames[i];

				foreach (var path in FindEditorPaths(editorName))
				{
					if (seenPaths.Add(path) && ValidateEditor(path, editorName))
					{
						installations.Add(new CodeEditor.Installation
						{
							Path = path,
							Name = displayName
						});
					}
				}
			}

			var currentEditor = UnityEditor.EditorPrefs.GetString("kScriptsDefaultApp");
			if (!string.IsNullOrEmpty(currentEditor)
				&& IsSupportedInstallation(currentEditor)
				&& !installations.Any(a => string.Equals(a.Path, currentEditor, StringComparison.OrdinalIgnoreCase))
				&& FileSystemUtil.EditorPathExists(currentEditor))
			{
				installations.Add(new CodeEditor.Installation
				{
					Path = currentEditor,
					Name = GetEditorDisplayName(currentEditor)
				});
			}

			return installations.ToArray();
		}

		private static IEnumerable<string> FindEditorPaths(string editorName)
		{
			var paths = new List<string>();

			paths.AddRange(FindOnPath(editorName));
			paths.AddRange(FindInKnownLocations(editorName));

			return paths;
		}

		private static IEnumerable<string> FindOnPath(string editorName)
		{
			var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
			foreach (var dir in pathEnv.Split(Path.PathSeparator))
			{
				if (string.IsNullOrEmpty(dir)) continue;

				var executable = Path.Combine(dir, editorName);
				if (OperatingSystemFamily.Windows == UnityEngine.SystemInfo.operatingSystemFamily)
				{
					if (File.Exists(executable + ".exe"))
						yield return executable + ".exe";
					if (File.Exists(executable + ".cmd"))
						yield return executable + ".cmd";
				}
				else
				{
					if (File.Exists(executable))
						yield return executable;
				}
			}
		}

		private static IEnumerable<string> FindInKnownLocations(string editorName)
		{
			switch (UnityEngine.SystemInfo.operatingSystemFamily)
			{
				case OperatingSystemFamily.MacOSX:
					var appName = GetMacAppName(editorName);
					var appPath = Path.Combine("/Applications", appName + ".app");
					if (Directory.Exists(appPath))
						yield return appPath;
					break;

				case OperatingSystemFamily.Windows:
					var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
					var winDir = Path.Combine(localAppData, "Programs", GetWindowsDirName(editorName));
					if (Directory.Exists(winDir))
					{
						var exe = Path.Combine(winDir, editorName + ".exe");
						if (File.Exists(exe)) yield return exe;
					}
					break;

				case OperatingSystemFamily.Linux:
					var home = Environment.GetEnvironmentVariable("HOME")
						?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
					var dirs = new[] { "/usr/share", "/usr/bin", "/snap/bin", Path.Combine(home, ".local/bin") };
					foreach (var dir in dirs)
					{
						var fullPath = Path.Combine(dir, editorName);
						if (File.Exists(fullPath))
							yield return fullPath;
					}
					break;
			}
		}

		private static string GetMacAppName(string editorName)
		{
			switch (editorName)
			{
				case "code": return "Visual Studio Code";
				case "code-insiders": return "Visual Studio Code - Insiders";
				case "cursor": return "Cursor";
				case "antigravity": return "Antigravity IDE";
				case "zed": return "Zed";
				default: return editorName;
			}
		}

		private static string GetWindowsDirName(string editorName)
		{
			switch (editorName)
			{
				case "code": return "Microsoft VS Code";
				case "cursor": return "Cursor";
				default: return editorName;
			}
		}

		private static bool ValidateEditor(string path, string editorName)
		{
			if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
				return true;

			try
			{
				var process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = path,
						Arguments = "--version",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};
				process.Start();
				var output = process.StandardOutput.ReadToEnd();
				process.StandardError.ReadToEnd();
				process.WaitForExit(5000);
				return output.IndexOf(editorName, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsSupportedInstallation(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;

			if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				var appName = System.IO.Path.GetFileNameWithoutExtension(path);
				return MacAppToDisplayName.ContainsKey(appName);
			}

			var filename = System.IO.Path.GetFileName(path);
			var names = new[] { "code", "cursor", "antigravity", "zed" };
			foreach (var name in names)
			{
				if (filename.StartsWith(name, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		private static string GetEditorDisplayName(string path)
		{
			if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				var appName = System.IO.Path.GetFileNameWithoutExtension(path);
				if (MacAppToDisplayName.TryGetValue(appName, out var name))
					return name;
				return "IDE Editor";
			}

			var filename = System.IO.Path.GetFileName(path);

			if (filename.StartsWith("antigravity", StringComparison.OrdinalIgnoreCase)) return "Antigravity IDE";
			if (filename.StartsWith("cursor", StringComparison.OrdinalIgnoreCase)) return "Cursor";
			if (filename.StartsWith("code-insiders", StringComparison.OrdinalIgnoreCase)) return "VS Code Insiders";
			if (filename.StartsWith("code", StringComparison.OrdinalIgnoreCase)) return "Visual Studio Code";
			if (filename.StartsWith("zed", StringComparison.OrdinalIgnoreCase)) return "Zed";

			return "IDE Editor";
		}
	}
}
