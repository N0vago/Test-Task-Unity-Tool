using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConsoleTool;

 public static class UnitySerializationAnalyzer
 { 
     public static readonly List<MetadataReference> UnityReferences;
    private static readonly HashSet<string?> NonSerializedSystemTypes = new()
    {
        "System.Delegate",
        "System.MulticastDelegate",
        "System.Action",
        "System.Func",
        "System.EventHandler"
    };

     static UnitySerializationAnalyzer()
     {
         UnityReferences = LoadUnityReferences();
     }

    public static bool IsUnitySerializedField(FieldDeclarationSyntax field, SemanticModel model)
    {
        if (field.Modifiers.Any(SyntaxKind.StaticKeyword) ||
            field.Modifiers.Any(SyntaxKind.ConstKeyword) ||
            field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
            return false;
        
        bool isPublic = field.Modifiers.Any(SyntaxKind.PublicKeyword);

        bool hasSerializeFieldAttribute = field.AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(a => a.Name.ToString() is "SerializeField" or "SerializeFieldAttribute");

        if (!isPublic && !hasSerializeFieldAttribute)
            return false;
        
        var variable = field.Declaration.Variables.First();
        var typeInfo = model.GetTypeInfo(field.Declaration.Type);
        var type = typeInfo.Type;

        if (type == null)
            return false;

        return IsUnitySerializableType(type);
    }

    public static bool IsUnitySerializableType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Delegate)
            return false;

        if (NonSerializedSystemTypes.Contains(type.ToString()))
            return false;

        
        if (type.IsValueType || type.SpecialType == SpecialType.System_String)
            return true;
        
        if (type is IArrayTypeSymbol arr)
            return IsUnitySerializableType(arr.ElementType);
        
        if (type is INamedTypeSymbol named &&
            named.Name == "List" &&
            named.ContainingNamespace.ToString() == "System.Collections.Generic")
        {
            var element = named.TypeArguments.First();
            return IsUnitySerializableType(element);
        }
        
        if (type is INamedTypeSymbol named2 &&
            named2.Name == "Dictionary" &&
            named2.ContainingNamespace.ToString() == "System.Collections.Generic")
            return false;
        
        if (IsUnityObject(type))
            return true;
        
        if (type.TypeKind == TypeKind.Class &&
            type.GetAttributes().Any(a => a.AttributeClass?.Name is "SerializableAttribute"))
            return true;
        
        return false;
    }

    public static bool IsUnityObject(ITypeSymbol? type)
    {
        while (type != null)
        {
            if (type.ToString() == "UnityEngine.Object")
                return true;

            type = type.BaseType;
        }
        return false;
    }
    private static string? GetUnityEditorPath()
    {
        string hubConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnityHub",
            "editors-v2.json"
        );

        if (!File.Exists(hubConfigPath))
            return null;

        var json = File.ReadAllText(hubConfigPath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var dataArray))
            return null;

        foreach (var entry in dataArray.EnumerateArray())
        {
            if (!entry.TryGetProperty("location", out var locations))
                continue;

            var exePath = locations[0].GetString();
            if (exePath == null) continue;
            
            var editorPath = Path.GetDirectoryName(exePath);
            if (editorPath != null)
                return editorPath;
        }

        return null;
    }
    private static List<MetadataReference> LoadUnityReferences()
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        string? unityPath = GetUnityEditorPath();
        if (unityPath == null)
        {
            Console.WriteLine("⚠ Unity not found — using heuristic mode");
            return refs;
        }

        Console.WriteLine($"Unity found: {unityPath}");

        var folders = new[]
        {
            Path.Combine(unityPath, @"Data\Managed"),
            Path.Combine(unityPath, @"Data\Managed\UnityEngine"),
            Path.Combine(unityPath, @"Data\NetStandard\compat")
        };

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;

            foreach (var dll in Directory.EnumerateFiles(folder, "*.dll"))
            {
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(dll));
                }
                catch
                {
                    // some system libraries may fail — ignore
                }
            }
        }

        Console.WriteLine($"Loaded {refs.Count} Unity references");
        return refs;
    }
}