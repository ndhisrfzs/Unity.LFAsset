using System.IO;

using UnityEngine;

namespace LFAsset.Runtime
{
    public static class ResMgr
    {
        private static IResourceManager resourceManager = null;

        static ResMgr()
        {
#if UNITY_EDITOR
            resourceManager = DevResourceManager.Ins;
#else
            AssetBundleManager.Ins.Init();  // 初始化，必须独立调用，不然会递归
            resourceManager = AssetBundleManager.Ins;
#endif
        }

        public static T GetResource<T>(string path, string name) where T : UnityEngine.Object
        {
            var filePath = Path.Combine(path, name);
            T obj = resourceManager.LoadAsset<T>(filePath);
            if(obj == null)
            {
                // 从热更目录没找到，尝试从Resource目录加载资源
                obj = LoadFromResources<T>(filePath);
            }
            return obj;
        }

        public static T LoadFromResources<T>(string path) where T : UnityEngine.Object
        {
            return Resources.Load<T>(path);
        }

        public static void Unload(string path)
        {
            resourceManager.UnloadAsset(path);
        }

        public static void UnloadAll()
        {
            resourceManager.UnloadAllAsset();
        }
    }
}
