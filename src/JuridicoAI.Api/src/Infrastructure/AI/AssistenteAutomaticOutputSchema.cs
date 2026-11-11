using System.Text.Json;
using System.Text.Json.Nodes;

namespace JuridicoAI.Api.Infrastructure.AI;

internal static class AssistenteAutomaticOutputSchema
{
    public static JsonElement Criar(bool podeEditar, bool podeGerarMinuta)
    {
        var intents = new List<string> { "Responder" };
        if (podeEditar) intents.Add("EditarPeca");
        if (podeGerarMinuta) intents.Add("GerarMinuta");
        var definitions = new JsonObject();
        JsonObject Ref(string name) => new() { ["$ref"] = "#/$defs/" + name };
        JsonObject Str(params string[] values) => values.Length == 0 ? new() { ["type"] = "string" } : new() { ["type"] = "string", ["enum"] = JsonSerializer.SerializeToNode(values) };
        JsonObject Nullable(JsonObject type) => new() { ["anyOf"] = new JsonArray(type, new JsonObject { ["type"] = "null" }) };
        JsonObject Array(JsonObject item) => new() { ["type"] = "array", ["items"] = item };
        JsonObject Object(params (string Name, JsonObject Schema)[] properties) => new()
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new JsonObject(properties.Select(p => new KeyValuePair<string, JsonNode?>(p.Name, p.Schema))),
            ["required"] = JsonSerializer.SerializeToNode(properties.Select(p => p.Name))
        };
        JsonObject Node(string type, params (string Name, JsonObject Schema)[] props) => Object(new[] { ("type", Str(type)) }.Concat(props).ToArray());
        JsonObject Number() => new() { ["type"] = "integer" };
        definitions["Mark"] = new JsonObject { ["anyOf"] = new JsonArray(
            Object(("type", Str("bold", "italic", "underline", "strike", "code"))),
            Object(("type", Str("link")), ("attrs", Object(("href", Str()), ("target", Nullable(Str("_blank", "_self"))),
                ("rel", Nullable(Str())), ("class", new JsonObject { ["type"] = "null" }))))) };
        definitions["Inline"] = new JsonObject { ["anyOf"] = new JsonArray(
            Node("text", ("text", Str()), ("marks", Array(Ref("Mark")))), Node("hardBreak")) };
        definitions["CodeText"] = Node("text", ("text", Str()));
        definitions["ListItem"] = Node("listItem", ("content", Array(Ref("Node"))));
        definitions["Node"] = new JsonObject { ["anyOf"] = new JsonArray(
            Node("paragraph", ("attrs", Object(("textAlign", Nullable(Str("left", "right", "center", "justify"))))), ("content", Array(Ref("Inline")))),
            Node("heading", ("attrs", Object(("level", Number()), ("textAlign", Nullable(Str("left", "right", "center", "justify"))))), ("content", Array(Ref("Inline")))),
            Node("blockquote", ("content", Array(Ref("Node")))),
            Node("bulletList", ("content", Array(Ref("ListItem")))),
            Node("orderedList", ("attrs", Object(("start", Number()), ("type", Nullable(Str("1", "a", "A", "i", "I"))))), ("content", Array(Ref("ListItem")))),
            Node("codeBlock", ("attrs", Object(("language", Nullable(Str())))), ("content", Array(Ref("CodeText")))),
            Node("horizontalRule")) };
        var schema = Object(("titulo", Str()), ("texto", Str()), ("resumo", Str()),
            ("intencao", Str(intents.ToArray())),
            ("alertas", Array(Object(("codigo", Str()), ("mensagem", Str())))),
            ("grupos", Array(Object(("explicacao", Str()), ("operacoes", Array(Object(
                ("startIndex", Number()), ("deleteCount", Number()), ("novosBlocos", Array(Ref("Node"))))))))),
            ("substituirPropostaReferenciada", new JsonObject { ["type"] = "boolean" }));
        schema["$defs"] = definitions;
        return JsonSerializer.SerializeToElement(schema);
    }
}
