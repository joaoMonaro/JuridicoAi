using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace JuridicoAI.Api.Application.Services;

public sealed partial class AssistentePiiPseudonymizationSession
{
    public const string ProviderInstruction =
        "- Preserve exatamente eventuais marcadores de pseudonimização no formato [CPF_1], [CNPJ_1], [RG_1], [EMAIL_1], [TEL_1] e [CEP_1], sem alterá-los, traduzi-los ou removê-los.";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    // 1. CNPJ formatado (validado por DV) ou CNPJ 14 dígitos precedido de rótulo CNPJ
    private static readonly Regex CnpjFormattedRegex = new(
        @"(?<!\d)\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}(?!\d)",
        RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex CnpjLabeledRegex = new(
        @"(?i)\b(CNPJ(?:/MF)?\s*(?:n[º°.]*\s*)?[:\-]?\s*)(\d{14})(?!\d)",
        RegexOptions.Compiled, RegexTimeout);

    // 2. CPF formatado (validado por DV) ou CPF 11 dígitos precedido de rótulo CPF
    private static readonly Regex CpfFormattedRegex = new(
        @"(?<!\d)\d{3}\.\d{3}\.\d{3}-\d{2}(?!\d)",
        RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex CpfLabeledRegex = new(
        @"(?i)\b(CPF(?:/MF)?\s*(?:n[º°.]*\s*)?[:\-]?\s*)(\d{11})(?!\d)",
        RegexOptions.Compiled, RegexTimeout);

    // 3. E-mail
    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled, RegexTimeout);

    // 4. Telefone brasileiro com DDD e separador (evita colisões com datas, valores e CNJ)
    private static readonly Regex PhoneRegex = new(
        @"(?<!\d)(?:\+55\s*)?(?:\(0?[1-9]{2}\)\s*|0?[1-9]{2}[\s\-])(?:9\d{4}|\d{4})[\-\s]\d{4}(?!\d)",
        RegexOptions.Compiled, RegexTimeout);

    // 5. RG precedido de rótulo explícito
    private static readonly Regex RgLabeledRegex = new(
        @"(?i)\b((?:R\.?G\.?|Registro\s+Geral|C[eé]dula\s+de\s+Identidade)\s*(?:n[º°.]*\s*)?[:\-]?\s*)([0-9]{1,2}\.?[0-9]{3}\.?[0-9]{3}-?[0-9Xx]|[0-9]{5,10}-?[0-9Xx])(?![0-9A-Za-z])",
        RegexOptions.Compiled, RegexTimeout);

    // 6. CEP precedido de rótulo explícito
    private static readonly Regex CepLabeledRegex = new(
        @"(?i)\b(CEP\s*(?:n[º°.]*\s*)?[:\-]?\s*)(\d{2}\.?\d{3}-\d{3})(?!\d)",
        RegexOptions.Compiled, RegexTimeout);

    // Reidratação em passada única
    private static readonly Regex PlaceholderRegex = new(
        @"\[(?:CPF|CNPJ|RG|EMAIL|TEL|CEP)_\d+\]",
        RegexOptions.Compiled, RegexTimeout);

    private readonly object _gate = new();
    private readonly Dictionary<(string Kind, string Value), string> _originalToToken = new();
    private readonly Dictionary<string, string> _tokenToOriginal = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _counters = new(StringComparer.Ordinal);

    public bool HasReplacements
    {
        get
        {
            lock (_gate) return _tokenToOriginal.Count > 0;
        }
    }

    public string Pseudonymize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        var result = CnpjFormattedRegex.Replace(text, m =>
            CnpjValido(SomenteDigitos(m.Value)) ? GetOrCreateToken("CNPJ", m.Value) : m.Value);

        result = CnpjLabeledRegex.Replace(result, m =>
            CnpjValido(m.Groups[2].Value)
                ? m.Groups[1].Value + GetOrCreateToken("CNPJ", m.Groups[2].Value)
                : m.Value);

        result = CpfFormattedRegex.Replace(result, m =>
            CpfValido(SomenteDigitos(m.Value)) ? GetOrCreateToken("CPF", m.Value) : m.Value);

        result = CpfLabeledRegex.Replace(result, m =>
            CpfValido(m.Groups[2].Value)
                ? m.Groups[1].Value + GetOrCreateToken("CPF", m.Groups[2].Value)
                : m.Value);

        result = EmailRegex.Replace(result, m => GetOrCreateToken("EMAIL", m.Value));

        result = PhoneRegex.Replace(result, m => GetOrCreateToken("TEL", m.Value));

        result = RgLabeledRegex.Replace(result, m =>
            m.Groups[1].Value + GetOrCreateToken("RG", m.Groups[2].Value));

        result = CepLabeledRegex.Replace(result, m =>
            m.Groups[1].Value + GetOrCreateToken("CEP", m.Groups[2].Value));

        return result;
    }

