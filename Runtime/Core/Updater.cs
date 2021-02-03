using System;
using System.Collections;
using System.IO;

using UnityEngine;
using UnityEngine.Networking;

namespace LFAsset.Runtime
{
    public enum EventId
    {
        Yes,
        No,
    }

    /// <summary>
    /// 更新资源时的一些回调
    /// </summary>
    public interface IUpdater
    {
        void OnStart();
        void OnMessage(string message);
        void OnProgress(float progress);
        void OnVersion(string var);
        void OnClear();
    }

    /// <summary>
    /// 一些事件，需要留给外部实现
    /// </summary>
    public interface IUpdaterEvent
    {
        /// <summary>
        /// 找不到网络
        /// </summary>
        /// <param name="action"></param>
        void OnNotReachable(Action<EventId> action);  
    }

    [RequireComponent(typeof(Downloader))]
    [RequireComponent(typeof(NetworkMonitor))]
    public class Updater : MonoBehaviour, IUpdater, IUpdaterEvent, INetworkMonitorListener
    {
        enum Step
        {
            Wait,
            CheckLocal,
            CheckRemote,
            Prepared,
            Download,
        }

        private Step _step;

        [SerializeField] private string baseUrl = "http://127.0.0.1:8088/";

        public IUpdaterEvent eventListener { get;set; }
        public IUpdater listener { get; set; }

        private Downloader _downloader;
        private NetworkMonitor _monitor;
        private string _platform;
        private string _savePath;
        private AssetsVersion _version;
            
        private bool _reachabilityChanged;
        public void OnReachablityChanged(NetworkReachability reachability)
        {
            if(_step == Step.Wait)
            {
                return;
            }

            _reachabilityChanged = true;
            if(_step == Step.Download)
            {
                _downloader.Stop();
            }

            if(reachability == NetworkReachability.NotReachable)
            {
                // 找不到网络
                OnNotReachable((id) =>
                {
                    if(id == EventId.Yes)
                    {
                        if(_step == Step.Download)
                        {
                            _downloader.Restart();
                        }
                        else
                        {
                            StartUpdate();
                        }
                        _reachabilityChanged = false;
                    }
                    else
                    {
                        Quit();
                    }
                });
            }
            else
            {
                if(_step == Step.Download)
                {
                    _downloader.Restart();
                }
                else
                {
                    StartUpdate();
                }
                _reachabilityChanged = false;
            }
        }

        private void Start()
        {
#if UNITY_EDITOR && !ASYNC
            OnVersion(string.Empty);
#else
            _downloader = gameObject.GetComponent<Downloader>();
            _downloader.onUpdate = OnUpdate;
            _downloader.onFinished = OnComplete;

            _monitor = gameObject.GetComponent<NetworkMonitor>();
            _monitor.listener = this;

            _savePath = PathHelper.AppHotfixResPath;
            _platform = GetPlatformForAssetBundles();

            _step = Step.Wait;

            StartUpdate();
#endif
        }

        /// <summary>
        /// 资源下载回调
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="size"></param>
        /// <param name="speed"></param>
        private void OnUpdate(long progress, long size, float speed)
        {
            OnMessage(string.Format("下载中...{0}/{1}, 速度:{2}",
                Downloader.GetDisplaySize(progress),
                Downloader.GetDisplaySize(size),
                Downloader.GetDisplaySpeed(speed)));

            OnProgress(progress * 1f / size);
        }

        /// <summary>
        /// 资源更新完成回调
        /// </summary>
        private void OnComplete()
        {
            OnProgress(1);
            OnMessage("更新完成");
            var version = Versions.LoadVersion(_savePath + Versions.Filename);
            if(version.Version > 0)
            {
                OnVersion(version.Version.ToString());
            }

            // 资源下载完成，加载Manifest文件
            AssetBundleManager.Ins.LoadManifest();
        }

        private IEnumerator _checking;
        /// <summary>
        /// 开始更新资源
        /// </summary>
        public void StartUpdate()
        {
            OnStart();

            if(_checking != null)
            {
                StopCoroutine(_checking);
            }

            _checking = Checking();
            StartCoroutine(_checking);
        }

        /// <summary>
        /// 检查资源
        /// </summary>
        /// <returns></returns>
        private IEnumerator Checking()
        {
            if(!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
            }

            if(_step == Step.Wait)
            {
                _step = Step.CheckLocal;
            }

            if(_step == Step.CheckLocal)    // 检查本地资源
            {
                yield return CheckLocalVersion();
            }

            if(_step == Step.CheckRemote)   // 检查远程资源
            {
                yield return CheckRemoteVersion();
            }

            if(_step == Step.Prepared)
            {
                var totalSize = _downloader.size;
                if(totalSize > 0)
                {
                    _downloader.StartDownload();
                    _step = Step.Download;
                }
                else
                {
                    OnComplete();
                }
            }
        }

