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
            _configHeader = RezPls.Version.Length > 0 ? $"{PluginName} v{RezPls.Version}###{PluginName}" : PluginName;

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

        private void DrawEnabledRaiseCheckbox()
            => DrawCheckbox("Enable Raise Highlighting",
                "Highlight players being raised.", _config.EnabledRaise, e => _config.EnabledRaise = e);

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

        private void DrawDispelHighlightingCheckbox()
            => DrawCheckbox("Enable Dispel Highlighting",
                "Highlight players with dispellable status effects.",
                _config.EnabledDispel, e => _config.EnabledDispel = e);

        private void DrawRestrictJobsDispelCheckbox()
            => DrawCheckbox("Restrict to dispelling Jobs",
                "Only displays the dispelling information when you are a job with inherent dispel capabilities.\n"
              + "CNJ, WHM, SCH, AST, BRD (at 35+), BLU",
                _config.RestrictedJobsDispel, e => _config.RestrictedJobsDispel = e);

        private void DrawStatusEffectList()
        {
            if (ImGui.RadioButton("Ignored Statuses", !_config.InvertStatusSet))
                ChangeAndSave(false, _config.InvertStatusSet, e => _config.InvertStatusSet = e);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Highlight all dispellable status effects except for those in the list below.");
            ImGui.SameLine();
            if (ImGui.RadioButton("Monitored Statuses", _config.InvertStatusSet))
                ChangeAndSave(true, _config.InvertStatusSet, e => _config.InvertStatusSet = e);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Highlight only those dispellable status effects that are in the list below.");
            if (ImGui.BeginCombo("##StatusListSelector", "Add Status..."))
            {
                for (var i = 0; i < _plugin.StatusSet.RestStatusSet.Count; ++i)
                {
                    var status = _plugin.StatusSet.RestStatusSet[i];
                    if (ImGui.Selectable($"{status.Name}##status{status.RowId}"))
                    {
                        _plugin.StatusSet.Swap((ushort) status.RowId);
                        --i;
                    }
                }

                ImGui.EndCombo();
            }

            if (ImGui.BeginListBox("##StatusList"))
            {
                for (var i = 0; i < _plugin.StatusSet.ListStatusSet.Count; ++i)
                {
                    var status = _plugin.StatusSet.ListStatusSet[i];
                    if (ImGui.Selectable($"{status.Name}##status{status.RowId}"))
                    {
                        _plugin.StatusSet.Swap((ushort) status.RowId);
                        --i;
                    }
                }

                ImGui.EndListBox();
            }

            if (ImGui.Button("Clear list.", ImGui.GetItemRectSize().X * Vector2.UnitX))
                _plugin.StatusSet.ClearList();
        }


        private void DrawColorPicker(string name, string tooltip, uint value, uint defaultValue, Action<uint> setter)
        {
            const ImGuiColorEditFlags flags = ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs;

            var tmp = ImGui.ColorConvertU32ToFloat4(value);
            if (ImGui.ColorEdit4($"##{name}", ref tmp, flags))
                ChangeAndSave(ImGui.ColorConvertFloat4ToU32(tmp), value, setter);
            ImGui.SameLine();
            if (ImGui.Button($"Default##{name}"))
                ChangeAndSave(defaultValue, value, setter);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    $"Reset to default: #{defaultValue & 0xFF:X2}{(defaultValue >> 8) & 0xFF:X2}{(defaultValue >> 16) & 0xFF:X2}{defaultValue >> 24:X2}");
            ImGui.SameLine();
            ImGui.Text(name);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        private void DrawCurrentRaiseColorPicker()
            => DrawColorPicker("Currently Being Raised",
                "The highlight color for a player that is currently being raised by other players or only by yourself.",
                _config.CurrentlyRaisingColor, RezPlsConfig.DefaultCurrentlyRaisingColor, c => _config.CurrentlyRaisingColor = c);

        private void DrawAlreadyRaisedColorPicker()
            => DrawColorPicker("Already Raised",
                "The highlight color for a player that is already raised and not also currently being raised by yourself.",
                _config.RaisedColor, RezPlsConfig.DefaultRaisedColor, c => _config.RaisedColor = c);

        private void DrawDoubleRaiseColorPicker()
            => DrawColorPicker("Unnecessary Raise",
                "The highlight color for a player that you are currently raising if they are already raised or someone else is also raising them.",
                _config.DoubleRaiseColor, RezPlsConfig.DefaultDoubleRaiseColor, c => _config.DoubleRaiseColor = c);

        private void DrawInWorldBackgroundColorPicker()
            => DrawColorPicker("In World Background",
                "The background color for text that is drawn into the world on corpses for raises.",
                _config.InWorldBackgroundColor, RezPlsConfig.DefaultInWorldBackgroundColorRaise, c => _config.InWorldBackgroundColor = c);

        private void DrawInWorldBackgroundColorPickerDispel()
            => DrawColorPicker("In World Background (Dispel)",
                "The background color for text that is drawn into the world on characters that are afflicted by a watched status effect.",
                _config.InWorldBackgroundColorDispel, RezPlsConfig.DefaultInWorldBackgroundColorDispel, c => _config.InWorldBackgroundColorDispel = c);

        private void DrawDispellableColorPicker()
            => DrawColorPicker("Has Dispellable Status",
                "The highlight color for a player that has any watched dispellable status.",
                _config.DispellableColor, RezPlsConfig.DefaultDispellableColor, c => _config.DispellableColor = c);

        private void DrawCurrentlyDispelledColorPicker()
            => DrawColorPicker("Currently Being Dispelled",
                "The highlight color for a player that is currently being dispelled by other players or only by yourself.",
                _config.CurrentlyDispelColor, RezPlsConfig.DefaultCurrentlyDispelColor, c => _config.CurrentlyDispelColor = c);

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

        public void Draw()
        {
            if (!Visible)
                return;

            var buttonHeight      = ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2;
            var horizontalSpacing = new Vector2(0, ImGui.GetTextLineHeightWithSpacing());

            var height = 15 * buttonHeight
              + 6 * horizontalSpacing.Y
              + 27 * ImGui.GetStyle().ItemSpacing.Y;
            var width       = 450 * ImGui.GetIO().FontGlobalScale;
            var constraints = new Vector2(width, height);
            ImGui.SetNextWindowSizeConstraints(constraints, constraints);

            if (!ImGui.Begin(_configHeader, ref Visible, ImGuiWindowFlags.NoResize))
                return;

            try
            {
                DrawEnabledCheckbox();

                if (ImGui.CollapsingHeader("Raise Settings"))
                {
                    DrawEnabledRaiseCheckbox();
                    DrawRestrictJobsCheckbox();
                    ImGui.Dummy(horizontalSpacing);
                }

                if (ImGui.CollapsingHeader("Dispel Settings"))
                {
                    DrawDispelHighlightingCheckbox();
                    DrawRestrictJobsDispelCheckbox();
                    ImGui.Dummy(horizontalSpacing);
                    DrawStatusEffectList();
                    ImGui.Dummy(horizontalSpacing);
                }

                if (ImGui.CollapsingHeader("General Settings"))
                {
                    DrawShowGroupCheckbox();
                    DrawShowAllianceCheckbox();
                    DrawShowCasterNamesCheckbox();
                    DrawRectTypeSelector();
                    ImGui.Dummy(horizontalSpacing);
                    DrawShowInWorldTextCheckbox();
                    DrawShowIconCheckbox();
                    DrawScaleButton();
                    ImGui.Dummy(horizontalSpacing);
                }

                if (ImGui.CollapsingHeader("Colors"))
                {
                    DrawCurrentRaiseColorPicker();
                    DrawAlreadyRaisedColorPicker();
                    DrawDoubleRaiseColorPicker();
                    ImGui.Dummy(horizontalSpacing);
                    DrawInWorldBackgroundColorPicker();
                    DrawInWorldBackgroundColorPickerDispel();
                    ImGui.Dummy(horizontalSpacing);
                    DrawDispellableColorPicker();
                    DrawCurrentlyDispelledColorPicker();
                    ImGui.Dummy(2 * horizontalSpacing);
                }
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
