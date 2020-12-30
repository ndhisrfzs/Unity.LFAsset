using System;
using System.Collections.Generic;

using UnityEngine;

namespace LFAsset.Runtime
{
    [Serializable]
    public class BundleRef
    {
        public string name;
        public ManifestItem item;
    }

    public class ManifestConfig : ScriptableObject
    {
        public bool IsHashName = false;
        public List<BundleRef> Bundles;

        public List<string> GetDependencies(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var item = Bundles.Find(x => x.name == name);
                if (item != null)
                {
                    return item.item.Depend;
                }

                Debug.LogError($"can't find asset dependencies name:{name}");
            }

            return null;
        }

        public ManifestItem GetManifest(string name)
        {
            if(!string.IsNullOrEmpty(name))
            {
                var item = Bundles.Find(x => x.name == name);
                if (item != null)
                {
                    return item.item;
                }

                Debug.LogError($"can't find asset manifest name:{name}");
            }

            return null;
        }
    }
}
