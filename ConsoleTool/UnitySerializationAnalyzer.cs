using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConsoleTool;

public static class UnitySerializationAnalyzer
{
    private static readonly HashSet<string?> NonSerializedSystemTypes = new()
    {
        "System.Delegate",
        "System.MulticastDelegate",
        "System.Action",
        "System.Func",
        "System.EventHandler"
    };

    private static List<MetadataReference> DefaultReferences =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
    ];


    public static bool IsUnitySerializedField(FieldDeclarationSyntax field)
    {
        if (field.Modifiers.Any(m =>
                m.IsKind(SyntaxKind.StaticKeyword) ||
                m.IsKind(SyntaxKind.ConstKeyword) ||
                m.IsKind(SyntaxKind.ReadOnlyKeyword)))
            return false;

        bool isPublic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));

        bool hasSerializeFieldAttribute = field.AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(a =>
            {
                string name = a.Name.ToString();
                return name == "SerializeField" ||
                       name == "SerializeFieldAttribute" ||
                       name.EndsWith(".SerializeField") ||
                       name.EndsWith(".SerializeFieldAttribute");
            });
        
        if (!isPublic && !hasSerializeFieldAttribute)
            return false;
        
        var typeSyntax = field.Declaration.Type;
        string typeName = typeSyntax.ToString();

        return IsUnitySerializableType(typeName);
    }

    private static bool IsUnitySerializableType(string typeName)
    {
        if (typeName.Contains("Action") || typeName.Contains("Func") || typeName.Contains("EventHandler"))
            return false;
        
        if (NonSerializedSystemTypes.Contains(typeName))
            return false;
        
        if (IsUnityPrimitive(typeName))
            return true;
        
        if (typeName.EndsWith("[]"))
        {
            var elementType = typeName[..^2];
            return IsUnitySerializableType(elementType);
        }
        
        if (typeName.StartsWith("List<") && typeName.EndsWith(">"))
        {
            var elementType = typeName.Substring(5, typeName.Length - 6);
            return IsUnitySerializableType(elementType);
        }
        
        if (typeName.StartsWith("Dictionary<"))
            return false;
        
        if (IsUnityObject(typeName))
            return true;
        
        return typeName.Contains("[Serializable]");
    }

    private static bool IsUnityObject(string typeName)
    {
        return typeName switch
        {
            "MonoBehaviour" or
            "ScriptableObject" or
            "GameObject" or
            "Component" or
            "Transform" or
            "RectTransform" or
            "Material" or
            "Texture" or
            "Sprite" or
            "Object" or
            "UnityEngine.MonoBehaviour" or
            "UnityEngine.GameObject" or
            "UnityEngine.Component" or
            "UnityEngine.ScriptableObject" => true,
            _ => false
        };
    }

    private static bool IsUnityPrimitive(string typeName)
    {
        return typeName switch
        {
            "int" or "float" or "double" or "bool" or "string" or
            "Vector2" or "Vector3" or "Vector4" or
            "Quaternion" or "Color" or "Rect" or "AnimationCurve" or
            "Bounds" or "Gradient" or "LayerMask" or
            "UnityEngine.Vector2" or "UnityEngine.Vector3" or "UnityEngine.Color" => true,
            _ => false
        };
    }
#if T
    public static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees) =>
            CSharpCompilation.Create(
                "UnityProjectAnalysis",
                trees,
                DefaultReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
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
#endif
}