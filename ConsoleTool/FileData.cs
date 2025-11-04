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
		public string Extension { get; }

		public FileData(string path)
		{
			FilePath = path;
			Name = Path.GetFileName(FilePath);
			Extension = Path.GetExtension(FilePath);
		}

		public static FileData Empty()
		{
			return new FileData();
		}
	}
}
