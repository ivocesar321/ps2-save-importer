using System.Text;

namespace Ps2SaveImporter.Core;

/// <summary>Resultado da verificação de um arquivo de Memory Card.</summary>
internal sealed record CardValidation(bool IsValid, string Message, long Length = 0, int CapacityMb = 0, bool HasEcc = false, string Badge = "Inválido")
{
    public static CardValidation Fail(string message, string badge = "Inválido") => new(false, message, 0, 0, false, badge);
}

/// <summary>
/// Verifica se um arquivo é uma imagem de Memory Card de PS2 (formato usado pelo PCSX2).
/// Só lê o cabeçalho (somente leitura); nunca altera o arquivo.
/// </summary>
internal static class MemoryCardValidator
{
    private const string Magic = "Sony PS2 Memory Card Format ";
    private const int HeaderSize = 0x154;

    public static CardValidation Validate(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return CardValidation.Fail("Arquivo não encontrado.");

            if (!string.Equals(info.Extension, ".ps2", StringComparison.OrdinalIgnoreCase))
                return CardValidation.Fail("Selecione um arquivo de Memory Card com a extensão .ps2.");

            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                return CardValidation.Fail("O arquivo está marcado como somente leitura.");

            if (info.Length < HeaderSize)
                return CardValidation.Fail("O arquivo é pequeno demais para ser um Memory Card de PS2.");

            var header = new byte[HeaderSize];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                fs.ReadExactly(header, 0, HeaderSize);

            if (header.All(b => b == 0xFF))
                return CardValidation.Fail(
                    "Este Memory Card ainda não foi formatado (arquivo vazio criado pelo PCSX2). Formate-o no PCSX2 antes de importar saves.",
                    "Não formatado");

            if (Encoding.ASCII.GetString(header, 0, Magic.Length) != Magic)
                return CardValidation.Fail("Este arquivo não é um Memory Card de PS2 (assinatura não encontrada).");

            int pageSize = BitConverter.ToUInt16(header, 0x28);
            int pagesPerCluster = BitConverter.ToUInt16(header, 0x2A);
            long clusters = BitConverter.ToUInt32(header, 0x30);
            if (pageSize == 0 || pagesPerCluster == 0 || clusters == 0)
                return CardValidation.Fail("O cabeçalho do Memory Card está incompleto ou danificado.");

            long pages = clusters * pagesPerCluster;
            long spare = (pageSize + 127) / 128 * 4;
            long withEcc = pages * (pageSize + spare);
            long withoutEcc = pages * pageSize;

            bool ecc;
            if (info.Length == withEcc) ecc = true;
            else if (info.Length == withoutEcc) ecc = false;
            else
                return CardValidation.Fail("O tamanho do arquivo não corresponde ao esperado para este Memory Card.");

            int capacityMb = (int)Math.Round(withoutEcc / (1024.0 * 1024.0));
            return new CardValidation(true, "Memory Card válido.", info.Length, capacityMb, ecc);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return CardValidation.Fail("Não foi possível ler o arquivo: " + e.Message);
        }
    }
}
