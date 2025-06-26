```C#
namespace JJson;

var obj = JJson.ParseJson(File.ReadAllText("TestFile.json"));

Console.WriteLine(obj["rootObject"]["id"].AsString());
Console.WriteLine(obj["rootObject"]["properties"]["enabled"].AsBoolean());
Console.WriteLine(obj["rootObject"]["properties"]["numbers"].AsArray<int>().StringJoin(", "));
```
> ROOT-12345 <br/>
> True       <br/>
> 1, 2, 3, 4 <br/>

```json
{
  "rootObject": "ROOT-12345",
  "properties": {
    "enabled": true,
    "numbers": [1, 2, 3, 4]
  }
```
