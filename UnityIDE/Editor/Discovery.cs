using System.Linq;
using Unity.CodeEditor;

namespace Packages.IDE.Editor
{
	internal class Discovery : IDiscovery
	{
		private static readonly IDEDiscovery s_Discovery = new IDEDiscovery();

		public CodeEditor.Installation[] PathCallback()
		{
			return s_Discovery.PathCallback();
		}
	}
}
