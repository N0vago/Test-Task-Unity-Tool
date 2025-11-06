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
	}
}
