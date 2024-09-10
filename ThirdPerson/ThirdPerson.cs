using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using VectorSystem = System.Numerics;
using System.Text.Json.Serialization;
using System.Diagnostics.SymbolStore;

namespace ThirdPerson
{
    public class ThirdPerson : BasePlugin, IPluginConfig<Config>
    {
        public override string ModuleName => "ThirdPerson";
        public override string ModuleVersion => "0.0.4";
        public override string ModuleAuthor => "BoinK & UgurhanK";
        public override string ModuleDescription => "Basic Third Person";

        public Config Config { get; set; } = null!;
        public static Config cfg = null!;
        public void OnConfigParsed(Config config) { Config = config; cfg = config; }

        public static Dictionary<CCSPlayerController, CDynamicProp> thirdPersonPool = new Dictionary<CCSPlayerController, CDynamicProp>();
        public static Dictionary<CCSPlayerController, CPhysicsPropMultiplayer> smoothThirdPersonPool = new Dictionary<CCSPlayerController, CPhysicsPropMultiplayer>();

        public static Dictionary<CCSPlayerController, WeaponList> weapons = new Dictionary<CCSPlayerController, WeaponList>();

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnTick>(OnGameFrame);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);

            AddCommand("css_tp", "Allows to use thirdperson", OnTPCommand);
            AddCommand("css_thirdperson", "Allows to use thirdperson", OnTPCommand);
        }

        public void OnGameFrame()
        {
            foreach (var player in thirdPersonPool.Keys)
            {
                thirdPersonPool[player].UpdateCamera(player);
            }

            foreach (var player in smoothThirdPersonPool.Keys)
            {
                smoothThirdPersonPool[player].UpdateCameraSmooth(player);
            }
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            thirdPersonPool.Clear();
            smoothThirdPersonPool.Clear();
            return HookResult.Continue;
        }

        private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            //Victim  
            var victim = @event.Userid;

            //Attacker
            var attacker = @event.Attacker;

            if (attacker == null || victim == null) return HookResult.Continue;

            if (thirdPersonPool.ContainsKey(attacker) || smoothThirdPersonPool.ContainsKey(attacker))
            {
                var isInfront = attacker.IsInfrontOfPlayer(victim);
                if (isInfront)
                {
                    victim.PlayerPawn.Value!.Health += @event.DmgHealth;
                    victim.PlayerPawn.Value!.ArmorValue += @event.DmgArmor;
                }
            }

            return HookResult.Continue;
        }

        public void OnTPCommand(CCSPlayerController? caller, CommandInfo command)
        {
            if (Config.UseOnlyAdmin && !AdminManager.PlayerHasPermissions(caller, Config.Flag))
            {
                command.ReplyToCommand(ReplaceColorTags(Config.NoPermission));
                return;
            }

            if (caller == null || !caller.PawnIsAlive) return;

            if (Config.UseSmooth)
            {
                SmoothThirdPerson(caller);
            }
            else
            {
                DefaultThirdPerson(caller);
            }
        }

        public void DefaultThirdPerson(CCSPlayerController caller)
        {
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

                if (Config.StripOnUse)
                {
                    caller.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = true;

                    if (weapons.ContainsKey(caller)) weapons.Remove(caller);

                    var WeaponList = new WeaponList();

                    foreach (var weapon in caller.PlayerPawn.Value!.WeaponServices!.MyWeapons)
                    {
                        if (weapons.ContainsKey(caller) && weapons[caller].weapons.Contains(weapon.Value!.DesignerName)) continue;
                        WeaponList.weapons.Add(weapon.Value!.DesignerName!);
                    }

                    weapons.Add(caller, WeaponList);
                    caller.RemoveWeapons();
                }
            }
            else
            {
                caller!.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
                AddTimer(0.3f, () => Utilities.SetStateChanged(caller.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices"));
                if (thirdPersonPool[caller] != null && thirdPersonPool[caller].IsValid) thirdPersonPool[caller].Remove();
                caller.PrintToChat(ReplaceColorTags(Config.Prefix + Config.OnDeactivated));
                thirdPersonPool.Remove(caller);

                caller.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = false;

                if (Config.StripOnUse)
                {
                    foreach (var weapon in weapons[caller].weapons)
                    {
                        caller.GiveNamedItem(weapon);
                    }
                }

            }
        }

        public void SmoothThirdPerson(CCSPlayerController caller)
        {
            if (!smoothThirdPersonPool.ContainsKey(caller))
            {

                var _cameraProp = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");

                if (_cameraProp == null || !_cameraProp.IsValid) return;

                _cameraProp.DispatchSpawn();

                _cameraProp.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
                _cameraProp.Collision.SolidFlags = 12;
                _cameraProp.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;

                _cameraProp.SetColor(Color.FromArgb(0, 255, 255, 255));

                //Changes players view to camera prop- ViewEntity Raw value can be set to uint.MaxValue to change back to normal player cam
                caller.PlayerPawn.Value!.CameraServices!.ViewEntity.Raw = _cameraProp.EntityHandle.Raw;
                Utilities.SetStateChanged(caller.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");

                _cameraProp.Teleport(caller.CalculatePositionInFront(-110, 90), caller.PlayerPawn.Value.V_angle, new Vector());

                smoothThirdPersonPool.Add(caller, _cameraProp);

                caller.PrintToChat(ReplaceColorTags(Config.Prefix + Config.OnActivated));

                if (Config.StripOnUse)
                {
                    caller.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = true;

                    if (weapons.ContainsKey(caller)) weapons.Remove(caller);

                    var WeaponList = new WeaponList();

                    foreach (var weapon in caller.PlayerPawn.Value!.WeaponServices!.MyWeapons)
                    {
                        if (weapons.ContainsKey(caller) && weapons[caller].weapons.Contains(weapon.Value!.DesignerName)) continue;
                        WeaponList.weapons.Add(weapon.Value!.DesignerName!);
                    }

                    weapons.Add(caller, WeaponList);
                    caller.RemoveWeapons();
                }
            }
            else
            {
                caller!.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
                AddTimer(0.3f, () => Utilities.SetStateChanged(caller.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices"));
                if (smoothThirdPersonPool[caller] != null && smoothThirdPersonPool[caller].IsValid) smoothThirdPersonPool[caller].Remove();
                caller.PrintToChat(ReplaceColorTags(Config.Prefix + Config.OnDeactivated));
                smoothThirdPersonPool.Remove(caller);

                caller.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = false;

                if (Config.StripOnUse)
                {
                    foreach (var weapon in weapons[caller].weapons)
                    {
                        caller.GiveNamedItem(weapon);
                    }
                }
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

    public class WeaponList
    {
        public List<string> weapons = new List<string>();
    }

    public class Config : BasePluginConfig
    {
        [JsonPropertyName("OnActivated")] public string OnActivated { get; set; } = "Third Person Activated";
        [JsonPropertyName("OnDeactivated")] public string OnDeactivated { get; set; } = "Third Person Deactivated";
        [JsonPropertyName("Prefix")] public string Prefix { get; set; } = " [{DARKRED}Third Person{DEFAULT}] ";
        [JsonPropertyName("UseOnlyAdmin")] public bool UseOnlyAdmin { get; set; } = false;
        [JsonPropertyName("OnlyAdminFlag")] public string Flag { get; set; } = "@css/slay";
        [JsonPropertyName("NoPermission")] public string NoPermission { get; set; } = "You dont have to access this command.";
        [JsonPropertyName("UseSmoothCam")] public bool UseSmooth { get; set; } = true;
        [JsonPropertyName("SmoothCamDuration")] public float SmoothDuration { get; set; } = 0.01f;
        [JsonPropertyName("StripOnUse")] public bool StripOnUse { get; set; } = false;
    }
}
