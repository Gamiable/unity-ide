using System;
using UnityEditor;
using UnityEngine;

namespace Packages.IDE.Editor
{
#if UNITY_2020_1_OR_NEWER
	[FilePath("Library/com.gamiable.unity-ide/PersistedState.asset", FilePathAttribute.Location.ProjectFolder)]
#endif
	internal class IDEScriptEditorPersistedState : ScriptableSingleton<IDEScriptEditorPersistedState>
	{
		[SerializeField] private long lastWriteTicks;
		[SerializeField] private long manifestJsonLastWriteTicks;

		public DateTime? LastWrite
		{
			get => DateTime.FromBinary(lastWriteTicks);
			set
			{
				if (!value.HasValue) return;
				lastWriteTicks = value.Value.ToBinary();
				Save(true);
			}
		}
    
		public DateTime? ManifestJsonLastWrite
		{
			get => DateTime.FromBinary(manifestJsonLastWriteTicks);
			set
			{
				if (!value.HasValue) return;
				manifestJsonLastWriteTicks = value.Value.ToBinary();
				Save(true);
			}
		}
	}
}