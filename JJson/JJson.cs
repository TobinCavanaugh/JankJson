using System.Diagnostics;

namespace JJson;

public static class JJson {
    public static JObject ParseJson(string json, JParserSettings? settings = null) {
        //TODO improve this syntax here to be nice, probably using bool and out

        Stopwatch sw = new();
        sw.Start();
        
        var tokens = JLexer.Lex(json, ref sw);
        // var tokens = OptimizedParallelLexer.Lex(json);
        
        sw.Stop();
        
        Console.WriteLine($"Lexing took {sw.ElapsedMilliseconds} ms");
        // tokens.ForEach(Console.WriteLine);
        
        var parser = new JParser(tokens, settings);
        var obj = (parser.Parse() as JObject) ?? throw new Exception("Failed to parse json");
        Console.WriteLine($"Parsing took {parser.Stopwatch.ElapsedMilliseconds}ms");
        //
        // if (parser.Settings.MeasureParseTime) {
        //     // Console.WriteLine($"Parsing took {parser.Stopwatch!.ElapsedMilliseconds} ms");
        // }

        return obj;
    }
}