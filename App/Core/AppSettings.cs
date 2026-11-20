using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ps2SaveImporter.Core;

/// <summary>Preferências do usuário, guardadas em %LOCALAPPDATA%\PS2SaveImporter\settings.json.</summary>
internal sealed class AppSettings
{
    public bool ManualMode { get; set; }
    public List<string> ManualCards { get; set; } = new();
    public string? LastSaveFolder { get; set; }
    public string? LastCardFolder { get; set; }

    /// <summary>Caminho alternativo do arquivo (usado nos testes automatizados).</summary>
    [JsonIgnore]
    public string? FilePathOverride { get; private set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PS2SaveImporter", "settings.json");

    private string TargetPath => FilePathOverride ?? DefaultPath;

    public static AppSettings Load(string? path = null)
    {
        var settings = new AppSettings();
        try
        {
            string file = path ?? DefaultPath;
            if (File.Exists(file))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? new AppSettings();
        }
        catch
        {
            // Preferências corrompidas não podem impedir o uso do programa.
        }
        settings.FilePathOverride = path;
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TargetPath)!);
            File.WriteAllText(TargetPath, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // Não conseguir salvar preferências não é um erro crítico.
        }
    }
}
