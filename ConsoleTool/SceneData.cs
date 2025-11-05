using YamlDotNet.RepresentationModel;

using GUID = string;
using ScriptName = string;

namespace ConsoleTool;

public class SceneData
{
    private string _name;
    private Dictionary<long, int> _componentClassById = new();
    private Dictionary<long, SceneGameObjectInfo> _objects = new();
    private HashSet<SceneBehaviourInfo> _scriptsInfo = new();

    public SceneData(FileData sceneFile)
    {
        _name = sceneFile.Name;
        GetClassIDs(sceneFile.FilePath);
        ParseScene(sceneFile.FilePath);
    }
    
    public void CreateHierarchyDump(string storePath)
    {
        if (!Directory.Exists(storePath))
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
        {
            if (_objects.TryGetValue(childId, out var child))
                WriteNodeRecursive(child, indent + 1, writer);
        }
    }
    
    public bool IsUnusedScript(ScriptData script, HashSet<ScriptData> allScripts)
    {
        bool isAttachedToGameObject = _scriptsInfo.Any(s => s.Guid == script.GUID);
        if (isAttachedToGameObject)
            return false;
        
        bool referencedInScene = false;

        foreach (var behaviour in _scriptsInfo)
        {
            foreach (var serializedRef in behaviour.SerializedFieldsByGuid)
            {
                var referencedGuid = serializedRef.Key;
                var referencedFieldName = serializedRef.Value;

                if (referencedGuid == script.GUID)
                {
                    referencedInScene = true;
                    
                    bool fieldExistsInCSharp = allScripts.Any(t => t.serializedField.Contains(referencedFieldName));

                    if (fieldExistsInCSharp)
                        return false;
                }
            }
        }
        
        if (referencedInScene)
            return true;
        
        return true;
    }

    
    private void ParseScene(string filePath)
    {
         using var reader = new StreamReader(filePath);
         
        var yaml = new YamlStream();
        yaml.Load(reader);
        
        foreach (var doc in yaml.Documents)
        {
            var root = (YamlMappingNode)doc.RootNode;
            
            foreach (var entry in root.Children)
            {
                string unityType = entry.Key.ToString();
                var anchor = doc.RootNode.Anchor;
                long fileId = long.Parse(anchor.ToString());
                var dataNode = (YamlMappingNode)entry.Value;
                
                if (unityType.Contains("GameObject"))
                {
                    var go = new SceneGameObjectInfo()
                    {
                        TransformId = GetTransformIdFromGameObject(dataNode),
                        Name = dataNode.Children[new YamlScalarNode("m_Name")].ToString()
                    };

                    _objects[go.TransformId] = go;
                }
                
                if (unityType.Contains("Transform"))
                {
                    if (!_objects.TryGetValue(fileId, out var go))
                        go = _objects[fileId] = new SceneGameObjectInfo() { TransformId = fileId };
                    
                    var fatherNode = (YamlMappingNode)dataNode.Children[new YamlScalarNode("m_Father")];
                    go.ParentId = long.Parse(fatherNode.Children[new YamlScalarNode("fileID")].ToString());
                    
                    if (dataNode.Children.TryGetValue(new YamlScalarNode("m_Children"), out var childrenNode))
                    {
                        foreach (var yamlNode in (YamlSequenceNode)childrenNode)
                        {
                            var childRef = (YamlMappingNode)yamlNode;
                            long childId = long.Parse(childRef.Children[new YamlScalarNode("fileID")].ToString());
                            go.Children.Add(childId);
                        }
                    }
                }
                if (unityType.Contains("MonoBehaviour"))
                { 
                    var scriptNode = (YamlMappingNode)dataNode.Children[new YamlScalarNode("m_Script")];
                    var guid = scriptNode.Children[new YamlScalarNode("guid")].ToString();
                    var info = new SceneBehaviourInfo { Guid = guid, FileId = fileId};
                    bool readingFields = false;
                    foreach (var yamlField in dataNode.Children)
                    {
                        var key = yamlField.Key.ToString();
                        if (key == "m_Script")
                        {
                            readingFields = true; continue;
                        }

                        if (readingFields)
                        {
                            if (key.StartsWith("m_")) continue;
                            var scripComponents = (YamlMappingNode)dataNode.Children[new YamlScalarNode(key)];
                            if (scripComponents.Children.TryGetValue(new YamlScalarNode("guid"), out var guidNode))
                            {
                                var referencedGuid = guidNode.ToString();
                                info.SerializedFieldsByGuid[referencedGuid] = key;
                            }
                            else
                            {
                                var referenceFileId = long.Parse(scripComponents.Children[new YamlScalarNode("fileID")].ToString());
                                if (_componentClassById[referenceFileId] == 114)
                                {
                                    foreach (var scriptInfo in _scriptsInfo)
                                    {
                                        if (scriptInfo.FileId == referenceFileId)
                                        {
                                            info.SerializedFieldsByGuid[scriptInfo.Guid] = key;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    } 
                    _scriptsInfo.Add(info);
                    
                }
            }
        }
    }

    private void GetClassIDs(string filePath)
    {
        using var reader = new StreamReader(filePath);
         
        var yaml = new YamlStream();
        yaml.Load(reader);

        foreach (var doc in yaml.Documents)
        {
            var tag = doc.RootNode.Tag;
            var anchor = doc.RootNode.Anchor;
            int classId = int.Parse(tag.ToString().Replace("tag:unity3d.com,2011:", ""));
            long fileId = long.Parse(anchor.ToString());

            _componentClassById[fileId] = classId;
        }
    }

    private long GetTransformIdFromGameObject(YamlMappingNode dataNode)
    {
        var mComponents = (YamlSequenceNode)dataNode.Children[new YamlScalarNode("m_Component")];

        foreach (var yamlNode in mComponents)
        {
            var component = (YamlMappingNode)yamlNode;
            var componentNode = (YamlMappingNode)component.Children[new YamlScalarNode("component")];
            var fileId = long.Parse(componentNode.Children[new YamlScalarNode("fileID")].ToString());
            
            if (_componentClassById[fileId] == 4)
                return fileId;
        }
        return -1;
    }

    private class SceneBehaviourInfo
    {
        public long FileId = 0;
        public string Guid = string.Empty;
        public Dictionary<GUID, ScriptName> SerializedFieldsByGuid = new();
    }
    
    private class SceneGameObjectInfo
    {
        public long TransformId;
        public string Name = string.Empty;
        public long ParentId;
        public readonly List<long> Children = new();
    }


}