using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConsoleTool
{
    public static class Program
    {
        static void Main(string[] args)
        {
            if (!ValidateProject(args, out var projectPath)) return;
            
            string assetsPath = Path.Combine(projectPath, "Assets");
            
            FileData[] sceneData = GetSceneFiles(assetsPath);
            FileData[] scriptDatas = GetScriptFiles(assetsPath);
            
            Dictionary<string, SyntaxTree> allTrees = GetAllSyntaxTrees(scriptDatas);
            
            var compilation = CSharpCompilation.Create(
                "UnityProjectAnalysis",
                allTrees.Values,
                UnitySerializationAnalyzer.UnityReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            
            FileData[] monoBehaviorScriptFiles = GetMonoBehaviourScriptFiles(scriptDatas, allTrees, compilation);
            
            if (sceneData.Length == 0)
            {
                Console.WriteLine($"Error: '{assetsPath}' doesn't contain any scene files.");
                return;
            }
            if (scriptDatas.Length == 0)
            {
                Console.WriteLine($"Error: '{assetsPath}' doesn't contain any script files.");
            }

            List<SceneData> scenes = new();
            List<ScriptData> scripts = new();
            
            foreach (var scenePath in sceneData)
            {
                scenes.Add(new SceneData(scenePath));
            }

            foreach (var scriptData in monoBehaviorScriptFiles)
            {
                scripts.Add(new ScriptData(scriptData, allTrees[scriptData.Name], compilation));
            }
            
            var unusedScripts = new HashSet<ScriptData>(scripts);
            
            foreach (var scene in scenes)
            {
                scene.CreateHierarchyDump(@"C:\Users\gleb3\Downloads\Temp");
                foreach (var script in scripts)
                {
                    if (unusedScripts.Contains(script) && !scene.IsUnusedScript(script, scripts.ToHashSet()))
                    {
                        unusedScripts.Remove(script);
                    }
                }

                if (unusedScripts.Count == 0)
                    break;
            }
            
            scripts = unusedScripts.ToList();

            foreach (var script in scripts)
            {
                Console.WriteLine(script.Name);
            }
        }

        private static Dictionary<string, SyntaxTree> GetAllSyntaxTrees(FileData[] scriptDatas)
        {
            Dictionary<string, SyntaxTree> allTrees = new();
            foreach (var script in scriptDatas)
            {
                allTrees.Add(script.Name, CSharpSyntaxTree.ParseText(File.ReadAllText(script.FilePath)));
            }
            return allTrees;
        }
        static FileData[] GetMonoBehaviourScriptFiles(FileData[] allScripts, Dictionary<string, SyntaxTree> allTrees, CSharpCompilation compilation)
        {

            var mono = compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
            if (mono == null)
            {
                Console.WriteLine("Unity MonoBehaviour type not found in references");
                return [];
            }

            var results = new List<FileData>();

            foreach (var file in allScripts)
            {
                var tree = allTrees[file.Name];
                var model = compilation.GetSemanticModel(tree);

                var root = tree.GetRoot();
                var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

                if (classDecl == null)
                    continue;

                var symbol = model.GetDeclaredSymbol(classDecl);
                if (symbol == null)
                    continue;
                
                if (UnitySerializationAnalyzer.IsUnityObject(symbol))
                {
                    results.Add(new FileData(file));
                }
            }

            return results.ToArray();
        }
        static FileData[] GetScriptFiles(string path)
        {
            string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

            List<FileData> scriptFiles = new();

            foreach (var file in files)
            {
                scriptFiles.Add(new FileData(file));
			}
            return scriptFiles.ToArray();
        }
        
        static FileData[] GetSceneFiles(string path)
        {
            string[] files = Directory.GetFiles(path, "*.unity", SearchOption.AllDirectories);

            List<FileData> sceneFiles = new();
            if (files.Length == 0)
            {
                return [];
            }

            foreach (var file in files) {
                sceneFiles.Add(new FileData(file));
            }
            
            return sceneFiles.ToArray();
        }

        static bool IsUnityProject(string path)
        {
            if (!Directory.Exists(path)) return false;

            bool hasAssets = Directory.Exists(Path.Combine(path, "Assets"));
            bool hasProjectSettings = Directory.Exists(Path.Combine(path, "ProjectSettings"));

            return hasAssets && hasProjectSettings;
        }

        private static bool ValidateProject(string[] args, out string projectPath)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: UnityAnalyzer <project-path>");
                projectPath = string.Empty;
                return false;
            }

            projectPath = args[0];

            if (!IsUnityProject(projectPath))
            {
                Console.WriteLine($"Error: '{projectPath}' is not a valid Unity project directory.");
                return false;
            }

            Console.WriteLine("Unity project validated ✅");
            return true;
        }
    }
}