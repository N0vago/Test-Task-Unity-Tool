using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YamlDotNet.RepresentationModel;

namespace ConsoleTool;

public class ScriptData
{
    public string Name { get; }
    public string GUID { get; }
    public HashSet<string> SerializedFields { get; }

    public ScriptData(FileData scriptData, SyntaxTree syntaxTree)
    {
        try
        {
            Name = scriptData.Name;
            GUID = ParseGuidFromMeta(scriptData.MetaFilePath);
            SerializedFields = ExtractSerializedFields(syntaxTree);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ScriptData] Failed to process '{scriptData.Name}': {e.Message}");
            Name = string.Empty;
            GUID = string.Empty;
            SerializedFields = new HashSet<string>();
        }
    }

    private static HashSet<string> ExtractSerializedFields(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var result = new HashSet<string>();
        
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();

        foreach (var field in fields)
        {
            if (field.Modifiers.Any(m => 
                    m.IsKind(SyntaxKind.StaticKeyword) || 
                    m.IsKind(SyntaxKind.ConstKeyword) || 
                    m.IsKind(SyntaxKind.ReadOnlyKeyword)))
                continue;

            bool isPublic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
            bool hasSerializeFieldAttribute = field.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a =>
                {
                    string name = a.Name.ToString();
                    return name == "SerializeField" || name == "SerializeFieldAttribute" ||
                           name.EndsWith(".SerializeField") || name.EndsWith(".SerializeFieldAttribute");
                });
            
            if (!isPublic && !hasSerializeFieldAttribute)
                continue;
            
            foreach (var variable in field.Declaration.Variables)
                result.Add(variable.Identifier.Text);
        }

        return result;
    }

    private static string ParseGuidFromMeta(string metaFilePath)
    {
        if (!File.Exists(metaFilePath))
            throw new FileNotFoundException($"Meta file not found for script: {metaFilePath}");

        using var reader = new StreamReader(metaFilePath);
        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
            throw new InvalidDataException($"Meta file '{metaFilePath}' is empty or invalid.");

        var root = yaml.Documents[0].RootNode as YamlMappingNode
                   ?? throw new InvalidDataException($"Meta file '{metaFilePath}' has invalid structure.");

        if (!root.Children.TryGetValue(new YamlScalarNode("guid"), out var guidNode))
            throw new InvalidDataException($"Meta file '{metaFilePath}' does not contain 'guid'.");

        var guid = (guidNode as YamlScalarNode)?.Value;
        return guid ?? throw new InvalidDataException($"Meta file '{metaFilePath}' has empty GUID field.");
    }
}