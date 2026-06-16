namespace JuridicoAI.Scrapper.Cli;

internal static class ScraperUsageWriter
{
    public static void Write(TextWriter writer)
    {
        writer.WriteLine("Uso:");
        writer.WriteLine("  dotnet run --project src/JuridicoAI.Scrapper -- --termo \"uso indevido\" --max 20 --output resultado.json");
        writer.WriteLine();
        writer.WriteLine("Opcoes:");
        writer.WriteLine("  --termo, -t          Termo de pesquisa livre. Obrigatorio.");
        writer.WriteLine("  --max, -m            Quantidade maxima de resultados. Padrao: 20. Limite interno: 200.");
        writer.WriteLine("  --tipo-decisao       A=acordaos, H=homologacoes, D=decisoes monocraticas. Padrao: A.");
        writer.WriteLine("  --sem-sinonimos      Desativa pesquisa por sinonimos.");
        writer.WriteLine("  --raw-html           Inclui HTML bruto da primeira pagina no JSON.");
        writer.WriteLine("  --output, -o         Caminho para salvar o JSON. Se omitido, escreve no stdout.");
    }
}
