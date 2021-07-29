using System.Reflection;
using Dalamud.Game.Command;
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

        private DalamudPluginInterface _pluginInterface = null!;
        private ActorWatcher           _actorWatcher    = null!;
        private Overlay                _overlay         = null!;
        private Interface              _interface       = null!;
        private RezPlsConfig           _config          = null!;
        public  StatusSet              StatusSet        = null!;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            Version          = Assembly.GetExecutingAssembly()?.GetName().Version.ToString() ?? "";
            if (_pluginInterface.GetPluginConfig() is RezPlsConfig config)
            {
                _config = config;
            }
            else
            {
                _config = new RezPlsConfig();
                Save();
            }

            StatusSet     = new StatusSet(_pluginInterface, _config);
            _actorWatcher = new ActorWatcher(_pluginInterface, StatusSet);
            _overlay      = new Overlay(_pluginInterface, _actorWatcher, _config);
            _interface    = new Interface(_pluginInterface, this, _config);

            if (_config.Enabled)
                Enable();

            _pluginInterface.CommandManager.AddHandler("/rezpls", new CommandInfo(OnRezPls)
            {
                HelpMessage = "Open the configuration window for RezPls.",
                ShowInHelp  = true,
            });
        }

        public void OnRezPls(string _, string arguments)
        {
            _interface!.Visible = !_interface.Visible;
        }

        public void Save()
            => _pluginInterface!.SavePluginConfig(_config);

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
            _pluginInterface!.CommandManager.RemoveHandler("/rezpls");
            _interface?.Dispose();
            _overlay?.Dispose();
            _actorWatcher?.Dispose();
            _pluginInterface?.Dispose();
        }
    }
}
