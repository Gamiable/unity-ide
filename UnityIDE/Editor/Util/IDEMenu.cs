using JetBrains.Annotations;
using Packages.IDE.Editor;
using Unity.CodeEditor;

namespace Packages.IDE.Editor.Util
{
	[UsedImplicitly]
	internal static class IDEMenu
	{
		[UsedImplicitly]
		public static void MenuOpenProject()
		{
			if (IDEScriptEditor.IsSupportedInstallation(IDEScriptEditor.CurrentEditor))
			{
				CodeEditor.CurrentEditor.SyncAll();
				CodeEditor.CurrentEditor.OpenProject();
			}
		}
	}
}
