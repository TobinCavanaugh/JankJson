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

        var x = obj["description"].Children;

        sw.Stop();
        Console.WriteLine(sw.ElapsedMilliseconds + "ms");
        Console.WriteLine("---");
    }
}