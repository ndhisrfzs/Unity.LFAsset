using System;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace LFAsset.Editor
{
    [Serializable]
    public class BuildConfig
    {
        public string buildName;
        public PlatformType platformType;
        public string keystorePass;
        public string keyaliasPass;
        public bool isContainAB;
        public bool isBuildExe;
        public bool isHashName;
        public bool isEncrypt;
        public string secret;
        public string symbols;
        public BuildType buildType;
        public BuildOptions buildOptions;
        public BuildAssetBundleOptions buildAssetBundleOptions;
    }

    public class BuildSetting : ScriptableObject
    {
        public List<BuildConfig> Configs = new List<BuildConfig>();
    }
}
