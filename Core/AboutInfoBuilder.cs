using System;
using System.Collections.Generic;

namespace ListForge.Core;

public sealed record AboutInfo(
    string ProductName,
    string Version,
    string Edition,
    string LicensedTo,
    bool IsTrial,
    int TrialRemaining,
    int TrialLimit,
    string Author,
    string Contact,
    string ConfigPath,
    string LogsPath,
    string SystemDescription);

public static class AboutInfoBuilder
{
    public static string BuildSupportText(AboutInfo info)
    {
        var lines = new List<string>
        {
            info.ProductName,
            $"Versão: {info.Version}",
            $"Edição: {info.Edition}",
            $"Licenciado para: {info.LicensedTo}",
        };

        if (info.IsTrial)
            lines.Add($"Créditos Trial: {info.TrialRemaining}/{info.TrialLimit}");
        else
            lines.Add("Versão completa: sem limite de créditos Trial.");

        lines.Add($"Autor: {info.Author}");
        lines.Add($"Contato: {info.Contact}");
        lines.Add($"Pasta de configuração: {info.ConfigPath}");
        lines.Add($"Pasta de logs: {info.LogsPath}");
        lines.Add($"Sistema: {info.SystemDescription}");

        return string.Join(Environment.NewLine, lines);
    }
}
