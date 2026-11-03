using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GGD.Events;

Console.OutputEncoding = new UTF8Encoding(false);
var catalog = EventDataCatalog.Default;
var document = new {
    version = EventDataCatalog.Version,
    data = catalog.Data.Select(data => new {
        DataKey = data.Key.ToString(), data.Name, Operation = data.Operation.ToString(), data.Unit
    }),
    hooks = catalog.Hooks.Select(hook => new {
        HookKey = hook.Key.ToString(), hook.Name, Sources = string.Join(";", hook.Sources)
    })
};
Console.WriteLine(JsonSerializer.Serialize(document, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
