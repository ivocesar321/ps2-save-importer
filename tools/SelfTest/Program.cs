using SelfTest;

internal static class Program
{
    // Uso:
    //   Ps2SaveImporter.SelfTest logic --mymc <mymc.exe> --work <pasta>
    //   Ps2SaveImporter.SelfTest ui    --mymc <mymc.exe> --work <pasta> --out <pasta-de-imagens>
    [STAThread]
    private static int Main(string[] args)
    {
        string Arg(string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        // Sem contexto de sincronização do WinForms: o teste bloqueia a thread principal aguardando tarefas assíncronas.
        WindowsFormsSynchronizationContext.AutoInstall = false;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        string mode = args.Length > 0 ? args[0] : "logic";
        string mymc = Arg("--mymc", "mymc.exe");
        string work = Arg("--work", Path.Combine(Path.GetTempPath(), "ps2si-selftest"));
        string outDir = Arg("--out", Path.Combine(work, "snapshots"));

        return mode switch
        {
            "logic" => LogicTests.Run(mymc, work).GetAwaiter().GetResult(),
            "ui" => UiSnapshots.Run(mymc, work, outDir),
            _ => 2,
        };
    }
}
