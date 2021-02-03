using System;
using System.IO;

using UnityEngine.Networking;

using Debug = UnityEngine.Debug;

namespace LFAsset.Runtime
{
    public class Download : DownloadHandlerScript, IDisposable, ICloneable
    {
        public int id { get; set; }
        public string error { get; private set; }
        public long len { get; set; }
        public string hash { get; set; }
        public string url { get; set; }
        public long position { get; set; }
        public string name { get; set; }
        public string savePath { get; set; }
        public Action<Download> completed { get; set; }
        public bool finished { get; set; }

        private UnityWebRequest _request;
        private FileStream _stream;
        private bool _running = false;

        /// <summary>
        /// 临时资源路径，使用资源hash命名文件
        /// </summary>
        public string tempFilePath
        {
            get
            {
                var dir = Path.GetDirectoryName(savePath);
                return string.Format($"{dir}/{hash}");
            }
        }

        protected override float GetProgress()
        {
            return position * 1f / len;
        }

        protected override byte[] GetData()
        {
            return null;
        }

        [Obsolete]
        protected override void ReceiveContentLength(int contentLength)
        {
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if(!string.IsNullOrEmpty(_request.error))
            {
                error = _request.error;
                Complete();
                return true;
            }

            _stream.Write(data, 0, dataLength);
            position += dataLength;
            return _running;
        }

        protected override void CompleteContent()
        {
            Complete();
        }

        public override string ToString()
        {
            return $"{url}, size:{len}, hash:{hash}";
        }

        public void Start()
        {
            if(_running)
            {
                return;
            }

            error = null;
            finished = false;
            _running = true;
            _stream = new FileStream(tempFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            position = _stream.Length;
            if(position < len)
            {
                _stream.Seek(position, SeekOrigin.Begin);
                _request = UnityWebRequest.Get(url);
                _request.SetRequestHeader("Range", $"bytes={position}-");
                _request.downloadHandler = this;
                _request.SendWebRequest();
                Debug.Log("Start Download:" + url);
            }
            else
            {
                Complete();
            }
        }

        public void Update()
        {
            if(_running)
            {
                if(_request.isDone && _request.downloadedBytes < (ulong)len)
                {
                    error = "unknown error: downloadedBytes < len";
                }
                if(!string.IsNullOrEmpty(_request.error))
                {
                    error = _request.error;
                }    
            }
        }


        public new void Dispose()
        {
            if(_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
                _stream = null;
            }

            if(_request != null)
            {
                _request.Abort();
                _request.Dispose();
                _request = null;
            }

            base.Dispose();
            _running = false;
            finished = true;
        }

        public void Complete(bool stop = false)
        {
            Dispose();
            if(stop)
            {
                return;
            }
            CheckError();
        }

        private void CheckError()
        {
            if(File.Exists(tempFilePath))
            {
                if(string.IsNullOrEmpty(error))
                {
                    using(var fs = File.OpenRead(tempFilePath))
                    {
                        // 校验文件长度
                        if(fs.Length != len)
                        {
                            error = $"file size error:{fs.Length}";
                        }

                        // 校验文件MD5
                        if(!hash.Equals(MD5Helper.Encrypt32(fs), StringComparison.OrdinalIgnoreCase))
                        {
                            error = $"file verification failed, File:{name} MD5:{hash}";
                        }
                    }
                }

                if(string.IsNullOrEmpty(error))
                {
                    File.Copy(tempFilePath, savePath, true);
                    File.Delete(tempFilePath);
                    Debug.Log($"Complete Download:{url}");

                    completed?.Invoke(this);
                    completed = null;
                }
                else
                {
                    File.Delete(tempFilePath);
                }
            }
            else
            {
                error = "not find file";
            }
        }

        public void Retry()
        {
            Dispose();
            Start();
        }

        public object Clone()
        {
            return new Download()
            {
                id = id,
                hash = hash,
                url = url,
                len = len,
                savePath = savePath,
                completed = completed,
                name = name
            };
        }
    }
}
