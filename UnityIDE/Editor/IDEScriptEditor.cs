using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Packages.IDE.Editor.ProjectGeneration;
using Packages.IDE.Editor.Util;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using OperatingSystemFamily = UnityEngine.OperatingSystemFamily;

namespace Packages.IDE.Editor
{
	[InitializeOnLoad]
	internal class IDEScriptEditor : IExternalCodeEditor
	{
		private IDiscovery m_Discoverability;
		private static IGenerator m_ProjectGeneration;
		private static IDEScriptEditor m_IDEScriptEditor;
		private static GUIStyle m_InfoLabelStyle => new GUIStyle("ControlLabel") { wordWrap = true };

		private static readonly Dictionary<string, string> MacAppToDisplayName = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "Visual Studio Code", "Visual Studio Code" },
			{ "Visual Studio Code - Insiders", "VS Code Insiders" },
			{ "Cursor", "Cursor" },
			{ "Windsurf", "Windsurf" },
			{ "Antigravity", "Antigravity" },
			{ "Zed", "Zed" },
			{ "Neovim", "Neovim" },
		};

		static IDEScriptEditor()
		{
			try
			{
				var projectGeneration = new ProjectGeneration.ProjectGeneration();
				m_IDEScriptEditor = new IDEScriptEditor(new Discovery(), projectGeneration);
				CodeEditor.Register(m_IDEScriptEditor);
				InitializeInternal(CurrentEditor);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private static void ShowWarningOnUnexpectedScriptEditor(string path)
		{
			try
			{
				var args = Environment.GetCommandLineArgs();
				var commandlineParser = new CommandLineParser(args);
				if (commandlineParser.Options.ContainsKey("-idePath"))
				{
					var originIDEPath = commandlineParser.Options["-idePath"];
					if (!string.Equals(originIDEPath, path, StringComparison.OrdinalIgnoreCase))
					{
						Debug.LogWarning("Unity was started by a different IDE editor than the current default external editor.");
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		internal static string GetEditorRealPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return path;

			if (!FileSystemUtil.EditorPathExists(path))
				return path;

			if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX
				&& path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				return path;
			}

			try { return new FileInfo(path).FullName; }
			catch { return path; }
		}

		public IDEScriptEditor(IDiscovery discovery, IGenerator projectGeneration)
		{
			m_Discoverability = discovery;
			m_ProjectGeneration = projectGeneration;
		}

		public void OnGUI()
		{
			GUILayout.BeginHorizontal();

			var style = GUI.skin.label;
			var text = "Customize handled extensions in";
			EditorGUILayout.LabelField(text, style, GUILayout.Width(style.CalcSize(new GUIContent(text)).x));

			if (IDESettings.LinkButton("Project Settings | Editor | Additional extensions to include"))
			{
				SettingsService.OpenProjectSettings("Project/Editor");
			}

			GUILayout.EndHorizontal();

			EditorGUILayout.LabelField("Generate .csproj files for:");
			EditorGUI.indentLevel++;
			SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "");
			SettingsButton(ProjectGenerationFlag.Local, "Local packages", "");
			SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "");
			SettingsButton(ProjectGenerationFlag.Git, "Git packages", "");
			SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "");
#if UNITY_2019_3_OR_NEWER
			SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "");
#endif
			SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "");
			SettingsButton(ProjectGenerationFlag.PlayerAssemblies, "Player projects", "For each player project generate an additional csproj with the name 'project-player.csproj'");
			RegenerateProjectFiles();
			EditorGUI.indentLevel--;
		}

		private void RegenerateProjectFiles()
		{
			var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(new GUILayoutOption[] { }));
			rect.width = 252;
			if (GUI.Button(rect, "Regenerate project files"))
			{
				m_ProjectGeneration.Sync();
			}
		}

		private void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip)
		{
			var prevValue = m_ProjectGeneration.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
			var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
			if (newValue != prevValue)
			{
				m_ProjectGeneration.AssemblyNameProvider.ToggleProjectGeneration(preference);
			}
		}

		public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles,
			string[] importedFiles)
		{
			m_ProjectGeneration.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles),
				importedFiles);
		}

		public void SyncAll()
		{
			m_ProjectGeneration.Sync();
		}

		[UsedImplicitly]
		public static void SyncSolution()
		{
			m_ProjectGeneration.Sync();
		}

		[UsedImplicitly]
		public static void SyncIfNeeded(bool checkProjectFiles)
		{
			AssetDatabase.Refresh();
			m_ProjectGeneration.SyncIfNeeded(new string[] { }, new string[] { }, checkProjectFiles);
		}

		[UsedImplicitly]
		public static void SyncSolutionAndOpenExternalEditor()
		{
			m_ProjectGeneration.Sync();
			CodeEditor.CurrentEditor.OpenProject();
		}

		public void Initialize(string editorInstallationPath)
		{
			var prevEditorVersion = IDEScriptEditorData.instance.prevEditorVersion.ToVersion();

			IDEScriptEditorData.instance.Invalidate(editorInstallationPath, true);

			var currentVersion = IDEScriptEditorData.instance.editorVersion.ToVersion();
			if (prevEditorVersion != null && currentVersion != null
				&& prevEditorVersion != currentVersion)
			{
#if UNITY_2019_3_OR_NEWER
				EditorUtility.RequestScriptReload();
#else
				UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
#endif
			}
		}

		private static void InitializeInternal(string currentEditorPath)
		{
			var path = GetEditorRealPath(currentEditorPath);

			if (IsSupportedInstallation(path))
			{
				var installations = new List<CodeEditor.Installation>();
				if (IDEScriptEditorData.instance.installations != null)
					installations.AddRange(IDEScriptEditorData.instance.installations);

				if (!IDEScriptEditorData.instance.initializedOnce || !FileSystemUtil.EditorPathExists(path))
				{
					foreach (var item in m_IDEScriptEditor.m_Discoverability.PathCallback())
						installations.Add(item);

					var matching = installations
						.FirstOrDefault(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase));

					if (matching.Path == null && installations.Any())
					{
						var best = installations.First();
						CodeEditor.SetExternalScriptEditor(best.Path);
						path = best.Path;
					}
					else if (matching.Path == null && !FileSystemUtil.EditorPathExists(path))
					{
						if (installations.Any())
						{
							var best = installations.First();
							CodeEditor.SetExternalScriptEditor(best.Path);
							path = best.Path;
						}
					}

					ShowWarningOnUnexpectedScriptEditor(path);
					IDEScriptEditorData.instance.initializedOnce = true;
				}

				if (FileSystemUtil.EditorPathExists(path) && !installations.Any(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase)))
				{
					installations.Add(new CodeEditor.Installation
					{
						Path = path,
						Name = GetEditorDisplayName(path)
					});
				}

				IDEScriptEditorData.instance.installations = installations.ToArray();
				IDEScriptEditorData.instance.Init();

				m_IDEScriptEditor.CreateSolutionIfDoesntExist();

