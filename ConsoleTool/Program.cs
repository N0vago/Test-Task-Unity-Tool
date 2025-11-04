using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace ConsoleTool
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Build 0.2");
            if (!ValidateProject(args, out var projectPath)) return;

            string assetsPath = Path.Combine(projectPath, "Assets");
            
            FileData[] sceneFiles = GetSceneFiles(assetsPath);
            
            Console.WriteLine($"Success: '{sceneFiles[0]}'");
            if (sceneFiles.Length == 0)
            {
                Console.WriteLine($"Error: '{assetsPath}' is not a valid Unity project directory.");
                return;
            }

            List<Scene> scenes = new();
            foreach (var scenePath in sceneFiles)
            {
                scenes.Add(new Scene(scenePath));
            }

            FileData[] scripts = GetMonoBehaviorScriptFiles(assetsPath);


        }

        static FileData[] GetMonoBehaviorScriptFiles(string path)
        {
            string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

            List<FileData> monoBehaviorScriptFiles = new();

            foreach (var file in files)
            {
                Console.WriteLine($"Candidate: {file}");
                string programText = File.ReadAllText(file);
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(programText);

				var root = syntaxTree.GetRoot();

                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

                if (classDeclarations != null && classDeclarations.BaseList != null)
                {
                    var baseTypes = classDeclarations.BaseList.Types;

                    if (baseTypes.Any().ToString() == "MonoBehaviour")
                    {
                        monoBehaviorScriptFiles.Add(new FileData(file));
                    }
                }
			}


            return monoBehaviorScriptFiles.ToArray();
        }

        static FileData GetMetaFile(FileData defaultFile)
        {
            var searchDirectory = Path.GetDirectoryName(defaultFile.FilePath);
            string[] files = Directory.GetFiles(searchDirectory, defaultFile.Name + ".meta", SearchOption.TopDirectoryOnly);

            if (files.Length > 0)
            {
                return new FileData(files[0]);
            }
            else
            {
                return FileData.Empty();
            }

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