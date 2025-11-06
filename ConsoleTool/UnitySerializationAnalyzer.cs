using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConsoleTool;

public static class UnitySerializationAnalyzer
{
    private static List<MetadataReference>? _unityReferences;
    private static string? _unityEditorPath;

    private static readonly HashSet<string?> NonSerializedSystemTypes = new()
    {
        "System.Delegate",
        "System.MulticastDelegate",
        "System.Action",
        "System.Func",
        "System.EventHandler"
    };

    public static string? UnityEditorPath
    {
        get => _unityEditorPath;
        set
        {
            _unityEditorPath = value;
            _unityReferences = null; // сбросить, чтобы пересоздалось
        }
    }

    public static List<MetadataReference> UnityReferences
    {
        get
        {
            if (_unityEditorPath != null) return _unityReferences ??= LoadUnityReferences(_unityEditorPath);
            return _unityReferences ??= LoadUnityReferences();
        }
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

        var typeInfo = model.GetTypeInfo(field.Declaration.Type);
        var type = typeInfo.Type;

        if (type == null)
            return false;

        return IsUnitySerializableType(type);
    }

    private static bool IsUnitySerializableType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Delegate)
            return false;

        if (NonSerializedSystemTypes.Contains(type.ToString()))
            return false;

        if (type.IsValueType || type.SpecialType == SpecialType.System_String)
            return true;

        if (type is IArrayTypeSymbol arrayType)
            return IsUnitySerializableType(arrayType.ElementType);

        if (type is INamedTypeSymbol named &&
            named.Name == "List" &&
            named.ContainingNamespace.ToString() == "System.Collections.Generic")
        {
            var elementType = named.TypeArguments.FirstOrDefault();
            return elementType != null && IsUnitySerializableType(elementType);
        }

        if (type is INamedTypeSymbol namedDict &&
            namedDict.Name == "Dictionary" &&
            namedDict.ContainingNamespace.ToString() == "System.Collections.Generic")
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
            "editors-v2.json" //May differ depending on Unity version, if doesn't work try to find similar file in AppData/Roaming/UnityHub. It most likely have similar signature
        );

        if (!File.Exists(hubConfigPath))
            return null;

        var json = File.ReadAllText(hubConfigPath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var dataArray))
            return null;

        foreach (var entry in dataArray.EnumerateArray())
        {
            if (!entry.TryGetProperty("location", out var locationArray))
                continue;

            var exePath = locationArray[0].GetString();
            if (string.IsNullOrEmpty(exePath))
                continue;

            var editorPath = Path.GetDirectoryName(exePath);
            if (!string.IsNullOrEmpty(editorPath))
                return editorPath;
        }

        return null;
    }

    private static List<MetadataReference> LoadUnityReferences(string unityEditorPath = "")
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        string? unityPath = unityEditorPath != string.Empty ? unityEditorPath : GetUnityEditorPath();
        if (unityPath == null)
        {
            Console.WriteLine("Unity not found — using fallback mode");
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
            if (!Directory.Exists(folder))
                continue;

            foreach (var dll in Directory.EnumerateFiles(folder, "*.dll"))
            {
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(dll));
                }
                catch
                {
                    // skip incompatible assemblies
                }
            }
        }

        Console.WriteLine($"Loaded {refs.Count} Unity references");
        return refs;
    }
}