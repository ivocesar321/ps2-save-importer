using System.Diagnostics;
using System.Reflection;

namespace Ps2SaveImporter.Core;

internal readonly record struct MyMcResult(int ExitCode, string Output);

/// <summary>Executa o MyMC embutido no aplicativo, extraindo-o de forma segura para uso temporário.</summary>
internal sealed class MyMcRunner
{
    private const string ResourceName = "Ps2SaveImporter.mymc.exe";

    public MyMcRunner(string? toolPath = null) => ToolPath = toolPath ?? ResolveDefault();

    public string ToolPath { get; }
    public bool Exists => File.Exists(ToolPath);

    private static string ResolveDefault()
    {
        // O recurso embutido permite distribuir somente o PS2SaveImporter.exe.
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream? resource = assembly.GetManifestResourceStream(ResourceName);
            if (resource is not null)
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PS2SaveImporter", "tools");
                Directory.CreateDirectory(directory);
                string destination = Path.Combine(directory, "mymc.exe");
                string temporary = destination + ".new";

                using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    resource.CopyTo(output);

                if (File.Exists(destination)) File.Delete(destination);
                File.Move(temporary, destination);
                return destination;
            }
        }
        catch
        {
            // Se a extração falhar, tenta o executável externo para diagnóstico/compatibilidade.
        }

        string main = Path.Combine(AppContext.BaseDirectory, "mymc.exe");
        if (File.Exists(main)) return main;

        string? dir = Path.GetDirectoryName(Environment.ProcessPath);
        if (dir is not null)
        {
            string beside = Path.Combine(dir, "mymc.exe");
            if (File.Exists(beside)) return beside;
        }
        return main;
    }

    public async Task<MyMcResult> RunAsync(string card, params string[] args)
    {
        var psi = new ProcessStartInfo(ToolPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(card);
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o importador.");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync().ConfigureAwait(false);
        return new MyMcResult(p.ExitCode, await stdout.ConfigureAwait(false) + "\r\n" + await stderr.ConfigureAwait(false));
    }
}

/// <summary>Proteção: não permite alterar Memory Cards enquanto o PCSX2 estiver aberto.</summary>
internal static class Pcsx2Guard
{
    public static bool IsRunning()
    {
        Process[] all;
        try { all = Process.GetProcesses(); }
        catch { return false; }

        bool found = false;
        foreach (var p in all)
        {
            try
            {
                if (p.ProcessName.StartsWith("pcsx2", StringComparison.OrdinalIgnoreCase)) found = true;
            }
            catch
            {
                // Processo encerrou durante a consulta.
            }
            finally
            {
                p.Dispose();
            }
        }
        return found;
    }
}
