using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Ps2SaveImporter.UI;

/// <summary>Interpolação suave de um valor 0..1 (hover, pressionado, deslizamento).</summary>
internal sealed class Animator : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Action _onTick;

    public Animator(Action onTick, float initial = 0f)
    {
        _onTick = onTick;
        Value = Target = initial;
        _timer.Tick += (_, _) => Step();
    }

    public float Value { get; private set; }
    public float Target { get; private set; }
    public float Speed { get; set; } = 0.22f;

    public void To(float target)
    {
        Target = target;
        // Respeita a configuração do Windows para desativar animações.
        if (!SystemInformation.UIEffectsEnabled)
        {
            Jump(target);
            return;
        }
        if (Math.Abs(Target - Value) > 0.001f && !_timer.Enabled) _timer.Start();
    }

    public void Jump(float value)
    {
        Value = Target = value;
        _timer.Stop();
        _onTick();
    }

    private void Step()
    {
        float d = Target - Value;
        if (Math.Abs(d) < 0.01f)
        {
            Value = Target;
            _timer.Stop();
        }
        else
        {
            Value += d * Speed;
        }
        _onTick();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

/// <summary>Base dos controles desenhados à mão: sem flicker, fundo opaco, escala por DPI.</summary>
internal abstract class ModernControl : Control
{
    protected ModernControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
    }

    protected int Dp(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Todo o desenho é feito em OnPaint.
    }

    protected static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }
}

/// <summary>Chamadas nativas opcionais (barra de título escura, rolagem escura). Falhas são ignoradas.</summary>
internal static class Native
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    public static void ApplyDarkTitleBar(IntPtr hwnd, Color caption, Color text, Color border)
    {
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref on, 4);
            int c = ToColorRef(caption);
            DwmSetWindowAttribute(hwnd, 35, ref c, 4);
            int t = ToColorRef(text);
            DwmSetWindowAttribute(hwnd, 36, ref t, 4);
            int b = ToColorRef(border);
            DwmSetWindowAttribute(hwnd, 34, ref b, 4);
        }
        catch
        {
            // Versões antigas do Windows 10 podem não suportar todos os atributos.
        }
    }

    public static void ApplyDarkScrollbars(IntPtr hwnd)
    {
        try { SetWindowTheme(hwnd, "DarkMode_Explorer", null); }
        catch { /* opcional */ }
    }

    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
