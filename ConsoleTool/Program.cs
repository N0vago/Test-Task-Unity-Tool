using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConsoleTool
{
    public static class Program
    { 
        static void Main(string[] args)
    {
        if (!IsValidArgs(args, out var projectPath))
            return;

        string assetsPath = Path.Combine(projectPath, "Assets");

        var scenes = GetSceneFiles(assetsPath);
        var scripts = GetScriptFiles(assetsPath);

        if (scenes.Length == 0)
        {
            Console.WriteLine($"Error: '{assetsPath}' doesn't contain any scene files.");
            return;
        }

        if (scripts.Length == 0)
        {
            Console.WriteLine($"Error: '{assetsPath}' doesn't contain any script files.");
            return;
        }

        var syntaxTrees = GetAllSyntaxTrees(scripts);
        var compilation = CreateCompilation(syntaxTrees.Values);
        var monoBehaviourScripts = GetMonoBehaviourScripts(scripts, syntaxTrees, compilation);

        var sceneData = GetSceneDatas(scenes);
        var scriptData = monoBehaviourScripts
            .Select(file => new ScriptData(file, syntaxTrees[file.FilePath], compilation))
            .ToList();

        var unusedScripts = new HashSet<ScriptData>(scriptData);

        foreach (var scene in sceneData)
        {
            scene.CreateHierarchyDump(args[1]);

            foreach (var script in scriptData)
            {
                if (unusedScripts.Contains(script) && !scene.IsUnusedScript(script, scriptData.ToHashSet()))
                    unusedScripts.Remove(script);
            }

            if (unusedScripts.Count == 0)
                break;
        }

        SaveScriptsInfoToFile(unusedScripts, Path.Combine(args[1], "UnusedScripts.txt"));
    }

    private static List<SceneData> GetSceneDatas(FileData[] sceneFiles)
    {
        var results = new ConcurrentBag<SceneData>();
        int total = sceneFiles.Length;
        int processed = 0;

        Parallel.ForEach(sceneFiles,
            new ParallelOptions { MaxDegreeOfParallelism = 3 },
            sceneFile =>
            {
                try
                {
                    var data = new SceneData(sceneFile);
                    results.Add(data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing {sceneFile.Name}: {ex.Message}");
                }
                finally
                {
                    int done = Interlocked.Increment(ref processed);
                    if (done % 5 == 0 || done == total)
                        Console.WriteLine($"Parsed {done}/{total} scenes...");
                }
            });
        
        return results.ToList();
    }

        // 🔹 Compilation utilities

    private static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees) =>
        CSharpCompilation.Create(
            "UnityProjectAnalysis",
            trees,
            UnitySerializationAnalyzer.UnityReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    private static Dictionary<string, SyntaxTree> GetAllSyntaxTrees(FileData[] scripts)
    {
        var syntaxTrees = new ConcurrentDictionary<string, SyntaxTree>();

        Parallel.ForEach(
            scripts,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            script =>
            {
                try
                {
                    var text = File.ReadAllText(script.FilePath);
                    var tree = CSharpSyntaxTree.ParseText(text);
                    syntaxTrees[script.FilePath] = tree;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to parse {script.FilePath}: {ex.Message}");
                }
            });
        
        return new Dictionary<string, SyntaxTree>(syntaxTrees);
    }

    private static FileData[] GetMonoBehaviourScripts(
        FileData[] allScripts,
        IReadOnlyDictionary<string, SyntaxTree> syntaxTrees,
        CSharpCompilation compilation)
    {
        var monoType = compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
        if (monoType == null)
        {
            Console.WriteLine("Unity MonoBehaviour type not found in references");
            return Array.Empty<FileData>();
        }

        return allScripts
            .Where(file =>
            {
                var tree = syntaxTrees[file.FilePath];
                var model = compilation.GetSemanticModel(tree);
                var classDecl = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDecl == null) return false;

                var symbol = model.GetDeclaredSymbol(classDecl);
                return symbol != null && UnitySerializationAnalyzer.IsUnityObject(symbol);
            })
            .Select(file => new FileData(file))
            .ToArray();
    }
    
    // 🔹 File utilities

    private static FileData[] GetScriptFiles(string path) =>
        Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                 .Select(file => new FileData(file))
                 .ToArray();

    private static FileData[] GetSceneFiles(string path) =>
        Directory.GetFiles(path, "*.unity", SearchOption.AllDirectories)
                 .Select(file => new FileData(file))
                 .ToArray();
    
    private static void SaveScriptsInfoToFile(IEnumerable<ScriptData> scripts, string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var writer = new StreamWriter(outputPath, false);
        foreach (var script in scripts)
            writer.WriteLine($"Script name: {script.Name} GUID: {script.GUID}");
    }
    
    // 🔹 Project validation
    
    private static bool IsValidArgs(string[] args, out string projectPath)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            Console.WriteLine("Usage: required <project-path> <output-path> optional <unity-editor-path>");
            projectPath = string.Empty;
            return false;
        }

        projectPath = args[0];
        if (!IsUnityProject(projectPath))
        {
            Console.WriteLine($"Error: '{projectPath}' is not a valid Unity project directory.");
            return false;
        }

        if (args[2] != string.Empty)
        {
            if (IsUnityEditor(args[2]))
            {
                UnitySerializationAnalyzer.UnityEditorPath = args[2];
            }
        }
        
        return true;
    }

    private static bool IsUnityEditor(string unityEditorPath) =>
        Directory.Exists(unityEditorPath) &&
        Directory.Exists(Path.Combine(unityEditorPath, @"Data\Managed")) &&
        Directory.Exists(Path.Combine(unityEditorPath, @"Data\Managed\UnityEngine")) &&
        Directory.Exists(Path.Combine(unityEditorPath, @"Data\NetStandard\compat"));
    

    private static bool IsUnityProject(string path) =>
        Directory.Exists(path) &&
        Directory.Exists(Path.Combine(path, "Assets")) &&
        Directory.Exists(Path.Combine(path, "ProjectSettings"));
}
}