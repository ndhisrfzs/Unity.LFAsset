using System.Collections.Generic;

namespace LFAsset.Runtime
{
    public class FileVersionInfo
	{
		public string MD5;
		public long Size;
	}

	public class AssetsVersionConfig 
	{
		public long Version;
		
		public long TotalSize;
		
		public Dictionary<string, FileVersionInfo> FileInfos = new Dictionary<string, FileVersionInfo>();
    }
}
