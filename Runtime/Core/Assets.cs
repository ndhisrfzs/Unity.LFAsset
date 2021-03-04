using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object; 

namespace LFAsset.Runtime
{
    public sealed class Assets : MonoBehaviour
    {
        public const string ManifestAsset = "Assets/manifest.asset";
        public const string Extension = ".unity3d";

        public static bool runtimeMode = true;
        public static Func<string, Type, Object> loadDelegate = null;

        // 资源是否加密和加密密钥
        public static bool BundleIsEncrypt = false;
        public static string BundleSecret = string.Empty; 

        // 资源基础目录
        private static string basePath;
        // 资源更新目录
        private static string updatePath;

        // 资源
        private static Dictionary<string, AssetRequest> _assets = new Dictionary<string, AssetRequest>();
        private static List<AssetRequest> _loadingAssets = new List<AssetRequest>();
        private static List<AssetRequest> _unusedAssets = new List<AssetRequest>();

        // AB包
        private static Dictionary<string, BundleRequest> _bundles = new Dictionary<string, BundleRequest>();
        private static List<BundleRequest> _loadingBundles = new List<BundleRequest>();
        private static List<BundleRequest> _unusedBundles = new List<BundleRequest>();

        // 场景
        private static List<SceneAssetRequest> _scenes = new List<SceneAssetRequest>();

        // 资源对应AB包名
        private static readonly Dictionary<string, string> _assetToBundles = new Dictionary<string, string>();
        // AB包对应依赖列表
        private static readonly Dictionary<string, string[]> _bundleToDependencies = new Dictionary<string, string[]>();

        // 资源查找路径
        private static readonly List<string> _searchPaths = new List<string>();
        public static List<string> SearchPaths { get { return _searchPaths; } }

        #region API
        /// <summary>
        /// 初始化AB管理器
        /// </summary>
        public static void Initialize(bool isEncrypt, string secret)
        {
            var instance = FindObjectOfType<Assets>();
            if(instance == null)
            {
                instance = new GameObject("Managers/Assets").AddComponent<Assets>();
                DontDestroyOnLoad(instance);
            }

            BundleIsEncrypt = isEncrypt;
            BundleSecret = secret;

            if (string.IsNullOrEmpty(basePath))
            {
                basePath = PathHelper.AppResPath;
            }

            if (string.IsNullOrEmpty(updatePath))
            {
                updatePath = PathHelper.AppHotfixResPath;
            }

            if (BundleIsEncrypt)
            { 
                AssetBundle.SetAssetBundleDecryptKey(MD5Helper.Encrypt16(BundleSecret));
            }

            Clear();

            if (runtimeMode)
            {
                LoadManifest();
            }
        }

        /// <summary>
        /// 加入搜索路径
        /// </summary>
        /// <param name="path"></param>
        public static void AddSearchPath(string path)
        {
            _searchPaths.Add(path);
        }

        private static SceneAssetRequest _runningScene;
        /// <summary>
        /// 加载场景
        /// </summary>
        /// <param name="path"></param>
        /// <param name="additive"></param>
        /// <returns></returns>
        public static SceneAssetRequest LoadSceneAsync(string path, bool additive)
        {
            if(string.IsNullOrEmpty(path))
            {
                Debug.LogError("invalid path");
                return null;
            }

            path = GetFixedPath(path);
            var asset = new SceneAssetRequestAsync(path, additive);
            if(!additive)
            {
                if(_runningScene != null)
                {
                    _runningScene.Release();
                    _runningScene = null;
                }
                _runningScene = asset;
            }
            asset.Load();
            asset.Retain();
            _scenes.Add(asset);
            Debug.Log($"LoadScene:{path}");
            return asset;
        }

        /// <summary>
        /// 卸载场景
        /// </summary>
        /// <param name="scene"></param>
        public static void UnloadScene(SceneAssetRequest scene)
        {
            scene.Release();
        }

