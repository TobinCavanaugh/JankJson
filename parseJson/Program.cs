using System.Diagnostics;

namespace parseJson;

public class Program {
    // TODO support comments with settings option
    // Group unexpected character throws into one
    // TODO Serializing and deserializing

    public static JObject ParseJson(string json, JParserSettings? settings = null) {
        //TODO improve this syntax here to be nice, probably using bool and out

        Stopwatch sw = new();
        var tokens = JLexer.Lex(json, ref sw);
        Console.WriteLine($"Lexing took {sw.ElapsedMilliseconds} ms");

        var parser = new JParser(tokens, settings);
        var obj = (parser.Parse() as JObject) ?? throw new Exception("Failed to parse json");

        if (parser.Settings.MeasureParseTime) {
            Console.WriteLine($"Parsing took {parser.Stopwatch!.ElapsedMilliseconds} ms");
        }

        return obj;
    }

    static void Main(string[] args) {
        var obj = ParseJson(File.ReadAllText("TestFile.json"));
        Console.WriteLine(obj["rootObject"]["id"].AsString());
        Console.WriteLine(obj["rootObject"]["properties"]["enabled"].AsBoolean());
    }
}