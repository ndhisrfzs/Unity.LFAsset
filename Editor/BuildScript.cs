using LFAsset.Runtime;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using UnityEditor;

using UnityEngine;

namespace LFAsset.Editor
{
    public enum PlatformType
    {
        None,
        Android,
        IOS,
        PC,
        MacOS
    }

    public class BuildInfo
    {
        public class AssetData
        {
            /// <summary>
            /// ID
            /// </summary>
            public int Id { get; set; }
            /// <summary>
            /// 资源类型
            /// </summary>
            public int Type { get; set; }
            /// <summary>
            /// AB包名
            /// 默认AB等于自己文件名
            /// 当自己处于某个AB包时这个不为null
            /// </summary>
            public string ABName { get; set; }
            /// <summary>
            /// 被依赖次数
            /// </summary>
            public int RefCount { get; set; }
            /// <summary>
            /// Hash值
            /// </summary>
            public string Hash { get; set; }
            /// <summary>
            /// 依赖列表
            /// </summary>
            public List<string> Depends { get; set; } = new List<string>();
        }

        public string Time { get; set; }
        public Dictionary<string, AssetData> AssetDatas = new Dictionary<string, AssetData>();
    }

    public static class BuildScript 
    {
        private const string Manifest = "Manifest.asset";
        private const string Version = "var";
        private const string bundles = "/bundles/";
        private const string RelativeDirPrefix = "./Release";
        private const string BuildFolder = "./Release/{0}/StreamingAssets/";
        private const string BuildInfoFile = "./Release/{0}/BuildInfo.json";

        private readonly static Dictionary<string, List<string>> DependenciesInfoCache = new Dictionary<string, List<string>>();
        private readonly static Dictionary<string, string> FilesHashCache = new Dictionary<string, string>();

        private readonly static Dictionary<AssetType, List<string>> AssetTypeConfigs = new Dictionary<AssetType, List<string>>()
        {
            { AssetType.Prefab, new List<string>(){ ".prefab" } },
            { AssetType.SpriteAtlas, new List<string>(){ ".spriteatlas" } },
        };
        
