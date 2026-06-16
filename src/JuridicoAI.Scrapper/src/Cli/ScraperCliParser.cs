using JuridicoAI.Scrapper.Models;

namespace JuridicoAI.Scrapper.Cli;

internal static class ScraperCliParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0 || IsHelp(args))
        {
            return CliParseResult.Fail(args.Length == 0 ? "Informe os argumentos obrigatorios." : null);
        }

        string? termo = null;
        var quantidadeMaxima = 20;
        var tipoDecisao = "A";
        var incluirSinonimos = true;
        var salvarHtmlBruto = false;
        string? outputPath = null;

        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--termo":
                    case "-t":
                        termo = ReadValue(args, ref i, arg);
                        break;
                    case "--max":
                    case "-m":
                        var maxValue = ReadValue(args, ref i, arg);
                        if (!int.TryParse(maxValue, out quantidadeMaxima) || quantidadeMaxima <= 0)
                        {
                            return CliParseResult.Fail("O argumento --max deve ser um inteiro maior que zero.");
                        }
                        break;
                    case "--tipo-decisao":
                        tipoDecisao = ReadValue(args, ref i, arg).ToUpperInvariant();
                        break;
                    case "--sem-sinonimos":
                        incluirSinonimos = false;
                        break;
                    case "--raw-html":
                        salvarHtmlBruto = true;
                        break;
                    case "--output":
                    case "-o":
                        outputPath = ReadValue(args, ref i, arg);
                        break;
                    default:
                        return CliParseResult.Fail($"Argumento desconhecido: {arg}");
                }
            }
        }
        catch (ArgumentException ex)
        {
            return CliParseResult.Fail(ex.Message);
        }

        if (string.IsNullOrWhiteSpace(termo))
        {
            return CliParseResult.Fail("O argumento --termo e obrigatorio.");
        }

        if (tipoDecisao is not ("A" or "H" or "D"))
        {
            return CliParseResult.Fail("O argumento --tipo-decisao deve ser A, H ou D.");
        }

        return CliParseResult.Ok(new TjspSearchRequest
        {
            Termo = termo,
            QuantidadeMaxima = quantidadeMaxima,
            TipoDecisao = tipoDecisao,
            IncluirSinonimos = incluirSinonimos,
            SalvarHtmlBruto = salvarHtmlBruto,
            OutputPath = outputPath
        });
    }

    public static bool IsHelp(string[] args)
    {
        return args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
               args.Contains("-h", StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            throw new ArgumentException($"O argumento {argumentName} exige um valor.");
        }

        index++;
        return args[index];
    }
}
