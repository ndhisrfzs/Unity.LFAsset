using System.IO;
using System.Security.Cryptography;

namespace LFAsset.Runtime
{
	public static class MD5Helper
	{
		public static string FileMD5(string filePath)
		{
            using (FileStream file = new FileStream(filePath, FileMode.Open))
			{
				return streamMD5(file);
			}
		}

		public static string FileMD5(FileInfo fileInfo)
		{
			using(FileStream file = fileInfo.Open(FileMode.Open))
			{
				return streamMD5(file);
			}
		}

		private static string streamMD5(FileStream file)
		{
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            return retVal.ToHex("x2");
        }
    }
}
