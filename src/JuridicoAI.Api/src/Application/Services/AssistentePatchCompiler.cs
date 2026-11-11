using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistentePatchCompiler
{
    public static JsonElement DocumentoDoEditor(JsonElement document)
    {
        var blocks = Blocks(document);
        var normalized = JsonNode.Parse(document.GetRawText())!.AsObject();
        if (normalized["content"] is not JsonArray content || content.Count == 0)
            normalized["content"] = new JsonArray(blocks.Select(b => JsonNode.Parse(b.GetRawText())).ToArray());
        return JsonSerializer.SerializeToElement(normalized);
    }
    private static readonly JsonElement EmptyParagraph = JsonSerializer.SerializeToElement(new { type = "paragraph" });
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static JsonDocument Json<T>(T value) => JsonSerializer.SerializeToDocument(value, JsonOptions);
    public static string Hash<T>(T value)
    {
        using var json = Json(value);
        var canonical = new StringBuilder();
        Canonical(json.RootElement, canonical);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Canonical(JsonElement element, StringBuilder output)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            output.Append('{');
            var first = true;
            foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                if (!first) output.Append(',');
                first = false;
                CanonicalString(property.Name, output); output.Append(':'); Canonical(property.Value, output);
            }
            output.Append('}');
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            output.Append('[');
            var first = true;
            foreach (var item in element.EnumerateArray())
            { if (!first) output.Append(','); first = false; Canonical(item, output); }
            output.Append(']');
        }
        else if (element.ValueKind == JsonValueKind.String) CanonicalString(element.GetString()!, output);
        else output.Append(element.GetRawText());
    }

    // Hash bytes have no optional escaping, including supplementary Unicode.
    // This is only canonical hash input; API responses use the standard serializer.
    private static void CanonicalString(string value, StringBuilder output)
    {
        output.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\f': output.Append("\\f"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    if (character < 0x20) output.Append("\\u").Append(((int)character).ToString("x4"));
                    else output.Append(character);
                    break;
            }
        }
        output.Append('"');
    }

    public IReadOnlyList<AssistenteGrupoPersistido> Compilar(JsonElement document, Guid snapshotId, IReadOnlyList<AssistenteGrupoRequest>? requests)
    {
        var blocks = Blocks(document);
        if (requests is null || requests.Count is < 1 or > 100) throw Invalid("Informe de 1 a 100 grupos.");
        var occupied = new List<(int Start, int End)>();
        var result = new List<AssistenteGrupoPersistido>();
        foreach (var group in requests)
        {
            if (group is null || string.IsNullOrWhiteSpace(group.Explicacao) || group.Explicacao.Length > 2000 ||
                group.Operacoes is null || group.Operacoes.Count is < 1 or > 100) throw Invalid("Grupo de alterações inválido.");
            var operations = new List<AssistenteOperacaoPersistida>();
            var original = new List<JsonElement>();
            var proposed = new List<JsonElement>();
            var anchors = new List<string>();
            if (group.Operacoes.Any(op => op is null)) throw Invalid("Operação inválida.");
            foreach (var op in group.Operacoes.OrderBy(x => x.StartIndex))
            {
                if (op.StartIndex < 0 || op.DeleteCount < 0 || op.StartIndex > blocks.Count ||
                    op.DeleteCount > blocks.Count - op.StartIndex || op.NovosBlocos is null || op.NovosBlocos.Count > 30000)
                    throw Invalid("A proposta aponta para blocos inválidos.");
                var end = op.StartIndex + op.DeleteCount;
                if (occupied.Any(x => (op.StartIndex < x.End && end > x.Start) ||
                    (op.DeleteCount == 0 && (op.StartIndex == x.Start || op.StartIndex > x.Start && op.StartIndex < x.End)) ||
                    (x.Start == x.End && x.Start >= op.StartIndex && x.Start < end)))
                    throw Invalid("Operações sobrepostas precisam ser consolidadas.");
                occupied.Add((op.StartIndex, end));
                foreach (var node in op.NovosBlocos) ValidateNode(node, "doc", 0);
                var before = blocks.Skip(op.StartIndex).Take(op.DeleteCount).Select(x => x.Clone()).ToArray();
                var after = op.NovosBlocos.Select(x => x.Clone()).ToArray();
                if (Hash(before) == Hash(after)) throw Invalid("A operação não contém diferenças.");
                original.AddRange(before); proposed.AddRange(after);
                anchors.Add($"{snapshotId:D}:bloco-{op.StartIndex}:{op.DeleteCount}");
                operations.Add(new("SpliceBlocos", op.StartIndex, op.DeleteCount, Hash(before), after));
            }
            var id = Guid.NewGuid();
            var preview = Hash(new { grupoId = id, alvosOriginais = anchors, original, proposto = proposed });
            result.Add(new(id, "Pendente", group.Explicacao.Trim(), preview, anchors, original, proposed, operations));
        }
        using var final = Aplicar(document, result);
        return result;
    }

    public JsonDocument Aplicar(JsonElement document, IReadOnlyList<AssistenteGrupoPersistido> groups)
    {
        Blocks(document);
        var current = JsonNode.Parse(document.GetRawText())!.AsObject();
        var blocks = current["content"] as JsonArray ?? new JsonArray();
        current["content"] ??= blocks;
        if (blocks.Count == 0) blocks.Add(JsonNode.Parse(EmptyParagraph.GetRawText()));
        foreach (var op in groups.SelectMany(x => x.Operacoes).OrderByDescending(x => x.StartIndex))
        {
            if (op.StartIndex < 0 || op.DeleteCount < 0 || op.StartIndex > blocks.Count || op.DeleteCount > blocks.Count - op.StartIndex)
                throw Stale();
            var before = blocks.Skip(op.StartIndex).Take(op.DeleteCount).Select(x => JsonSerializer.SerializeToElement(x, JsonOptions)).ToArray();
            if (Hash(before) != op.ExpectedBlocksSha256) throw Stale();
            for (var i = 0; i < op.DeleteCount; i++) blocks.RemoveAt(op.StartIndex);
            for (var i = 0; i < op.NovosBlocos.Count; i++) blocks.Insert(op.StartIndex + i, JsonNode.Parse(op.NovosBlocos[i].GetRawText()));
        }
        if (blocks.Count == 0) blocks.Add(new JsonObject { ["type"] = "paragraph" });
        var text = current.ToJsonString();
        if (text.Length > 1024 * 1024) throw Invalid("O documento excede o limite permitido.");
        var result = JsonDocument.Parse(text);
        var count = 0;
        CheckComplexity(result.RootElement, 0, ref count);
        return result;
    }

    public IReadOnlyList<AssistenteGrupoPersistido> Remapear(IReadOnlyList<AssistenteGrupoPersistido> groups, IReadOnlySet<Guid> applied)
    {
        var changes = groups.Where(x => applied.Contains(x.Id)).SelectMany(x => x.Operacoes).ToArray();
        return groups.Select(group => applied.Contains(group.Id) ? group with { Estado = "Aplicado" } :
            group.Estado != "Pendente" ? group : group with { Operacoes = group.Operacoes.Select(op => op with
            {
                StartIndex = op.StartIndex + changes.Where(change => change.StartIndex + change.DeleteCount <= op.StartIndex)
                    .Sum(change => change.NovosBlocos.Count - change.DeleteCount)
            }).ToArray() }).ToArray();
    }

    private static IReadOnlyList<JsonElement> Blocks(JsonElement doc)
    {
        if (doc.ValueKind != JsonValueKind.Object || !doc.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "doc")
            throw Invalid("Documento TipTap inválido.");
        if (!doc.TryGetProperty("content", out var content)) return [EmptyParagraph];
        if (content.ValueKind != JsonValueKind.Array) throw Invalid("Conteúdo inválido.");
        return content.GetArrayLength() == 0 ? [EmptyParagraph] : content.EnumerateArray().ToArray();
    }

    private static void ValidateNode(JsonElement node, string parent, int depth)
    {
        if (depth > 24 || node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("type", out var rawType) || rawType.ValueKind != JsonValueKind.String)
            throw Invalid("Elemento inválido na proposta.");
        var type = rawType.GetString()!;
        string[] allowed = parent switch
        {
            "doc" or "blockquote" or "listItem" => ["paragraph", "heading", "bulletList", "orderedList", "blockquote", "codeBlock", "horizontalRule"],
            "bulletList" or "orderedList" => ["listItem"],
            "paragraph" or "heading" => ["text", "hardBreak"],
            "codeBlock" => ["text"], _ => []
        };
        if (!allowed.Contains(type) || node.EnumerateObject().Any(p => p.Name is not ("type" or "attrs" or "content" or "text" or "marks")))
            throw Invalid("O elemento não é suportado na proposta.");
        if (type == "text")
        {
            if (!node.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(text.GetString()) || node.TryGetProperty("content", out _))
                throw Invalid("Texto inválido.");
        }
        else if (node.TryGetProperty("text", out _)) throw Invalid("Texto fora de um nó textual.");
        if (node.TryGetProperty("attrs", out var attrs))
        {
            if (attrs.ValueKind != JsonValueKind.Object) throw Invalid("Atributos inválidos.");
            foreach (var attr in attrs.EnumerateObject())
            {
                var valid = attr.Name switch
                {
                    "level" => type == "heading" && attr.Value.ValueKind == JsonValueKind.Number && attr.Value.TryGetInt32(out var level) && level is >= 1 and <= 6,
                    "start" => type == "orderedList" && attr.Value.ValueKind == JsonValueKind.Number && attr.Value.TryGetInt32(out var start) && start is >= 1 and <= 100000,
                    "type" => type == "orderedList" && IsOptionalEnum(attr.Value, ["1", "a", "A", "i", "I"]),
                    "textAlign" => type is "paragraph" or "heading" && IsOptionalEnum(attr.Value, ["left", "right", "center", "justify"]),
                    "language" => type == "codeBlock" && (attr.Value.ValueKind == JsonValueKind.Null || attr.Value.ValueKind == JsonValueKind.String && attr.Value.GetString()!.Length <= 40),
                    _ => false
                };
                if (!valid) throw Invalid("Atributo não suportado.");
            }
        }
        if (type == "heading" && (!node.TryGetProperty("attrs", out var heading) || !heading.TryGetProperty("level", out _))) throw Invalid("Título sem nível.");
        if (node.TryGetProperty("marks", out var marks))
        {
            if (type != "text" || marks.ValueKind != JsonValueKind.Array || marks.GetArrayLength() > 8) throw Invalid("Marcas inválidas.");
            if (parent == "codeBlock" && marks.GetArrayLength() > 0) throw Invalid("Marcas não são permitidas em bloco de código.");
            var seen = new HashSet<string>();
            foreach (var mark in marks.EnumerateArray())
            {
                if (mark.ValueKind != JsonValueKind.Object || !mark.TryGetProperty("type", out var mt) || mt.ValueKind != JsonValueKind.String ||
                    !new[] { "bold", "italic", "underline", "strike", "code", "link" }.Contains(mt.GetString()) ||
                    mark.EnumerateObject().Any(p => p.Name is not ("type" or "attrs"))) throw Invalid("Marca não suportada.");
                if (!seen.Add(mt.GetString()!)) throw Invalid("Marca duplicada.");
                if (mark.TryGetProperty("attrs", out var ma))
                {
                    if (mt.GetString() != "link" || ma.ValueKind != JsonValueKind.Object ||
                        ma.EnumerateObject().Any(p => p.Name is not ("href" or "target" or "rel" or "class"))) throw Invalid("Atributos de marca inválidos.");
                    if (ma.TryGetProperty("class", out var cl) && cl.ValueKind != JsonValueKind.Null) throw Invalid("Classe de link não permitida.");
                    if (ma.TryGetProperty("target", out var target) && !IsOptionalEnum(target, ["_blank", "_self"])) throw Invalid("Destino de link inválido.");
                    if (ma.TryGetProperty("rel", out var rel) && rel.ValueKind != JsonValueKind.Null && (rel.ValueKind != JsonValueKind.String || rel.GetString()!.Length > 120)) throw Invalid("Rel inválido.");
                }
                if (mt.GetString() == "link" && (!mark.TryGetProperty("attrs", out var la) || !la.TryGetProperty("href", out var href) ||
                    href.ValueKind != JsonValueKind.String || href.GetString()!.Length > 2048 ||
                    !Uri.TryCreate(href.GetString(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http" or "mailto")))
                    throw Invalid("Link não permitido.");
            }
        }
        if (node.TryGetProperty("content", out var children))
        {
            if (children.ValueKind != JsonValueKind.Array || type is "text" or "hardBreak" or "horizontalRule") throw Invalid("Filhos inválidos.");
            foreach (var child in children.EnumerateArray()) ValidateNode(child, type, depth + 1);
            if (type is "bulletList" or "orderedList" or "listItem" or "blockquote" && children.GetArrayLength() == 0) throw Invalid("Bloco vazio.");
            if (type == "listItem" && children[0].GetProperty("type").GetString() != "paragraph") throw Invalid("Item de lista precisa iniciar com parágrafo.");
        }
        else if (type is "bulletList" or "orderedList" or "listItem" or "blockquote") throw Invalid("Bloco sem conteúdo.");
    }

    private static void CheckComplexity(JsonElement node, int depth, ref int count)
    {
        if (node.ValueKind != JsonValueKind.Object || depth > 24 || ++count > 30000) throw Invalid("Documento complexo demais.");
        if (node.TryGetProperty("content", out var children))
        {
            if (children.ValueKind != JsonValueKind.Array) throw Invalid("Conteúdo inválido.");
            foreach (var child in children.EnumerateArray()) CheckComplexity(child, depth + 1, ref count);
        }
    }
    public static AssistenteIaException Invalid(string message) => new(400, "AI_INVALID_REQUEST", message);
    private static bool IsOptionalEnum(JsonElement value, string[] allowed) => value.ValueKind == JsonValueKind.Null ||
        value.ValueKind == JsonValueKind.String && allowed.Contains(value.GetString());
    public static AssistenteIaException Stale() => new(409, "AI_PROPOSAL_STALE", "A Peça mudou desde a preparação da proposta.");
}
