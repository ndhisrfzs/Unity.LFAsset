using UnityEngine;

namespace LFAsset.Runtime
{
    public static class Asset 
    {
        private static IResourceManager resourceManager = null;

        static Asset()
        {
#if UNITY_EDITOR && !ASYNC
            resourceManager = DevResourceManager.Ins;
#else
            // 初始化，必须独立调用，不然会递归
            AssetSetting assetSetting = Resources.Load<AssetSetting>("AssetSetting");
            if (assetSetting == null)
            {
                AssetBundleManager.Ins.Init(false, null);
            }
            else
            {
                AssetBundleManager.Ins.Init(assetSetting.IsEncrypt, assetSetting.Secret);
            }
            resourceManager = AssetBundleManager.Ins;
#endif
        }

        public static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            T obj = resourceManager.LoadAsset<T>(path);
            if(obj == null)
            {
                // 从热更目录没找到，尝试从Resource目录加载资源
                obj = Resources.Load<T>(path);
            }
            return obj;
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
