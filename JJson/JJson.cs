using System.Diagnostics;

namespace JJson;

public static class JJson {
    public static JObject ParseJson(string json, JParserSettings? settings = null) {
        //TODO improve this syntax here to be nice, probably using bool and out

        Stopwatch sw = new();
        var tokens = JLexer.Lex(json, ref sw);
        // Console.WriteLine($"Lexing took {sw.ElapsedMilliseconds} ms");

        var parser = new JParser(tokens, settings);
        var obj = (parser.Parse() as JObject) ?? throw new Exception("Failed to parse json");

        if (parser.Settings.MeasureParseTime) {
            // Console.WriteLine($"Parsing took {parser.Stopwatch!.ElapsedMilliseconds} ms");
        }

        return obj;
    }
}