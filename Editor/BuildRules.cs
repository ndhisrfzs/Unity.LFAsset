using LFAsset.Runtime;

using System;
using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEngine;

namespace LFAsset.Editor
{
    /// <summary>
    /// 资源包命名方式
    /// </summary>
    public enum NameBy
    {
        Explicit,
        Path,
        Directory,
        TopDirectory
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class RuleAsset
    {
        public string bundle;
        public string path;
    }

    [Serializable]
    public class RuleBundle
    {
        public string name;
        public string[] assets;
    }

    [Serializable]
    public class BuildRule
    {
        [Tooltip("搜索路径")] public string searchPath;
        [Tooltip("搜索通配符，多个之间用,(英文逗号)隔开")] public string searchPattern;
        [Tooltip("命名规则")] public NameBy nameBy = NameBy.Path;
        [Tooltip("Explicit名称")] public string assetBundleName;

        public string[] GetAssets()
        {
            var patterns = searchPattern.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if(!Directory.Exists(searchPath))
            {
                Debug.LogWarning($"Rule searchPath not exist:{searchPath}");
                return new string[0];
            }

            var getFiles = new List<string>();
            foreach (var item in patterns)
            {
                var files = Directory.GetFiles(searchPath, item, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (Directory.Exists(file)) continue;   // 如果是文件夹，跳过
                    var ext = Path.GetExtension(file).ToLower();
                    if ((ext == ".fbx" || ext == ".anim" || ext == ".ds_store") && !item.Contains(ext)) continue;
                    if (!BuildRules.ValidateAsset(file)) continue;
                    var asset = file.Replace("\\", "/");
                    getFiles.Add(asset);
                }
            }

            return getFiles.ToArray();
        }
    }
   
    [CreateAssetMenu(fileName = "rules", menuName = "Tools/Create Build Rules")]
    public class BuildRules : ScriptableObject
    {
        private readonly Dictionary<string, string> _asset2Bundles = new Dictionary<string, string>();
        private readonly Dictionary<string, string[]> _conflicted = new Dictionary<string, string[]>();
        private readonly List<string> _duplicated = new List<string>();
        private readonly Dictionary<string, HashSet<string>> _tracker = new Dictionary<string, HashSet<string>>();

        [Header("Patterns")]
        public string searchPatternAsset = "*.asset";
        public string searchPatternController = "*.controller";
        public string searchPatternDir = "*";
        public string searchPatternMaterial = "*.mat";
        public string searchPatternPng = "*.png";
        public string searchPatternPrefab = "*.prefab";
        public string searchPatternScene = "*.Unity";
        public string searchPatternText = "*.txt,*.bytes,*.json,*.csv,*.xml,*.htm,*.html,*.yaml,*.fnt";

        public static bool namedByHash = true;
        public static string extension = ".unity3d";

        [Tooltip("构建版本号")]
        [Header("Builds")]
        public int version;
        [Tooltip("BuildPlayer的时候被打包的场景")] public SceneAsset[] scenesInBuild = new SceneAsset[0];
        public BuildRule[] rules = new BuildRule[0];
        [Header("Assets")]
        [HideInInspector] public RuleAsset[] ruleAssets = new RuleAsset[0];
        [HideInInspector] public RuleBundle[] ruleBundles = new RuleBundle[0];

        #region API
        public int AddVersion()
        {
            version = version + 1;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return version;
        }

        public void Apply(bool isHashName)
        {
            namedByHash = isHashName;
            Clear();
            CollectAssets();
            AnalysisAssets();
            OptimizeAssets();
            Save();
        }

        public AssetBundleBuild[] GetBuilds()
        {
            var builds = new List<AssetBundleBuild>();
            foreach (var bundle in ruleBundles)
            {
                builds.Add(new AssetBundleBuild
                {
                    assetNames = bundle.assets,
                    assetBundleName = bundle.name
                });
            }
            return builds.ToArray();
        }

        #endregion

        #region Private
        internal static bool ValidateAsset(string asset)
        {
            if (!asset.StartsWith("Assets/")) return false;

            var ext = Path.GetExtension(asset).ToLower();
            return ext != ".dll" && ext != ".cs" && ext != ".meta" && ext != ".js" && ext != ".boo" && ext != ".ds_store";
        }

        private static bool IsScene(string asset)
        {
            return asset.EndsWith(".unity");
        }

        private string RuledAssetBundleName(string name)
        {
            if(namedByHash)
            {
                return MD5Helper.Encrypt32(name) + extension;
            }
            return name.Replace("\\", "_").Replace("/", "_").Replace(".", "_").ToLower() + extension;
        }

        /// <summary>
        /// 资源跟踪
        /// </summary>
        /// <param name="asset">资源路径</param>
        /// <param name="bundle">引用这个资源的AB包名</param>
        private void Track(string asset, string bundle)
        {
            HashSet<string> assets;
            if(!_tracker.TryGetValue(asset, out assets))
            {
                assets = new HashSet<string>();
                _tracker.Add(asset, assets);
            }

            assets.Add(bundle);
            if(assets.Count > 1) // 资源被多个AB引用
            {
                if(!_asset2Bundles.ContainsKey(asset))
                {
                    _duplicated.Add(asset);
                }
            }
        }

