using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FethEditor.Gui;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class UiLocaleJsonContext : JsonSerializerContext { }
