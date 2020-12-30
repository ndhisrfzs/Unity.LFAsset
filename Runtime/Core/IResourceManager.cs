using UnityEngine;

namespace LFAsset.Runtime
{
    public interface IResourceManager
    {
        T LoadAsset<T>(string path) where T : Object;
        void UnloadAsset(string path);
        void UnloadAllAsset();
    }
}
