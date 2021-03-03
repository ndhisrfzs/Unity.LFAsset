using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LFAsset.Runtime
{
    public enum LoadState
    {
        Init,
        LoadAssetBundle,
        LoadAsset,
        Loaded,
        Unload
    }

    public class AssetRequest : Reference 
    {
        private LoadState _loadState = LoadState.Init;
        public Type assetType;
        public string name;

        public Action<AssetRequest> completed;

        public AssetRequest()
        {
            name = null;
            loadState = LoadState.Init;
        }

        public LoadState loadState
        {
            get { return _loadState; }
            set
            {
                _loadState = value;
                if(value == LoadState.Loaded)
                {
                    Complete();
                }
            }
        }

        private void Complete()
        {
            if (completed != null)
            {
                completed(this);
                completed = null;
            }
        }

        public virtual bool isDone
        {
            get { return loadState == LoadState.Loaded || loadState == LoadState.Unload; }
        }

        public virtual float progress
        {
            get { return 1; }
        }

        public virtual string error { get; protected set; }

        public string text { get; protected set; }
        public byte[] bytes { get; protected set; }
        public Object asset { get; internal set; }

        internal virtual void Load()
        {
            if(!Assets.runtimeMode && Assets.loadDelegate != null)
            {
                asset = Assets.loadDelegate(name, assetType);
            }
            if(asset == null)
            {
                error = $"file not exist:{ name }";
            }
            loadState = LoadState.Loaded;
        }

        internal virtual bool Update()
        {
            if (!isDone)
            {
                return true;
            }

            if (completed == null)
            {
                return false;
            }

            try
            {
                completed.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            completed = null;
            return false;
        }

        internal virtual void Unload()
        {
            if (asset == null)
                return;

            if(!Assets.runtimeMode)
            {
                if(!(asset is GameObject))
                {
                    Resources.UnloadAsset(asset);
                }
            }

            asset = null;
            loadState = LoadState.Unload;
        }
    }

    public class ManifestRequest : AssetRequest
    {
        private string assetName;
        private BundleRequest request;

        internal override void Load()
        {
            assetName = Path.GetFileName(name);
            var assetBundleName = assetName.Replace(".asset", ".unity3d");
            request = Assets.LoadBundle(assetBundleName);

            loadState = LoadState.Loaded;
        }

        internal override void Unload()
        {
            if(request != null)
            {
                request.Release();
                request = null;
            }

            loadState = LoadState.Unload;
        }

        public Manifest Manifest 
        {
            get
            {
                return request.assetBundle.LoadAsset<Manifest>(assetName);
            }
        }
    }

    public class BundleAssetRequest : AssetRequest
    {
        protected readonly string assetBundleName;
        protected BundleRequest bundleRequest;
        protected List<BundleRequest> children = new List<BundleRequest>();

        public BundleAssetRequest(string bundle)
        {
            assetBundleName = bundle;
        }

        internal override void Load()
        {
            if (Assets.GetAllDependencies(assetBundleName, out var bundles))
            {
                foreach (var item in bundles)
                {
                    children.Add(Assets.LoadBundle(item));
                }
            }
            bundleRequest = Assets.LoadBundle(assetBundleName);
            var assetName = Path.GetFileName(name);
            var ab = bundleRequest.assetBundle;
            if(ab != null)
            {
                asset = ab.LoadAsset(assetName, assetType);
            }
            if(asset == null)
            {
                error = "asset == null";
            }

            loadState = LoadState.Loaded;
        }

        internal override void Unload()
        {
            if(bundleRequest != null)
            {
                bundleRequest.Release();
                bundleRequest = null;
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

    public class BundleAssetRequestAsync : BundleAssetRequest
    {
        private AssetBundleRequest _request;

        public BundleAssetRequestAsync(string bundle)
            : base(bundle)
        {
        }

        public override float progress
        {
            get
            {
                if (isDone)
                {
                    return 1;
                }

                if(loadState == LoadState.Init)
                {
                    return 0;
                }

                if(_request != null)
                {
                    return _request.progress * 0.7f + 0.3f;
                }

                if (bundleRequest == null)
                {
                    return 1;
                }

                var value = bundleRequest.progress;
                var max = children.Count;
                if(max <= 0)
                {
                    return value * 0.3f;
                }

                for(var i = 0; i < max; i++)
                {
                    var item = children[i];
                    value += item.progress;
                }

                return value / (max + 1) * 0.3f;
            }
        }

        private bool OnError(BundleRequest bundleRequest)
        {
            error = bundleRequest.error;
            if(!string.IsNullOrEmpty(error))
            {
                loadState = LoadState.Loaded;
                return true;
            }

            return false;
        }

        internal override void Load()
        {
            if (Assets.GetAllDependencies(assetBundleName, out var bundles))
            {
                foreach (var item in bundles)
                {
                    children.Add(Assets.LoadBundle(item));
                }
            }
            bundleRequest = Assets.LoadBundleAsync(assetBundleName);
        }

        internal override bool Update()
        {
            if (!base.Update())
            {
                return false;
            }

            if (loadState == LoadState.Init) return true;

            if(_request == null)
            {
                if (!bundleRequest.isDone)
                {
                    return true;
                }

                if (OnError(bundleRequest)) 
                {
                    return false;
                }

                for(int i = 0; i < children.Count; i++)
                {
                    var item = children[i];
                    if (!item.isDone)
                    {
                        return true;
                    }
                    if(OnError(bundleRequest))
                    {
                        return false;
                    }
                }

                var assetName = Path.GetFileName(name);
                _request = bundleRequest.assetBundle.LoadAssetAsync(assetName, assetType);
                if(_request == null)
                {
                    error = "request == null";
                    loadState = LoadState.Loaded;
                    return false;
                }

                return true;
            }

            if(_request.isDone)
            {
                asset = _request.asset;
                loadState = LoadState.Loaded;
                if(asset == null)
                {
                    error = "asset == null";
                }
                return false;
            }

            return true;
        }

        internal override void Unload()
        {
            _request = null;
            loadState = LoadState.Unload;
            base.Unload();
        }
    }


    public class SceneAssetRequest : AssetRequest 
    {
        public string assetBundleName;
        public LoadSceneMode loadSceneMode;
        protected readonly string sceneName;
        protected BundleRequest bundleRequest;
        protected List<BundleRequest> children = new List<BundleRequest>();

        public SceneAssetRequest(string path, bool addictive)
        {
            name = path;
            Assets.GetAssetBundleName(path, out assetBundleName);
            sceneName = Path.GetFileNameWithoutExtension(name);
            loadSceneMode = addictive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        }

        internal override void Load()
        {
            if(!string.IsNullOrEmpty(assetBundleName))
            {
                if (Assets.GetAllDependencies(assetBundleName, out var bundles))
                {
                    foreach (var item in bundles)
                    {
                        children.Add(Assets.LoadBundle(item));
                    }
                }
                bundleRequest = Assets.LoadBundle(assetBundleName);
                if(bundleRequest != null)
                {
                    SceneManager.LoadScene(sceneName, loadSceneMode);
                }
            }
            else
            {
                SceneManager.LoadScene(sceneName, loadSceneMode);
            }

            loadState = LoadState.Loaded;
        }

        internal override void Unload()
        {
            if (bundleRequest != null)
                bundleRequest.Release();

            if(children.Count > 0)
            {
                foreach (var item in children)
                {
                    item.Release();
                }
                children.Clear();
            }

            if(loadSceneMode == LoadSceneMode.Additive)
            {
                if(SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    SceneManager.UnloadSceneAsync(sceneName);
                }
            }

            bundleRequest = null;
            loadState = LoadState.Unload;
        }
    }

    public class SceneAssetRequestAsync : SceneAssetRequest
    {
        private AsyncOperation _request;

        public SceneAssetRequestAsync(string path, bool addictive)
            : base(path, addictive)
        {
        }

        public override float progress
        {
            get
            {
                if(isDone)
                {
                    return 1;
                }

                if(loadState == LoadState.Init)
                {
                    return 0;
                }

                if (_request != null)
                {
                    return _request.progress * 0.7f + 0.3f;
                }

                if(bundleRequest == null)
                {
                    return 1;
                }

                var value = bundleRequest.progress;
                var max = children.Count;
                if(max <= 0)
                {
                    return value * 0.3f;
                }

                for(int i = 0; i < max; i++)
                {
                    var item = children[i];
                    value += item.progress;
                }

                return value / (max + 1) * 0.3f;
            }
        }

        private bool OnError(BundleRequest bundleRequest)
        {
            error = bundleRequest.error;
            if(!string.IsNullOrEmpty(error))
            {
                loadState = LoadState.Loaded;
                return true;
            }

            return false;
        }

        private void LoadScene()
        {
            try
            {
                _request = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
                loadState = LoadState.LoadAsset;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                error = e.Message;
                loadState = LoadState.Loaded;
            }
        }

        internal override void Load()
        {
            if(!string.IsNullOrEmpty(assetBundleName))
            {
                if (Assets.GetAllDependencies(assetBundleName, out var bundles))
                {
                    foreach (var item in bundles)
                    {
                        children.Add(Assets.LoadBundleAsync(item));
                    }
                }
                bundleRequest = Assets.LoadBundleAsync(assetBundleName);
                loadState = LoadState.LoadAssetBundle;
            }
            else
            {
                LoadScene();
            }
        }

        internal override bool Update()
        {
            if (base.Update())
            {
                return false;
            }

            if(loadState == LoadState.Init)
            {
                return true;
            }

            if(_request == null)
            {
                if(bundleRequest == null)
                {
                    error = "bundle == null";
                    loadState = LoadState.Loaded;
                    return false;
                }

                if (!bundleRequest.isDone)
                {
                    return true;
                }

                if(OnError(bundleRequest))
                {
                    return false;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    var item = children[i];
                    if(!item.isDone)
                    {
                        return true;
                    }

                    if(OnError(item))
                    {
                        return false;
                    }
                }

                LoadScene();
                return true;
            }

            if(_request.isDone)
            {
                loadState = LoadState.Loaded;
                return false;
            }

            return true;
        }

        internal override void Unload()
        {
            _request = null;
            loadState = LoadState.Unload;
            base.Unload();
        }
    }

    public class WebAssetRequest : AssetRequest
    {
        private UnityWebRequest _request;

        public override float progress
        {
            get
            {
                if (isDone)
                {
                    return 1;
                }

                if(loadState == LoadState.Init)
                {
                    return 0;
                }

                if(_request == null)
                {
                    return 1;
                }

                return _request.downloadProgress;
            }
        }

        public override string error
        {
            get 
            {
                return _request.error;
            }
        }

        internal override void Load()
        {
            if(assetType == typeof(AudioClip))
            {
                _request = UnityWebRequestMultimedia.GetAudioClip(name, AudioType.WAV);
            }
            else if(assetType == typeof(Texture2D))
            {
                _request = UnityWebRequestTexture.GetTexture(name);
            }
            else
            {
                _request = new UnityWebRequest(name);
                _request.downloadHandler = new DownloadHandlerBuffer();
            }

            _request.SendWebRequest();
            loadState = LoadState.LoadAsset;
        }

        internal override bool Update()
        {
            if(!base.Update())
            {
                return false;
            }

            if(loadState == LoadState.LoadAsset)
            {
                if(_request == null)
                {
                    error = "request == null";
                    return false;
                }

                if(!string.IsNullOrEmpty(_request.error))
                {
                    error = _request.error;
                    loadState = LoadState.Loaded;
                    return false;
                }

                if(_request.isDone)
                {
                    GetAsset();
                    loadState = LoadState.Loaded;
                    return false;
                }

                return true;
            }

            return true;
        }

        private void GetAsset()
        {
            if(assetType == typeof(Texture2D))
            {
                asset = DownloadHandlerTexture.GetContent(_request);
            }
            else if(assetType == typeof(AudioClip))
            {
                asset = DownloadHandlerAudioClip.GetContent(_request);
            }
            else if(assetType == typeof(TextAsset))
            {
                text = _request.downloadHandler.text;
            }
            else
            {
                bytes = _request.downloadHandler.data;
            }
        }

        internal override void Unload()
        {
            if(asset != null)
            {
                Object.Destroy(asset);
                asset = null;
            }

            if(_request != null)
            {
                _request.Dispose();
            }

            bytes = null;
            text = null;
            loadState = LoadState.Unload;
        }
    }


    public class BundleRequest : AssetRequest
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
            }

            loadState = LoadState.Loaded;
        }

        internal override void Unload()
        {
            if(assetBundle == null)
            {
                return;
            }

            assetBundle.Unload(true);
            assetBundle = null;
            loadState = LoadState.Unload;
        }
    }

    public class BundleRequestAsync : BundleRequest
    {
        private AssetBundleCreateRequest _request;

        public override float progress
        {
            get
            {
                if (isDone)
                {
                    return 1;
                }

                if (loadState == LoadState.Init)
                {
                    return 0;
                }

                if (_request == null)
                {
                    return 1;
                }
                return _request.progress;
            }
        }

        internal override void Load()
        {
            if(_request == null)
            {
                _request = AssetBundle.LoadFromFileAsync(name);
                if(_request == null)
                {
                    error = $"{name} LoadFromFile failed";
                    return;
                }

                loadState = LoadState.LoadAssetBundle;
            }
        }

        internal override bool Update()
        {
            if (!base.Update()) 
            {
                return false;
            }

            if(loadState == LoadState.LoadAssetBundle)
            {
                if(_request.isDone)
                {
                    assetBundle = _request.assetBundle;
                    if (assetBundle == null)
                    {
                        error = $"unable to load AssetBundle:{name}";
                    }
                    loadState = LoadState.Loaded;
                    return false;
                }
            }

            return true;
        }

        internal override void Unload()
        {
            _request = null;
            loadState = LoadState.Unload;
            base.Unload();
        }
    }

    public class WebBundleRequestAsync : BundleRequest
    {
        private UnityWebRequest _request;
        public bool cache;
        public Hash128 hash;

        public override float progress
        {
            get
            {
                if(isDone)
                {
                    return 1;
                }

                if(loadState == LoadState.Init)
                {
                    return 0;
                }

                if(_request == null)
                {
                    return 1;
                }

                return _request.downloadProgress;
            }
        }

        internal override void Load()
        {
            _request = cache ? UnityWebRequestAssetBundle.GetAssetBundle(name, hash) : UnityWebRequestAssetBundle.GetAssetBundle(name);
            _request.SendWebRequest();
            loadState = LoadState.LoadAssetBundle;
        }

        internal override bool Update()
        {
            if (!base.Update()) 
            {
                return false;
            }

            if(loadState == LoadState.LoadAssetBundle)
            {
                if(_request.isDone)
                {
                    assetBundle = DownloadHandlerAssetBundle.GetContent(_request);
                    if (assetBundle == null)
                    {
                        error = $"unable to load AssetBundle:{name}";
                    }
                    loadState = LoadState.Loaded;
                    return false;
                }
            }

            return true;
        }

        internal override void Unload()
        {
            if(_request != null)
            {
                _request.Dispose();
                _request = null;
            }

            loadState = LoadState.Unload;
            base.Unload();
        }
    }
}
