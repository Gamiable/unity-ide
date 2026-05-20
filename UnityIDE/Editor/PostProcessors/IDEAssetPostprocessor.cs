using UnityEditor;

namespace Packages.IDE.Editor.PostProcessors
{
	internal class IDEAssetPostprocessor : AssetPostprocessor
	{
		public static bool OnPreGeneratingCSProjectFiles()
		{
			return false;
		}
	}
}
