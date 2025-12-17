using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using RezPls.Managers;

namespace RezPls.GUI;

public class Interface : IDisposable
{
    public const string PluginName = "RezPls";

    private readonly string _configHeader;
    private readonly RezPls _plugin;

    private          string          _statusFilter = string.Empty;
    private readonly HashSet<string> _seenNames;

    public bool Visible;

    public bool TestMode = false;

    private static void ChangeAndSave<T>(T value, T currentValue, Action<T> setter) where T : IEquatable<T>
    {
        if (value.Equals(currentValue))
            return;

        setter(value);
        RezPls.Config.Save();
    }

    public Interface(RezPls plugin)
    {
        _plugin       = plugin;
        _configHeader = RezPls.Version.Length > 0 ? $"{PluginName} v{RezPls.Version}###{PluginName}" : PluginName;
        _seenNames    = new HashSet<string>(_plugin.StatusSet.DisabledStatusSet.Count + _plugin.StatusSet.EnabledStatusSet.Count);

        Dalamud.PluginInterface.UiBuilder.Draw         += Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi += Enable;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi   += Enable;
    }

    private static void DrawCheckbox(string name, string tooltip, bool value, Action<bool> setter)
    {
        var tmp = value;
        if (ImGui.Checkbox(name, ref tmp))
            ChangeAndSave(tmp, value, setter);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private void DrawEnabledCheckbox()
        => DrawCheckbox("Enabled", "Enable or disable the plugin.", RezPls.Config.Enabled, e =>
        {
            RezPls.Config.Enabled = e;
            if (e)
                _plugin.Enable();
            else
                _plugin.Disable();
        });

    private void DrawHideSymbolsOnSelfCheckbox()
        => DrawCheckbox("Hide Symbols on Self", "Hide the symbol and/or text drawn into the world on the player character.",
            RezPls.Config.HideSymbolsOnSelf,    e => RezPls.Config.HideSymbolsOnSelf = e);

    private void DrawShowCastProgressCheckbox()
        => DrawCheckbox("Show Cast Progress", "Show the progress of raise casts compared to the total cast time. This only works with the filled cast box styles. ",
            RezPls.Config.ShowCastProgress,   e => RezPls.Config.ShowCastProgress = e);

    private void DrawEnabledRaiseCheckbox()
        => DrawCheckbox("Enable Raise Highlighting",
            "Highlight players being raised.", RezPls.Config.EnabledRaise, e => RezPls.Config.EnabledRaise = e);

    private void DrawShowGroupCheckbox()
        => DrawCheckbox("Highlight in Party Frames",
            "Highlights players in your party frames according to your color and state selection.",
            RezPls.Config.ShowGroupFrame,
            e => RezPls.Config.ShowGroupFrame = e);

    private void DrawShowAllianceCheckbox()
        => DrawCheckbox("Highlight in Alliance Frames",
            "Highlights players in your alliance frames according to your color and state selection.",
            RezPls.Config.ShowAllianceFrame,
            e => RezPls.Config.ShowAllianceFrame = e);

    private void DrawShowCasterNamesCheckbox()
        => DrawCheckbox("Write Caster Names",
            "When highlighting players, also write the name of a caster resurrecting or cleansing them in the frame.",
            RezPls.Config.ShowCasterNames,
            e => RezPls.Config.ShowCasterNames = e);

    private void DrawShowIconCheckbox()
        => DrawCheckbox("Draw In World Icon",
            "Draw a Raised icon on corpses that are already raised or currently being raised.", RezPls.Config.ShowIcon,
            e => RezPls.Config.ShowIcon = e);

    private void DrawShowIconDispelCheckbox()
        => DrawCheckbox("Draw In World Icon##Dispel",
            "Draw a debuff icon on players that have a removable detrimental status effect.", RezPls.Config.ShowIconDispel,
            e => RezPls.Config.ShowIconDispel = e);

    private void DrawShowInWorldTextCheckbox()
        => DrawCheckbox("Draw In World Text",
            "Writes the current resurrector under a corpse currently being raised, or that he is already raised.",
            RezPls.Config.ShowInWorldText,
            e => RezPls.Config.ShowInWorldText = e);

    private void DrawShowInWorldTextDispelCheckbox()
        => DrawCheckbox("Draw In World Text##Dispel",
            "Writes the current caster under an afflicted player currently being cleansed, or that he has a removable detrimental status effect.",
            RezPls.Config.ShowInWorldTextDispel,
            e => RezPls.Config.ShowInWorldTextDispel = e);

    private void DrawRestrictJobsCheckbox()
        => DrawCheckbox("Restrict to resurrecting Jobs",
            "Only display the resurrecting information when you are a job with inherent raise capabilities.\n"
          + "CNJ, WHM, ACN, SCH, SMN, AST, BLU, RDM (at 64+)."
          + "Ignores Lost and Logos Actions.\n", RezPls.Config.RestrictedJobs,
            e => RezPls.Config.RestrictedJobs = e);

    private void DrawDispelHighlightingCheckbox()
        => DrawCheckbox("Enable Cleanse Highlighting",
            "Highlight players with removable detrimental status effects.",
            RezPls.Config.EnabledDispel, e => RezPls.Config.EnabledDispel = e);

    private void DrawRestrictJobsDispelCheckbox()
        => DrawCheckbox("Restrict to cleansing Jobs",
            "Only displays the cleansing information when you are a job with inherent cleanse capabilities.\n"
          + "CNJ, WHM, SCH, AST, BRD (at 35+), BLU",
            RezPls.Config.RestrictedJobsDispel, e => RezPls.Config.RestrictedJobsDispel = e);

    private void DrawTestModeCheckBox1()
        => DrawCheckbox("Test Player Raised", "Should show the active \"Already Raised\" effects on the player character and party frames.",
            ActorWatcher.TestMode == 1,       e => ActorWatcher.TestMode = e ? 1 : 0);

    private void DrawTestModeCheckBox2()
        => DrawCheckbox("Test Player Being Raised by Target",
            "Should show the active \"Currently Being Raised\" effects on the player character and party frames, as if the caster is its current target.",
            ActorWatcher.TestMode == 2, e => ActorWatcher.TestMode = e ? 2 : 0);

    private void DrawTestModeCheckBox3()
        => DrawCheckbox("Test Player Unnecessary Raise",
            "Should show the active \"Unnecessary Raise\" effects on the player character, as if the player character and its current target raise it.",
            ActorWatcher.TestMode == 3, e => ActorWatcher.TestMode = e ? 3 : 0);

    private void DrawTestModeCheckBox4()
        => DrawCheckbox("Test Player Negative Status Effect",
            "Should show the active \"Has Monitored Status Effect\" effects on the player character, as if the player character has a monitored condition.",
            ActorWatcher.TestMode == 4, e => ActorWatcher.TestMode = e ? 4 : 0);

    private void DrawTestModeCheckBox5()
        => DrawCheckbox("Test Player Negative Status Effect Being Cleansed",
            "Should show the active \"Currently Being Cleaned\" effects on the player character, as if it is being cleansed by its current target.",
            ActorWatcher.TestMode == 5, e => ActorWatcher.TestMode = e ? 5 : 0);

    private void DrawTestModeCheckBox6()
        => DrawCheckbox("Test Player Unnecessary Cleanse",
            "Should show the active \"Unnecessary Cleanse\" effects on the player character, as if it uses a double cleanse or a cleanse with no monitored status.",
            ActorWatcher.TestMode == 6, e => ActorWatcher.TestMode = e ? 6 : 0);


    private void DrawSingleStatusEffectList(string header, bool which, float width)
    {
        using var group = ImRaii.Group();
        var       list  = which ? _plugin.StatusSet.DisabledStatusSet : _plugin.StatusSet.EnabledStatusSet;
        _seenNames.Clear();
        if (ImGui.BeginListBox($"##{header}box", width / 2 * Vector2.UnitX))
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var (status, name) = list[i];
                if (!name.Contains(_statusFilter) || _seenNames.Contains(name))
                    continue;

                _seenNames.Add(name);
                if (ImGui.Selectable($"{status.Name}##status{status.RowId}"))
                {
                    _plugin.StatusSet.Swap((ushort)status.RowId);
                    --i;
                }
            }

            ImGui.EndListBox();
        }

