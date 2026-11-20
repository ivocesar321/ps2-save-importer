using System.Globalization;
using System.Reflection;

namespace Ps2SaveImporter.Core;

/// <summary>Informações de identidade do aplicativo (nome, autoria, versão).</summary>
internal static class AppInfo
{
    public const string Name = "PS2 Save Importer";
    public const string Author = "Ivo César";
    public const string AuthorUrl = "https://github.com/ivocesar321";
    public const string AuthorHandle = "github.com/ivocesar321";
    public const string Tagline = "Importe saves de PlayStation 2 para os Memory Cards do PCSX2, com backup automático.";
    public const string Disclaimer = "Projeto independente. Não é um produto oficial da Sony nem do PCSX2.";

    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "2.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}

/// <summary>Formatação de valores para exibição.</summary>
internal static class Fmt
{
    public static string Size(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return string.Format(CultureInfo.CurrentCulture, "{0:0.#} KB", kb);
        double mb = kb / 1024.0;
        return string.Format(CultureInfo.CurrentCulture, "{0:0.##} MB", mb);
    }
}