        /// <summary>
        /// 获取所有Bundles
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetBundles()
        {
            var bundles = new Dictionary<string, List<string>>();
            foreach (var item in _asset2Bundles)
            {
                var bundle = item.Value;
                List<string> list;
                if(!bundles.TryGetValue(bundle, out list))
                {
                    list = new List<string>();
                    bundles[bundle] = list;
                }

                if (!list.Contains(item.Key)) list.Add(item.Key);
            }

            return bundles;
        }

        /// <summary>
        /// 清除
        /// </summary>
        private void Clear()
        {
            _tracker.Clear();
            _duplicated.Clear();
            _conflicted.Clear();
            _asset2Bundles.Clear();
        }

        /// <summary>
        /// 保存
        /// </summary>
        private void Save()
        {
            var getBundles = GetBundles();
            ruleBundles = new RuleBundle[getBundles.Count];
            int i = 0;
            foreach (var item in getBundles)
            {
                ruleBundles[i] = new RuleBundle
                {
                    name = item.Key,
                    assets = item.Value.ToArray()
                };

                i++;
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 收集资源
        /// </summary>
        private void CollectAssets()
        {
            for(int i = 0, max = rules.Length; i < max; i++)
            {
                var rule = rules[i];
                if (EditorUtility.DisplayCancelableProgressBar($"收集资源{i}/{max}", rule.searchPath, i / (float)max))
                    break;
                ApplyRule(rule);
            }

            var list = new List<RuleAsset>();
            foreach (var item in _asset2Bundles)
            {
                list.Add(new RuleAsset
                {
                    path = item.Key,
                    bundle = item.Value,
                });
            }
            list.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
            ruleAssets = list.ToArray();
        }

        /// <summary>
        /// 应用规则生成AssetBundleName
        /// </summary>
        /// <param name="rule"></param>
        private void ApplyRule(BuildRule rule)
        {
            var assets = rule.GetAssets();
            switch(rule.nameBy)
            {
                case NameBy.Explicit:
                    {
                        foreach (var asset in assets)
                        {
                            OptimizeAsset(asset, rule.assetBundleName);
                        }
                    }
                    break;
                case NameBy.Path:
                    {
                        foreach (var asset in assets)
                        {
                            OptimizeAsset(asset, asset);
                        }
                    }
                    break;
                case NameBy.Directory:
                    {
                        foreach (var asset in assets)
                        {
                            OptimizeAsset(asset, Path.GetDirectoryName(asset));
                        }
                    }
                    break;
                case NameBy.TopDirectory:
                    {
                        var starIndex = rule.searchPath.Length;
                        foreach (var asset in assets)
                        {
                            var dir = Path.GetDirectoryName(asset);
                            if(!string.IsNullOrEmpty(dir))
                            {
                                if(!dir.Equals(rule.searchPath))
                                {
                                    var pos = dir.IndexOf("/", starIndex + 1, StringComparison.Ordinal);
                                    if (pos != -1) dir = dir.Substring(0, pos);
                                }
                            }
                            OptimizeAsset(asset, dir);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// 分析资源依赖
        /// </summary>
        private void AnalysisAssets()
        {
            var getBundles = GetBundles();
            int i = 0, max = getBundles.Count;
            foreach (var item in getBundles)
            {
                var bundle = item.Key;
                if (EditorUtility.DisplayCancelableProgressBar($"分析依赖{i}/{max}", bundle, i / (float)max)) break;

                var assetPaths = getBundles[bundle];
                if(assetPaths.Exists(IsScene) && !assetPaths.TrueForAll(IsScene))
                {
                    _conflicted.Add(bundle, assetPaths.ToArray());
                }
                var dependencies = AssetDatabase.GetDependencies(assetPaths.ToArray(), true);
                if(dependencies.Length > 0)
                {
                    foreach (var asset in dependencies)
                    {
                        if(ValidateAsset(asset))
                        {
                            Track(asset, bundle);
                        }
                    }
                }

                i++;
            }

        }

        /// <summary>
        /// 优化资源
        /// </summary>
        private void OptimizeAssets()
        {
            int i = 0, max = _conflicted.Count;
            foreach (var item in _conflicted)
            {
                if (EditorUtility.DisplayCancelableProgressBar($"优化冲突{i}/{max}", item.Key, i / (float)max)) break;
                var list = item.Value;
                foreach (var asset in list)
                {
                    if(!IsScene(asset))
                    {
                        _duplicated.Add(asset);
                    }
                }
                i++;
            }

            for (i = 0, max = _duplicated.Count; i < max; i++)
            {
                var item = _duplicated[i];
                if (EditorUtility.DisplayCancelableProgressBar($"优化冗余{i}/{max}", item, i / (float)max)) break;

                OptimizeAsset(item, item);
            }
        }

        /// <summary>
        /// 优化资源
        /// </summary>
        /// <param name="asset"></param>
        private void OptimizeAsset(string asset, string bundle)
        {
            if(asset.EndsWith(".shader") || asset.EndsWith(".mat") || asset.EndsWith(".shadervariants"))
            {
                _asset2Bundles[asset] = RuledAssetBundleName("shaders");
            }
            else
            {
                _asset2Bundles[asset] = RuledAssetBundleName(bundle);
            }
        }
        #endregion
    }
}
