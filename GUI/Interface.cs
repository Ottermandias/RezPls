using System;
using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;

namespace RezPls.GUI
{
    public class Interface : IDisposable
    {
        public const string PluginName = "RezPls";

        private readonly string                 _configHeader;
        private readonly DalamudPluginInterface _pi;
        private readonly RezPls                 _plugin;
        private readonly RezPlsConfig           _config;

        public bool Visible = false;

        private void ChangeAndSave<T>(T value, T currentValue, Action<T> setter) where T : IEquatable<T>
        {
            if (value.Equals(currentValue))
                return;

            setter(value);
            _plugin.Save();
        }

        public Interface(DalamudPluginInterface pi, RezPls plugin, RezPlsConfig config)
        {
            _pi           = pi;
            _plugin       = plugin;
            _config       = config;
            _configHeader = RezPls.Version.Length > 0 ? $"{PluginName} v{RezPls.Version}" : PluginName;

            _pi.UiBuilder.OnBuildUi      += Draw;
            _pi.UiBuilder.OnOpenConfigUi += Enable;
        }

        private void DrawCheckbox(string name, string tooltip, bool value, Action<bool> setter)
        {
            var tmp = value;
            if (ImGui.Checkbox(name, ref tmp))
                ChangeAndSave(tmp, value, setter);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        private void DrawEnabledCheckbox()
            => DrawCheckbox("Enabled", "Enable or disable the plugin.", _config.Enabled, e =>
            {
                _config.Enabled = e;
                if (e)
                    _plugin.Enable();
                else
                    _plugin.Disable();
            });

        private void DrawShowGroupCheckbox()
            => DrawCheckbox("Highlight in Party Frames",
                "Highlights players already raised or currently being raised in your party frames according to your color selection.",
                _config.ShowGroupFrame,
                e => _config.ShowGroupFrame = e);

        private void DrawShowAllianceCheckbox()
            => DrawCheckbox("Highlight in Alliance Frames",
                "Highlights players already raised or currently being raised in your alliance frames according to your color selection.",
                _config.ShowAllianceFrame,
                e => _config.ShowAllianceFrame = e);

        private void DrawShowCasterNamesCheckbox()
            => DrawCheckbox("Write Caster Names",
                "When highlighting players, also write the name of the resurrector in the frame.", _config.ShowCasterNames,
                e => _config.ShowCasterNames = e);

        private void DrawShowIconCheckbox()
            => DrawCheckbox("Draw In World Icon",
                "Draw a Raised icon on corpses that are already raised or currently being raised.", _config.ShowIcon,
                e => _config.ShowIcon = e);

        private void DrawShowInWorldTextCheckbox()
            => DrawCheckbox("Draw In World Text",
                "Writes the current resurrector under a corpse currently being raised, or that he is already raised.", _config.ShowInWorldText,
                e => _config.ShowInWorldText = e);

        private void DrawRestrictJobsCheckbox()
            => DrawCheckbox("Restrict to resurrecting Jobs",
                "Only display the resurrecting information when you are a job with inherent raise capabilities.\n"
              + "CNJ, WHM, ACN, SCH, SMN, AST, BLU, RDM (at 64+)."
              + "Ignores Lost and Logos Actions.\n", _config.RestrictedJobs,
                e => _config.RestrictedJobs = e);

        private void DrawColorPicker(string name, string tooltip, uint value, Action<uint> setter)
        {
            const ImGuiColorEditFlags flags = ImGuiColorEditFlags.AlphaPreviewHalf;

            var tmp = ImGui.ColorConvertU32ToFloat4(value);
            if (ImGui.ColorEdit4(name, ref tmp, flags))
                ChangeAndSave(ImGui.ColorConvertFloat4ToU32(tmp), value, setter);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        private void DrawCurrentRaiseColorPicker()
            => DrawColorPicker("Currently Being Raised",
                "The highlight color for a player that is currently being raised by other players or only by yourself.",
                _config.CurrentlyRaisingColor, c => _config.CurrentlyRaisingColor = c);

        private void DrawAlreadyRaisedColorPicker()
            => DrawColorPicker("Already Raised",
                "The highlight color for a player that is already raised and not also currently being raised by yourself.",
                _config.RaisedColor, c => _config.RaisedColor = c);

        private void DrawDoubleRaiseColorPicker()
            => DrawColorPicker("Unnecessary Raise",
                "The highlight color for a player that you are currently raising if they are already raised or someone else is also raising them.",
                _config.DoubleRaiseColor, c => _config.DoubleRaiseColor = c);

        private void DrawInWorldBackgroundColorPicker()
            => DrawColorPicker("In World Background",
                "The background color for text that is drawn into the world on corpses.",
                _config.InWorldBackgroundColor, c => _config.InWorldBackgroundColor = c);

        private void DrawScaleButton()
        {
            const float min  = 0.1f;
            const float max  = 3.0f;
            const float step = 0.005f;

            var tmp = _config.IconScale;
            if (ImGui.DragFloat("In World Icon Scale", ref tmp, step, min, max))
                ChangeAndSave(tmp, _config.IconScale, f => _config.IconScale = Math.Max(min, Math.Min(f, max)));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Set the scale of the Raised icon that is drawn into the world on raised corpses.");
        }

        private static readonly string[] RectTypeStrings = new[]
        {
            "Fill",
            "Only Outline",
            "Only Full Alpha Outline",
            "Fill and Full Alpha Outline",
        };

        private void DrawRectTypeSelector()
        {
            var type = (int) _config.RectType;
            if (!ImGui.Combo("Rectangle Type", ref type, RectTypeStrings, RectTypeStrings.Length))
                return;

            ChangeAndSave(type, (int) _config.RectType, t => _config.RectType = (RectType) t);
        }

        private void DrawResetColorsButton()
        {
            if (!ImGui.Button("Reset Colors to Default", new Vector2(-1, 0)))
                return;

            _config.CurrentlyRaisingColor  = RezPlsConfig.DefaultCurrentlyRaisingColor;
            _config.RaisedColor            = RezPlsConfig.DefaultRaisedColor;
            _config.DoubleRaiseColor       = RezPlsConfig.DefaultDoubleRaiseColor;
            _config.InWorldBackgroundColor = RezPlsConfig.DefaultInWorldBackgroundColor;
            _plugin.Save();
        }

        public void Draw()
        {
            if (!Visible)
                return;

            var buttonHeight      = ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2;
            var horizontalSpacing = new Vector2(0, ImGui.GetTextLineHeightWithSpacing());

            var height = 15 * buttonHeight
              + 6 * horizontalSpacing.Y
              + 22 * ImGui.GetStyle().ItemSpacing.Y;
            var width       = 450 * ImGui.GetIO().FontGlobalScale;
            var constraints = new Vector2(width, height);
            ImGui.SetNextWindowSizeConstraints(constraints, constraints);

            if (!ImGui.Begin(_configHeader, ref Visible, ImGuiWindowFlags.NoResize))
                return;

            try
            {
                DrawEnabledCheckbox();
                DrawRestrictJobsCheckbox();
                ImGui.Dummy(horizontalSpacing);
                DrawShowGroupCheckbox();
                DrawShowAllianceCheckbox();
                DrawShowCasterNamesCheckbox();
                DrawRectTypeSelector();
                ImGui.Dummy(horizontalSpacing);
                DrawCurrentRaiseColorPicker();
                DrawAlreadyRaisedColorPicker();
                DrawDoubleRaiseColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawShowInWorldTextCheckbox();
                DrawShowIconCheckbox();
                ImGui.Dummy(horizontalSpacing);
                DrawInWorldBackgroundColorPicker();
                DrawScaleButton();

                ImGui.Dummy(2 * horizontalSpacing);
                DrawResetColorsButton();
            }
            finally
            {
                ImGui.End();
            }
        }

        public void Enable(object _1, object _2)
            => Visible = true;

        public void Dispose()
        {
            _pi.UiBuilder.OnBuildUi      -= Draw;
            _pi.UiBuilder.OnOpenConfigUi -= Enable;
        }
    }
}
