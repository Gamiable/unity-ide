using UnityEditor;
using UnityEngine;

namespace Packages.IDE.Editor
{
	internal static class IDESettings
	{
		[SettingsProvider]
		private static SettingsProvider IDEPreferencesItem()
		{
			if (!IDEScriptEditor.IsSupportedInstallation(IDEScriptEditor.CurrentEditor))
				return null;

			var provider = new SettingsProvider("Preferences/IDE", SettingsScope.User)
			{
				label = "IDE Editor",
				keywords = new[] { "IDE", "VSCode", "Zed", "Neovim", "Code", "Cursor", "Windsurf", "Antigravity" },
				guiHandler = (searchContext) =>
				{
					EditorGUIUtility.labelWidth = 200f;
					EditorGUILayout.BeginVertical();
					GUILayout.Label("IDE Editor settings are configured in", EditorStyles.label);
					if (LinkButton("External Tools preferences"))
					{
						SettingsService.OpenProjectSettings("Project/Editor");
					}
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndVertical();
				}
			};
			return provider;
		}

		public static bool LinkButton(string url)
		{
			var bClicked = GUILayout.Button(url, IDEStyles.LinkLabelStyle);
			var rect = GUILayoutUtility.GetLastRect();
			rect.width = IDEStyles.LinkLabelStyle.CalcSize(new GUIContent(url)).x;
			EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
			return bClicked;
		}
	}
}
