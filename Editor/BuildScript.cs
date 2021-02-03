using LFAsset.Runtime;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;

using UnityEngine;

using Debug = UnityEngine.Debug;

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

    public static class BuildScript 
    {
        private const string Manifest = "Assets/Manifest.asset";
        private const string Version = "var";
        private const string BundlesPath = "/bundles/";
        internal const string RelativeDirPrefix = "./Release";
        internal const string BuildFolder = RelativeDirPrefix + "/{0}/StreamingAssets/";

        public static void Build(PlatformType platform, BuildAssetBundleOptions buildAssetBundleOptions, BuildOptions buildOptions, bool isContainAB, bool isBuildExe, bool isHashName, bool isEncrypt, string secret, string symbols)
        {
            string exeName = "Game";
            BuildTarget buildTarget = BuildTarget.StandaloneWindows;
            BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone;
            switch(platform)
            {
                case PlatformType.PC:
                    buildTarget = BuildTarget.StandaloneWindows64;
                    buildTargetGroup = BuildTargetGroup.Standalone;
                    exeName += ".exe";
                    break;
                case PlatformType.Android:
                    buildTarget = BuildTarget.Android;
                    buildTargetGroup = BuildTargetGroup.Android;
                    exeName += ".apk";
                    break;
                case PlatformType.IOS:
                    buildTarget = BuildTarget.iOS;
                    buildTargetGroup = BuildTargetGroup.iOS;
                    break;
                case PlatformType.MacOS:
                    buildTarget = BuildTarget.StandaloneOSX;
                    buildTargetGroup = BuildTargetGroup.Standalone;
                    break;
            }

            // Bundle输出路径
            string outputPath = string.Format(BuildFolder, platform);          
            // 创建输出目录
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // 收集Shader
            SharderCollection.GenShaderVariant();

            var rules = GetBuildRules();
            rules.Apply(isHashName);
            AssetBundleBuild[] builds = rules.GetBuilds();

            // 移除不再使用的资源
            RemoveUnusedAssets(builds, outputPath);

            //// 设置AB名--测试使用
            //SetABName(builds);

            if (isEncrypt)
            {
                BuildPipeline.SetAssetBundleEncryptKey(MD5Helper.Encrypt16(secret));
            }
            var assetBundleManifest = BuildPipeline.BuildAssetBundles(outputPath, builds, buildAssetBundleOptions, buildTarget);
            if(assetBundleManifest == null)
            {
                return;
            }

            // 生成Manifest
            var manifest = CreateManifest(outputPath, rules.ruleAssets, assetBundleManifest);
            var manifestBundleName = "manifest.unity3d";
            builds = new[]
            {
                new AssetBundleBuild
                {
                    assetNames = new[] { AssetDatabase.GetAssetPath(manifest) },
                    assetBundleName = manifestBundleName
                }
            };

            BuildPipeline.BuildAssetBundles(outputPath, builds, buildAssetBundleOptions, buildTarget);

            // 生成版本文件
            GenerateVersion(outputPath, GetBuildRules().AddVersion());

            if (isContainAB)
            {
                Debug.Log("开始复制资源");
                string streamingAssetsPath = "Assets/StreamingAssets";
                FileHelper.CleanDirectory(streamingAssetsPath);
                FileHelper.CopyDirectory(outputPath, streamingAssetsPath);
                Debug.Log("复制资源结束");
                AssetDatabase.Refresh();
            }

            if (isBuildExe)
            {
                Debug.Log("开始打包游戏");
                var originSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, symbols);
                BuildPipeline.BuildPlayer(GetScenePaths(), $"{RelativeDirPrefix}/{exeName}", buildTarget, buildOptions);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, originSymbols);
                Debug.Log("打包游戏结束");
            }
        }

        /// <summary>
        /// 创建Manifest
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="ruleAssets"></param>
        /// <param name="assetBundleManifest"></param>
        /// <returns></returns>
        private static Manifest CreateManifest(string outputPath, RuleAsset[] ruleAssets, AssetBundleManifest assetBundleManifest)
        {
            var manifest = GetManifest();

            var dirs = new List<string>();
            var assetRefs = new List<AssetRef>();
            var bundleRefs = new List<BundleRef>();

            var bundles = assetBundleManifest.GetAllAssetBundles();
            var bundle2Ids = new Dictionary<string, int>();
            for (int i = 0; i < bundles.Length; i++)
            {
                var bundle = bundles[i];
                bundle2Ids[bundle] = i;
            }

            for(int i = 0; i < bundles.Length; i++)
            {
                var bundle = bundles[i];
                var deps = assetBundleManifest.GetAllDependencies(bundle);
                var path = $"{outputPath}/{bundle}";
                if(File.Exists(path))
                {
                    using(var stream = File.OpenRead(path))
                    {
                        bundleRefs.Add(new BundleRef
                        {
                            name = bundle,
                            id = i,
                            deps = Array.ConvertAll(deps, input => bundle2Ids[input]),
                            len = stream.Length,
                            hash = assetBundleManifest.GetAssetBundleHash(bundle).ToString(),
                        });
                    }
                }
                else
                {
                    Debug.LogError(path + "file not exsit.");
                }
            }

            for(var i = 0; i < ruleAssets.Length; i++)
            {
                var asset = ruleAssets[i];
                var path = asset.path.ToLower();
                if(!path.Contains(BundlesPath))
                {
                    // 只记录Bundles目录下的资源文件
                    continue;
                }
                path = BundlePath(path);
                var dir = Path.GetDirectoryName(path).Replace("\\", "/");
                var index = dirs.FindIndex(x => x.Equals(dir));
                if(index == -1)
                {
                    index = dirs.Count;
                    dirs.Add(dir);
                }

                var assetRef = new AssetRef { bundle = bundle2Ids[asset.bundle], dir = index, name = Path.GetFileName(path) };
                assetRefs.Add(assetRef);
            }

            manifest.dirs = dirs.ToArray();
            manifest.assets = assetRefs.ToArray();
            manifest.bundles = bundleRefs.ToArray();
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return manifest;
        }

        /// <summary>
        /// 清除所有AssetBundle包名
        /// </summary>
        public static void ClearAssetBundles()
        {
            var allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            for (var i = 0; i < allAssetBundleNames.Length; i++)
            {
                var text = allAssetBundleNames[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                                    string.Format("Clear AssetBundles {0}/{1}", i, allAssetBundleNames.Length), text,
                                    i * 1f / allAssetBundleNames.Length))
                    break;

                var paths = AssetDatabase.GetAssetPathsFromAssetBundle(text);
                foreach (var path in paths)
                {
                    AssetImporter ai = AssetImporter.GetAtPath(path);
                    if (ai)
                    {
                        ai.SetAssetBundleNameAndVariant(null, null);
                    }
                }
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.Refresh();
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
        public static BuildRules GetBuildRules()
        {
            return GetAsset<BuildRules>("Assets/rules.asset");
        }

        /// <summary>
        /// 获取Manifest
        /// </summary>
        /// <returns></returns>
        public static Manifest GetManifest()
        {
            return GetAsset<Manifest>(Manifest);
        }

        /// <summary>
        /// 规范Bundle资源路径
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string BundlePath(string path)
        {
            if (path.Contains(BundlesPath))
            {
                int index = path.IndexOf(BundlesPath);
                path = path.Substring(index + BundlesPath.Length);
            }
            // 去除后缀
            string extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension))
            {
                path = path.Replace(extension, "");
            }
            return path;
        }

        public static T GetAsset<T>(string path) where T : ScriptableObject
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
        /// 删除不再使用的资源
        /// </summary>
        /// <param name="builds"></param>
        /// <param name="dir"></param>
        private static void RemoveUnusedAssets(AssetBundleBuild[] builds, string dir)
        {
            if(Directory.Exists(dir))
            {
                var rets = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".meta")).Select(x=>Path.GetFileName(x));
                var abNames = builds.Select(x => x.assetBundleName).Distinct();
                List<string> excepts = new List<string>();
                foreach (var item in rets)
                {
                    var ext = Path.GetExtension(item);
                    if(ext == ".manifest")
                    {
                        if(!abNames.Contains(Path.GetFileNameWithoutExtension(item)))
                        {
                            excepts.Add(item);
                        }
                    }
                    else
                    {
                        if (!abNames.Contains(Path.GetFileName(item)))
                        {
                            excepts.Add(item);
                        }
                    }
                }

                for (int i = 0, max = excepts.Count; i < max; i++)
                {
                    EditorUtility.DisplayProgressBar("删除多余文件", $"处理:{ excepts[i] }", i / (float)max);
                    File.Delete(Path.Combine(dir, excepts[i]));
                }

                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 设置AssetBundle名字
        /// </summary>
        /// <param name="builds"></param>
        private static void SetABName(AssetBundleBuild[] builds)
        {
            // 设置ABName
            for(int i = 0, max = builds.Length; i < max; i++)
            {
                var build = builds[i];
                if (EditorUtility.DisplayCancelableProgressBar("设置ABName", $"处理:{ build.assetBundleName } {i}/{max}", i / (float)max)) break;

                foreach (var asset in build.assetNames)
                {
                    AssetImporter ai = AssetImporter.GetAtPath(asset);
                    if (ai)
                    {
                        ai.SetAssetBundleNameAndVariant(build.assetBundleName, build.assetBundleVariant);
                    }
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        /// <summary>
        /// 生成version文件
        /// </summary>
        private static void GenerateVersion(string dir, int ver)
        {
            string versionPath = Path.Combine(dir, Version);
            if(File.Exists(versionPath))
            {
                // 先删除原先version文件
                File.Delete(versionPath);
            }

            AssetsVersion version = new AssetsVersion();

            string[] files = Directory.GetFiles(dir);
            for (int i = 0, max = files.Length; i < max; i++)
            {
                var file = files[i];
                string extension = Path.GetExtension(file);
                if (extension == ".DS_Store" || extension == ".manifest" || Path.GetFileName(file) == "StreamingAssets")
                {
                    continue;
                }

                string md5 = MD5Helper.FileMD5(file);
                FileInfo fi = new FileInfo(file);
                long size = fi.Length;
                version.FileInfos.Add(fi.Name, new Runtime.FileVersion()
                {
                    MD5 = md5,
                    Size = size
                });

                version.TotalSize += size;
            }

            version.Version = ver;

            // 写入新version
            FileHelper.WriteAllText(versionPath, JsonConvert.SerializeObject(version));
            EditorUtility.ClearProgressBar();
        }
    }
}
