using YamlDotNet.RepresentationModel;

namespace ConsoleTool;

public class Scene
{
    private List<GameObject> _gameObjects = new();
    private List<MonoBehavior> _monoBehaviors = new();

    public Scene(FileData sceneFile)
    {
        ParseScene(sceneFile);
    }
    
    private void ParseScene(FileData sceneFile)
    {
            
        using var reader = new StreamReader(sceneFile.FilePath);

        var yaml = new YamlStream();
        yaml.Load(reader);

        foreach (var doc in yaml.Documents)
        {
            var root = (YamlMappingNode)doc.RootNode;

            foreach (var entry in root.Children)
            {
                string unityType = entry.Key.ToString();
                var dataNode = (YamlMappingNode)entry.Value;

                if (unityType.Contains("GameObject"))
                {
                    var nameNode = dataNode.Children[new YamlScalarNode("m_Name")];
                    _gameObjects.Add(new GameObject(nameNode.ToString()));
                    //Console.WriteLine("GameObject: " + nameNode);
                }

                if (unityType.Contains("MonoBehaviour"))
                {
                    var scriptNode = (YamlMappingNode)dataNode.Children[new YamlScalarNode("m_Script")];
                    var guid = scriptNode.Children[new YamlScalarNode("guid")].ToString();
                        
                    _monoBehaviors.Add(new MonoBehavior(guid));
                    //Console.WriteLine($"MonoBehaviour: \n guid: {guid}");
                }
            }
        }
    }
}