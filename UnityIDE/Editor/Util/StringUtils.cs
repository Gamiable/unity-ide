using System.IO;

namespace Packages.IDE.Editor.Util
{
	internal static class StringUtils
	{
		public static string NormalizePath(this string path)
		{
			return path.Replace(Path.DirectorySeparatorChar == '\\'
				? '/'
				: '\\', Path.DirectorySeparatorChar);
		}
	}
}