using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LFAsset.Runtime
{
	public static class MD5Helper
	{
        static readonly MD5 md5 = new MD5CryptoServiceProvider();
        public static string FileMD5(string filePath)
		{
            using (FileStream file = new FileStream(filePath, FileMode.Open))
			{
				return Encrypt32(file);
			}
		}

		public static string FileMD5(FileInfo fileInfo)
		{
			using(FileStream file = fileInfo.Open(FileMode.Open))
			{
				return Encrypt32(file);
			}
		}

		public static string Encrypt32(string input)
        {
			return Encrypt32(Encoding.UTF8.GetBytes(input));
        }

		public static string Encrypt32(FileStream file)
		{
            byte[] retVal = md5.ComputeHash(file);
            return retVal.ToHex("x2");
        }

		public static string Encrypt32(byte[] bytes)
        {
            byte[] retVal = md5.ComputeHash(bytes);
            return retVal.ToHex("x2");
        }

		public static string Encrypt16(string input)
        {
			return Encrypt16(Encoding.UTF8.GetBytes(input));
        }

		public static string Encrypt16(byte[] bytes)
        {
            byte[] retVal = md5.ComputeHash(bytes);
			return retVal.ToHex("x2", 4, 8);
        }
    }
}
