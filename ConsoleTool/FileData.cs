using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTool
{
	public struct FileData
	{
		public string Name { get; }
		public string FilePath { get; }
		public string MetaFilePath { get; }
		public string Extension { get; }

		public FileData(string path)
		{
			FilePath = path;
			MetaFilePath = path + ".meta";
			Name = Path.GetFileName(FilePath);
			Extension = Path.GetExtension(FilePath);
		}

		public FileData(FileData fileData)
		{
			Name = fileData.Name;
			FilePath = fileData.FilePath;
			MetaFilePath = fileData.MetaFilePath;
			Extension = fileData.Extension;
		}

		public bool IsEmpty()
		{
			if(Name == string.Empty || FilePath == string.Empty || Extension == string.Empty)
				return true;
			return false;
		}

		public static FileData Empty()
		{
			return new FileData();
		}
	}
}
