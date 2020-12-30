using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LFAsset.Runtime
{
    public class AssetLoader : Reference
    {
        public Object asset { get; internal set; }
        public string name;
        public Type assetType;

        internal virtual void Load()
        {
        }

        internal virtual void Unload()
        {
            if (asset == null)
                return;

            asset = null;
        }
    }

    public class ManifestLoader : AssetLoader
    {
        private string assetName;
        private BundleLoader bundle;

        internal override void Load()
        {
            assetName = Path.GetFileName(name);
            var assetBundleName = assetName.Replace(".asset", ".unity3d").ToLower(); 
            bundle = AssetBundleManager.Ins.LoadBundle(assetBundleName);
        }

        internal override void Unload()
        {
            if(bundle != null)
            {
                bundle.Release();
                bundle = null;
            }
        }

        public ManifestConfig Manifest 
        {
            get
            {
                return bundle.assetBundle.LoadAsset<ManifestConfig>(assetName);
            }
        }
    }

    public class BundleAssetLoader : AssetLoader
    {
        protected readonly string assetBundleName;
        protected BundleLoader bundleLoader;
        protected List<BundleLoader> children = new List<BundleLoader>();

        public BundleAssetLoader(string bundle)
        {
            assetBundleName = bundle;
        }

        internal override void Load()
        {
            var bundles = AssetBundleManager.Ins.GetAllDependencies(name);
            foreach (var item in bundles)
            {
                children.Add(AssetBundleManager.Ins.LoadBundle(item));
            }
            bundleLoader = AssetBundleManager.Ins.LoadBundle(assetBundleName);
            var assetName = Path.GetFileName(name);
            var ab = bundleLoader.assetBundle;
            if(ab != null)
            {
                asset = ab.LoadAsset(assetName, assetType);
            }
        }

        internal override void Unload()
        {
            if(bundleLoader != null)
            {
                bundleLoader.Release();
                bundleLoader = null;
            }

            if(children.Count > 0)
            {
                foreach (var item in children)
                {
                    item.Release();
                }
                children.Clear();
            }

            asset = null;
        }
    }

    public class BundleLoader : AssetLoader
    {
        public string assetBundleName { get; set; }

        public AssetBundle assetBundle
        {
            get { return asset as AssetBundle; }
            internal set { asset = value; }
        }

        internal override void Load()
        {
            asset = AssetBundle.LoadFromFile(name);
            if(assetBundle == null)
            {
                Debug.LogError($"{name} LoadFromFile failed.");
                return;
            }
        }

        internal override void Unload()
        {
            if(assetBundle == null)
            {
                return;
            }

            assetBundle.Unload(true);
            assetBundle = null;
        }
    }
}
