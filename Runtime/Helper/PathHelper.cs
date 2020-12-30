using System.IO;

using UnityEngine;

namespace LFAsset.Runtime
{
    public static class PathHelper
    {
        /// <summary>
        /// 应用程序热更目录
        /// </summary>
        public static string AppHotfixResPath
        {
            get
            {
                if(Application.isMobilePlatform)
                {
                    return Path.Combine(Application.persistentDataPath, Application.productName);
                }
                else
                {
                    return AppResPath;
                }
            }
        }

        /// <summary>
        /// 应用程序内部资源目录
        /// </summary>
        public static string AppResPath
        {
            get
            {
                return Application.streamingAssetsPath;
            }
        }

        /// <summary>
        /// 应用程序内部资源存放路径（www/webrequest）使用
        /// </summary>
        public static string AppResPath4Web
        {
            get
            {
#if UNITY_IOS || UNITY_STANDALONE_OSX
                return $"file://{Application.streamingAssetsPath}";
#else
                return Application.streamingAssetsPath;
#endif
            }
        }
    }
}
