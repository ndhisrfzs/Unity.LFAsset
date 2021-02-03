
using LFAsset.Runtime;

using Newtonsoft.Json;

using System.IO;

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
        private static bool isBuilding = false;
        private int selectSettingIndex;
        private string buildName;
        private PlatformType platformType;
        private string keystorePass;
        private string keyaliasPass;
        private bool isContainAB;
        private bool isBuildExe;
        private bool isHashName;
        private bool isEncrypt;
        private string secret;
        private string symbols;
        private BuildType buildType;
        private BuildOptions buildOptions = BuildOptions.AllowDebugging | BuildOptions.Development;
        private BuildAssetBundleOptions buildAssetBundleOptions = BuildAssetBundleOptions.None;

        private static BuildSetting BuildSettings;
        private static string[] SettingNames;
        private BuildConfig BuildConfig;

        [MenuItem("Tools/Build")]
        private static void ShowWindow()
        {
            isBuilding = false;
            GetWindow(typeof(BuildEditor));
        }

        [MenuItem("Tools/ClearBundleName")]
        private static void ClearAllBundles()
        {
            BuildScript.ClearAssetBundles();
        }

        [MenuItem("Tools/ClearStreamingAssets")]
        private static void ClearStreamingAssets()
        {
            FileHelper.CleanDirectory("Assets/StreamingAssets");
            FileHelper.WriteAllText("Assets/StreamingAssets/" + Versions.Filename, JsonConvert.SerializeObject(new AssetsVersion()));
            AssetDatabase.Refresh();
        }

        private void OnGUI()
        {
            if(BuildSettings == null)
            {
                BuildSettings = GetBuildSettings();
                SettingNames = GetSettingNames();
            }

            selectSettingIndex = EditorGUILayout.Popup("选择配置", selectSettingIndex, SettingNames);
            if(selectSettingIndex == SettingNames.Length - 1)
            {
                this.buildName = EditorGUILayout.TextField("配置名称", this.buildName);
            }
            else
            {
                if(BuildConfig != BuildSettings.Configs[selectSettingIndex])
                {
                    BuildConfig = BuildSettings.Configs[selectSettingIndex];

                    buildName = BuildConfig.buildName;
                    platformType = BuildConfig.platformType;
                    keystorePass = BuildConfig.keystorePass;
                    keyaliasPass = BuildConfig.keyaliasPass;
                    isContainAB = BuildConfig.isContainAB;
                    isBuildExe = BuildConfig.isBuildExe;
                    isHashName = BuildConfig.isHashName;
                    isEncrypt = BuildConfig.isEncrypt;
                    secret = BuildConfig.secret;
                    symbols = BuildConfig.symbols;
                    buildType = BuildConfig.buildType;
                    buildOptions = BuildConfig.buildOptions;
                    buildAssetBundleOptions = BuildConfig.buildAssetBundleOptions;
                }
            }

            this.platformType = (PlatformType)EditorGUILayout.EnumPopup("打包平台", this.platformType);
            this.isContainAB = EditorGUILayout.Toggle("是否将资源复制到项目", this.isContainAB);
            this.isBuildExe = EditorGUILayout.Toggle("是否打包", this.isBuildExe);
            this.isHashName = EditorGUILayout.Toggle("是否使用Hash值打包", this.isHashName);
            this.isEncrypt = EditorGUILayout.Toggle("是否加密", this.isEncrypt);
            if(this.isEncrypt)
            {
                this.secret = EditorGUILayout.TextField("加密密钥", this.secret);
            }
            if(this.platformType == PlatformType.Android)
            {
                this.keystorePass = EditorGUILayout.TextField("keystorePass", this.keystorePass);
                this.keyaliasPass = EditorGUILayout.TextField("keyaliasPass", this.keyaliasPass);
            }
            this.symbols = EditorGUILayout.TextField("宏", this.symbols);
            this.buildType = (BuildType)EditorGUILayout.EnumPopup("BuildType", this.buildType);
            this.buildAssetBundleOptions = (BuildAssetBundleOptions)EditorGUILayout.EnumFlagsField("BuildAssetBundleOptions(可多选):", this.buildAssetBundleOptions);

            switch(this.buildType)
            {
                case BuildType.Development:
                    this.buildOptions = BuildOptions.Development | BuildOptions.AutoRunPlayer | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging;
                    break;
                case BuildType.Release:
                    this.buildOptions = BuildOptions.None;
                    break;
            }

            GUILayout.BeginHorizontal();
            if(GUILayout.Button("删除配置"))
            {
                RemoveSetting(this.buildName);
            }
            if(GUILayout.Button("打开目录"))
            {
                 EditorUtility.RevealInFinder(BuildScript.RelativeDirPrefix);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if(GUILayout.Button("保存设置"))
            {
                SaveSetting();
                
                EditorUtility.DisplayDialog("保存设置", "打包设置已保存", "确定");
            }
            if(GUILayout.Button("开始打包"))
            {
                if(isBuilding)
                {
                    return;
                }

                if(this.platformType == PlatformType.None)
                {
                    Debug.LogError("请选择打包平台！");
                    return;
                }

                if (this.platformType == PlatformType.Android)
                {
                    //PlayerSettings.Android.keystoreName = "";
                    PlayerSettings.Android.keystorePass = this.keystorePass;
                    //PlayerSettings.Android.keyaliasName = "";
                    PlayerSettings.Android.keyaliasPass = this.keyaliasPass;
                }

                // 设置打包密钥
                var assetSetting = BuildScript.GetAsset<AssetSetting>("Assets/Resources/AssetSetting.asset");
                assetSetting.IsEncrypt = this.isEncrypt;
                assetSetting.Secret = this.secret;
                EditorUtility.SetDirty(assetSetting);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                BuildScript.Build(this.platformType, this.buildAssetBundleOptions, this.buildOptions, this.isContainAB, this.isBuildExe, this.isHashName, this.isEncrypt, this.secret, this.symbols);

                isBuilding = false;
            }
            GUILayout.EndHorizontal();

        }

        private static BuildSetting GetBuildSettings()
        {
            return BuildScript.GetAsset<BuildSetting>("Assets/build.asset");
        }

        private static string[] GetSettingNames()
        {
            string[] settingNames = new string[BuildSettings.Configs.Count + 1];
            int i = 0;
            for(i = 0; i < BuildSettings.Configs.Count; i++)
            {
                settingNames[i] = BuildSettings.Configs[i].buildName;
            }
            settingNames[i] = "新增...";
            return settingNames;
        }

        private void SaveSetting()
        {
            var buildSettings = GetBuildSettings();
            var config = buildSettings.Configs.Find(x => x.buildName == this.buildName);
            if (config == null)
            {
                config = new BuildConfig();
                BuildSettings.Configs.Add(config);
            }
            config.buildName = this.buildName;
            config.platformType = this.platformType;
            config.keystorePass = this.keystorePass;
            config.keyaliasPass = this.keyaliasPass;
            config.isContainAB = this.isContainAB;
            config.isBuildExe = this.isBuildExe;
            config.isHashName = this.isHashName;
            config.isEncrypt = this.isEncrypt;
            config.secret = this.secret;
            config.symbols = this.symbols;
            config.buildType = this.buildType;
            config.buildOptions = this.buildOptions;
            config.buildAssetBundleOptions = this.buildAssetBundleOptions;

            RefreshAsset(buildSettings);
            selectSettingIndex = BuildSettings.Configs.IndexOf(config);
        }

        private void RemoveSetting(string buildName)
        {
            var buildSettings = GetBuildSettings();
            buildSettings.Configs.RemoveAll(x => x.buildName == this.buildName);

            RefreshAsset(buildSettings);
            selectSettingIndex = 0;
        }

        private void RefreshAsset(BuildSetting buildSettings)
        {
            EditorUtility.SetDirty(buildSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildSettings = buildSettings;
            SettingNames = GetSettingNames();
        }
    }
}