        public static void Build(PlatformType platform, BuildAssetBundleOptions buildAssetBundleOptions, BuildOptions buildOptions, bool isContainAB, bool isBuildExe, bool isHashName)
        {
            string exeName = "Game";
            BuildTarget buildTarget = BuildTarget.StandaloneWindows;
            switch(platform)
            {
                case PlatformType.PC:
                    buildTarget = BuildTarget.StandaloneWindows64;
                    exeName += ".exe";
                    break;
                case PlatformType.Android:
                    buildTarget = BuildTarget.Android;
                    exeName += ".apk";
                    break;
                case PlatformType.IOS:
                    buildTarget = BuildTarget.iOS;
                    break;
                case PlatformType.MacOS:
                    buildTarget = BuildTarget.StandaloneOSX;
                    break;
            }

            // 清除缓存
            DependenciesInfoCache.Clear();
            FilesHashCache.Clear();

            string outputPath = string.Format(BuildFolder, platform);           // Bundle输出路径
            string buildInfoPath = string.Format(BuildInfoFile, platform);      // 打包资源记录文件路径

            // 先生成Manifest文件
            ManifestConfig manifest = GetAsset<ManifestConfig>(Path.Combine(ApplicationHelper.BundleResourcePath, Manifest));

            // 获取所有AssetBundle资源的主路径
            List<string> assetPaths = ApplicationHelper.GetAllBundleAssetsPath();
            for(int i = 0; i < assetPaths.Count; i++)
            {
                assetPaths[i] = assetPaths[i].ToLower();
            }

            // 获取上次打包的记录文件
            BuildInfo oldBuildInfo = new BuildInfo();
            if(File.Exists(buildInfoPath))
            {
                string content = File.ReadAllText(buildInfoPath);
                oldBuildInfo = JsonConvert.DeserializeObject<BuildInfo>(content);
            }

            // 收集Shader
            SharderCollection.GenShaderVariant();

            // 获取本次需要打包资源
            BuildInfo newBuildInfo = GetAssetInfo(assetPaths, isHashName);
            FileHelper.WriteAllText(buildInfoPath, JsonConvert.SerializeObject(newBuildInfo));

            // 获取差异更新资源
            var rebuildInfo = GetAssetChanges(oldBuildInfo, newBuildInfo);

            // 整理路径名
            Dictionary<string, string> abNameDict = GetPath2ABNames(newBuildInfo.AssetDatas, isHashName);   // 获取资源路径对应ABName
            foreach (var asset in newBuildInfo.AssetDatas)
            {
                if(abNameDict.TryGetValue(asset.Key, out string newName))
                {
                    asset.Value.ABName = newName;
                }

                for(int i = 0; i < asset.Value.Depends.Count; i++)
                {
                    if(abNameDict.TryGetValue(asset.Value.Depends[i], out newName))
                    {
                        asset.Value.Depends[i] = newName;
                    }
                }

                asset.Value.Depends = asset.Value.Depends.Distinct().ToList();
                asset.Value.Depends.Remove(asset.Value.ABName);
            }

            // 移除不再使用的资源
            RemoveUnusedAssets(newBuildInfo, outputPath);

            // 生成Manifest文件
            manifest.IsHashName = isHashName;
            manifest.Bundles = new List<BundleRef>();
            foreach (var asset in newBuildInfo.AssetDatas)
            {
                if (asset.Key.Contains(bundles))    // 只需要记录bundles目录下的文件即可
                {
                    var mi = new ManifestItem(asset.Value.ABName, (AssetType)asset.Value.Type, new List<string>(asset.Value.Depends));
                    manifest.Bundles.Add(new BundleRef() { name = BundlePath(asset.Key), item = mi });
                }
            }
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 设置AB名
            Dictionary<string, AssetImporter> assetImporterCache = SetABName(rebuildInfo);
            AssetDatabase.Refresh();

            Debug.Log("开始打包AssetBundle");
            BuildPipeline.BuildAssetBundles(outputPath, buildAssetBundleOptions, buildTarget);
            Debug.Log("打包AssetBundle结束");
            // 生成Version文件
            GenerateVersionInfo(outputPath);

            // 清理AB包名
            ClearABName(assetImporterCache);
            AssetDatabase.Refresh();

            if (isContainAB)
            {
                Debug.Log("开始复制资源");
                string streamingAssetsPath = "Assets/StreamingAssets";
                if(Directory.Exists(streamingAssetsPath))
                {
                    FileHelper.CleanDirectory(streamingAssetsPath);
                }
                FileHelper.CopyDirectory(outputPath, streamingAssetsPath);
                Debug.Log("复制资源结束");
                AssetDatabase.Refresh();
            }

            if (isBuildExe)
            {
                Debug.Log("开始打包游戏");
                BuildPipeline.BuildPlayer(GetScenePaths(), $"{RelativeDirPrefix}/{exeName}", buildTarget, buildOptions);
                Debug.Log("打包游戏结束");
            }
        }

        /// <summary>
        /// 获取场景
        /// </summary>
        /// <returns></returns>
        private static string[] GetScenePaths()
        {
            List<string> scenes = new List<string>();
            for(int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                {
                    scenes.Add(EditorBuildSettings.scenes[i].path);
                }
            }
            return scenes.ToArray();
        }

        /// <summary>
        /// 获取打包规则配置
        /// </summary>
        /// <returns></returns>
        private static BuildRules GetBuildRules()
        {
            return GetAsset<BuildRules>("Assets/rules.asset");
        }

        /// <summary>
        /// 规范资源AB名
        /// </summary>
        /// <param name="assets"></param>
        /// <returns></returns>
        private static Dictionary<string, string> GetPath2ABNames(Dictionary<string, BuildInfo.AssetData> assets, bool isHashName)
        {
            Dictionary<string, string> path2Name = new Dictionary<string, string>();
            foreach (var asset in assets)
            {
                if(isHashName)
                {
                    var abName = AssetDatabase.AssetPathToGUID(asset.Value.ABName);
                    if(!string.IsNullOrEmpty(abName))
                    {
                        path2Name.Add(asset.Key, abName);
                    }
                    else
                    {
                        path2Name.Add(asset.Key, asset.Value.ABName);
                    }
                }
                else
                {
                    path2Name.Add(asset.Key, FormatABName(asset.Value.ABName));
                }
            }

            return path2Name;
        }