        if (which)
        {
            if (ImGui.Button("Disable All Statuses", width / 2 * Vector2.UnitX))
                _plugin.StatusSet.ClearEnabledList();
        }
        else if (ImGui.Button("Enable All Statuses", width / 2 * Vector2.UnitX))
        {
            _plugin.StatusSet.ClearDisabledList();
        }
    }

    private static void DrawStatusSelectorTitles(float width)
    {
        const string disabledHeader = "Disabled Statuses";
        const string enabledHeader  = "Monitored Statuses";
        var          pos1           = width / 4 - ImGui.CalcTextSize(disabledHeader).X / 2;
        var          pos2           = 3 * width / 4 + ImGui.GetStyle().ItemSpacing.X - ImGui.CalcTextSize(enabledHeader).X / 2;
        ImGui.SetCursorPosX(pos1);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(disabledHeader);
        ImGui.SameLine(pos2);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(enabledHeader);
    }

    private void DrawStatusEffectList()
    {
        var width = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.GetStyle().ItemSpacing.X;
        DrawStatusSelectorTitles(width);
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##statusFilter", "Filter...", ref _statusFilter, 64);
        DrawSingleStatusEffectList("Disabled Statuses", true, width);
        ImGui.SameLine();
        DrawSingleStatusEffectList("Monitored Statuses", false, width);
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
            RezPls.Config.CurrentlyRaisingColor, RezPlsConfig.DefaultCurrentlyRaisingColor, c => RezPls.Config.CurrentlyRaisingColor = c);


    private void DrawAlreadyRaisedColorPicker()
        => DrawColorPicker("Already Raised",
            "The highlight color for a player that is already raised and not also currently being raised by yourself.",
            RezPls.Config.RaisedColor, RezPlsConfig.DefaultRaisedColor, c => RezPls.Config.RaisedColor = c);

    private void DrawDoubleRaiseColorPicker()
        => DrawColorPicker("Redundant Cast",
            "The highlight color for a player that you are currently raising if they are already raised or someone else is also raising them,\n"
          + "if you and another player cleanse them, or if you cleanse someone without monitored detrimental status effects.",
            RezPls.Config.DoubleRaiseColor, RezPlsConfig.DefaultDoubleRaiseColor, c => RezPls.Config.DoubleRaiseColor = c);

    private void DrawInWorldBackgroundColorPicker()
        => DrawColorPicker("In World Background",
            "The background color for text that is drawn into the world on corpses for raises.",
            RezPls.Config.InWorldBackgroundColor, RezPlsConfig.DefaultInWorldBackgroundColorRaise,
            c => RezPls.Config.InWorldBackgroundColor = c);

    private void DrawInWorldBackgroundColorPickerDispel()
        => DrawColorPicker("In World Background (Cleanse)",
            "The background color for text that is drawn into the world on characters that are afflicted by a watched detrimental status effect.",
            RezPls.Config.InWorldBackgroundColorDispel, RezPlsConfig.DefaultInWorldBackgroundColorDispel,
            c => RezPls.Config.InWorldBackgroundColorDispel = c);

    private void DrawDispellableColorPicker()
        => DrawColorPicker("Has Monitored Status Effect",
            "The highlight color for a player that has any monitored detrimental status effect.",
            RezPls.Config.DispellableColor, RezPlsConfig.DefaultDispellableColor, c => RezPls.Config.DispellableColor = c);

    private void DrawCurrentlyDispelledColorPicker()
        => DrawColorPicker("Currently Being Cleansed",
            "The highlight color for a player that is currently being cleansed by other players or only by yourself.",
            RezPls.Config.CurrentlyDispelColor, RezPlsConfig.DefaultCurrentlyDispelColor, c => RezPls.Config.CurrentlyDispelColor = c);

    private void DrawScaleButton()
    {
        const float min  = 0.1f;
        const float max  = 3.0f;
        const float step = 0.005f;

        var tmp = RezPls.Config.IconScale;
        if (ImGui.DragFloat("In World Icon Scale", ref tmp, step, min, max))
            ChangeAndSave(tmp, RezPls.Config.IconScale, f => RezPls.Config.IconScale = Math.Max(min, Math.Min(f, max)));
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
        var type = (int)RezPls.Config.RectType;
        if (!ImGui.Combo("Rectangle Type", ref type, RectTypeStrings, RectTypeStrings.Length))
            return;

        ChangeAndSave(type, (int)RezPls.Config.RectType, t => RezPls.Config.RectType = (RectType)t);
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
                DrawShowIconCheckbox();
                DrawShowInWorldTextCheckbox();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("Cleanse Settings"))
            {
                DrawDispelHighlightingCheckbox();
                DrawRestrictJobsDispelCheckbox();
                DrawShowIconDispelCheckbox();
                DrawShowInWorldTextDispelCheckbox();
                ImGui.Dummy(horizontalSpacing);
                DrawStatusEffectList();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("General Settings"))
            {
                DrawShowCastProgressCheckbox();
                DrawHideSymbolsOnSelfCheckbox();
                DrawShowGroupCheckbox();
                DrawShowAllianceCheckbox();
                DrawShowCasterNamesCheckbox();
                DrawRectTypeSelector();
                DrawScaleButton();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("Colors"))
            {
                DrawCurrentRaiseColorPicker();
                DrawAlreadyRaisedColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawDispellableColorPicker();
                DrawCurrentlyDispelledColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawDoubleRaiseColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawInWorldBackgroundColorPicker();
                DrawInWorldBackgroundColorPickerDispel();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("Testing"))
            {
                DrawTestModeCheckBox1();
                DrawTestModeCheckBox2();
                DrawTestModeCheckBox3();
                DrawTestModeCheckBox4();
                DrawTestModeCheckBox5();
                DrawTestModeCheckBox6();
            }

            DrawDebug();
        }
        finally
        {
            ImGui.End();
        }
    }

    [Conditional("DEBUG")]
    private void DrawDebug()
    {
        if (!ImGui.CollapsingHeader("Debug"))
            return;

        ImGui.TextUnformatted($"In PVP: {Dalamud.ClientState.IsPvP}");
        ImGui.TextUnformatted($"Test Mode: {ActorWatcher.TestMode}");
        using (var tree = ImRaii.TreeNode("Names"))
        {
            if (tree)
                foreach (var (id, name) in _plugin.ActorWatcher.ActorNames)
                    ImRaii.TreeNode($"{name} ({id})", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var tree = ImRaii.TreeNode("Casts"))
        {
            if (tree)
                foreach (var (id, state) in _plugin.ActorWatcher.RezList)
                {
                    ImRaii.TreeNode($"{id}: {state.Type} by {state.Caster}, {(state.HasStatus ? "Has Status" : string.Empty)}",
                        ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                }
        }

        ImGui.Checkbox("Use Debug Casts"u8, ref RezPls.GlobalDebug);
    }

    public void Enable()
        => Visible = true;

    public void Dispose()
    {
        Dalamud.PluginInterface.UiBuilder.Draw         -= Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= Enable;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi   -= Enable;
    }
}
