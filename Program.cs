using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace EffectorShaderRepro;

// ── Minimal Effector runtime-shader effect ─────────────────────────
// A trivial SkSL shader that just tints everything red.
// Any runtime shader triggers the crash; the shader source is irrelevant.

using Effector;
using SkiaSharp;

[SkiaEffect(typeof(RedTintShaderFactory))]
public sealed class RedTintShader : SkiaEffectBase
{
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<RedTintShader, double>(nameof(Intensity), 0.5d);

    static RedTintShader()
    {
        AffectsRender<RedTintShader>(IntensityProperty);
    }

    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }
}

public sealed class RedTintShaderFactory :
    ISkiaEffectFactory<RedTintShader>,
    ISkiaShaderEffectFactory<RedTintShader>,
    ISkiaEffectValueFactory,
    ISkiaShaderEffectValueFactory
{
    private const string ShaderSource = """
        uniform float width;
        uniform float height;
        uniform float intensity;

        half4 main(float2 coord) {
            return half4(intensity, 0.0, 0.0, intensity);
        }
        """;

    public Thickness GetPadding(RedTintShader effect) => default;
    public Thickness GetPadding(object[] values) => default;
    public SKImageFilter? CreateFilter(RedTintShader effect, SkiaEffectContext context) => null;
    public SKImageFilter? CreateFilter(object[] values, SkiaEffectContext context) => null;

    public SkiaShaderEffect CreateShaderEffect(RedTintShader effect, SkiaShaderEffectContext context)
        => CreateShaderEffect(new object[] { effect.Intensity }, context);

    public SkiaShaderEffect CreateShaderEffect(object[] values, SkiaShaderEffectContext context)
    {
        var intensity = (float)Math.Clamp((double)values[0], 0d, 1d);
        return SkiaRuntimeShaderBuilder.Create(
            ShaderSource,
            context,
            uniforms =>
            {
                uniforms.Add("width", context.EffectBounds.Width);
                uniforms.Add("height", context.EffectBounds.Height);
                uniforms.Add("intensity", intensity);
            },
            blendMode: SKBlendMode.SrcOver);
    }
}

// ── Application ────────────────────────────────────────────────────

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var panel = new Panel
            {
                Width = 400,
                Height = 300,
                Background = Brushes.CornflowerBlue
            };

            desktop.MainWindow = new Window
            {
                Title = "Effector Shader SIGSEGV Repro",
                Width = 600,
                Height = 400,
                Content = panel
            };

            // Apply the runtime shader effect after 2 seconds
            // On Linux/NVIDIA this causes SIGSEGV (exit 139)
            DispatcherTimer.RunOnce(() =>
            {
                Console.Error.WriteLine("[REPRO] Applying RedTintShader...");
                panel.Effect = new RedTintShader { Intensity = 0.7 };
                Console.Error.WriteLine("[REPRO] Effect applied. If no crash, shader rendering works on this system.");
            }, TimeSpan.FromSeconds(2));

            // If we survive 10 seconds, the test passed
            DispatcherTimer.RunOnce(() =>
            {
                Console.Error.WriteLine("[REPRO] SUCCESS: No crash after 10 seconds. Shader rendering works.");
            }, TimeSpan.FromSeconds(10));
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[UNHANDLED] {e.ExceptionObject}");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[TOPLEVEL] {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
