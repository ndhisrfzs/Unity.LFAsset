using System;
using System.Collections.Generic;

namespace LFAsset.Runtime
{
    public enum AssetType
    {
        Other = 0,
        Prefab = 1,
        TextAsset = 2,
        Texture = 3,
        SpriteAtlas = 4
    }

    [Serializable]
    public class ManifestItem
    {
        /// <summary>
        /// 资源实际路径
        /// </summary>
        public string Path;
        /// <summary>
        /// 资源类型
        /// </summary>
        public int Type;
        /// <summary>
        /// 依赖
        /// </summary>
        public List<string> Depend;

        public ManifestItem(string path, AssetType type, List<string> depend = null)
        {
            this.Path = path;
            this.Type = (int)type;
            this.Depend = depend;
        }
    }
}
