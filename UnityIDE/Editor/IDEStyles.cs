using UnityEditor;
using UnityEngine;

namespace Packages.IDE.Editor
{
	internal static class IDEStyles
	{
		static IDEStyles()
		{
			LinkLabelStyle = new GUIStyle(EditorStyles.linkLabel)
			{
				padding = GUI.skin.label.padding,
				margin = GUI.skin.label.margin
			};
			LinkLabelStyle.padding.left = 0;
			LinkLabelStyle.padding.right = 0;
			LinkLabelStyle.margin.left = 0;
			LinkLabelStyle.margin.right = 0;
		}

		public static readonly GUIStyle LinkLabelStyle;
	}
}