        /// <summary>
        /// 规范化AB包名
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string FormatABName(string name)
        {
            string abName = name;
            if (abName.Contains(bundles))
            {
                int index = abName.IndexOf(bundles);
                abName = abName.Substring(index + bundles.Length);
            }
            // 去除后缀
            string extension = Path.GetExtension(abName);
            if (!string.IsNullOrEmpty(extension))
            {
                abName = abName.Replace(extension, "");
            }

            return abName.Replace("/", "_") + ".unity3d";
        }

        /// <summary>
        /// 规范Bundle资源路径
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string BundlePath(string path)
        {
            if (path.Contains(bundles))
            {
                int index = path.IndexOf(bundles);
                path = path.Substring(index + bundles.Length);
            }
            // 去除后缀
            string extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension))
            {
                path = path.Replace(extension, "");
            }
            return path;
        }

        private static T GetAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }
            return asset;
        }

        /// <summary>
        /// 获取需要打包资源信息
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        private static BuildInfo GetAssetInfo(List<string> paths, bool isHashName)
        {
            Dictionary<string, List<string>> packageAssets = new Dictionary<string, List<string>>();
            // 获取图集信息
            var atlas = paths.FindAll(x => x.EndsWith(".spriteatlas"));
            for (int i = 0; i < atlas.Count; i++)
            {
                var asset = atlas[i];
                var dps = GetDependencies(asset);
                packageAssets[asset] = dps;
            }

            // 搜集设置的规则包
            BuildRules roles = GetBuildRules();
           
            BuildInfo buildInfo = new BuildInfo();
            buildInfo.Time = DateTime.Now.ToShortDateString();
            int id = 0;
            foreach (string path in paths)
            {
                List<string> dependencies = GetDependencies(path);
                foreach (string assetPath in dependencies)
                {
                    if (buildInfo.AssetDatas.ContainsKey(assetPath))
                    {
                        // 依赖文件已存在
                        continue;
                    }

                    var asset = new BuildInfo.AssetData();
                    asset.Id = id;
                    asset.Hash = GetHashFromAssets(assetPath, isHashName);
                    asset.ABName = assetPath;
                    asset.Type = (int)AssetType.Other;
                    string ext = Path.GetExtension(assetPath);
                    foreach (var item in AssetTypeConfigs)
                    {
                        if (item.Value.Contains(ext))
                        {
                            asset.Type = (int)item.Key;
                            break;
                        }
                    }

                    List<string> dependeAssets = GetDependencies(assetPath);
                    // 修正依赖关系，删除自己依赖自己
                    foreach (var depend in dependeAssets)
                    {
                        if(depend != assetPath)
                        {
                            asset.Depends.Add(depend);
                        }
                    }

                    buildInfo.AssetDatas.Add(assetPath, asset);
                    // 图集
                    foreach (var item in packageAssets)
                    {
                        if (item.Value.Contains(assetPath))
                        {
                            asset.ABName = item.Key;
                            break;
                        }
                    }
                    // 规则包
                    foreach (var role in roles.Rules)
                    {
                        if (role.FilePath == "*" || assetPath.Contains(role.FilePath.ToLower()))
                        {
                            if (role.FileExtens.Contains(".*") || role.FileExtens.Contains(ext))
                            {
                                asset.ABName = role.AssetBundleName;
                                break;
                            }
                        }
                    }

                    id++;
                }
            }

            // 加入规则AB包
            foreach (var role in roles.Rules)
            {
                var asset = new BuildInfo.AssetData();
                asset.ABName = role.AssetBundleName;
                var rets = buildInfo.AssetDatas.Values.Where(x => x.ABName == role.AssetBundleName);
                asset.Depends.AddRange(rets.Select(x => x.ABName)); 
                buildInfo.AssetDatas.Add(asset.ABName, asset);
            }

            // 搜集引用次数
            BuildInfo.AssetData lastDependAsset = null;
            foreach (var item in buildInfo.AssetDatas)
            {
                int count = 0;
                foreach (var assetdata in buildInfo.AssetDatas.Values)
                {
                    if(item.Value == assetdata)
                    {
                        // 跳过自己
                        continue;
                    }

                    if(assetdata.Depends.Contains(item.Key))
                    {
                        lastDependAsset = assetdata;
                        count++;
                    }
                }
                item.Value.RefCount = count;
                if(count == 1)
                {
                    if(packageAssets.ContainsKey(item.Value.ABName) || roles.Rules.Exists(x=>x.AssetBundleName == item.Value.ABName))
                    {
                        // 图集包或者规则包,跳过
                        continue;
                    }
                    // 若资源只被另外一个资源引用一次，将资源和依赖资源打包到一起
                    item.Value.ABName = lastDependAsset.ABName;
                }
            }

            return buildInfo;
        }

        /// <summary>
        /// 获取资源依赖
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static List<string> GetDependencies(string path)
        {
            if(!DependenciesInfoCache.TryGetValue(path, out var dps))
            {
                dps = AssetDatabase.GetDependencies(path).Select(x => x.ToLower()).ToList();
                CheckAssetPath(dps);
                DependenciesInfoCache.Add(path, dps);
            }

            return dps;
        }

        /// <summary>
        /// 获取资源Hash值
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static string GetHashFromAssets(string fileName, bool isHashName)
        {
            if(FilesHashCache.TryGetValue(fileName, out var hash))
            {
                return hash;
            }

            byte[] assetBytes = File.ReadAllBytes(fileName);
            byte[] metaBytes = File.ReadAllBytes(fileName + ".meta");
            byte[] buf = new byte[assetBytes.Length + metaBytes.Length + 1];
            buf[0] = isHashName ? (byte)1 : (byte)0;
            Buffer.BlockCopy(assetBytes, 0, buf, 1, assetBytes.Length);
            Buffer.BlockCopy(metaBytes, 0, buf, assetBytes.Length + 1, metaBytes.Length);
            var sha1 = SHA1.Create();
            byte[] retVal = sha1.ComputeHash(buf);
            hash = retVal.ToHex("x2");
            FilesHashCache.Add(fileName, hash);
            return hash;
        }

        /// <summary>
        /// 资源路径筛选，去除Editor路径下的资源，以及js cs dll文件
        /// </summary>
        /// <param name="paths"></param>
        private static void CheckAssetPath(List<string> paths)
        {
            if(paths.Count == 0)
            {
                return;
            }

            for(int i = paths.Count - 1; i >= 0; i--)
            {
                string path = paths[i];
                if(!File.Exists(path))
                {
                    paths.RemoveAt(i);
                    continue;
                }

                if(path.Contains("/editor/"))
                {
                    paths.RemoveAt(i);
                    continue;
                }

                var ext = Path.GetExtension(path).ToLower();
                if(ext == ".cs" || ext == ".js" || ext == ".dll")
                {
                    paths.RemoveAt(i);
                    continue;
                }
            }
        }

        /// <summary>
        /// 获取资源变动
        /// </summary>
        /// <param name="oldBuildInfo"></param>
        /// <param name="newBuildInfo"></param>
        /// <returns></returns>
        private static BuildInfo GetAssetChanges(BuildInfo oldBuildInfo, BuildInfo newBuildInfo)
        {
            if(oldBuildInfo.AssetDatas.Count != 0)
            {
                Debug.Log("开始分析增量资源");
                List<BuildInfo.AssetData> changeAssets = new List<BuildInfo.AssetData>();
                // 首先找出变化的资源
                foreach (var newAsset in newBuildInfo.AssetDatas)
                {
                    if(oldBuildInfo.AssetDatas.TryGetValue(newAsset.Key, out var oldAsset))
                    {
                        if(oldAsset.Hash == newAsset.Value.Hash)
                        {
                            // 资源无变化
                            continue;
                        }
                    }

                    changeAssets.Add(newAsset.Value);
                }

                List<string> rebuildABNames = new List<string>();
                foreach (var changeAsset in changeAssets)
                {
                    rebuildABNames.Add(changeAsset.ABName);
                    foreach (var depend in changeAsset.Depends)
                    {
                        // 资源包变动时依赖包也需要设置ABName，否则打包会失去依赖
                        rebuildABNames.Add(depend);
                    }
                }

                // 去重
                rebuildABNames = rebuildABNames.Distinct().ToList();

                int counter = 0;
                while(counter < rebuildABNames.Count)
                {
                    string abName = rebuildABNames[counter];
                    var rebuildAssets = newBuildInfo.AssetDatas.Where(x => x.Value.ABName == abName);

                    foreach (var asset in rebuildAssets)
                    {
                        if(!rebuildABNames.Contains(asset.Value.ABName))
                        {
                            rebuildABNames.Add(asset.Value.ABName);
                        }

                        foreach (var dependItem in asset.Value.Depends)
                        {
                            if(newBuildInfo.AssetDatas.TryGetValue(dependItem, out var dependAsset))
                            {
                                if(!rebuildABNames.Contains(dependAsset.ABName))
                                {
                                    rebuildABNames.Add(dependAsset.ABName);
                                }
                            }
                        }
                    }

                    counter++;
                }

                var allRebuildAssets = new List<KeyValuePair<string, BuildInfo.AssetData>>();
                foreach (var abName in rebuildABNames)
                {
                    allRebuildAssets.AddRange(newBuildInfo.AssetDatas.Where(x => x.Value.ABName == abName));
                }

                var rebuildInfo = new BuildInfo();
                foreach (var kv in allRebuildAssets)
                {
                    rebuildInfo.AssetDatas.Add(kv.Key, kv.Value);
                }

                Debug.Log("分析增量资源结束");
                return rebuildInfo;
            }

            return newBuildInfo;
        }

        /// <summary>
        /// 删除不再使用的资源
        /// </summary>
        /// <param name="buildInfo"></param>
        /// <param name="dir"></param>
        private static void RemoveUnusedAssets(BuildInfo buildInfo, string dir)
        {
            if(Directory.Exists(dir))
            {
                var rets = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".meta"));
                foreach (var fileName in rets)
                {
                    bool unused = true;
                    foreach (var item in buildInfo.AssetDatas.Values)
                    {
                        if(item.ABName == Path.GetFileName(fileName))
                        {
                            unused = false;
                            break;
                        }
                    }

                    if(unused)
                    {
                        File.Delete(fileName);
                    }
                }
            }
        }

        private static Dictionary<string, AssetImporter> SetABName(BuildInfo buildInfo)
        {
            Dictionary<string, AssetImporter> assetImporterCache = new Dictionary<string, AssetImporter>();
            // 设置ABName
            foreach (var asset in buildInfo.AssetDatas)
            {
                AssetImporter ai = AssetImporter.GetAtPath(asset.Key);
                if (ai)
                {
                    ai.assetBundleName = asset.Value.ABName;
                    assetImporterCache.Add(asset.Key, ai);
                }
            }

            return assetImporterCache;
        }

        /// <summary>
        /// 清除所有ABName
        /// </summary>
        /// <param name="assetImporterCache"></param>
        private static void ClearABName(Dictionary<string, AssetImporter> assetImporterCache)
        {
            int total = assetImporterCache.Count;

            int count = 1;
            foreach (var ai in assetImporterCache)
            {
                EditorUtility.DisplayProgressBar("资源清理", "清理中...", count++ / total);
                if (ai.Value != null)
                {
                    ai.Value.assetBundleVariant = "";
                    ai.Value.assetBundleName = "";
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        /// <summary>
        /// 生成新的版本文件
        /// </summary>
        /// <param name="dir"></param>
        private static void GenerateVersionInfo(string dir)
        {
            string versionPath = Path.Combine(dir, Version);
            // 先删除原先Version
            FileHelper.WriteAllText(versionPath, "");
            File.Delete(versionPath);

            AssetsVersionConfig version = new AssetsVersionConfig();

            string[] files = Directory.GetFiles(dir);
            foreach (var file in files)
            {
                string extension = Path.GetExtension(file);
                if(extension == ".DS_Store" || extension == ".manifest" || Path.GetFileName(file) == "StreamingAssets")
                {
                    File.Delete(file);
                    continue;
                }

                string md5 = MD5Helper.FileMD5(file);
                FileInfo fi = new FileInfo(file);
                long size = fi.Length;
                version.FileInfos.Add(fi.Name, new FileVersionInfo()
                {
                    MD5 = md5,
                    Size = size
                });

                version.TotalSize += size;
            }

            version.Version = DateTime.UtcNow.Ticks;

            // 写入新version
            FileHelper.WriteAllText(versionPath, JsonConvert.SerializeObject(version));
        }
    }
}