        /// <summary>
        /// 加载资源方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            var loader = LoadAsset(path, typeof(T), false);
            return loader.asset as T;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        /// <param name="path"></param>
        public static void UnloadAsset(string path)
        {
            if(_assets.TryGetValue(path, out var loader))
            {
                loader.Release();
            }
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        public static void UnloadAllAsset()
        {
            _unusedAssets.Clear();
            _unusedBundles.Clear();
            _assets.Clear();
            _bundles.Clear();
            _assetToBundles.Clear();
            _bundleToDependencies.Clear();
            AssetBundle.UnloadAllAssetBundles(true);
        }

        #endregion

        /// <summary>
        /// 加载Manifest
        /// </summary>
        private static void LoadManifest()
        {
            ManifestRequest request = new ManifestRequest() { name = ManifestAsset };
            request.Load();
            request.Retain();
            _assets.Add(request.name, request);
            _loadingAssets.Add(request);
            ProcessManifest(request.Manifest);
            request.Release();
        }

        /// <summary>
        /// Manifest处理
        /// </summary>
        /// <param name="manifest"></param>
        private static void ProcessManifest(Manifest manifest)
        {
            _assetToBundles.Clear();
            _bundleToDependencies.Clear();

            var assets = manifest.assets;
            var dirs = manifest.dirs;
            var bundles = manifest.bundles;

            foreach (var item in bundles)
            {
                _bundleToDependencies[item.name] = Array.ConvertAll(item.deps, id => bundles[id].name);
            }

            foreach (var item in assets)
            {
                var path = $"{dirs[item.dir]}/{item.name}";
                if(item.bundle >= 0 && item.bundle < bundles.Length)
                {
                    _assetToBundles[path] = bundles[item.bundle].name;
                }
                else
                {
                    Debug.LogError($"{path} bundle {item.bundle} not exist.");
                }
            }
        }

        /// <summary>
        /// 根据资源路径，获取资源AB包名
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="assetBundleName">资源的AB包名</param>
        /// <returns>是否获取到</returns>
        internal static bool GetAssetBundleName(string path, out string assetBundleName)
        {
            return _assetToBundles.TryGetValue(path, out assetBundleName);
        }

        /// <summary>
        /// 根据AB包名加载一个AB包
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <returns></returns>
        internal static BundleRequest LoadBundle(string assetBundleName, bool asyncMode)
        {
            if(string.IsNullOrEmpty(assetBundleName))
            {
                Debug.LogError("assetBundleName == null");
                return null;
            }

            BundleRequest bundle;
            if(_bundles.TryGetValue(assetBundleName, out bundle))
            {
                bundle.Retain();
                _loadingBundles.Add(bundle);
                return bundle;
            }

            var url = Path.Combine(GetDataPath(assetBundleName), assetBundleName);
            if(url.StartsWith("http://", StringComparison.Ordinal) ||
               url.StartsWith("https://", StringComparison.Ordinal) ||
               url.StartsWith("file://", StringComparison.Ordinal) ||
               url.StartsWith("ftp://", StringComparison.Ordinal))
            {
                bundle = new WebBundleRequestAsync();
            }
            else
            {
                bundle = asyncMode ? new BundleRequestAsync() : new BundleRequest();
            }

            bundle.name = url;
            bundle.assetBundleName = assetBundleName;
            bundle.Load();
            _loadingBundles.Add(bundle);
            _bundles.Add(assetBundleName, bundle);

            bundle.Retain();
            return bundle;
        }

        internal static BundleRequest LoadBundle(string assetBundleName)
        {
            return LoadBundle(assetBundleName, false);
        }

        internal static BundleRequest LoadBundleAsync(string assetBundleName)
        {
            return LoadBundle(assetBundleName, true);
        }


        /// <summary>
        /// 加载资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static AssetRequest LoadAsset(string path, Type type, bool async)
        {
            if(string.IsNullOrEmpty(path))
            {
                Debug.LogError("invalid path");
                return null;
            }

            path = GetFixedPath(path);
            if(_assets.TryGetValue(path, out var request))
            {
                request.Retain();
                _loadingAssets.Add(request);
                return request;
            }

            if(GetAssetBundleName(path, out string assetBundleName))
            {
                request = async 
                    ? new BundleAssetRequestAsync(assetBundleName) 
                    : new BundleAssetRequest(assetBundleName);
            }
            else
            {
                if (path.StartsWith("http://", StringComparison.Ordinal) ||
                   path.StartsWith("https://", StringComparison.Ordinal) ||
                   path.StartsWith("file://", StringComparison.Ordinal) ||
                   path.StartsWith("ftp://", StringComparison.Ordinal) ||
                   path.StartsWith("jar:file://", StringComparison.Ordinal))
                {
                    request = new WebAssetRequest();
                }
                else
                {
                    request = new AssetRequest();
                }
            }

            request.name = path;
            request.assetType = type;
            request.Load();
            request.Retain();
            _assets.Add(request.name, request);
            _loadingAssets.Add(request);

            Debug.Log($"LoadAsset:{path}");

            return request;
        }

        /// <summary>
        /// 更新资源
        /// </summary>
        private void Update()
        {
            AssetsUpdate();
            BundlesUpdate();
        }

        /// <summary>
        /// 更新Bundle，删除不再使用的Bundle
        /// </summary>
        private static void BundlesUpdate()
        {
            for (int i = 0; i < _loadingBundles.Count; i++)
            {
                var request = _loadingBundles[i];
                if(request.Update())
                {
                    continue;
                }

                _loadingBundles.RemoveAt(i);
                --i;
            }

            foreach (var item in _bundles)
            {
                if(item.Value.isDone && item.Value.IsUnused())
                {
                    _unusedBundles.Add(item.Value);
                }
            }

            for (int i = 0; i < _unusedBundles.Count; i++)
            {
                var request = _unusedBundles[i];
                request.Unload();
                _bundles.Remove(request.assetBundleName);
            }
            _unusedBundles.Clear();
        }

        /// <summary>
        /// 更新Asset，删除不再使用的Asset
        /// </summary>
        private static void AssetsUpdate()
        {
            for(int i = 0; i < _loadingAssets.Count; i++)
            {
                var request = _loadingAssets[i];
                if(request.Update())
                {
                    continue;
                }
                _loadingAssets.RemoveAt(i);
                --i;
            }

            foreach (var item in _assets)
            {
                if(item.Value.isDone && item.Value.IsUnused())
                {
                    _unusedAssets.Add(item.Value);
                }
            }

            for (int i = 0; i < _unusedAssets.Count; i++)
            {
                var request = _unusedAssets[i];
                _assets.Remove(request.name);
                request.Unload();
            }
            _unusedAssets.Clear();

            for(int i = 0; i < _scenes.Count; i++)
            {
                var request = _scenes[i];
                if (request.Update() || !request.IsUnused())
                    continue;

                _scenes.RemoveAt(i);
                request.Unload();
                --i;
            }
        }

        /// <summary>
        /// 获取资源文件所有依赖
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static bool GetAllDependencies(string name, out string[] deps)
        {
            return _bundleToDependencies.TryGetValue(name, out deps);
        }

        /// <summary>
        /// 获取资源路径,首先查找热更目录，如果热更目录没有再去项目目录找
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        private static string GetDataPath(string bundleName)
        {
            if (string.IsNullOrEmpty(updatePath))
                return basePath;

            if (File.Exists(Path.Combine(updatePath, bundleName)))
                return updatePath;

            return basePath;
        }

        private static string GetFixedPath(string path)
        {
            return path.Replace("\\", "/");
        }

        private static void Clear()
        {
            _searchPaths.Clear();
            _assetToBundles.Clear();
            _bundleToDependencies.Clear();
        }
    }
}
