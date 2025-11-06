For the program and Roslyn API to work correctly, the application collects UnityEditor .dll files by finding the directory from the AppData file.
```
public static class UnitySerializationAnalyzer
{
...
   private static string? GetUnityEditorPath()
      {
          string hubConfigPath = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
              "UnityHub",
              "editors-v2.json" 
          );
  
          ...
      {
}
```

"editors-v2.json" may have different name depending on Unity version, if doesn't work try to find similar file in AppData/Roaming/UnityHub.It most likely have similar signature.
Or you can specify the path to the Editor folder as the third argument of the application call. For example: C:\folder-name\folder-name2\Unity\2022.X.XXXX\Editor
