using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace LFAsset.Runtime
{
    [MonoSingletonPath("Managers/AssetBundleManager")]
    public class AssetBundleManager : MonoSingleton<AssetBundleManager>, IResourceManager
    {
        private const string ManifestAsset = "Manifest.asset";

        // 资源基础目录
        private static string basePath;
        // 资源更新目录
        private static string updatePath;

        // 依赖配置文件
        private ManifestConfig Manifest;

        // 当前加载的资源和AB包
        private Dictionary<string, AssetLoader> _assets = new Dictionary<string, AssetLoader>();
        private Dictionary<string, BundleLoader> _bundles = new Dictionary<string, BundleLoader>();

        // 当前没用的资源和AB包
        private List<AssetLoader> _unusedAssets = new List<AssetLoader>();
        private List<BundleLoader> _unusedBundles = new List<BundleLoader>();

        /// <summary>
        /// 初始化AB管理器
        /// </summary>
        public void Init()
        {
            if(string.IsNullOrEmpty(basePath))
            {
                basePath = PathHelper.AppResPath;
            }

            if(string.IsNullOrEmpty(updatePath))
            {
                updatePath = PathHelper.AppHotfixResPath;
            }

            LoadManifest();
        }

        /// <summary>
        /// 加载Manifest
        /// </summary>
        internal void LoadManifest()
        {
            ManifestLoader loader = new ManifestLoader() { name = ManifestAsset };
            AddAssetLoader(loader);
            loader.Retain();
            Manifest = loader.Manifest;
            loader.Release();
        }

        /// <summary>
        /// 根据资源路径，获取资源AB包名
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="assetBundleName">资源的AB包名</param>
        /// <returns>是否获取到</returns>
        internal bool GetAssetBundleName(string path, out string assetBundleName)
        {
            assetBundleName = string.Empty;
            var assetBundle = Manifest.GetManifest(path);
            if(assetBundle != null)
            {
                assetBundleName = assetBundle.Path;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 根据AB包名加载一个AB包
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <returns></returns>
        internal BundleLoader LoadBundle(string assetBundleName)
        {
            if(string.IsNullOrEmpty(assetBundleName))
            {
                Debug.LogError("assetBundleName == null");
                return null;
            }

            BundleLoader bundle;
            if(_bundles.TryGetValue(assetBundleName, out bundle))
            {
                bundle.Retain();
                return bundle;
            }

            var url = Path.Combine(GetDataPath(assetBundleName), assetBundleName);

            bundle = new BundleLoader();
            bundle.name = url;
            bundle.assetBundleName = assetBundleName;
            bundle.Load();
            _bundles.Add(assetBundleName, bundle);

            bundle.Retain();
            return bundle;
        }

        /// <summary>
        /// 加载资源方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            path = path.Replace("\\", "/");
            var loader = LoadAsset(path.ToLower(), typeof(T));
            return loader.asset as T;
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private AssetLoader LoadAsset(string path, Type type)
        {
            if(string.IsNullOrEmpty(path))
            {
                Debug.LogError("invalid path");
                return null;
            }

            if(_assets.TryGetValue(path, out var loader))
            {
                loader.Retain();
                return loader;
            }

            if(GetAssetBundleName(path, out string assetBundleName))
            {
                loader = new BundleAssetLoader(assetBundleName);
            }
            else
            {
                loader = new AssetLoader();
            }

            loader.name = path;
            loader.assetType = type;
            AddAssetLoader(loader);
            loader.Retain();

            Debug.Log($"LoadAsset:{path}");

            return loader;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        /// <param name="path"></param>
        public void UnloadAsset(string path)
        {
            path = path.ToLower();

            if(_assets.TryGetValue(path, out var loader))
            {
                loader.Release();
            }
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        public void UnloadAllAsset()
        {
            Manifest = null;
            _unusedAssets.Clear();
            _unusedBundles.Clear();
            _assets.Clear();
            _bundles.Clear();
            AssetBundle.UnloadAllAssetBundles(true);
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
        private void BundlesUpdate()
        {
            foreach (var item in _bundles)
            {
                if(item.Value.IsUnused())
                {
                    _unusedBundles.Add(item.Value);
                }
            }

            foreach (var item in _unusedBundles)
            {
                item.Unload();
                _bundles.Remove(item.name);
            }
            _unusedBundles.Clear();
        }

        /// <summary>
        /// 更新Asset，删除不再使用的Asset
        /// </summary>
        private void AssetsUpdate()
        {
            foreach (var item in _assets)
            {
                if(item.Value.IsUnused())
                {
                    _unusedAssets.Add(item.Value);
                }
            }

            foreach (var item in _unusedAssets)
            {
                _assets.Remove(item.name);
                item.Unload();
            }
            _unusedAssets.Clear();
        }

        /// <summary>
        /// 获取资源文件所有依赖
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal List<string> GetAllDependencies(string name)
        {
            return Manifest.GetDependencies(name);
        }

        /// <summary>
        /// 获取资源路径,首先查找热更目录，如果热更目录没有再去项目目录找
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        private string GetDataPath(string bundleName)
        {
            if (string.IsNullOrEmpty(updatePath))
                return basePath;

            if (File.Exists(Path.Combine(updatePath, bundleName)))
                return updatePath;

            return basePath;
        }

        /// <summary>
        /// 将资源加载器加到_asset缓存表中
        /// </summary>
        /// <param name="loader"></param>
        private void AddAssetLoader(AssetLoader loader)
        {
            _assets.Add(loader.name, loader);
            loader.Load();
        }
    }
}
