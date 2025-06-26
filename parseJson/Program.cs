using System.Diagnostics;
using System.Text.RegularExpressions;

namespace parseJson;

public partial class Program {
    // TODO support empty arrays / objects
    // TODO support comments with settings option
    // Group unexpected character throws into one
    // TOOD implement .Parent for JValues

    static void Main(string[] args) {
        Stopwatch sw = new();
        sw.Start();

        var content = File.ReadAllText("TestFile.json");
        var tokens = JLexer.Lex(content);

        var parser = new JParser(tokens, new JParser.Settings() { });
        var jsonVal = (parser.Parse() as JObject)!;
        
        // var mt = jsonVal["empty"];
        // Console.WriteLine(mt.RawContent);

        sw.Stop();
        // tokens.ForEach(x => Console.Write($"{x}\n"));
        Console.WriteLine($"Lexing and parsing took {sw.ElapsedMilliseconds} ms");
        return;

        // jsonVal["rootObject"]["properties"]["references"].As<List<string>>()!.ForEach(Console.WriteLine);

        Console.WriteLine(jsonVal["rootObject"]["properties"]["enabled"].AsBoolean());


        // Console.WriteLine(jsonVal.ToString());
        File.WriteAllText("../../../TestFile-GENERATED.json", jsonVal.ToString());

        {
            string gen = File.ReadAllText("../../../TestFile-GENERATED.json");
            var x = new JParser(JLexer.Lex(gen)).Parse() as JObject;

            if (gen != x.ToString()) {
                Console.Error.WriteLine(" ----------------- DIFF --------------------");
            }
            else {
                Console.WriteLine("SUCCESS");
            }

            File.WriteAllText("../../../TestFile-GENERATED-2.json", x.ToString());
        }

        // if (jsonVal.TryGet(out var v, "rootObject", "properties", "references")) {
        //     v.As<JArray>().Elements.Add(new JString("REF-004"));
        //     Console.WriteLine(String.Join(", ", v.As<List<string>>()));
        // }

        // Console.WriteLine(jsonVal.ToString());
    }
}