        /// <summary>
        /// 将本地资源和热更资源比较，选较新的作为基础资源
        /// </summary>
        /// <returns></returns>
        private IEnumerator CheckLocalVersion()
        {
            var updateVer = Versions.LoadVersion(_savePath + Versions.Filename);
            var basePath = PathHelper.AppResPath4Web;
            var request = UnityWebRequest.Get(basePath + Versions.Filename);
            var path = _savePath + Versions.Filename + ".tmp";
            request.downloadHandler = new DownloadHandlerFile(path);
            yield return request.SendWebRequest();
            if(string.IsNullOrEmpty(request.error))
            {
                var baseVer = Versions.LoadBaseVersion(path);
                if(baseVer.Version > updateVer.Version)
                {
                    // 因为包体中的资源比本地热更资源更新，删除所有本地热更资源
                    FileHelper.CleanDirectory(_savePath);
                }
                // 删除临时文件
                FileHelper.RemoveFile(path);
            }

            _step = Step.CheckRemote;
            request.Dispose();
        }

        /// <summary>
        /// 获取远程版本信息
        /// </summary>
        /// <returns></returns>
        private IEnumerator CheckRemoteVersion()
        {
            OnMessage("Download Remote Version");
            var request = UnityWebRequest.Get(GetDownloadUrl(Versions.Filename));
            request.downloadHandler = new DownloadHandlerFile(_savePath + Versions.Filename);
            yield return request.SendWebRequest();
            var error = request.error;
            request.Dispose();
            if (!string.IsNullOrEmpty(error))
            {
                OnMessage(error);
                yield break;
            }

            try
            {
                _version = Versions.LoadVersion(_savePath + Versions.Filename);
                if (_version.FileInfos.Count > 0)
                {
                    PrepareDownloads();
                    _step = Step.Prepared;
                }
                else
                {
                    OnComplete();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// 增加一个资源下载
        /// </summary>
        /// <param name="name"></param>
        /// <param name="hash"></param>
        /// <param name="size"></param>
        private void AddDownload(string name, string hash, long size)
        {
            _downloader.AddDownload(GetDownloadUrl(name), name, _savePath + name, hash, size);
        }

        /// <summary>
        /// 验证远程资源是否需要下载，并且准备下载资源
        /// </summary>
        private void PrepareDownloads()
        {
            foreach (var fileInfo in _version.FileInfos)
            {
                if(Versions.IsNew(string.Format("{0}{1}", _savePath, fileInfo.Key), fileInfo.Value.Size, fileInfo.Value.MD5))
                {
                    AddDownload(fileInfo.Key, fileInfo.Value.MD5, fileInfo.Value.Size);
                }
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            Debug.Log("OnApplicationFocus");
            if(_reachabilityChanged || _step == Step.Wait)
            {
                return;
            }

            if(focus)
            {
                if(_step == Step.Download)
                {
                    _downloader.Restart();
                }
                else
                {
                    StartUpdate();
                }
            }
            else
            {
                if(_step == Step.Download)
                {
                    _downloader.Stop();
                }
            }
        }

        /// <summary>
        /// 获取下载地址
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string GetDownloadUrl(string fileName)
        {
            return string.Format("{0}{1}/StreamingAssets/{2}", baseUrl, _platform, fileName);
        }

        private static string GetPlatformForAssetBundles()
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#elif UNITY_WEBGL
            return "WebGL";
#elif UNITY_STANDALONE_OSX
            return "iOS";
#else
            return "PC";
#endif
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void Clear()
        {
            OnClear();
        }

#region IUpdater
        public void OnStart()
        {
            if(listener != null)
            {
                listener.OnStart();
            }
        }

        public void OnVersion(string var)
        {
            if(listener != null)
            {
                listener.OnVersion(var);
            }
        }

        public void OnClear()
        {
            OnMessage("数据清除完毕");
            OnProgress(0);
            _version = null;
            if (_downloader != null)
            {
                _downloader.Clear();
            }
            _step = Step.Wait;
            _reachabilityChanged = false;

            if (listener != null)
            {
                listener.OnClear();
            }

            if (Directory.Exists(_savePath))
            {
                Directory.Delete(_savePath, true);
            }
        }

        public void OnMessage(string message)
        {
            if (listener != null)
            {
                listener.OnMessage(message);
            }
        }

        public void OnProgress(float progress)
        {
            if(listener != null)
            {
                listener.OnProgress(progress);
            }
        }
#endregion

#region IUpdaterEvent
        public void OnNotReachable(Action<EventId> action)
        {
            if(eventListener != null)
            {
                eventListener.OnNotReachable(action);
            }
        }
#endregion
    }
}
