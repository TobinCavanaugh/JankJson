using System.Diagnostics;
using System.Text.RegularExpressions;

namespace parseJson;

public class Program {
    // TODO support empty arrays / objects
    // TODO support comments with settings option
    // Group unexpected character throws into one

    public static JObject ParseJson(string json, JParserSettings? settings = null) {
        Stopwatch sw = new();
        sw.Start();

        var tokens = JLexer.Lex(json);
        sw.Stop();
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
        Console.WriteLine(obj);
    }
}