    public string Rehydrate(string? text)
    {
        if (string.IsNullOrEmpty(text) || !HasReplacements) return text ?? string.Empty;

        return PlaceholderRegex.Replace(text, m =>
        {
            lock (_gate)
            {
                return _tokenToOriginal.TryGetValue(m.Value, out var original) ? original : m.Value;
            }
        });
    }

    public JsonElement RehydrateJson(JsonElement element)
    {
        if (!HasReplacements) return element;

        var node = JsonNode.Parse(element.GetRawText());
        if (node is null) return element;

        var changed = RehydrateNode(node);
        return changed ? JsonSerializer.SerializeToElement(node) : element;
    }

    private bool RehydrateNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var changed = false;
                var keys = obj.Select(kv => kv.Key).ToArray();
                foreach (var key in keys)
                {
                    var child = obj[key];
                    if (child is JsonValue val && val.TryGetValue<string>(out var str))
                    {
                        var replaced = Rehydrate(str);
                        if (!ReferenceEquals(replaced, str) && !string.Equals(replaced, str, StringComparison.Ordinal))
                        {
                            obj[key] = JsonValue.Create(replaced);
                            changed = true;
                        }
                    }
                    else if (child is not null && RehydrateNode(child))
                    {
                        changed = true;
                    }
                }
                return changed;
            }
            case JsonArray arr:
            {
                var changed = false;
                for (var i = 0; i < arr.Count; i++)
                {
                    var child = arr[i];
                    if (child is JsonValue val && val.TryGetValue<string>(out var str))
                    {
                        var replaced = Rehydrate(str);
                        if (!ReferenceEquals(replaced, str) && !string.Equals(replaced, str, StringComparison.Ordinal))
                        {
                            arr[i] = JsonValue.Create(replaced);
                            changed = true;
                        }
                    }
                    else if (child is not null && RehydrateNode(child))
                    {
                        changed = true;
                    }
                }
                return changed;
            }
            default:
                return false;
        }
    }

    private string GetOrCreateToken(string kind, string original)
    {
        lock (_gate)
        {
            var key = (kind, original);
            if (_originalToToken.TryGetValue(key, out var existing))
                return existing;

            var next = _counters.TryGetValue(kind, out var count) ? count + 1 : 1;
            _counters[kind] = next;
            var token = $"[{kind}_{next}]";
            _originalToToken[key] = token;
            _tokenToOriginal[token] = original;
            return token;
        }
    }

    private static string SomenteDigitos(string value)
    {
        var buffer = new char[value.Length];
        var len = 0;
        foreach (var c in value)
            if (char.IsAsciiDigit(c))
                buffer[len++] = c;
        return new string(buffer, 0, len);
    }

    private static bool CpfValido(string cpf)
    {
        if (cpf.Length != 11 || cpf.All(c => c == cpf[0])) return false;
        var primeiro = CalcularDigito(cpf.AsSpan(0, 9), 10);
        var segundo = CalcularDigito(cpf.AsSpan(0, 10), 11);
        return cpf[9] - '0' == primeiro && cpf[10] - '0' == segundo;
    }

    private static bool CnpjValido(string cnpj)
    {
        if (cnpj.Length != 14 || cnpj.All(c => c == cnpj[0])) return false;
        ReadOnlySpan<int> pesosPrimeiro = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        ReadOnlySpan<int> pesosSegundo = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var primeiro = CalcularDigito(cnpj.AsSpan(0, 12), pesosPrimeiro);
        var segundo = CalcularDigito(cnpj.AsSpan(0, 13), pesosSegundo);
        return cnpj[12] - '0' == primeiro && cnpj[13] - '0' == segundo;
    }

    private static int CalcularDigito(ReadOnlySpan<char> digitos, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < digitos.Length; i++)
            soma += (digitos[i] - '0') * (pesoInicial - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int CalcularDigito(ReadOnlySpan<char> digitos, ReadOnlySpan<int> pesos)
    {
        var soma = 0;
        for (var i = 0; i < digitos.Length; i++)
            soma += (digitos[i] - '0') * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
