using System;
using System.Collections.Generic;

using UnityEngine;

namespace LFAsset.Editor
{
    [Serializable]
    public class PackageSetting
    {
        public string AssetBundleName;
        public string FilePath;
        public List<string> FileExtens;
    }

    [CreateAssetMenu(fileName = "rules", menuName = "Tools/Create Build Rules")]
    public class BuildRules : ScriptableObject
    {
        [Header("BuildRules")]
        public List<PackageSetting> Rules;
    }
}
