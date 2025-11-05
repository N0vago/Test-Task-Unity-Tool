using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YamlDotNet.RepresentationModel;

namespace ConsoleTool;

public class ScriptData
{
    public string Name { get; private set; }
    public string GUID { get; private set; }
    public HashSet<string> serializedField {get; private set;}

    public ScriptData(FileData scriptData, SyntaxTree syntaxTree, CSharpCompilation compilation)
    {
        try
        {
            Name = scriptData.Name;
            GUID = ParseMetaFileData(scriptData.MetaFilePath);
            serializedField = GetSerializedFields(syntaxTree, compilation);
        }
        catch (Exception e)
        {
            Name = string.Empty;
            GUID = string.Empty;
            serializedField = new HashSet<string>();
            Console.WriteLine(e);
        }
    }

    private HashSet<string> GetSerializedFields(
        SyntaxTree tree,
        CSharpCompilation compilation)
    {
        var model = compilation.GetSemanticModel(tree);

        var serializableFields = new HashSet<string>();
        var root = tree.GetRoot();
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();

        foreach (var field in fields)
        {
            if (UnitySerializationAnalyzer.IsUnitySerializedField(field, model))
            {
                var name = field.Declaration.Variables.First().Identifier.Text;
                serializableFields.Add(name);
            }
        }
        return serializableFields;
    }

    private string ParseMetaFileData(string scriptMetaFilePath)
    {
        
        using var reader = new StreamReader(scriptMetaFilePath);

        var yaml = new YamlStream();
        yaml.Load(reader);
        
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        
        var guidNode = (YamlScalarNode)root.Children[new YamlScalarNode("guid")];
        string? guid = guidNode.Value;
        
        return guid ?? throw new Exception($"File '{scriptMetaFilePath}' hasn't valid GUID.");
    }
}