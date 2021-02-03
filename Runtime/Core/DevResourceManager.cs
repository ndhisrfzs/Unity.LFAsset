#if UNITY_EDITOR
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace LFAsset.Runtime
{
    [MonoSingletonPath("Managers/DevResourceManager")]
    public class DevResourceManager : MonoSingleton<DevResourceManager>, IResourceManager
    {
        private List<string> AllResources;
        private Dictionary<string, Object> AllObjs;
        public override void OnSingletonInit()
        {
            AllObjs = new Dictionary<string, Object>();
            AllResources = ApplicationHelper.GetAllBundleAssetsPath();
        }

        public T LoadAsset<T>(string path) where T : Object
        {
            path = path.Replace("\\", "/");
            if (AllObjs.TryGetValue(path, out Object obj))
            {
                return obj as T;
            }
            else
            {
                string findTarget = path + ".";
                string findFile = AllResources.Find(x => x.Contains(findTarget));
                return AssetDatabase.LoadAssetAtPath<T>(findFile);
            }
        }

        public void UnloadAsset(string path)
        {
            try
            {
                if(AllObjs.ContainsKey(path))
                {
                    AllObjs.Remove(path);
                    Resources.UnloadUnusedAssets();
                }
            }
            catch(System.Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        public void UnloadAllAsset()
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }
    }
}
#endif
