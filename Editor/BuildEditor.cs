
using UnityEditor;

using UnityEngine;

namespace LFAsset.Editor
{
    public enum BuildType
    {
        Development,
        Release
    }

    public class BuildEditor : EditorWindow
    {
        private PlatformType platformType;
        private bool isContainAB;
        private bool isBuildExe;
        private bool isHashName;
        private BuildType buildType;
        private BuildOptions buildOptions = BuildOptions.AllowDebugging | BuildOptions.Development;
        private BuildAssetBundleOptions buildAssetBundleOptions = BuildAssetBundleOptions.None;

        [MenuItem("Tools/Build2")]
        public static void ShowWindow()
        {
            GetWindow(typeof(BuildEditor));
        }

        private void OnGUI()
        {
            platformType = (PlatformType)EditorGUILayout.EnumPopup(platformType);
            isContainAB = EditorGUILayout.Toggle("是否将资源复制到项目", this.isContainAB);
            isBuildExe = EditorGUILayout.Toggle("是否打包", this.isBuildExe);
            isHashName = EditorGUILayout.Toggle("是否使用Hash值打包", this.isHashName);
            buildType = (BuildType)EditorGUILayout.EnumPopup("BuildType", this.buildType);
            buildAssetBundleOptions = (BuildAssetBundleOptions)EditorGUILayout.EnumFlagsField("BuildAssetBundleOptions(可多选):", this.buildAssetBundleOptions);

            switch(buildType)
            {
                case BuildType.Development:
                    buildOptions = BuildOptions.Development | BuildOptions.AutoRunPlayer | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging;
                    break;
                case BuildType.Release:
                    buildOptions = BuildOptions.None;
                    break;
            }

            if(GUILayout.Button("开始打包"))
            {
                if(this.platformType == PlatformType.None)
                {
                    Debug.LogError("请选择打包平台！");
                    return;
                }

                //PlayerSettings.Android.keystoreName = "";
                //PlayerSettings.Android.keystorePass = "";
                //PlayerSettings.Android.keyaliasName = "";
                //PlayerSettings.Android.keyaliasPass = "";

                BuildScript.Build(platformType, buildAssetBundleOptions, buildOptions, isContainAB, isBuildExe, isHashName);
            }

        }
    }
}
