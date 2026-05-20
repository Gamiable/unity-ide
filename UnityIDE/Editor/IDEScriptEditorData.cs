using System;
using System.Linq;
using Packages.IDE.Editor.Util;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace Packages.IDE.Editor
{
	internal class IDEScriptEditorData : ScriptableSingleton<IDEScriptEditorData>
	{
		[SerializeField] internal bool hasChanges;
		[SerializeField] internal bool initializedOnce;
		[SerializeField] internal SerializableVersion editorVersion;
		[SerializeField] internal SerializableVersion prevEditorVersion;
		[SerializeField] internal CodeEditor.Installation[] installations;
		[SerializeField] internal string[] activeScriptCompilationDefines;

		public void Init()
		{
			if (editorVersion == null)
			{
				Invalidate(IDEScriptEditor.CurrentEditor);
			}
		}

		public void InvalidateSavedCompilationDefines()
		{
			activeScriptCompilationDefines = EditorUserBuildSettings.activeScriptCompilationDefines;
		}

		public bool HasChangesInCompilationDefines()
		{
			if (activeScriptCompilationDefines == null)
				return false;
			return !EditorUserBuildSettings.activeScriptCompilationDefines.SequenceEqual(activeScriptCompilationDefines);
		}

		public void Invalidate(string editorInstallationPath, bool shouldInvalidatePrevEditorVersion = false)
		{
			var version = GetEditorVersion(editorInstallationPath);
			editorVersion = version.ToSerializableVersion();
			if (shouldInvalidatePrevEditorVersion)
				prevEditorVersion = editorVersion;
		}

		private static Version GetEditorVersion(string editorPath)
		{
			if (string.IsNullOrEmpty(editorPath))
				return new Version(0, 0);

			try
			{
				var process = new System.Diagnostics.Process
				{
					StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = editorPath,
						Arguments = "--version",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};
				process.Start();
				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit(5000);

				var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)\.(\d+)\.(\d+)");
				if (match.Success)
					return new Version(
						int.Parse(match.Groups[1].Value),
						int.Parse(match.Groups[2].Value),
						int.Parse(match.Groups[3].Value));
			}
			catch { }

			return new Version(0, 0);
		}
	}
}
