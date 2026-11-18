using System.Text.Json;
using JuridicoAI.Api.Application.DTOs;

namespace JuridicoAI.Api.Application.Services;

public static class AssistenteAutomaticProposalPolicy
{
    // Whole-body coverage is computed across every group, not trusted to the
    // model's intent label. Even a one-block Peça is protected conservatively.
    public static bool CobreDocumento(JsonElement document, IReadOnlyList<AssistenteGrupoRequest> groups)
    {
        var count = document.TryGetProperty("content", out var content) ? content.GetArrayLength() : 0;
        if (count == 0) return true;
        var meaningful = content.EnumerateArray().Select((block, index) => (block, index)).Where(x => HasContent(x.block)).Select(x => x.index).ToArray();
        if (meaningful.Length == 0) return true;
        var covered = new HashSet<int>();
        foreach (var op in groups.SelectMany(g => g.Operacoes))
            for (var i = op.StartIndex; i < op.StartIndex + op.DeleteCount; i++) covered.Add(i);
        return meaningful.All(covered.Contains);
    }

    private static bool HasContent(JsonElement node) =>
        node.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString()) ||
        node.TryGetProperty("type", out var type) && type.GetString() == "horizontalRule" ||
        node.TryGetProperty("content", out var children) && children.ValueKind == JsonValueKind.Array && children.EnumerateArray().Any(HasContent);
}
