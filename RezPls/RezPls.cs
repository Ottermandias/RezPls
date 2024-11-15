using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using RezPls.GUI;
using RezPls.Managers;

namespace RezPls;
// auto-format:off

public partial class RezPls : IDalamudPlugin
{
    public string Name
        => "RezPls";

    public static string Version = "";

    public static    RezPlsConfig Config { get; private set; } = null!;
    public readonly  ActorWatcher ActorWatcher;
    private readonly Overlay      _overlay;
    private readonly Interface    _interface;

    public readonly StatusSet StatusSet;

    public RezPls(IDalamudPluginInterface pluginInterface)
    {
        Dalamud.Initialize(pluginInterface);
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        Config  = RezPlsConfig.Load();

        StatusSet    = new StatusSet();
        ActorWatcher = new ActorWatcher(StatusSet);
        _overlay     = new Overlay(ActorWatcher);
        _interface   = new Interface(this);

        if (Config.Enabled)
            Enable();
        else
            Disable();
        Dalamud.Commands.AddHandler("/rezpls", new CommandInfo(OnRezPls)
        {
            HelpMessage = "Open the configuration window for RezPls.",
            ShowInHelp  = true,
        });
    }

    public void OnRezPls(string _, string arguments)
    {
        _interface!.Visible = !_interface.Visible;
    }

    public void Enable()
    {
        ActorWatcher!.Enable();
        _overlay!.Enable();
    }

    public void Disable()
    {
        ActorWatcher!.Disable();
        _overlay!.Disable();
    }

    public void Dispose()
    {
        Dalamud.Commands.RemoveHandler("/rezpls");
        _interface.Dispose();
        _overlay.Dispose();
        ActorWatcher.Dispose();
    }
}
