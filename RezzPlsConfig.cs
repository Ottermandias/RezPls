using Dalamud.Configuration;

namespace RezzPls
{
    public class RezzPlsConfig : IPluginConfiguration
    {
        public const uint DefaultRaisedColor            = 0x60D2FF00;
        public const uint DefaultCurrentlyRaisingColor  = 0x6000FF00;
        public const uint DefaultDoubleRaiseColor       = 0x600000FF;
        public const uint DefaultInWorldBackgroundColor = 0xC8143C0A;


        public int   Version                { get; set; } = 1;
        public float IconScale              { get; set; } = 1f;
        public uint  RaisedColor            { get; set; } = DefaultRaisedColor;
        public uint  CurrentlyRaisingColor  { get; set; } = DefaultCurrentlyRaisingColor;
        public uint  DoubleRaiseColor       { get; set; } = DefaultDoubleRaiseColor;
        public uint  InWorldBackgroundColor { get; set; } = DefaultInWorldBackgroundColor;
        public bool  Enabled                { get; set; } = true;
        public bool  ShowIcon               { get; set; } = true;
        public bool  ShowInWorldText        { get; set; } = true;
        public bool  ShowCasterNames        { get; set; } = true;
        public bool  ShowAllianceFrame      { get; set; } = true;
        public bool  ShowGroupFrame         { get; set; } = true;
    }
}
