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
                    return Application.persistentDataPath + "/DLC/";
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
                return Application.streamingAssetsPath + "/";
            }
        }

        /// <summary>
        /// 应用程序内部资源存放路径（www/webrequest）使用
        /// </summary>
        public static string AppResPath4Web
        {
            get
            {
                if (Application.platform == RuntimePlatform.Android)
                {
                    return Application.streamingAssetsPath + "/";
                }

                if (Application.platform == RuntimePlatform.WindowsPlayer ||
                    Application.platform == RuntimePlatform.WindowsEditor)
                {
                    return "file:///" + Application.streamingAssetsPath + "/";
                }

                return "file://" + Application.streamingAssetsPath + "/";
            }
        }
    }
}
