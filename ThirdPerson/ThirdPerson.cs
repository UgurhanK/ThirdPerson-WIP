using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Text.Json.Serialization;

namespace ThirdPerson 
{
    public class ThirdPerson : BasePlugin, IPluginConfig<Config>
    {
        public override string ModuleName => "ThirdPerson";
        public override string ModuleVersion => "1.0.2";
        public override string ModuleAuthor => "BoinK & UgurhanK";
        public override string ModuleDescription => "Basic Third Person";

        public Config Config { get; set; } = null!;
        public void OnConfigParsed(Config config) { Config = config; }

        public static Dictionary<CCSPlayerController, CDynamicProp> thirdPersonPool = new Dictionary<CCSPlayerController, CDynamicProp>();

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnTick>(OnGameFrame);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);

            AddCommand("css_tp", "Allows to use thirdperson", OnTPCommand);
            AddCommand("css_thirdperson", "Allows to use thirdperson", OnTPCommand);
        }

        public void OnGameFrame()
        {
            foreach (var player in thirdPersonPool.Keys)
            {
                thirdPersonPool[player].UpdateCamera(player);
            }
        }

        [GameEventHandler]
        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            thirdPersonPool.Clear();
            return HookResult.Continue;
        }

        public void OnTPCommand(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null || !caller.PawnIsAlive) return;

            if (!thirdPersonPool.ContainsKey(caller))
            {
                CDynamicProp? _cameraProp = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");

                if (_cameraProp == null) return;

                _cameraProp.DispatchSpawn();
                _cameraProp.SetColor(Color.FromArgb(0, 255, 255, 255));

                _cameraProp.Teleport(caller.CalculatePositionInFront(-110, 90), caller.PlayerPawn.Value!.V_angle, new Vector());

                caller.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = _cameraProp.EntityHandle.Raw;
                Utilities.SetStateChanged(caller.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");

                caller.PrintToChat(ReplaceColorTags(Config.Prefix + Config.OnActivated));

                thirdPersonPool.Add(caller, _cameraProp);

                AddTimer(0.5f, () =>
                {
                    _cameraProp.Teleport(caller.CalculatePositionInFront(-110, 90), caller.PlayerPawn.Value.V_angle, new Vector());
                });

            } 
            else
            {
                caller!.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
                AddTimer(0.3f, () => Utilities.SetStateChanged(caller.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices"));
                if (thirdPersonPool[caller] != null && thirdPersonPool[caller].IsValid) thirdPersonPool[caller].Remove();
                caller.PrintToChat(ReplaceColorTags(Config.Prefix + Config.OnDeactivated));
                thirdPersonPool.Remove(caller);
            }
        }
        public string ReplaceColorTags(string input)
        {

        string[] colorPatterns =
            {
            "{DEFAULT}", "{DARKRED}", "{LIGHTPURPLE}", "{GREEN}", "{OLIVE}", "{LIME}", "{RED}", "{GREY}",
            "{YELLOW}", "{SILVER}", "{BLUE}", "{DARKBLUE}", "{ORANGE}", "{PURPLE}"
        };
            string[] colorReplacements =
            {
            "\x01", "\x02", "\x03", "\x04", "\x05", "\x06", "\x07", "\x08", "\x09", "\x0A", "\x0B", "\x0C", "\x10", "\x0E"
        };

            for (var i = 0; i < colorPatterns.Length; i++)
                input = input.Replace(colorPatterns[i], colorReplacements[i]);

            return input;
        }
    }

    public class Config : BasePluginConfig
    {
        [JsonPropertyName("OnActivated")] public string OnActivated { get; set; } = "Third Person Activated";
        [JsonPropertyName("OnDeactivated")] public string OnDeactivated { get; set; } = "Third Person Deactivated";
        [JsonPropertyName("Prefix")] public string Prefix { get; set; } = " [ {DARKRED}Third Person {DEFAULT}] ";
    }
}