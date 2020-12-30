using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

namespace LFAsset.Runtime
{
    public static class ApplicationHelper
    {
        public static string ProjectRoot { get; private set; }
        public static string Library { get; private set; }
        public static string BundleResourcePath { get; private set; }
        static ApplicationHelper()
        {
            if (Application.isEditor)
            {
                Init();
            }
        }

        public static void Init()
        {
            ProjectRoot = Application.dataPath.Replace("/Assets", "");
            Library = ProjectRoot + "Library";
            BundleResourcePath = "Assets/Bundles";
        }

        public static List<string> GetAllBundleAssetsPath()
        {
            List<string> allAssets = new List<string>();
            if(Directory.Exists(BundleResourcePath))
            {
                var rets = Directory.GetFiles(BundleResourcePath, "*.*", SearchOption.AllDirectories)
                    .Where(x => !x.EndsWith(".meta") && !x.EndsWith(".cs") && !x.EndsWith(".js"));
                allAssets.AddRange(rets);
            }

            for(int i = 0; i < allAssets.Count; i++)
            {
                var res = allAssets[i];
                allAssets[i] = res.Replace("\\", "/");
            }

            return allAssets;
        }
    }
}
