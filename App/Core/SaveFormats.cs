namespace Ps2SaveImporter.Core;

/// <summary>Formatos de save de PS2 suportados (os mesmos aceitos pelo mecanismo de importação).</summary>
internal static class SaveFormats
{
    public static readonly string[] Extensions = { ".max", ".psu", ".cbs", ".sps", ".psv" };

    public static bool IsFolderSave(string path)
    {
        if (!Directory.Exists(path)) return false;
        return File.Exists(Path.Combine(path, "icon.sys"))
            && File.Exists(Path.Combine(path, "game.icn"))
            && Directory.GetFiles(path).Any(p => !string.Equals(Path.GetFileName(p), "icon.sys", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetFileName(p), "game.icn", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSupportedInput(string path) => IsFolderSave(path) || IsSupported(path);

    public const string DialogFilter =
        "Saves PS2 suportados (*.max;*.psu;*.cbs;*.sps;*.psv)|*.max;*.psu;*.cbs;*.sps;*.psv|Todos os arquivos (*.*)|*.*";

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static string Describe(string path)
    {
        if (IsFolderSave(path)) return "Pasta de save do PCSX2";
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".max" => "Action Replay MAX",
            ".psu" => "PSU (EMS)",
            ".cbs" => "CodeBreaker",
            ".sps" => "SharkPort",
            ".psv" => "PSV (PlayStation 3)",
            _ => "Formato desconhecido",
        };
    }
}
