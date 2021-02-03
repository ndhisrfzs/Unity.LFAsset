using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;

namespace LFAsset.Runtime
{
    public class FileVersion
	{
		public string MD5;
		public long Size;
	}

	public class AssetsVersion 
	{
		public long Version;
		
		public long TotalSize;
		
		public Dictionary<string, FileVersion> FileInfos = new Dictionary<string, FileVersion>();
    }

	public static class Versions
    {
		public const string Filename = "var";

		private static AssetsVersion _baseVersion;

		public static AssetsVersion LoadBaseVersion(string fileName)
        {
			_baseVersion = LoadVersion(fileName);
			return _baseVersion;
        }

        public static AssetsVersion LoadVersion(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return new AssetsVersion();
            }

			var file = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<AssetsVersion>(file);
        }

		public static bool IsNew(string path, long size, string hash)
        {
			var key = Path.GetFileName(path);
			if(_baseVersion.FileInfos.TryGetValue(key, out var file))
            {
				if(file.Size == size && file.MD5.Equals(hash, StringComparison.OrdinalIgnoreCase))
                {
					return false;
                }
            }

			if(!File.Exists(path))
            {
				return true;
            }

			using(var stream = File.OpenRead(path))
            {
				if(stream.Length != size)
                {
					return true;
                }

				return !MD5Helper.Encrypt32(stream).Equals(hash, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
