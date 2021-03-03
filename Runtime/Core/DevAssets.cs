#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;

using Object = UnityEngine.Object;

namespace LFAsset.Runtime
{
    public static class DevAssets
    {
        private static List<string> _allAssets = new List<string>();

        public static void Initialize()
        {
            _allAssets.Clear();

            foreach (var path in Assets.SearchPaths)
            {
                if (Directory.Exists(path))
                {
                    var rets = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(x => !x.EndsWith(".meta") && !x.EndsWith(".cs") && !x.EndsWith(".js"));
                    _allAssets.AddRange(rets);
                }
            }

            for (int i = 0; i < _allAssets.Count; i++)
            {
                var res = _allAssets[i];
                _allAssets[i] = res.Replace("\\", "/");
            }
            _allAssets = _allAssets.Distinct().ToList();
        }

        public static Object LoadAsset(string path, Type type)
        {
            path = path.Replace("\\", "/");
            string findTarget = path + ".";
            string findFile = _allAssets.Find(x => x.Contains(findTarget));
            return AssetDatabase.LoadAssetAtPath(findFile, type);
        }
    }
}
#endif
