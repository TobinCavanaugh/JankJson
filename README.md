# JankJson (JJson)

A lightweight, intuitive JSON manipulation library for C# that prioritizes simplicity and ease of use. JankJson provides a clean API for parsing, querying, and transforming JSON data without the overhead of complex configuration or verbose syntax.

## Features

- **Type-Safe Conversions**: Built-in type casting with helpful error messages
- **Flexible Querying**: Navigate JSON structures with path-based lookups
- **LINQ Integration**: Full enumeration support for arrays and objects

## Quick Start

```csharp
using JJson;

var json = JJson.ParseJson(File.ReadAllText("data.json"));

// Direct property access
var id = json["rootObject"]["id"].AsString();
var enabled = json["rootObject"]["properties"]["enabled"].AsBoolean();
var numbers = json["rootObject"]["properties"]["numbers"].AsArray<int>();

Console.WriteLine($"ID: {id}");
Console.WriteLine($"Enabled: {enabled}");
Console.WriteLine($"Numbers: {string.Join(", ", numbers)}");
```

## Core Types

JankJson represents JSON data through a hierarchy of strongly-typed classes:

- `JString` - JSON strings
- `JNumber` - JSON numbers (stored as double)
- `JBoolean` - JSON booleans
- `JNull` - JSON null values
- `JObject` - JSON objects (key-value pairs)
- `JArray` - JSON arrays

## Property Access

### Direct Indexing
```csharp
// Object property access
var name = json["user"]["name"].AsString();

// Array element access
var firstItem = json["items"][0].AsString();

// Mixed access
var nestedValue = json["data"][0]["properties"]["id"].AsString();
```

### Safe Navigation
```csharp
// Check if a nested path exists
if (json.TryGet(out var result, "user", "settings", "theme")) {
    var theme = result.AsString();
}
```

## Type Conversions

### Generic As<T>() Method
```csharp
var text = json["message"].As<string>();
var count = json["count"].As<int>();
var price = json["price"].As<decimal>();
var active = json["active"].As<bool>();
```

### Explicit Casting
```csharp
var id = (string)json["id"];
var count = (int)json["count"];
var enabled = (bool)json["enabled"];
```

### Specialized Conversion Methods
```csharp
var user = json["user"].AsJObject();
var tags = json["tags"].AsJArray();
var score = json["score"].AsNumber();
var message = json["message"].AsString();
var active = json["active"].AsBoolean();
```

## Collection Operations

### Working with Arrays
```csharp
var items = json["items"].AsJArray();

// Convert to typed arrays
var ids = json["userIds"].AsArray<int>();
var names = json["names"].AsList<string>();

// Enumerate directly
foreach (var item in json["products"]) {
    Console.WriteLine(item["name"].AsString());
}
```

### Working with Objects
```csharp
var settings = json["settings"].AsJObject();

// Iterate over key-value pairs
foreach (var kvp in settings.Pairs) {
    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
}

// Access keys and values separately
var configKeys = settings.Keys;
var configValues = settings.Values;
```

## Advanced Querying

### Recursive Traversal
```csharp
// Apply function to all nodes
json.Recurse(node => {
    if (node.IsString && node.AsString().Contains("error")) {
        Console.WriteLine($"Found error: {node.AsString()}");
    }
    return true; // Continue traversal
});
```

### Finding Specific Values
```csharp
// Find first matching node
var errorNode = json.RecurseFirst(node => 
    node.IsString && node.AsString().StartsWith("ERR"));

// Find all matching nodes
var allNumbers = json.RecurseWhere(node => 
    node.IsNumber && node.AsNumber() > 100);
```

### Transformation
```csharp
// Transform the entire structure
var transformed = json.RecurseTransform(node => {
    if (node.IsString) {
        return new JString(node.AsString().ToUpper());
    }
    return node;
});
```

### Type-Specific Queries
```csharp
// Get all strings in the document
var allStrings = json.DescendantsOfType<JString>();

// Get all numbers
var allNumbers = json.DescendantsOfType<JNumber>();
```

## Type Checking

```csharp
if (json["data"].IsArray) {
    var items = json["data"].AsJArray();
}

if (json["user"].IsObject) {
    var user = json["user"].AsJObject();
}

// Check for leaf nodes (primitives)
if (json["value"].IsLeaf) {
    var stringValue = json["value"].ValueToString();
}

// Check for Nodes (Array / Object)
if (json["value"].IsNode) {
    var stringValue = json["value"].ToString();
}
```

## Error Handling

JankJson provides clear error messages when operations fail:

```csharp
try {
    var value = json["nonexistent"]["property"].AsString();
} catch (Exception ex) {
    // Clear indication of what went wrong and where
    Console.WriteLine(ex.Message);
}
```

## Example: Processing Configuration

```json
{
  "database": {
    "host": "localhost",
    "port": 5432,
    "credentials": {
      "username": "admin",
      "password": "secret"
    }
  },
  "features": ["logging", "caching", "monitoring"],
  "settings": {
    "debug": true,
    "maxConnections": 100
  }
}
```

```csharp
var config = JJson.ParseJson(jsonString);

// Extract database configuration
var dbHost = config["database"]["host"].AsString();
var dbPort = config["database"]["port"].As<int>();
var username = config["database"]["credentials"]["username"].AsString();

// Process feature list
var features = config["features"].AsList<string>();
Console.WriteLine($"Enabled features: {string.Join(", ", features)}");

// Check settings
var debugMode = config["settings"]["debug"].AsBoolean();
var maxConns = config["settings"]["maxConnections"].As<int>();

// Find all numeric values in configuration
var allNumbers = config.DescendantsOfType<JNumber>()
    .Select(n => n.Value)
    .ToList();
```

## Installation

Add JankJson to your project and start parsing JSON with minimal setup required.

## License

[License information here]

## Contributing

Contributions are welcome. Please ensure all changes maintain the library's focus on simplicity and ease of use.
