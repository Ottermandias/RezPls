using System.Reflection;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using RezPls.GUI;
using RezPls.Managers;

namespace RezPls
{
    public class RezPls : IDalamudPlugin
    {
        public string Name
            => "RezPls";

        public static string Version = "";

        public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        public static CommandManager         CommandManager  { get; private set; } = null!;
        public static DataManager            GameData        { get; private set; } = null!;
        public static SigScanner             Scanner         { get; private set; } = null!;
        public static GameGui                GameGui         { get; private set; } = null!;
        public static Framework              Framework       { get; private set; } = null!;
        public static RezPlsConfig           Config          { get; private set; } = null!;
        public static ClientState            ClientState     { get; private set; } = null!;
        public static ObjectTable            Objects         { get; private set; } = null!;

        private readonly ActorWatcher _actorWatcher;
        private readonly Overlay      _overlay;
        private readonly Interface    _interface;

        public StatusSet StatusSet;

        public RezPls(DalamudPluginInterface pluginInterface, CommandManager commandManager, DataManager gameData, SigScanner sigScanner
            , GameGui gameGui, Framework framework, ClientState clientState, ObjectTable objects)
        {
            PluginInterface = pluginInterface;
            CommandManager  = commandManager;
            GameData        = gameData;
            Scanner         = sigScanner;
            GameGui         = gameGui;
            Framework       = framework;
            ClientState     = clientState;
            Objects         = objects;

            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
            Config  = RezPlsConfig.Load();

            StatusSet     = new StatusSet();
            _actorWatcher = new ActorWatcher(StatusSet);
            _overlay      = new Overlay(_actorWatcher);
            _interface    = new Interface(this);

            if (Config.Enabled)
                Enable();
            else
                Disable();

            CommandManager.AddHandler("/rezpls", new CommandInfo(OnRezPls)
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
            _actorWatcher!.Enable();
            _overlay!.Enable();
        }

        public void Disable()
        {
            _actorWatcher!.Disable();
            _overlay!.Disable();
        }

        public void Dispose()
        {
            CommandManager.RemoveHandler("/rezpls");
            _interface.Dispose();
            _overlay.Dispose();
            _actorWatcher.Dispose();
        }
    }
}
