using LFAsset.Runtime;

using UnityEditor;

using UnityEngine;

namespace LFAsset.Editor
{
    public class EditorRuntimeInitializeOnLoad
    {
        [RuntimeInitializeOnLoadMethod]
        private static void OnInitialize()
        {
#if !ASYNC
            Assets.runtimeMode = false;
            Assets.Initialize(false, null);

            var rule = BuildScript.GetBuildRules();
            foreach (var item in rule.rules)
            {
                Assets.AddSearchPath(item.searchPath);
            }
            Assets.loadDelegate = DevAssets.LoadAsset;
            DevAssets.Initialize();
#endif
        }

        [InitializeOnLoadMethod]
        private static void OnEditorInitialize()
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
