using LFAsset.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

namespace LFAsset.Editor
{
    public static class SharderCollection
    {
        public class ShaderData
        {
            public int[] passtypes = new int[] { };
            public List<List<string>> keywords = new List<List<string>>();
        }

        private static HashSet<string> allSharderNames = new HashSet<string>();
        private static Dictionary<string, ShaderData> ShaderDatas = new Dictionary<string, ShaderData>();
        private static List<string> passShaders = new List<string>();

        private static ShaderVariantCollection toolSVC = null;

        public static string GenShaderVariant()
        {
            allSharderNames.Clear();
            ShaderDatas.Clear();
            passShaders.Clear();

            toolSVC = new ShaderVariantCollection();
            List<string> shaders = AssetDatabase.FindAssets("t:Shader", new string[] { "Assets", "Packages" }).ToList();
            foreach (var shader in shaders)
            {
                ShaderVariantCollection.ShaderVariant sv = new ShaderVariantCollection.ShaderVariant();
                string shaderPath = AssetDatabase.GUIDToAssetPath(shader);
                sv.shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                toolSVC.Add(sv);
                allSharderNames.Add(shaderPath);
            }

            //// 测试使用代码
            //string toolsSVCPath = "Assets/Resource/Shaders/Tools.shadervariants";
            //// 防止目录没有
            //FileHelper.WriteAllText(toolsSVCPath, "");
            //File.Delete(toolsSVCPath);
            //// 生成临时文件
            //AssetDatabase.CreateAsset(toolSVC, toolsSVCPath);

            string path = ApplicationHelper.BundleResourcePath;
            List<string> assets = new List<string>();
            assets.AddRange(AssetDatabase.FindAssets("t:Prefab", new string[] { path }).ToList());
            assets.AddRange(AssetDatabase.FindAssets("t:Material", new string[] { path }).ToList());

            List<string> allMats = new List<string>();
            for(int i = 0; i < assets.Count; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assets[i]);
                string[] dependenciesPath = AssetDatabase.GetDependencies(assetPath, true);

                List<string> mats = dependenciesPath.ToList().FindAll(x => x.EndsWith(".mat"));
                allMats.AddRange(mats);
            }

            allMats = allMats.Distinct().ToList();

            Dictionary<string, List<ShaderVariantCollection.ShaderVariant>> ShaderVariantDict = new Dictionary<string, List<ShaderVariantCollection.ShaderVariant>>();
            int count = 1;
            foreach (string mat in allMats)
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(mat);
                if(obj is Material _mat)
                {
                    EditorUtility.DisplayProgressBar("处理mat", $"处理:{Path.GetFileName(mat)} - {_mat.shader.name}", count / allMats.Count);
                    AddMat(ShaderVariantDict, _mat);
                }

                count++;
            }

            EditorUtility.ClearProgressBar();
            ShaderVariantCollection svc = new ShaderVariantCollection();
            foreach (var item in ShaderVariantDict)
            {
                foreach (var _sv in item.Value)
                {
                    svc.Add(_sv);
                }
            }

            string shadervariantsPath = Path.Combine(path, "Shader/TheShaderVariantForAll.shadervariants");
            FileHelper.WriteAllText(shadervariantsPath, "");
            File.Delete(shadervariantsPath);

            AssetDatabase.CreateAsset(svc, shadervariantsPath);
            AssetDatabase.Refresh();

            return shadervariantsPath;
        }

        private static void AddMat( Dictionary<string, List<ShaderVariantCollection.ShaderVariant>> dict, Material mat)
        {
            if (!mat || !mat.shader)
                return;

            string path = AssetDatabase.GetAssetPath(mat.shader);
            if(!allSharderNames.Contains(path))
            {
                Debug.LogError($"Mat Path: {AssetDatabase.GetAssetPath(mat)} Shader:{mat.shader.name} Path:{path} 不存在");
                return;
            }

            if(!ShaderDatas.TryGetValue(mat.shader.name, out ShaderData sd))
            {
                sd = GetShaderKeywords(mat.shader);
                ShaderDatas[mat.shader.name] = sd;
            }

            if(sd.passtypes.Length > 20000)
            {
                if(!passShaders.Contains(mat.shader.name))
                {
                    Debug.Log($"Shader:{mat.shader.name},变体数量:{sd.keywords.Count},不建议继续分析，后续也会跳过!");
                    passShaders.Add(mat.shader.name);
                }
                else
                {
                    Debug.Log($"Mat:{mat.name}, Shader:{mat.shader.name}, KeywordCount:{sd.passtypes.Length}");
                }
                return;
            }

            if(!dict.TryGetValue(mat.shader.name, out var svs))
            {
                svs = new List<ShaderVariantCollection.ShaderVariant>();
                dict.Add(mat.shader.name, svs);
            }

            for(int i = 0; i < sd.passtypes.Length; i++)
            {
                string[] result = new string[] { };
                if(mat.shaderKeywords.Length > 0)
                {
                    result = sd.keywords[i].Intersect(mat.shaderKeywords).ToArray();
                }

                var pt = (PassType)sd.passtypes[i];
                ShaderVariantCollection.ShaderVariant? sv = null;

                try
                {
                    if(result.Length > 0)
                    {
                        sv = new ShaderVariantCollection.ShaderVariant(mat.shader, pt, result);
                    }
                    else
                    {
                        sv = new ShaderVariantCollection.ShaderVariant(mat.shader, pt);
                    }
                }
                catch(Exception e)
                {
                    if(sd.passtypes.Length < 10000)
                    {
                        Debug.LogError(e);
                    }
                    continue;
                }

                if(sv != null)
                {
                    bool isContain = false;
                    var _sv = (ShaderVariantCollection.ShaderVariant)sv;
                    foreach (var item in svs)
                    {
                        if(item.passType == _sv.passType && Enumerable.SequenceEqual(item.keywords, _sv.keywords))
                        {
                            isContain = true;
                            break;
                        }
                    }

                    if(!isContain)
                    {
                        svs.Add(_sv);
                    }
                }
            }
        }

        private static MethodInfo GetShaderVariantEntries = null;
        private static ShaderData GetShaderKeywords(Shader shader)
        {
            if(GetShaderVariantEntries == null)
            {
                GetShaderVariantEntries = typeof(ShaderUtil).GetMethod("GetShaderVariantEntriesFiltered", BindingFlags.NonPublic | BindingFlags.Static);
            }

            if(toolSVC == null)
            {
                Debug.LogError("不存在ToolSVC");
                return null;
            }

            var _filterKeywords = new string[] { };
            var _passtypes = new int[] { };
            var _keywords = new string[] { };
            var _remainingKeywords = new string[] { };
            object[] args = new object[]
            {
                shader, 256, _filterKeywords, toolSVC, _passtypes, _keywords, _remainingKeywords
            };
            GetShaderVariantEntries.Invoke(null, args);

            ShaderData sd = new ShaderData();
            sd.passtypes = args[4] as int[];
            var kws = args[5] as string[];
            sd.keywords = new List<List<string>>();
            foreach (var kw in kws)
            {
                var _kws = kw.Split(' ');
                sd.keywords.Add(new List<string>(_kws));
            }

            return sd;
        }
    }
}
