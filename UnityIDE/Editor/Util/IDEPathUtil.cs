using System.IO;

namespace Packages.IDE.Editor.Util
{
	internal static class IDEPathUtil
	{
		public static bool IsIDEDevEditor(string editorPath)
		{
			if (editorPath == null)
				return false;
			return "code-dev".Equals(Path.GetFileNameWithoutExtension(editorPath));
		}
	}
}