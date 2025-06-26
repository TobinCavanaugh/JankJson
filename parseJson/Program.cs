namespace parseJson;

public class Program {
    // TODO support comments with settings option
    // Group unexpected character throws into one
    // TODO Serializing and deserializing
    static void Main(string[] args) {
        var obj = JJson.ParseJson(File.ReadAllText("TestFile.json"));
        Console.WriteLine(obj["rootObject"]["id"].AsString());
        Console.WriteLine(obj["rootObject"]["properties"]["enabled"].AsBoolean());

        Console.WriteLine("---");

        obj.Recurse(x => {
            if (x.IsLeaf) Console.WriteLine(x.ValueToString());
            return x.IsNode;
        });
    }
}