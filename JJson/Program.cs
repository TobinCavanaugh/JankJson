using System.Diagnostics;

namespace JJson;

public class Program {
    // TODO support comments with settings option
    // Group unexpected character throws into one
    // TODO Serializing and deserializing
    static void Main(string[] args) {
        Stopwatch sw = new Stopwatch();
            sw.Start();
        var obj = JJson.ParseJson(File.ReadAllText("TestFile.json"));
        // var obj = JJson.ParseJson(File.ReadAllText(@"C:\Users\tobin\AppData\Roaming\.minecraft\assets\indexes\24.json"));
        // var obj = JJson.ParseJson(File.ReadAllText(@"D:\500GB-HDD\Unity\Creations\ForestBuilding\Library\PackageCache\com.simonoliver.unityfigma@dbd12bff29\UnityFigmaBridge\Assets\google-fonts.json"), new JParserSettings(){AllowTrailingCommas =  true,});

        sw.Stop();
        Console.WriteLine(sw.ElapsedMilliseconds + "ms");
        // obj.Recurse(x => {
        //     if (x.IsLeaf) Console.WriteLine(x.ValueToString());
        //     return x.IsNode;
        // });
        
        Console.WriteLine("---");
        // Console.WriteLine(obj.ToString());
        
        // Console.WriteLine(obj["rootObject"]["id"].AsString());
        // Console.WriteLine(obj["rootObject"]["properties"]["enabled"].AsBoolean());
        // Console.WriteLine(obj["rootObject"]["properties"]["numbers"].AsArray<int>().StringJoin(", "));
    }
}