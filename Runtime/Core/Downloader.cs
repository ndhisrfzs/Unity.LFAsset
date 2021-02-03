using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace LFAsset.Runtime
{
    public class Downloader : MonoBehaviour
    {
        private const float BYTES_2_MB = 1f / (1024 * 1024);

        /// <summary>
        /// 最大并行数量
        /// </summary>
        public int maxDownloads = 3;

        /// <summary>
        /// 当前的下载列表
        /// </summary>
        private readonly List<Download> _downloads = new List<Download>();
        /// <summary>
        /// 当前准备下载的列表
        /// </summary>
        private readonly List<Download> _tostarts = new List<Download>();
        /// <summary>
        /// 当前正在下载的列表
        /// </summary>
        private readonly List<Download> _progressing = new List<Download>();

        /// <summary>
        /// 状态更新，参数：当前下载大小，总大小，下载速度
        /// </summary>
        public Action<long, long, float> onUpdate;
        /// <summary>
        /// 下载完成回调
        /// </summary>
        public Action onFinished;

        /// <summary>
        /// 当前下载完成的资源数量
        /// </summary>
        private int _finishedCount;
        /// <summary>
        /// 当前最大下载资源下标
        /// </summary>
        private int _downloadIndex;
        /// <summary>
        /// 开始时间
        /// </summary>
        private float _startTime;
        /// <summary>
        /// 最后记录下的时间
        /// </summary>
        private float _lastTime;
        /// <summary>
        /// 最后记录下载的资源大小 
        /// </summary>
        private float _lastSize;

        /// <summary>
        /// 需要下载数据的总大小，去除了断点续传已经下载完成的大小
        /// </summary>
        public long size { get; private set; }
        /// <summary>
        /// 当前已经下载的数据长度
        /// </summary>
        public long position { get; private set; }
        /// <summary>
        /// 下载速度
        /// </summary>
        public float speed { get; private set; }
        
        /// <summary>
        /// 获取已经下载的数据大小
        /// </summary>
        /// <returns></returns>
        private long GetDownloadSize()
        {
            long len = 0L;
            long downloadSize = 0L;
            foreach (var download in _downloads)
            {
                downloadSize += download.position;
                len += download.len;
            }

            return downloadSize - (len - size);     // 当前下载总长度 - 下载前已经完成的长度
        }

        private bool _started;
        /// <summary>
        /// 刷新间隔
        /// </summary>
        [SerializeField] private float sampleTime = 0.5f;

        /// <summary>
        /// 开始下载资源
        /// </summary>
        public void StartDownload()
        {
            _tostarts.Clear();
            _finishedCount = 0;
            _lastSize = 0L;
            Restart();
        }

        /// <summary>
        /// 重新开始下载资源，支持断点续传
        /// </summary>
        public void Restart()
        {
            _startTime = Time.realtimeSinceStartup;
            _lastTime = 0;
            _started = true;
            _downloadIndex = _downloads.Count; 
            for(int i = 0; i < _downloads.Count; i++)
            {
                var item = _downloads[i];
                if (!item.finished)
                {
                    _tostarts.Add(item);
                    _downloadIndex = i;
                }

                if (_tostarts.Count >= maxDownloads)
                {
                    break;
                }
            }
            //_downloadIndex = _finishedCount;
            //var max = Math.Min(_downloads.Count - _finishedCount, maxDownloads);
            //for(var i = 0; i < max; i++)
            //{
            //    var item = _downloads[_downloadIndex++];
            //    _tostarts.Add(item);
            //}
        }

        /// <summary>
        /// 停止资源下载
        /// </summary>
        public void Stop()
        {
            _tostarts.Clear();
            foreach (var download in _progressing)
            {
                download.Complete(true);
                _downloads[download.id] = download.Clone() as Download; 
            }

            _progressing.Clear();
            _started = false;
        }

        public void Clear()
        {
            size = 0;
            position = 0;

            _downloadIndex = 0;
            _finishedCount = 0;
            _lastTime = 0f;
            _lastSize = 0L;
            _started = false;

            foreach (var item in _progressing)
            {
                item.Complete(true);
            }

            _progressing.Clear();
            _downloads.Clear();
            _tostarts.Clear();
        }

        /// <summary>
        /// 新增一个资源下载
        /// </summary>
        /// <param name="url">资源地址</param>
        /// <param name="fileName">资源名称</param>
        /// <param name="savePath">保存路径</param>
        /// <param name="hash">资源hash值</param>
        /// <param name="len">资源大小</param>
        public void AddDownload(string url, string fileName, string savePath, string hash, long len)
        {
            var download = new Download()
            {
                id = _downloads.Count,
                url = url,
                name = fileName,
                hash = hash,
                len = len,
                savePath = savePath,
                completed = OnFinished
            };

            _downloads.Add(download);
            var info = new FileInfo(download.tempFilePath);
            if(info.Exists)
            {
                size += len - info.Length;
            }
            else
            {
                size += len;
            }
        }

        /// <summary>
        /// 资源下载完成回调
        /// </summary>
        /// <param name="download"></param>
        private void OnFinished(Download download)
        {
            if(_downloadIndex < _downloads.Count)
            {
                var item = _downloads[_downloadIndex++];
                if (!item.finished)
                    _tostarts.Add(item);
            }

            _finishedCount++;
            Debug.Log($"OnFinished:{_finishedCount}/{_downloads.Count}");
            if(_finishedCount != _downloads.Count)
            {
                return;
            }

            onFinished?.Invoke();
            _started = false;
        }

        /// <summary>
        /// 获取资源下载速度文本
        /// </summary>
        /// <param name="downloadSpeed"></param>
        /// <returns></returns>
        public static string GetDisplaySpeed(float downloadSpeed)
        {
            if(downloadSpeed >= 1024 * 1024)
            {
                return string.Format("{0:f2}MB/s", downloadSpeed * BYTES_2_MB);
            }
            if(downloadSpeed >= 1024)
            {
                return string.Format("{0:f2}KB/s", downloadSpeed / 1024);
            }
            return string.Format("{0:f2}B/s", downloadSpeed);
        }

        /// <summary>
        /// 获取资源下载大小文本
        /// </summary>
        /// <param name="downloadSize"></param>
        /// <returns></returns>
        public static string GetDisplaySize(long downloadSize)
        {
            if(downloadSize >= 1024 * 1024)
            {
                return string.Format("{0:f2}MB", downloadSize * BYTES_2_MB);
            }
            if(downloadSize > 1024)
            {
                return string.Format("{0:f2}KB", downloadSize / 1024);
            }
            return string.Format("{0:f2}B", downloadSize);
        }

        private void Update()
        {
            if(!_started)
            {
                return;
            }

            if(_tostarts.Count > 0)
            {
                for (int i = 0; i < Math.Min(maxDownloads, _tostarts.Count); i++)
                {
                    var item = _tostarts[i];
                    item.Start();
                    _tostarts.RemoveAt(i);
                    _progressing.Add(item);
                    i--;
                }
            }

            for (int index = 0; index < _progressing.Count; index++)
            {
                var download = _progressing[index];
                download.Update();
                if(!download.finished)
                {
                    continue;
                }
                _progressing.RemoveAt(index);
                index--;
            }

            position = GetDownloadSize();

            var elapsed = Time.realtimeSinceStartup - _startTime;
            if(elapsed - _lastTime < sampleTime)
            {
                return;
            }

            var deltaTime = elapsed - _lastTime;
            speed = (position - _lastSize) / deltaTime;
            onUpdate?.Invoke(position, size, speed);

            _lastTime = elapsed;
            _lastSize = position;
        }
    }
}