#pragma warning disable 618
				EditorUserBuildSettings.activeBuildTargetChanged += () =>
#pragma warning restore 618
				{
					IDEScriptEditorData.instance.hasChanges = true;
				};
			}
		}

		public bool OpenProject(string path, int line, int column)
		{
			var projectGeneration = (ProjectGeneration.ProjectGeneration)m_ProjectGeneration;
			if (path != "" && !projectGeneration.HasValidExtension(path))
			{
				return false;
			}

			if (!IsUnityScript(path))
			{
				m_ProjectGeneration.SyncIfNeeded(affectedFiles: new string[] { }, new string[] { });
			}

			return OpenFileInIDE(CurrentEditor, path, line, column);
		}

		private static string GetMacExecutablePath(string appPath)
		{
			var macOsDir = Path.Combine(appPath, "Contents", "MacOS");
			if (!Directory.Exists(macOsDir))
				return appPath;

			foreach (var file in Directory.GetFiles(macOsDir))
			{
				var name = Path.GetFileName(file);
				if (!name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
					return file;
			}

			return appPath;
		}

		private static bool OpenFileInIDE(string editorPath, string filePath, int line, int column)
		{
			if (string.IsNullOrEmpty(editorPath) || !FileSystemUtil.EditorPathExists(editorPath))
				return false;

			try
			{
				var projectDir = Directory.GetParent(Application.dataPath).FullName;
				var trimmedPath = filePath?.Trim();
				var hasGoto = !string.IsNullOrEmpty(trimmedPath) && line > 0;

				if (editorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
				{
					if (ExecutableStartsWithAny(editorPath, "neovim", "nv"))
					{
						var executablePath = GetMacExecutablePath(editorPath);
						var args = $"\"{projectDir}\"";
						if (hasGoto)
						{
							var gotoArg = column > 0
								? $"\"{trimmedPath}:{line}:{column}\""
								: $"\"{trimmedPath}:{line}\"";
							args += $" --goto {gotoArg}";
						}

						var proc = Process.Start(executablePath, args);
						proc?.WaitForExit(5000);
						return proc?.ExitCode == 0;
					}

					string openArgs;
					if (hasGoto)
					{
						if (ExecutableStartsWithAny(editorPath, "zed"))
						{
							var cliPath = GetMacExecutablePath(editorPath);
							var absolutePath = Path.Combine(projectDir, trimmedPath);
							var fileArg = column > 0
								? $"\"{absolutePath}:{line}:{column}\""
								: $"\"{absolutePath}:{line}\"";
							var proc = Process.Start(cliPath, fileArg);
							proc?.WaitForExit(5000);
							return proc?.ExitCode == 0;
						}
						else
						{
							var executablePath = GetMacExecutablePath(editorPath);
							var gotoArg = column > 0
								? $"\"{trimmedPath}:{line}:{column}\""
								: $"\"{trimmedPath}:{line}\"";
							var vsCodeArgs = $"\"{projectDir}\" --goto {gotoArg}";
							var proc = Process.Start(executablePath, vsCodeArgs);
							proc?.WaitForExit(5000);
							return proc?.ExitCode == 0;
						}
					}
					else if (!string.IsNullOrEmpty(trimmedPath))
					{
						openArgs = $"-a \"{editorPath}\" \"{trimmedPath}\"";
					}
					else
					{
						openArgs = $"-a \"{editorPath}\" \"{projectDir}\"";
					}

					var openProc = Process.Start("open", openArgs);
					openProc?.WaitForExit(5000);
					return openProc?.ExitCode == 0;
				}

				if (ExecutableStartsWithAny(editorPath, "zed"))
				{
					if (!string.IsNullOrEmpty(trimmedPath))
					{
						var absolutePath = Path.Combine(projectDir, trimmedPath);
						var fileArg = line > 0
							? column > 0
								? $"\"{absolutePath}:{line}:{column}\""
								: $"\"{absolutePath}:{line}\""
							: $"\"{absolutePath}\"";
						Process.Start(editorPath, fileArg);
					}
					else
					{
						Process.Start(editorPath, $"\"{projectDir}\"");
					}
					return true;
				}

				var arguments = $"\"{projectDir}\"";
				if (hasGoto)
				{
					var gotoArg = column > 0
						? $"\"{trimmedPath}:{line}:{column}\""
						: $"\"{trimmedPath}:{line}\"";
					arguments += $" --goto {gotoArg}";
				}

				var binProc = Process.Start(editorPath, arguments);
				binProc?.WaitForExit(5000);
				return binProc?.ExitCode == 0;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"Failed to open file in IDE editor: {e.Message}");
				try
				{
					if (!string.IsNullOrEmpty(filePath))
					{
						EditorUtility.OpenWithDefaultApp(filePath);
						return true;
					}
					return false;
				}
				catch
				{
					return false;
				}
			}
		}

		private string GetSolutionFile(string path)
		{
			if (IsUnityScript(path))
			{
				return Path.Combine(GetBaseUnityDeveloperFolder(), "Projects/CSharp/Unity.CSharpProjects.gen.sln");
			}

			var solutionFile = m_ProjectGeneration.SolutionFile();
			if (File.Exists(solutionFile))
			{
				return solutionFile;
			}

			return "";
		}

		private static bool IsUnityScript(string path)
		{
			if (UnityEditor.Unsupported.IsDeveloperBuild())
			{
				var baseFolder = GetBaseUnityDeveloperFolder().Replace("\\", "/");
				var lowerPath = path.ToLowerInvariant().Replace("\\", "/");

				if (lowerPath.Contains((baseFolder + "/Runtime").ToLowerInvariant())
					|| lowerPath.Contains((baseFolder + "/Editor").ToLowerInvariant()))
				{
					return true;
				}
			}

			return false;
		}

		private static string GetBaseUnityDeveloperFolder()
		{
			return Directory.GetParent(EditorApplication.applicationPath).Parent.Parent.FullName;
		}

		public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
		{
			installation = default;
			if (string.IsNullOrEmpty(editorPath)) return false;

			if (FileSystemUtil.EditorPathExists(editorPath) && IsSupportedInstallation(editorPath))
			{
				if (IDEScriptEditorData.instance.installations == null)
				{
					IDEScriptEditorData.instance.installations = m_Discoverability.PathCallback();
				}

				var realPath = GetEditorRealPath(editorPath);
				var editor = IDEScriptEditorData.instance.installations
					.FirstOrDefault(a => string.Equals(GetEditorRealPath(a.Path), realPath, StringComparison.OrdinalIgnoreCase));

				if (editor.Path != null)
				{
					installation = new CodeEditor.Installation
					{
						Name = editor.Name,
						Path = editor.Path
					};
					return true;
				}

				installation = new CodeEditor.Installation
				{
					Name = GetEditorDisplayName(editorPath),
					Path = editorPath
				};
				return true;
			}

			return false;
		}

		public static bool IsSupportedInstallation(string path)
		{
			if (IsAssetImportWorkerProcess())
				return false;

#if UNITY_2021_1_OR_NEWER
			if (UnityEditor.MPE.ProcessService.level == UnityEditor.MPE.ProcessLevel.Secondary)
				return false;
#elif UNITY_2020_2_OR_NEWER
			if (UnityEditor.MPE.ProcessService.level == UnityEditor.MPE.ProcessLevel.Slave)
				return false;
#elif UNITY_2020_1_OR_NEWER
			if (Unity.MPE.ProcessService.level == Unity.MPE.ProcessLevel.UMP_SLAVE)
				return false;
#endif

			if (string.IsNullOrEmpty(path))
				return false;

			if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				var appName = Path.GetFileNameWithoutExtension(path);
				return MacAppToDisplayName.ContainsKey(appName);
			}

			return ExecutableStartsWithAny(path, "code", "cursor", "windsurf", "antigravity", "zed", "neovim", "nv");
		}

		public static bool ExecutableStartsWithAny(string path, params string[] names)
		{
			var fileInfo = new FileInfo(path);
			var filename = fileInfo.Name;
			foreach (var name in names)
			{
				if (filename.StartsWith(name, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		public static string GetEditorDisplayName(string path)
		{
			if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				var appName = Path.GetFileNameWithoutExtension(path);
				if (MacAppToDisplayName.TryGetValue(appName, out var name))
					return name;
				return "IDE Editor";
			}

			var fileInfo = new FileInfo(path);
			var filename = fileInfo.Name;

			if (filename.StartsWith("antigravity", StringComparison.OrdinalIgnoreCase)) return "Antigravity";
			if (filename.StartsWith("windsurf", StringComparison.OrdinalIgnoreCase)) return "Windsurf";
			if (filename.StartsWith("cursor", StringComparison.OrdinalIgnoreCase)) return "Cursor";
			if (filename.StartsWith("code-insiders", StringComparison.OrdinalIgnoreCase)) return "VS Code Insiders";
			if (filename.StartsWith("code", StringComparison.OrdinalIgnoreCase)) return "Visual Studio Code";
			if (filename.StartsWith("zed", StringComparison.OrdinalIgnoreCase)) return "Zed";
			if (filename.StartsWith("neovim", StringComparison.OrdinalIgnoreCase)) return "Neovim";
			if (filename.StartsWith("nv", StringComparison.OrdinalIgnoreCase)) return "Neovim";

			return "IDE Editor";
		}

		private static bool IsAssetImportWorkerProcess()
		{
#if UNITY_2020_2_OR_NEWER
			return UnityEditor.AssetDatabase.IsAssetImportWorkerProcess();
#elif UNITY_2019_3_OR_NEWER
			return UnityEditor.Experimental.AssetDatabaseExperimental.IsAssetImportWorkerProcess();
#else
			return false;
#endif
		}

		public static string CurrentEditor
			=> EditorPrefs.GetString("kScriptsDefaultApp");

		public CodeEditor.Installation[] Installations => m_Discoverability.PathCallback();

		private void CreateSolutionIfDoesntExist()
		{
			if (!m_ProjectGeneration.HasSolutionBeenGenerated())
			{
				m_ProjectGeneration.Sync();
			}
		}
	}
}
