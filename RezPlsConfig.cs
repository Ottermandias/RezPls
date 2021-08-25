using System.Collections.Generic;
using Dalamud.Configuration;

namespace RezPls
{
    public enum RectType : byte
    {
        Fill = 0,
        OnlyOutline,
        OnlyFullAlphaOutline,
        FillAndFullAlphaOutline,
    }

    public class RezPlsConfig : IPluginConfiguration
    {
        public const uint DefaultRaisedColor                  = 0x60D2FF00;
        public const uint DefaultCurrentlyRaisingColor        = 0x6000FF00;
        public const uint DefaultDoubleRaiseColor             = 0x600000FF;
        public const uint DefaultInWorldBackgroundColorRaise  = 0xC8143C0A;
        public const uint DefaultInWorldBackgroundColorDispel = 0xC8140A3C;
        public const uint DefaultDispellableColor             = 0x60FF00CA;
        public const uint DefaultCurrentlyDispelColor         = 0x60FFFFFF;


        public int      Version           { get; set; } = 1;
        public float    IconScale         { get; set; } = 1f;
        public bool     Enabled           { get; set; } = true;
        public RectType RectType          { get; set; } = RectType.FillAndFullAlphaOutline;
        public bool     ShowCasterNames   { get; set; } = true;
        public bool     ShowAllianceFrame { get; set; } = true;
        public bool     ShowGroupFrame    { get; set; } = true;
        public bool     HideSymbolsOnSelf { get; set; } = false;

        public bool EnabledRaise           { get; set; } = true;
        public bool RestrictedJobs         { get; set; } = false;
        public uint RaisedColor            { get; set; } = DefaultRaisedColor;
        public uint CurrentlyRaisingColor  { get; set; } = DefaultCurrentlyRaisingColor;
        public uint InWorldBackgroundColor { get; set; } = DefaultInWorldBackgroundColorRaise;
        public bool ShowIcon               { get; set; } = true;
        public bool ShowInWorldText        { get; set; } = true;
        public uint DoubleRaiseColor       { get; set; } = DefaultDoubleRaiseColor;


        public bool            EnabledDispel                { get; set; } = true;
        public bool            RestrictedJobsDispel         { get; set; } = false;
        public uint            DispellableColor             { get; set; } = DefaultDispellableColor;
        public uint            CurrentlyDispelColor         { get; set; } = DefaultCurrentlyDispelColor;
        public uint            InWorldBackgroundColorDispel { get; set; } = DefaultInWorldBackgroundColorDispel;
        public bool            ShowIconDispel               { get; set; } = true;
        public bool            ShowInWorldTextDispel        { get; set; } = true;
        public HashSet<ushort> UnmonitoredStatuses          { get; set; } = new();

        public void Save()
            => Dalamud.PluginInterface.SavePluginConfig(this);

        public static RezPlsConfig Load()
        {
            if (Dalamud.PluginInterface.GetPluginConfig() is RezPlsConfig cfg)
                return cfg;

            cfg = new RezPlsConfig();
            cfg.Save();
            return cfg;
        }
    }
}
