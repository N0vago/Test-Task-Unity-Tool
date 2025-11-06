using YamlDotNet.RepresentationModel;
using GUID = string;
using ScriptName = string;

namespace ConsoleTool;

public class SceneData
{
    private readonly string _name;
    private readonly Dictionary<long, int> _componentClassById = new();
    private readonly Dictionary<long, SceneGameObjectInfo> _objects = new();
    private readonly HashSet<SceneBehaviourInfo> _scriptsInfo = new();

    public SceneData(FileData sceneFile)
    {
        _name = sceneFile.Name;
        GetClassIDs(sceneFile.FilePath);
        ParseScene(sceneFile.FilePath);
    }

    #region Hierarchy Dump
    public void CreateHierarchyDump(string storePath)
    {
        Directory.CreateDirectory(storePath);
        string dumpPath = Path.Combine(storePath, $"{_name}.dump");

        using var writer = new StreamWriter(dumpPath);
        foreach (var root in _objects.Values.Where(o => o.ParentId == 0))
            WriteNodeRecursive(root, 0, writer);
    }

    private void WriteNodeRecursive(SceneGameObjectInfo obj, int indent, StreamWriter writer)
    {
        writer.WriteLine($"{new string(' ', indent * 2)}- {obj.Name}");
        foreach (var childId in obj.Children)
            if (_objects.TryGetValue(childId, out var child))
                WriteNodeRecursive(child, indent + 1, writer);
    }
    #endregion

    #region Script Usage
    public bool IsUnusedScript(ScriptData script, HashSet<ScriptData> allScripts)
    {
        if (_scriptsInfo.Any(s => s.Guid == script.GUID))
            return false;
        
        foreach (var behaviour in _scriptsInfo)
        {
            foreach (var (referencedGuid, referencedField) in behaviour.SerializedFieldsByGuid)
            {
                if (referencedGuid != script.GUID)
                    continue;

                bool fieldExists = allScripts.Any(s => s.SerializedFields.Contains(referencedField));
                if (fieldExists)
                    return false;

                return true;
            }
        }

        return true;
    }
    #endregion

    #region Scene Parsing
    private void ParseScene(string filePath)
    {
        foreach (var doc in LoadYamlDocuments(filePath))
        {
            if (doc.RootNode is not YamlMappingNode root)
                continue;

            foreach (var (key, value) in root.Children)
            {
                string unityType = key.ToString();
                long fileId = long.Parse(doc.RootNode.Anchor.ToString());
                var dataNode = (YamlMappingNode)value;

                if (unityType.Contains("GameObject"))
                    ParseGameObject(dataNode);

                else if (unityType.Contains("Transform"))
                    ParseTransform(fileId, dataNode);

                else if (unityType.Contains("MonoBehaviour"))
                    ParseMonoBehaviour(fileId, dataNode);
            }
        }
    }

    private void ParseGameObject(YamlMappingNode dataNode)
    {
        var go = new SceneGameObjectInfo
        {
            TransformId = GetTransformIdFromGameObject(dataNode),
            Name = dataNode[new YamlScalarNode("m_Name")].ToString()
        };
        _objects[go.TransformId] = go;
    }

    private void ParseTransform(long fileId, YamlMappingNode dataNode)
    {
        if (!_objects.TryGetValue(fileId, out var go))
            go = _objects[fileId] = new SceneGameObjectInfo { TransformId = fileId };

        if (dataNode.Children.TryGetValue(new YamlScalarNode("m_Father"), out var fatherNode))
            go.ParentId = long.Parse(((YamlMappingNode)fatherNode)["fileID"].ToString());

        if (dataNode.Children.TryGetValue(new YamlScalarNode("m_Children"), out var childrenNode))
        {
            foreach (var yamlNode in (YamlSequenceNode)childrenNode)
            {
                var childRef = (YamlMappingNode)yamlNode;
                go.Children.Add(long.Parse(childRef["fileID"].ToString()));
            }
        }
    }

    private void ParseMonoBehaviour(long fileId, YamlMappingNode dataNode)
    {
        var scriptNode = (YamlMappingNode)dataNode["m_Script"];
        var info = new SceneBehaviourInfo
        {
            FileId = fileId,
            Guid = scriptNode["guid"].ToString()
        };

        bool readingFields = false;

        foreach (var (keyNode, valueNode) in dataNode.Children)
        {
            string key = keyNode.ToString();

            if (key == "m_Script") { readingFields = true; continue; }
            if (!readingFields || key.StartsWith("m_")) continue;

            switch (valueNode)
            {
                case YamlMappingNode fieldMap:
                    ParseFieldNode(fieldMap, key, info);
                    break;

                case YamlSequenceNode seqNode:
                    foreach (var element in seqNode.Children)
                    {
                        if (element is YamlMappingNode elemMap)
                            ParseFieldNode(elemMap, key, info);
                    }
                    break;
            }
        }

        _scriptsInfo.Add(info);
    }

    private void ParseFieldNode(YamlMappingNode fieldMap, string key, SceneBehaviourInfo info)
    {
        if (fieldMap.Children.TryGetValue(new YamlScalarNode("guid"), out var guidNode))
        {
            info.SerializedFieldsByGuid[guidNode.ToString()] = key;
        }
        else if (fieldMap.Children.TryGetValue(new YamlScalarNode("fileID"), out var fileNode))
        {
            if (long.TryParse(fileNode.ToString(), out long refId)
                && _componentClassById.TryGetValue(refId, out int classId)
                && classId == 114)
            {
                var match = _scriptsInfo.FirstOrDefault(s => s.FileId == refId);
                if (match != null)
                    info.SerializedFieldsByGuid[match.Guid] = key;
            }
        }
    }

    #endregion

    #region Helpers
    private IEnumerable<YamlDocument> LoadYamlDocuments(string filePath)
    {
        using var reader = new StreamReader(filePath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return yaml.Documents;
    }

    private void GetClassIDs(string filePath)
    {
        foreach (var doc in LoadYamlDocuments(filePath))
        {
            int classId = int.Parse(doc.RootNode.Tag.ToString().Replace("tag:unity3d.com,2011:", ""));
            long fileId = long.Parse(doc.RootNode.Anchor.ToString());
            _componentClassById[fileId] = classId;
        }
    }

    private long GetTransformIdFromGameObject(YamlMappingNode dataNode)
    {
        var components = (YamlSequenceNode)dataNode["m_Component"];
        foreach (var yamlNode in components)
        {
            var node = (YamlMappingNode)yamlNode;
            var comp = (YamlMappingNode)node["component"];
            long id = long.Parse(comp["fileID"].ToString());
            if (_componentClassById.TryGetValue(id, out int classId) && classId == 4)
                return id;
        }
        return -1;
    }
    #endregion

    #region Inner Classes
    private class SceneBehaviourInfo
    {
        public long FileId;
        public string Guid = string.Empty;
        public readonly Dictionary<GUID, ScriptName> SerializedFieldsByGuid = new();
    }

    private class SceneGameObjectInfo
    {
        public long TransformId;
        public string Name = string.Empty;
        public long ParentId;
        public readonly List<long> Children = new();
    }
    #endregion
}