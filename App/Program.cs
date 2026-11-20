using Ps2SaveImporter.UI;
using Ps2SaveImporter.Views;

namespace Ps2SaveImporter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // DPI por monitor: cada janela se ajusta ao monitor em que está.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) =>
        {
            try
            {
                ModernDialog.Show(null, new DialogSpec(
                    "Algo inesperado aconteceu",
                    e.Exception.Message,
                    DialogTone.Danger, IconKind.Alert, "Fechar"));
            }
            catch
            {
                MessageBox.Show(e.Exception.Message, "PS2 Save Importer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        Application.Run(new MainForm());
    }
}
