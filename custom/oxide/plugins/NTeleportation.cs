//#define DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Rust;
using UnityEngine;
using System.Reflection;
using Oxide.Core.Libraries.Covalence;
using Network;

namespace Oxide.Plugins
{
    [Info("NTeleportation", "Author Nogrod, Maintainer nivex", "1.3.7")]
    class NTeleportation : RustPlugin
    {
        private bool newSave;
        private string banditPrefab;
        private string outpostPrefab;
        private const bool True = true;
        private const bool False = false;
        private Vector3 Zero = default(Vector3);
        private readonly Vector3 Up = Vector3.up;
        private readonly Vector3 Down = Vector3.down;
        private const string NewLine = "\n";
        private const string ConfigDefaultPermVip = "nteleportation.vip";
        private const string PermHome = "nteleportation.home";
        private const string PermTpR = "nteleportation.tpr";
        private const string PermTpT = "nteleportation.tpt";
        private const string PermDeleteHome = "nteleportation.deletehome";
        private const string PermHomeHomes = "nteleportation.homehomes";
        private const string PermImportHomes = "nteleportation.importhomes";
        private const string PermRadiusHome = "nteleportation.radiushome";
        private const string PermTp = "nteleportation.tp";
        private const string PermTpB = "nteleportation.tpb";
        private const string PermTpConsole = "nteleportation.tpconsole";
        private const string PermTpHome = "nteleportation.tphome";
        private const string PermTpTown = "nteleportation.tptown";
        private const string PermTpOutpost = "nteleportation.tpoutpost";
        private const string PermTpBandit = "nteleportation.tpbandit";
        private const string PermTpN = "nteleportation.tpn";
        private const string PermTpL = "nteleportation.tpl";
        private const string PermTpRemove = "nteleportation.tpremove";
        private const string PermTpSave = "nteleportation.tpsave";
        private const string PermWipeHomes = "nteleportation.wipehomes";
        private const string PermCraftHome = "nteleportation.crafthome";
        private const string PermCraftTown = "nteleportation.crafttown";
        private const string PermCraftOutpost = "nteleportation.craftoutpost";
        private const string PermCraftBandit = "nteleportation.craftbandit";
        private const string PermCraftTpR = "nteleportation.crafttpr";
        private DynamicConfigFile dataConvert;
        private DynamicConfigFile dataDisabled;
        private DynamicConfigFile dataAdmin;
        private DynamicConfigFile dataHome;
        private DynamicConfigFile dataTPR;
        private DynamicConfigFile dataTPT;
        private DynamicConfigFile dataTown;
        private DynamicConfigFile dataOutpost;
        private DynamicConfigFile dataBandit;
        private Dictionary<ulong, AdminData> Admin;
        private Dictionary<ulong, HomeData> Home;
        private Dictionary<ulong, TeleportData> TPR;
        private Dictionary<string, List<string>> TPT;
        private Dictionary<ulong, TeleportData> Town;
        private Dictionary<ulong, TeleportData> Outpost;
        private Dictionary<ulong, TeleportData> Bandit;
        private bool changedAdmin;
        private bool changedHome;
        private bool changedTPR;
        private bool changedTPT;
        private bool changedTown;
        private bool changedOutpost;
        private bool changedBandit;
        private float boundary;
        private readonly int triggerLayer = LayerMask.GetMask("Trigger");
        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World");
        private int buildingLayer { get; set; } = LayerMask.GetMask("Terrain", "World", "Construction", "Deployed");
        private readonly int blockLayer = LayerMask.GetMask("Construction");
        private readonly int deployedLayer = LayerMask.GetMask("Deployed");
        private readonly Dictionary<ulong, TeleportTimer> TeleportTimers = new Dictionary<ulong, TeleportTimer>();
        private readonly Dictionary<ulong, Timer> PendingRequests = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, BasePlayer> PlayersRequests = new Dictionary<ulong, BasePlayer>();
        private readonly Dictionary<int, string> ReverseBlockedItems = new Dictionary<int, string>();
        private readonly Dictionary<ulong, Vector3> teleporting = new Dictionary<ulong, Vector3>();
        private SortedDictionary<string, Vector3> caves = new SortedDictionary<string, Vector3>();
        private SortedDictionary<string, MonInfo> monuments = new SortedDictionary<string, MonInfo>();
        private bool outpostEnabled;
        private string OutpostTPDisabledMessage = "OutpostTPDisabled";
        private bool banditEnabled;
        private string BanditTPDisabledMessage = "BanditTPDisabled";

        [PluginReference]
        private Plugin Clans, Economics, ServerRewards, Friends, CompoundTeleport, ZoneManager, NoEscape, Vanish;

        class MonInfo
        {
            public Vector3 Position;
            public float Radius;
        }

        #region Configuration

        private static Configuration config;

        public class InterruptSettings
        {
            [JsonProperty(PropertyName = "Above Water")]
            public bool AboveWater { get; set; } = True;

            [JsonProperty(PropertyName = "Balloon")]
            public bool Balloon { get; set; } = True;

            [JsonProperty(PropertyName = "Cargo Ship")]
            public bool Cargo { get; set; } = True;

            [JsonProperty(PropertyName = "Cold")]
            public bool Cold { get; set; } = False;

            [JsonProperty(PropertyName = "Excavator")]
            public bool Excavator { get; set; } = False;

            [JsonProperty(PropertyName = "Hot")]
            public bool Hot { get; set; } = False;

            [JsonProperty(PropertyName = "Hostile")]
            public bool Hostile { get; set; } = False;

            [JsonProperty(PropertyName = "Hurt")]
            public bool Hurt { get; set; } = True;

            [JsonProperty(PropertyName = "Lift")]
            public bool Lift { get; set; } = True;

            [JsonProperty(PropertyName = "Monument")]
            public bool Monument { get; set; } = False;

            [JsonProperty(PropertyName = "Mounted")]
            public bool Mounted { get; set; } = True;

            [JsonProperty(PropertyName = "Oil Rig")]
            public bool Oilrig { get; set; } = False;

            [JsonProperty(PropertyName = "Safe Zone")]
            public bool Safe { get; set; } = True;

            [JsonProperty(PropertyName = "Swimming")]
            public bool Swimming { get; set; } = False;
        }

        public class PluginSettings
        {
            [JsonProperty(PropertyName = "Interrupt TP")]
            public InterruptSettings Interrupt { get; set; } = new InterruptSettings();

            [JsonProperty(PropertyName = "Block Teleport (NoEscape)")]
            public bool BlockNoEscape { get; set; } = False;

            [JsonProperty(PropertyName = "Block Teleport (ZoneManager)")]
            public bool BlockZoneFlag { get; set; } = False;

            [JsonProperty(PropertyName = "Chat Name")]
            public string ChatName { get; set; } = "<color=red>Teleportation</color>: ";

            [JsonProperty(PropertyName = "Chat Steam64ID")]
            public ulong ChatID { get; set; } = 76561199056025689;

            [JsonProperty(PropertyName = "Check Boundaries On Teleport X Y Z")]
            public bool CheckBoundaries { get; set; } = True;

            [JsonProperty(PropertyName = "Draw Sphere On Set Home")]
            public bool DrawHomeSphere { get; set; } = True;

            [JsonProperty(PropertyName = "Homes Enabled")]
            public bool HomesEnabled { get; set; } = True;

            [JsonProperty(PropertyName = "TPR Enabled")]
            public bool TPREnabled { get; set; } = True;

            [JsonProperty(PropertyName = "Town Enabled")]
            public bool TownEnabled { get; set; } = True;

            [JsonProperty(PropertyName = "Outpost Enabled")]
            public bool OutpostEnabled { get; set; } = True;

            [JsonProperty(PropertyName = "Bandit Enabled")]
            public bool BanditEnabled { get; set; } = True;

            [JsonProperty(PropertyName = "Strict Foundation Check")]
            public bool StrictFoundationCheck { get; set; } = False;

            [JsonProperty(PropertyName = "Cave Distance Small")]
            public float CaveDistanceSmall { get; set; } = 50f;

            [JsonProperty(PropertyName = "Cave Distance Medium")]
            public float CaveDistanceMedium { get; set; } = 70f;

            [JsonProperty(PropertyName = "Cave Distance Large")]
            public float CaveDistanceLarge { get; set; } = 110f;

            [JsonProperty(PropertyName = "Default Monument Size")]
            public float DefaultMonumentSize { get; set; } = 50f;

            [JsonProperty(PropertyName = "Minimum Temp")]
            public float MinimumTemp { get; set; } = 0f;

            [JsonProperty(PropertyName = "Maximum Temp")]
            public float MaximumTemp { get; set; } = 40f;

            [JsonProperty(PropertyName = "Blocked Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> BlockedItems { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty(PropertyName = "Bypass CMD")]
            public string BypassCMD { get; set; } = "pay";

            [JsonProperty(PropertyName = "Use Economics")]
            public bool UseEconomics { get; set; } = False;

            [JsonProperty(PropertyName = "Use Server Rewards")]
            public bool UseServerRewards { get; set; } = False;

            [JsonProperty(PropertyName = "Wipe On Upgrade Or Change")]
            public bool WipeOnUpgradeOrChange { get; set; } = False;

            [JsonProperty(PropertyName = "Auto Generate Outpost Location")]
            public bool AutoGenOutpost { get; set; } = False;

            [JsonProperty(PropertyName = "Auto Generate Bandit Location")]
            public bool AutoGenBandit { get; set; } = False;
        }

        public class AdminSettings
        {
            [JsonProperty(PropertyName = "Announce Teleport To Target")]
            public bool AnnounceTeleportToTarget { get; set; } = False;

            [JsonProperty(PropertyName = "Usable By Admins")]
            public bool UseableByAdmins { get; set; } = True;

            [JsonProperty(PropertyName = "Usable By Moderators")]
            public bool UseableByModerators { get; set; } = True;

            [JsonProperty(PropertyName = "Location Radius")]
            public int LocationRadius { get; set; } = 25;

            [JsonProperty(PropertyName = "Teleport Near Default Distance")]
            public int TeleportNearDefaultDistance { get; set; } = 30;
        }

        public class HomesSettings
        {
            [JsonProperty(PropertyName = "Homes Limit")]
            public int HomesLimit { get; set; } = 2;

            [JsonProperty(PropertyName = "VIP Homes Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPHomesLimits { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "Cooldown")]
            public int Cooldown { get; set; } = 600;

            [JsonProperty(PropertyName = "Countdown")]
            public int Countdown { get; set; } = 15;

            [JsonProperty(PropertyName = "Daily Limit")]
            public int DailyLimit { get; set; } = 5;

            [JsonProperty(PropertyName = "VIP Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPDailyLimits { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "VIP Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPCooldowns { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "VIP Countdowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPCountdowns { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "Location Radius")]
            public int LocationRadius { get; set; } = 25;

            [JsonProperty(PropertyName = "Force On Top Of Foundation")]
            public bool ForceOnTopOfFoundation { get; set; } = True;

            [JsonProperty(PropertyName = "Check Foundation For Owner")]
            public bool CheckFoundationForOwner { get; set; } = True;

            [JsonProperty(PropertyName = "Use Friends")]
            public bool UseFriends { get; set; } = True;

            [JsonProperty(PropertyName = "Use Clans")]
            public bool UseClans { get; set; } = True;

            [JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams { get; set; } = True;

            [JsonProperty(PropertyName = "Usable Out Of Building Blocked")]
            public bool UsableOutOfBuildingBlocked { get; set; } = False;

            [JsonProperty(PropertyName = "Usable Into Building Blocked")]
            public bool UsableIntoBuildingBlocked { get; set; } = False;

            [JsonProperty(PropertyName = "Allow Cupboard Owner When Building Blocked")]
            public bool CupOwnerAllowOnBuildingBlocked { get; set; } = True;

            [JsonProperty(PropertyName = "Allow Iceberg")]
            public bool AllowIceberg { get; set; } = False;

            [JsonProperty(PropertyName = "Allow Cave")]
            public bool AllowCave { get; set; } = False;

            [JsonProperty(PropertyName = "Allow Crafting")]
            public bool AllowCraft { get; set; } = False;

            [JsonProperty(PropertyName = "Allow Above Foundation")]
            public bool AllowAboveFoundation { get; set; } = True;

            [JsonProperty(PropertyName = "Check If Home Is Valid On Listhomes")]
            public bool CheckValidOnList { get; set; } = False;

            [JsonProperty(PropertyName = "Pay")]
            public int Pay { get; set; } = 0;

            [JsonProperty(PropertyName = "Bypass")]
            public int Bypass { get; set; } = 0;
        }

        public class TPTSettings
        {
            [JsonProperty(PropertyName = "Use Friends")]
            public bool UseFriends { get; set; }

            [JsonProperty(PropertyName = "Use Clans")]
            public bool UseClans { get; set; }

            [JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams { get; set; }

            [JsonProperty(PropertyName = "Allow Cave")]
            public bool AllowCave { get; set; }
        }

        public class TPRSettings
        {
            [JsonProperty(PropertyName = "Allow Cave")]
            public bool AllowCave { get; set; } = False;

            [JsonProperty(PropertyName = "Allow TPB")]
            public bool AllowTPB { get; set; } = True;

            [JsonProperty(PropertyName = "Cooldown")]
            public int Cooldown { get; set; } = 600;

            [JsonProperty(PropertyName = "Countdown")]
            public int Countdown { get; set; } = 15;

            [JsonProperty(PropertyName = "Daily Limit")]
            public int DailyLimit { get; set; } = 5;

            [JsonProperty(PropertyName = "VIP Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPDailyLimits { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "VIP Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPCooldowns { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "VIP Countdowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPCountdowns { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "Request Duration")]
            public int RequestDuration { get; set; } = 30;

            [JsonProperty(PropertyName = "Block TPA On Ceiling")]
            public bool BlockTPAOnCeiling { get; set; } = True;

            [JsonProperty(PropertyName = "Usable Out Of Building Blocked")]
            public bool UsableOutOfBuildingBlocked { get; set; } = False;

            [JsonProperty(PropertyName = "Usable Into Building Blocked")]
            public bool UsableIntoBuildingBlocked { get; set; } = False;

            [JsonProperty(PropertyName = "Allow Cupboard Owner When Building Blocked")]
            public bool CupOwnerAllowOnBuildingBlocked { get; set; } = True;

            [JsonProperty(PropertyName = "Allow Crafting")]
            public bool AllowCraft { get; set; } = False;

            [JsonProperty(PropertyName = "Pay")]
            public int Pay { get; set; } = 0;

            [JsonProperty(PropertyName = "Bypass")]
            public int Bypass { get; set; } = 0;
        }

        public class TownSettings
        {
            [JsonProperty(PropertyName = "Allow Cave")]
            public bool AllowCave { get; set; } = False;

            [JsonProperty(PropertyName = "Cooldown")]
            public int Cooldown { get; set; } = 600;

            [JsonProperty(PropertyName = "Countdown")]
            public int Countdown { get; set; } = 15;

            [JsonProperty(PropertyName = "Daily Limit")]
            public int DailyLimit { get; set; } = 5;

            [JsonProperty(PropertyName = "VIP Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPDailyLimits { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "VIP Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPCooldowns { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "VIP Countdowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> VIPCountdowns { get; set; } = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };

            [JsonProperty(PropertyName = "Location")]
            public Vector3 Location { get; set; } = Vector3.zero;

            [JsonProperty(PropertyName = "Usable Out Of Building Blocked")]
            public bool UsableOutOfBuildingBlocked { get; set; } = False;

            [JsonProperty(PropertyName = "Allow Crafting")]
            public bool AllowCraft { get; set; } = False;

            [JsonProperty(PropertyName = "Pay")]
            public int Pay { get; set; } = 0;

            [JsonProperty(PropertyName = "Bypass")]
            public int Bypass { get; set; } = 0;
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public PluginSettings Settings = new PluginSettings();

            [JsonProperty(PropertyName = "Admin")]
            public AdminSettings Admin = new AdminSettings();

            [JsonProperty(PropertyName = "Home")]
            public HomesSettings Home = new HomesSettings();

            [JsonProperty(PropertyName = "TPT")]
            public TPTSettings TPT = new TPTSettings();

            [JsonProperty(PropertyName = "TPR")]
            public TPRSettings TPR = new TPRSettings();

            [JsonProperty(PropertyName = "Town")]
            public TownSettings Town = new TownSettings();

            [JsonProperty(PropertyName = "Outpost")]
            public TownSettings Outpost = new TownSettings();

            [JsonProperty(PropertyName = "Bandit")]
            public TownSettings Bandit = new TownSettings();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                Config.Settings.Converters = new JsonConverter[] { new UnityVector3Converter() };
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            Puts("Loaded default configuration.");
        }

        #endregion

        class DisabledData
        {
            [JsonProperty("List of disabled commands")]
            public List<string> DisabledCommands = new List<string>();

            public DisabledData() { }
        }

        DisabledData DisabledTPT = new DisabledData();

        class AdminData
        {
            [JsonProperty("pl")]
            public Vector3 PreviousLocation { get; set; }

            [JsonProperty("l")]
            public Dictionary<string, Vector3> Locations { get; set; } = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        }

        class HomeData
        {
            [JsonProperty("l")]
            public Dictionary<string, Vector3> Locations { get; set; } = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty("t")]
            public TeleportData Teleports { get; set; } = new TeleportData();
        }

        class TeleportData
        {
            [JsonProperty("a")]
            public int Amount { get; set; }

            [JsonProperty("d")]
            public string Date { get; set; }

            [JsonProperty("t")]
            public int Timestamp { get; set; }
        }

        class TeleportTimer
        {
            public Timer Timer { get; set; }
            public BasePlayer OriginPlayer { get; set; }
            public BasePlayer TargetPlayer { get; set; }
        }

        private enum checkmode
        {
            home, tpr, tpa, town
        };

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AdminTP", "You teleported to {0}!"},
                {"AdminTPTarget", "{0} teleported to you!"},
                {"AdminTPPlayers", "You teleported {0} to {1}!"},
                {"AdminTPPlayer", "{0} teleported you to {1}!"},
                {"AdminTPPlayerTarget", "{0} teleported {1} to you!"},
                {"AdminTPCoordinates", "You teleported to {0}!"},
                {"AdminTPTargetCoordinates", "You teleported {0} to {1}!"},
                {"AdminTPOutOfBounds", "You tried to teleport to a set of coordinates outside the map boundaries!"},
                {"AdminTPBoundaries", "X and Z values need to be between -{0} and {0} while the Y value needs to be between -100 and 2000!"},
                {"AdminTPLocation", "You teleported to {0}!"},
                {"AdminTPLocationSave", "You have saved the current location!"},
                {"AdminTPLocationRemove", "You have removed the location {0}!"},
                {"AdminLocationList", "The following locations are available:"},
                {"AdminLocationListEmpty", "You haven't saved any locations!"},
                {"AdminTPBack", "You've teleported back to your previous location!"},
                {"AdminTPBackSave", "Your previous location has been saved, use /tpb to teleport back!"},
                {"AdminTPTargetCoordinatesTarget", "{0} teleported you to {1}!"},
                {"AdminTPConsoleTP", "You were teleported to {0}"},
                {"AdminTPConsoleTPPlayer", "You were teleported to {0}"},
                {"AdminTPConsoleTPPlayerTarget", "{0} was teleported to you!"},
                {"HomeTP", "You teleported to your home '{0}'!"},
                {"HomeAdminTP", "You teleported to {0}'s home '{1}'!"},
                {"HomeSave", "You have saved the current location as your home!"},
                {"HomeNoFoundation", "You can only use a home location on a foundation!"},
                {"HomeFoundationNotOwned", "You can't use home on someone else's house."},
                {"HomeFoundationUnderneathFoundation", "You can't use home on a foundation that is underneath another foundation."},
                {"HomeFoundationNotFriendsOwned", "You or a friend need to own the house to use home!"},
                {"HomeRemovedInvalid", "Your home '{0}' was removed because not on a foundation or not owned!"},
                {"HighWallCollision", "High Wall Collision!"},
                {"HomeRemovedInsideBlock", "Your home '{0}' was removed because inside a foundation!"},
                {"HomeRemove", "You have removed your home {0}!"},
                {"HomeDelete", "You have removed {0}'s home '{1}'!"},
                {"HomeList", "The following homes are available:"},
                {"HomeListEmpty", "You haven't saved any homes!"},
                {"HomeMaxLocations", "Unable to set your home here, you have reached the maximum of {0} homes!"},
                {"HomeQuota", "You have set {0} of the maximum {1} homes!"},
                {"HomeTPStarted", "Teleporting to your home {0} in {1} seconds!"},
                {"PayToHome", "Standard payment of {0} applies to all home teleports!"},
                {"PayToTown", "Standard payment of {0} applies to all town teleports!"},
                {"PayToTPR", "Standard payment of {0} applies to all tprs!"},
                {"HomeTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"HomeTPCooldownBypass", "Your teleport was currently on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"HomeTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"HomeTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"HomeTPCooldownBypassP2", "Type /home NAME {0}." },
                {"HomeTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"HomeTPAmount", "You have {0} home teleports left today!"},
                {"HomesListWiped", "You have wiped all the saved home locations!"},
                {"HomeTPBuildingBlocked", "You can't set your home if you are not allowed to build in this zone!"},
                {"HomeTPSwimming", "You can't set your home while swimming!"},
                {"HomeTPCrafting", "You can't set your home while crafting!"},
                {"Request", "You've requested a teleport to {0}!"},
                {"RequestTarget", "{0} requested to be teleported to you! Use '/tpa' to accept!"},
                {"PendingRequest", "You already have a request pending, cancel that request or wait until it gets accepted or times out!"},
                {"PendingRequestTarget", "The player you wish to teleport to already has a pending request, try again later!"},
                {"NoPendingRequest", "You have no pending teleport request!"},
                {"AcceptOnRoof", "You can't accept a teleport while you're on a ceiling, get to ground level!"},
                {"Accept", "{0} has accepted your teleport request! Teleporting in {1} seconds!"},
                {"AcceptTarget", "You've accepted the teleport request of {0}!"},
                {"NotAllowed", "You are not allowed to use this command!"},
                {"Success", "You teleported to {0}!"},
                {"SuccessTarget", "{0} teleported to you!"},
                {"Cancelled", "Your teleport request to {0} was cancelled!"},
                {"CancelledTarget", "{0} teleport request was cancelled!"},
                {"TPCancelled", "Your teleport was cancelled!"},
                {"TPCancelledTarget", "{0} cancelled teleport!"},
                {"TPYouCancelledTarget", "You cancelled {0} teleport!"},
                {"TimedOut", "{0} did not answer your request in time!"},
                {"TimedOutTarget", "You did not answer {0}'s teleport request in time!"},
                {"TargetDisconnected", "{0} has disconnected, your teleport was cancelled!"},
                {"TPRCooldown", "Your teleport requests are currently on cooldown. You'll have to wait {0} to send your next teleport request."},
                {"TPRCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"TPRCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"TPRCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"TPMoney", "{0} deducted from your account!"},
                {"TPNoMoney", "You do not have {0} in any account!"},
                {"TPRCooldownBypassP2", "Type /tpr {0}." },
                {"TPRCooldownBypassP2a", "Type /tpr NAME {0}." },
                {"TPRLimitReached", "You have reached the daily limit of {0} teleport requests today!"},
                {"TPRAmount", "You have {0} teleport requests left today!"},
                {"TPRTarget", "Your target is currently not available!"},
                {"TPDead", "You can't teleport while being dead!"},
                {"TPWounded", "You can't teleport while wounded!"},
                {"TPTooCold", "You're too cold to teleport!"},
                {"TPTooHot", "You're too hot to teleport!"},
                {"TPHostile", "Can't teleport to outpost or bandit when hostile!"},
                {"HostileTimer", "Teleport available in {0} minutes."},
                {"TPMounted", "You can't teleport while seated!"},
                {"TPBuildingBlocked", "You can't teleport while in a building blocked zone!"},
                {"TPAboveWater", "You can't teleport while above water!"},
                {"TPTargetBuildingBlocked", "You can't teleport in a building blocked zone!"},
                {"TPTargetInsideBlock", "You can't teleport into a foundation!"},
                {"TPSwimming", "You can't teleport while swimming!"},
                {"TPCargoShip", "You can't teleport from the cargo ship!"},
                {"TPOilRig", "You can't teleport from the oil rig!"},
                {"TPExcavator", "You can't teleport from the excavator!"},
                {"TPHotAirBalloon", "You can't teleport to or from a hot air balloon!"},
                {"TPLift", "You can't teleport while in an elevator or bucket lift!"},
                {"TPBucketLift", "You can't teleport while in a bucket lift!"},
                {"TPRegLift", "You can't teleport while in an elevator!"},
                {"TPSafeZone", "You can't teleport from a safezone!"},
                {"TPFlagZone", "You can't teleport from this zone!"},
                {"TPNoEscapeBlocked", "You can't teleport while blocked!"},
                {"TPCrafting", "You can't teleport while crafting!"},
                {"TPBlockedItem", "You can't teleport while carrying: {0}!"},
                {"TooCloseToMon", "You can't teleport so close to the {0}!"},
                {"TooCloseToCave", "You can't teleport so close to a cave!"},
                {"HomeTooCloseToCave", "You can't set home so close to a cave!"},
                {"TownTP", "You teleported to town!"},
                {"TownTPNotSet", "Town is currently not set!"},
                {"TownTPDisabled", "Town is currently not enabled!"},
                {"TownTPLocation", "You have set the town location to {0}!"},
                {"TownTPStarted", "Teleporting to town in {0} seconds!"},
                {"TownTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"TownTPCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"TownTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"TownTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"TownTPCooldownBypassP2", "Type /town {0}." },
                {"TownTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"TownTPAmount", "You have {0} town teleports left today!"},

                {"OutpostTP", "You teleported to the outpost!"},
                {"OutpostTPNotSet", "Outpost is currently not set!"},
                {"OutpostTPDisabled", "Outpost is currently not enabled!"},
                {"OutpostTPDisabledConfig", "Bandit is currently not enabled because it isn't enabled in the config"},
                {"OutpostTPDisabledNoLocation", "Bandit is currently not enabled, location is not set and auto generation is disabled!"},
                {"OutpostTPDisabledNoLocationAutoGen", "Bandit is currently not enabled because auto generation failed!"},
                {"OutpostTPLocation", "You have set the outpost location to {0}!"},
                {"OutpostTPStarted", "Teleporting to the outpost in {0} seconds!"},
                {"OutpostTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"OutpostTPCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"OutpostTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"OutpostTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"OutpostTPCooldownBypassP2", "Type /outpost {0}." },
                {"OutpostTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"OutpostTPAmount", "You have {0} outpost teleports left today!"},

                {"BanditTP", "You teleported to bandit town!"},
                {"BanditTPNotSet", "Bandit is currently not set!"},
                {"BanditTPDisabled", "Bandit is currently not enabled!"},
                {"BanditTPDisabledConfig", "Bandit is currently not enabled because it isn't enabled in the config!"},
                {"BanditTPDisabledNoLocation", "Bandit is currently not enabled, location is not set and auto generation is disabled!"},
                {"BanditTPDisabledNoLocationAutoGen", "Bandit is currently not enabled because auto generation failed!"},
                {"BanditTPLocation", "You have set the bandit town location to {0}!"},
                {"BanditTPStarted", "Teleporting to bandit town in {0} seconds!"},
                {"BanditTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"BanditTPCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"BanditTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"BanditTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"BanditTPCooldownBypassP2", "Type /bandit {0}." },
                {"BanditTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"BanditTPAmount", "You have {0} bandit town teleports left today!"},

                {"Interrupted", "Your teleport was interrupted!"},
                {"InterruptedTarget", "{0}'s teleport was interrupted!"},
                {"Unlimited", "Unlimited"},
                {
                    "TPInfoGeneral", string.Join(NewLine, new[]
                    {
                        "Please specify the module you want to view the info of.",
                        "The available modules are: ",
                    })
                },
                {
                    "TPHelpGeneral", string.Join(NewLine, new[]
                    {
                        "/tpinfo - Shows limits and cooldowns.",
                        "Please specify the module you want to view the help of.",
                        "The available modules are: ",
                    })
                },
                {
                    "TPHelpadmintp", string.Join(NewLine, new[]
                    {
                        "As an admin you have access to the following commands:",
                        "/tp \"targetplayer\" - Teleports yourself to the target player.",
                        "/tp \"player\" \"targetplayer\" - Teleports the player to the target player.",
                        "/tp x y z - Teleports you to the set of coordinates.",
                        "/tpl - Shows a list of saved locations.",
                        "/tpl \"location name\" - Teleports you to a saved location.",
                        "/tpsave \"location name\" - Saves your current position as the location name.",
                        "/tpremove \"location name\" - Removes the location from your saved list.",
                        "/tpb - Teleports you back to the place where you were before teleporting.",
                        "/home radius \"radius\" - Find all homes in radius.",
                        "/home delete \"player name|id\" \"home name\" - Remove a home from a player.",
                        "/home tp \"player name|id\" \"name\" - Teleports you to the home location with the name 'name' from the player.",
                        "/home homes \"player name|id\" - Shows you a list of all homes from the player."
                    })
                },
                {
                    "TPHelphome", string.Join(NewLine, new[]
                    {
                        "With the following commands you can set your home location to teleport back to:",
                        "/home add \"name\" - Saves your current position as the location name.",
                        "/home list - Shows you a list of all the locations you have saved.",
                        "/home remove \"name\" - Removes the location of your saved homes.",
                        "/home \"name\" - Teleports you to the home location."
                    })
                },
                {
                    "TPHelptpr", string.Join(NewLine, new[]
                    {
                        "With these commands you can request to be teleported to a player or accept someone else's request:",
                        "/tpr \"player name\" - Sends a teleport request to the player.",
                        "/tpa - Accepts an incoming teleport request.",
                        "/tpc - Cancel teleport or request."
                    })
                },
                {
                    "TPSettingsGeneral", string.Join(NewLine, new[]
                    {
                        "Please specify the module you want to view the settings of. ",
                        "The available modules are:",
                    })
                },
                {
                    "TPSettingshome", string.Join(NewLine, new[]
                    {
                        "Home System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}",
                        "Amount of saved Home locations: {2}"
                    })
                },
                {
                    "TPSettingsbandit", string.Join(NewLine, new[]
                    {
                        "Bandit System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}"
                    })
                },
                {
                    "TPSettingsoutpost", string.Join(NewLine, new[]
                    {
                        "Bandit System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}"
                    })
                },
                {
                    "TPSettingstpr", string.Join(NewLine, new[]
                    {
                        "TPR System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}"
                    })
                },
                {
                    "TPSettingstown", string.Join(NewLine, new[]
                    {
                        "Town System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}"
                    })
                },
                {"TPT_True", "enabled"},
                {"TPT_False", "disabled"},
                {"TPT_clan", "TPT clan has been {0}."},
                {"TPT_friend", "TPT friend has been {0}."},
                {"TPT_team", "TPT team has been {0}."},
                {"NotValidTPT", "Not valid, player is not"},
                {"NotValidTPTFriend", " a friend!"},
                {"NotValidTPTTeam", " on your team!"},
                {"NotValidTPTClan", " in your clan!"},
                {"TPTInfo", "`/tpt clan|team|friend` - toggle allowing/blocking of players trying to TPT to you via one of these options."},
                {"PlayerNotFound", "The specified player couldn't be found please try again!"},
                {"MultiplePlayers", "Found multiple players: {0}"},
                {"CantTeleportToSelf", "You can't teleport to yourself!"},
                {"CantTeleportPlayerToSelf", "You can't teleport a player to himself!"},
                {"TeleportPending", "You can't initiate another teleport while you have a teleport pending!"},
                {"TeleportPendingTarget", "You can't request a teleport to someone who's about to teleport!"},
                {"LocationExists", "A location with this name already exists at {0}!"},
                {"LocationExistsNearby", "A location with the name {0} already exists near this position!"},
                {"LocationNotFound", "Couldn't find a location with that name!"},
                {"NoPreviousLocationSaved", "No previous location saved!"},
                {"HomeExists", "You have already saved a home location by this name!"},
                {"HomeExistsNearby", "A home location with the name {0} already exists near this position!"},
                {"HomeNotFound", "Couldn't find your home with that name!"},
                {"InvalidCoordinates", "The coordinates you've entered are invalid!"},
                {"InvalidHelpModule", "Invalid module supplied!"},
                {"InvalidCharacter", "You have used an invalid character, please limit yourself to the letters a to z and numbers."},
                {
                    "SyntaxCommandTP", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tp command as follows:",
                        "/tp \"targetplayer\" - Teleports yourself to the target player.",
                        "/tp \"player\" \"targetplayer\" - Teleports the player to the target player.",
                        "/tp x y z - Teleports you to the set of coordinates.",
                        "/tp \"player\" x y z - Teleports the player to the set of coordinates."
                    })
                },
                {
                    "SyntaxCommandTPL", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpl command as follows:",
                        "/tpl - Shows a list of saved locations.",
                        "/tpl \"location name\" - Teleports you to a saved location."
                    })
                },
                {
                    "SyntaxCommandTPSave", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpsave command as follows:",
                        "/tpsave \"location name\" - Saves your current position as 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPRemove", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpremove command as follows:",
                        "/tpremove \"location name\" - Removes the location with the name 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPN", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpn command as follows:",
                        "/tpn \"targetplayer\" - Teleports yourself the default distance behind the target player.",
                        "/tpn \"targetplayer\" \"distance\" - Teleports you the specified distance behind the target player."
                    })
                },
                {
                    "SyntaxCommandSetHome", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home add command as follows:",
                        "/home add \"name\" - Saves the current location as your home with the name 'name'."
                    })
                },
                {
                    "SyntaxCommandRemoveHome", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home remove command as follows:",
                        "/home remove \"name\" - Removes the home location with the name 'name'."
                    })
                },
                {
                    "SyntaxCommandHome", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home command as follows:",
                        "/home \"name\" - Teleports yourself to your home with the name 'name'.",
                        "/home \"name\" pay - Teleports yourself to your home with the name 'name', avoiding cooldown by paying for it.",
                        "/home add \"name\" - Saves the current location as your home with the name 'name'.",
                        "/home list - Shows you a list of all your saved home locations.",
                        "/home remove \"name\" - Removes the home location with the name 'name'."
                    })
                },
                {
                    "SyntaxCommandHomeAdmin", string.Join(NewLine, new[]
                    {
                        "/home radius \"radius\" - Shows you a list of all homes in radius(10).",
                        "/home delete \"player name|id\" \"name\" - Removes the home location with the name 'name' from the player.",
                        "/home tp \"player name|id\" \"name\" - Teleports you to the home location with the name 'name' from the player.",
                        "/home homes \"player name|id\" - Shows you a list of all homes from the player."
                    })
                },
                {
                    "SyntaxCommandTown", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /town command as follows:",
                        "/town - Teleports yourself to town.",
                        "/town pay - Teleports yourself to town, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandTownAdmin", string.Join(NewLine, new[]
                    {
                        "/town set - Saves the current location as town.",
                    })
                },
                {
                    "SyntaxCommandOutpost", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /town command as follows:",
                        "/outpost - Teleports yourself to the Outpost.",
                        "/outpost pay - Teleports yourself to the Outpost, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandOutpostAdmin", string.Join(NewLine, new[]
                    {
                        "/outpost set - Saves the current location as Outpost.",
                    })
                },
                {
                    "SyntaxCommandBandit", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /bandit command as follows:",
                        "/bandit - Teleports yourself to the Bandit Town.",
                        "/bandit pay - Teleports yourself to the Bandit Town, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandBanditAdmin", string.Join(NewLine, new[]
                    {
                        "/bandit set - Saves the current location as Bandit Town.",
                    })
                },
                {
                    "SyntaxCommandHomeDelete", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home delete command as follows:",
                        "/home delete \"player name|id\" \"name\" - Removes the home location with the name 'name' from the player."
                    })
                },
                {
                    "SyntaxCommandHomeAdminTP", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home tp command as follows:",
                        "/home tp \"player name|id\" \"name\" - Teleports you to the home location with the name 'name' from the player."
                    })
                },
                {
                    "SyntaxCommandHomeHomes", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home homes command as follows:",
                        "/home homes \"player name|id\" - Shows you a list of all homes from the player."
                    })
                },
                {
                    "SyntaxCommandListHomes", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home list command as follows:",
                        "/home list - Shows you a list of all your saved home locations."
                    })
                },
                {
                    "SyntaxCommandTPT", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpt command as follows:",
                        "/tpt \"player name\" - Teleports you to a team or clan member."
                    })
                },
                {
                    "SyntaxCommandTPR", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpr command as follows:",
                        "/tpr \"player name\" - Sends out a teleport request to 'player name'."
                    })
                },
                {
                    "SyntaxCommandTPA", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpa command as follows:",
                        "/tpa - Accepts an incoming teleport request."
                    })
                },
                {
                    "SyntaxCommandTPC", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpc command as follows:",
                        "/tpc - Cancels an teleport request."
                    })
                },
                {
                    "SyntaxConsoleCommandToPos", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the teleport.topos console command as follows:",
                        " > teleport.topos \"player\" x y z"
                    })
                },
                {
                    "SyntaxConsoleCommandToPlayer", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the teleport.toplayer console command as follows:",
                        " > teleport.toplayer \"player\" \"target player\""
                    })
                },
                {"LogTeleport", "{0} teleported to {1}."},
                {"LogTeleportPlayer", "{0} teleported {1} to {2}."},
                {"LogTeleportBack", "{0} teleported back to previous location."}
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AdminTP", "Ты телепортировался в {0}!"},
                {"AdminTPTarget", "{0} телепортировался к тебе!"},
                {"AdminTPPlayers", "Ты телепортировался {0} к {1}!"},
                {"AdminTPPlayer", "{0} телепортировался вам {1}!"},
                {"AdminTPPlayerTarget", "{0} телепортированный {1} для вас!"},
                {"AdminTPCoordinates", "Ты телепортировался в {0}!"},
                {"AdminTPTargetCoordinates", "Ты телепортировался {0} к {1}!"},
                {"AdminTPOutOfBounds", "Вы пытались телепортироваться в набор координат за пределами границ карты!"},
                {"AdminTPBoundaries", "X и Z ценности должны быть между -{0} и {0} а Y значение должно быть между -100 и 2000!"},
                {"AdminTPLocation", "Ты телепортировался в {0}!"},
                {"AdminTPLocationSave", "Вы сохранили текущее местоположение!"},
                {"AdminTPLocationRemove", "Вы удалили местоположение {0}!"},
                {"AdminLocationList", "Доступны следующие местоположения,"},
                {"AdminLocationListEmpty", "Вы не сохранили никаких мест!"},
                {"AdminTPBack", "Ты телепортировался обратно на прежнее место!"},
                {"AdminTPBackSave", "Ваше предыдущее местоположение было сохранено, используйте /tpb телепортироваться обратно!"},
                {"AdminTPTargetCoordinatesTarget", "{0} телепортировался вам {1}!"},
                {"AdminTPConsoleTP", "Тебя телепортировали в {0}"},
                {"AdminTPConsoleTPPlayer", "Тебя телепортировали в {0}"},
                {"AdminTPConsoleTPPlayerTarget", "{0} телепортировался к тебе!"},
                {"HomeTP", "Ты телепортировался в свой дом '{0}'!"},
                {"HomeAdminTP", "Ты телепортировался в {0}'s дом '{1}'!"},
                {"HomeSave", "Вы сохранили текущее местоположение в качестве своего дома!"},
                {"HomeNoFoundation", "Вы можете использовать только расположение дома на фундаменте!"},
                {"HomeFoundationNotOwned", "Ты не можешь использовать дом на чужом доме."},
                {"HomeFoundationUnderneathFoundation", "Вы не можете использовать home на фундаменте, который находится под другим фундаментом."},
                {"HomeFoundationNotFriendsOwned", "Вы или друг должны владеть домом, чтобы использовать дом!"},
                {"HomeRemovedInvalid", "Твой дом '{0}' убрали, потому что не на фундаменте или не в собственности!"},
                {"HomeRemovedInsideBlock", "Твой дом '{0}' убрали, потому что внутри фундамент!"},
                {"HomeRemove", "Вы удалили свой дом {0}!"},
                {"HomeDelete", "Вы удалили {0}'s дом '{1}'!"},
                {"HomeList", "Доступны следующие дома,"},
                {"HomeListEmpty", "Вы не спасли ни одного дома!"},
                {"HomeMaxLocations", "Не в состоянии установить свой дом здесь, вы достигли максимума {0} дома!"},
                {"HomeQuota", "Вы установили {0} максимума {1} дома!"},
                {"HomeTPStarted", "Телепортация в ваш дом {0} в {1} секунды!"},
                {"PayToHome", "Стандартная оплата {0} применяется ко всем домашним телепортам!"},
                {"PayToTown", "Стандартная оплата {0} применяется ко всем городским телепортам!"},
                {"PayToTPR", "Стандартная оплата {0} распространяться на всех tprs!"},
                {"HomeTPCooldown", "Ваш телепорт в настоящее время находится на перезарядке. Вам придется подождать {0} для следующего телепорта."},
                {"HomeTPCooldownBypass", "Ваш телепорт в настоящее время был на перезарядке. Вы решили обойти это, заплатив {0} с вашего баланса."},
                {"HomeTPCooldownBypassF", "Ваш телепорт в настоящее время находится на перезарядке. У вас недостаточно средств - {0} - обходить."},
                {"HomeTPCooldownBypassP", "Вы можете заплатить {0} чтобы обойти это охлаждение."},
                {"HomeTPCooldownBypassP2", "Тип /home NAME {0}."},
                {"HomeTPLimitReached", "Вы достигли дневного предела {0} телепорты сегодня!"},
                {"HomeTPAmount", "У вас есть {0} домашние телепорты ушли сегодня!"},
                {"HomesListWiped", "Вы стерли все сохраненные домашние местоположения!"},
                {"HomeTPBuildingBlocked", "Вы не можете установить свой дом, если вам не разрешено строить в этой зоне!"},
                {"HomeTPSwimming", "Вы не можете установить свой дом во время плавания!"},
                {"HomeTPCrafting", "Вы не можете установить свой дом во время крафта!"},
                {"Request", "Вы запросили телепорт на {0}!"},
                {"RequestTarget", "{0} просят телепортироваться к вам! Использовать '/tpa' принять!"},
                {"PendingRequest", "У вас уже есть запрос в ожидании, отменить этот запрос или ждать, пока он не будет принят или тайм-аут!"},
                {"PendingRequestTarget", "Игрок, которому вы хотите телепортироваться, уже имеет ожидающий запрос, повторите попытку позже!"},
                {"NoPendingRequest", "У вас нет запроса на телепортацию!"},
                {"AcceptOnRoof", "Вы не можете принять телепорт, пока вы на потолке, добраться до уровня земли!"},
                {"Accept", "{0} принял ваш запрос на телепортацию! Телепортироваться в {1} секунды!"},
                {"AcceptTarget", "Вы приняли запрос на телепортацию {0}!"},
                {"NotAllowed", "Вы не имеете права использовать эту команду!"},
                {"Success", "Ты телепортировался в {0}!"},
                {"SuccessTarget", "{0} телепортировался к тебе!"},
                {"Cancelled", "Ваш телепорт запрос {0} был отменен!"},
                {"CancelledTarget", "{0} запрос на телепортацию был отменен!"},
                {"TPCancelled", "Твой телепорт был отменен!"},
                {"TPCancelledTarget", "{0} отменен телепорт!"},
                {"TPYouCancelledTarget", "Вы отменили {0} телепортацию!"},
                {"TimedOut", "{0} не ответил на ваш запрос!"},
                {"TimedOutTarget", "Ты не ответил. {0}'s телепортируйте запрос вовремя!"},
                {"TargetDisconnected", "{0} отключился, телепорт отменен!"},
                {"TPRCooldown", "Ваши запросы на телепортацию в настоящее время находятся на перезарядке. Вам придется подождать {0} отправить следующий запрос на телепортацию."},
                {"TPRCooldownBypass", "Ваш запрос на телепортацию был на перезарядке. Вы решили обойти это, заплатив {0} с вашего баланса."},
                {"TPRCooldownBypassF", "Ваш телепорт в настоящее время находится на перезарядке. У вас недостаточно средств - {0} - обходить."},
                {"TPRCooldownBypassP", "Вы можете заплатить {0} чтобы обойти это охлаждение."},
                {"TPMoney", "{0} вычитается из вашего счета!"},
                {"TPNoMoney", "У вас нет {0} в любом аккаунте!"},
                {"TPRCooldownBypassP2", "Тип /tpr {0}."},
                {"TPRCooldownBypassP2a", "Тип /tpr NAME {0}."},
                {"TPRLimitReached", "Вы достигли дневного предела {0} телепорт просит сегодня!"},
                {"TPRAmount", "У вас есть {0} телепорт просит оставить сегодня!"},
                {"TPRTarget", "Ваша цель в настоящее время недоступна!"},
                {"TPDead", "Ты не можешь телепортироваться, будучи мертвым!"},
                {"TPWounded", "Ты не можешь телепортироваться, пока ранен!"},
                {"TPTooCold", "Ты слишком замерз, чтобы телепортироваться!"},
                {"TPTooHot", "Ты слишком горячий, чтобы телепортироваться!"},
                {"TPMounted", "Ты не можешь телепортироваться сидя!"},
                {"TPBuildingBlocked", "Вы не можете телепортироваться в заблокированной зоне здания!"},
                {"TPTargetBuildingBlocked", "Вы не можете телепортироваться в заблокированной зоне здания!"},
                {"TPTargetInsideBlock", "Ты не можешь телепортироваться в Фонд!"},
                {"TPSwimming", "Ты не можешь телепортироваться во время плавания!"},
                {"TPCargoShip", "Ты не можешь телепортироваться с грузового корабля!"},
                {"TPOilRig", "Ты не можешь телепортироваться с нефтяная вышка!"},
                {"TPExcavator", "Ты не можешь телепортироваться с экскаватор!"},
                {"TPHotAirBalloon", "Вы не можете телепортироваться на воздушный шар или с него!"},
                {"TPLift", "Вы не можете телепортироваться в лифте или ковшовом подъемнике!"},
                {"TPBucketLift", "Вы не можете телепортироваться, находясь в ковшовом подъемнике!"},
                {"TPRegLift", "Ты не можешь телепортироваться в лифте!"},
                {"TPSafeZone", "Ты не можешь телепортироваться из безопасной зоны!"},
                {"TPCrafting", "Вы не можете телепортироваться во время крафта!"},
                {"TPBlockedItem", "Вы не можете телепортироваться во время переноски, {0}!"},
                {"TooCloseToMon", "Ты не можешь телепортироваться так близко к {0}!"},
                {"TooCloseToCave", "Ты не можешь телепортироваться так близко к пещере!"},
                {"HomeTooCloseToCave", "Нельзя сидеть дома так близко к пещере!"},
                {"TownTP", "Ты телепортировался в город!"},
                {"TownTPNotSet", "Город в настоящее время не установлен!"},
                {"TownTPDisabled", "Город в настоящее время не включен!"},
                {"TownTPLocation", "Вы установили местоположение города в {0}!"},
                {"TownTPStarted", "Телепортация в город в {0} секунды!"},
                {"TownTPCooldown", "Ваш телепорт в настоящее время находится на перезарядке. Вам придется подождать {0} для следующего телепорта."},
                {"TownTPCooldownBypass", "Ваш запрос на телепортацию был на перезарядке. Вы решили обойти это, заплатив {0} с вашего баланса."},
                {"TownTPCooldownBypassF", "Ваш телепорт в настоящее время находится на перезарядке. У вас недостаточно средств - {0} - обходить."},
                {"TownTPCooldownBypassP", "Вы можете заплатить {0} чтобы обойти это охлаждение."},
                {"TownTPCooldownBypassP2", "Тип /town {0}."},
                {"TownTPLimitReached", "Вы достигли дневного предела {0} телепорты сегодня!"},
                {"TownTPAmount", "У вас есть {0} городские телепорты ушли сегодня!"},
                {"Interrupted", "Ваш телепорт был прерван!"},
                {"InterruptedTarget", "{0}'s телепорт был прерван!"},
                {"Unlimited", "Unlimited"},
                {
                    "TPInfoGeneral", string.Join(NewLine, new[]
                    {
                        "Пожалуйста, укажите модуль, который вы хотите просмотреть.",
                        "Имеющиеся модули, "
                    })
                },
                {
                    "TPHelpGeneral", string.Join(NewLine, new[]
                    {
                        "/tpinfo - Показывает ограничения и кулдауны.",
                        "Пожалуйста, укажите модуль, который вы хотите просмотреть в справке.",
                        "Имеющиеся модули, "
                    })
                },
                {
                    "TPHelpadmintp", string.Join(NewLine, new[]
                    {
                        "Как администратор Вы имеете доступ к следующим командам,",
                        "/tp \"targetplayer\" - Телепортирует себя к целевому игроку.",
                        "/tp \"player\" \"targetplayer\" - Телепортирует игрока к целевому игроку.",
                        "/tp x y z - Телепортирует вас в набор координат.",
                        "/tpl - Показывает список сохраненных местоположений.",
                        "/tpl \"location name\" - Телепортирует вас в сохраненное место.",
                        "/tpsave \"location name\" - Сохраняет текущую позицию в качестве имени местоположения.",
                        "/tpremove \"location name\" - Удаляет местоположение из сохраненного списка.",
                        "/tpb - Телепортирует вас обратно в то место, где вы были до телепортации.",
                        "/home radius \"radius\" - Найти все дома в радиусе.",
                        "/home delete \"player name|id\" \"home name\" - Удалите дом из плеера.",
                        "/home tp \"player name|id\" \"name\" - Телепортирует вас на главную локацию с именем 'name' от игрока.",
                        "/home homes \"player name|id\" - Показывает список всех домов из плеера."
                    })
                },
                {
                    "TPHelphome", string.Join(NewLine, new[]
                    {
                        "С помощью следующих команд вы можете установить свое домашнее местоположение для телепортации обратно в,",
                        "/home add \"name\" - Сохраняет текущую позицию в качестве имени местоположения.",
                        "/home list - Показывает список всех сохраненных местоположений.",
                        "/home remove \"name\" - Удаляет местоположение сохраненных домов.",
                        "/home \"name\" - Телепортирует вас на родное место."
                    })
                },
                {
                    "TPHelptpr", string.Join(NewLine, new[]
                    {
                        "С помощью этих команд вы можете запросить телепортацию к игроку или принять чей-либо запрос,",
                        "/tpr \"player name\" - Отправляет запрос на телепортацию игроку.",
                        "/tpa - Принимает входящий запрос телепорта.",
                        "/tpc - Отменить телепортацию или запрос."
                    })
                },
                {
                    "TPSettingsGeneral", string.Join(NewLine, new[]
                    {
                        "Пожалуйста, укажите модуль, который вы хотите просмотреть настройки.",
                        "Имеющиеся модули,"
                    })
                },
                {
                    "TPSettingshome", string.Join(NewLine, new[]
                    {
                        "В домашней системе включены текущие настройки,",
                        "Время между телепортами, {0}",
                        "Ежедневное количество телепортов, {1}",
                        "Количество сохраненных домашних местоположений, {2}"
                    })
                },
                {
                    "TPSettingstpr", string.Join(NewLine, new[]
                    {
                        "TPR В системе включены текущие настройки,",
                        "Время между телепортами, {0}",
                        "Ежедневное количество телепортов, {1}"
                    })
                },
                {
                    "TPSettingstown", string.Join(NewLine, new[]
                    {
                        "Town В системе включены текущие настройки,",
                        "Время между телепортами, {0}",
                        "Ежедневное количество телепортов, {1}"
                    })
                },
                {"PlayerNotFound", "Не удалось найти указанного игрока, повторите попытку!"},
                {"MultiplePlayers", "Найдено несколько игроков, {0}"},
                {"CantTeleportToSelf", "Ты не можешь телепортироваться к себе!"},
                {"CantTeleportPlayerToSelf", "Вы не можете телепортировать игрока к себе!"},
                {"TeleportPending", "Вы не можете инициировать другой телепорт, пока у вас есть телепорт в ожидании!"},
                {"TeleportPendingTarget", "Ты не можешь просить телепортации у того, кто собирается телепортироваться!"},
                {"LocationExists", "Место с таким именем уже существует в {0}!"},
                {"LocationExistsNearby", "Место с именем {0} уже существует рядом с этой позицией!"},
                {"LocationNotFound", "Не мог найти место с таким именем!"},
                {"NoPreviousLocationSaved", "Предыдущее местоположение не сохранено!"},
                {"HomeExists", "Вы уже сохранили расположение дома под этим именем!"},
                {"HomeExistsNearby", "Расположение дома с именем {0} уже существует рядом с этой позицией!"},
                {"HomeNotFound", "Не мог найти свой дом с таким именем!"},
                {"InvalidCoordinates", "Введенные вами координаты недействительны!"},
                {"InvalidHelpModule", "Неверный модуль поставляется!"},
                {"InvalidCharacter", "Вы использовали недопустимый символ, пожалуйста, ограничьте себя буквами от А до Я и цифрами."},
                {
                    "SyntaxCommandTP", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tp команда следующим образом,",
                        "/tp \"targetplayer\" - Телепортирует себя к целевому игроку.",
                        "/tp \"player\" \"targetplayer\" - Телепортирует игрока к целевому игроку.",
                        "/tp x y z - Телепортирует вас в набор координат.",
                        "/tp \"player\" x y z - Телепортирует игрока в набор координат."
                    })
                },
                {
                    "SyntaxCommandTPL", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpl команда следующим образом,",
                        "/tpl - Показывает список сохраненных местоположений.",
                        "/tpl \"location name\" - Телепортирует вас в сохраненное место."
                    })
                },
                {
                    "SyntaxCommandTPSave", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpsave команда следующим образом,",
                        "/tpsave \"location name\" - Сохраняет текущую позицию как 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPRemove", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpremove команда следующим образом,",
                        "/tpremove \"location name\" - Удаляет расположение с именем 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPN", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpn команда следующим образом,",
                        "/tpn \"targetplayer\" - Телепортирует себя на расстояние по умолчанию позади целевого игрока.",
                        "/tpn \"targetplayer\" \"distance\" - Телепортирует вас на указанное расстояние позади целевого игрока."
                    })
                },
                {
                    "SyntaxCommandSetHome", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home add команда следующим образом,",
                        "/home add \"name\" - Сохранение текущего местоположения в качестве вашего дома с именем 'name'."
                    })
                },
                {
                    "SyntaxCommandRemoveHome", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home remove команда следующим образом,",
                        "/home remove \"name\" - Удаляет расположение дома с именем 'name'."
                    })
                },
                {
                    "SyntaxCommandHome", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home команда следующим образом,",
                        "/home \"name\" - Телепортирует себя в свой дом с именем 'name'.",
                        "/home \"name\" pay - Телепортирует себя в свой дом с именем 'name', избежать перезарядки, заплатив за это.",
                        "/home add \"name\" - Сохранение текущего местоположения в качестве вашего дома с именем 'name'.",
                        "/home list - Показывает список всех сохраненных домашних местоположений.",
                        "/home remove \"name\" - Удаляет расположение дома с именем 'name'."
                    })
                },
                {
                    "SyntaxCommandHomeAdmin", string.Join(NewLine, new[]
                    {
                        "/home radius \"radius\" - Показывает список всех домов в radius(10).",
                        "/home delete \"player name|id\" \"name\" - Удаляет расположение дома с именем 'name' от игрока.",
                        "/home tp \"player name|id\" \"name\" - Телепортирует вас на главную локацию с именем 'name' от игрока.",
                        "/home homes \"player name|id\" - Показывает список всех домов из плеера."
                    })
                },
                {
                    "SyntaxCommandTown", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /town команда следующим образом,",
                        "/town - Teleports yourself to town.",
                        "/town pay - Teleports yourself to town, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandTownAdmin", string.Join(NewLine, new[]
                    {
                        "/town set - Сохраняет текущее местоположение как town."
                    })
                },
                {
                    "SyntaxCommandHomeDelete", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home delete команда следующим образом,",
                        "/home delete \"player name|id\" \"name\" - Удаляет расположение дома с именем 'name' от игрока."
                    })
                },
                {
                    "SyntaxCommandHomeAdminTP", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home tp команда следующим образом,",
                        "/home tp \"player name|id\" \"name\" - Телепортирует вас на главную локацию с именем 'name' от игрока."
                    })
                },
                {
                    "SyntaxCommandHomeHomes", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home homes команда следующим образом,",
                        "/home homes \"player name|id\" - Показывает список всех домов из плеера."
                    })
                },
                {
                    "SyntaxCommandListHomes", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home list команда следующим образом,",
                        "/home list - Показывает список всех сохраненных домашних местоположений."
                    })
                },
                {
                    "SyntaxCommandTPR", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpr команда следующим образом,",
                        "/tpr \"player name\" - Отправляет запрос на телепортацию 'player name'."
                    })
                },
                {
                    "SyntaxCommandTPA", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpa команда следующим образом,",
                        "/tpa - Принимает входящий запрос телепорта."
                    })
                },
                {
                    "SyntaxCommandTPC", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpc команда следующим образом,",
                        "/tpc - Отменяет телепорт запросу."
                    })
                },
                {
                    "SyntaxConsoleCommandToPos", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только teleport.topos console команда следующим образом,",
                        " > teleport.topos \"player\" x y z"
                    })
                },
                {
                    "SyntaxConsoleCommandToPlayer", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только teleport.toplayer console команда следующим образом,",
                        " > teleport.toplayer \"player\" \"target player\""
                    })
                },
                {"LogTeleport", "{0} телепортироваться {1}."},
                {"LogTeleportPlayer", "{0} телепортированный {1} к {2}."},
                {"LogTeleportBack", "{0} телепортировался на прежнее место."}
            }, this, "ru");
        }

        private void Init()
        {
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnPlayerDisconnected));
        }

        private void Loaded()
        {
            dataAdmin = GetFile(nameof(NTeleportation) + "Admin");
            Admin = dataAdmin.ReadObject<Dictionary<ulong, AdminData>>();
            dataHome = GetFile(nameof(NTeleportation) + "Home");
            Home = dataHome.ReadObject<Dictionary<ulong, HomeData>>();
            dataTPT = GetFile(nameof(NTeleportation) + "TPT");
            TPT = dataTPT.ReadObject<Dictionary<string, List<string>>>();
            dataTPR = GetFile(nameof(NTeleportation) + "TPR");
            TPR = dataTPR.ReadObject<Dictionary<ulong, TeleportData>>();
            dataTown = GetFile(nameof(NTeleportation) + "Town");
            Town = dataTown.ReadObject<Dictionary<ulong, TeleportData>>();
            dataOutpost = GetFile(nameof(NTeleportation) + "Outpost");
            Outpost = dataOutpost.ReadObject<Dictionary<ulong, TeleportData>>();
            dataBandit = GetFile(nameof(NTeleportation) + "Bandit");
            Bandit = dataBandit.ReadObject<Dictionary<ulong, TeleportData>>();
            dataDisabled = GetFile(nameof(NTeleportation) + "DisabledCommands");
            DisabledTPT = dataDisabled.ReadObject<DisabledData>();
            permission.RegisterPermission(PermDeleteHome, this);
            permission.RegisterPermission(PermHome, this);
            permission.RegisterPermission(PermHomeHomes, this);
            permission.RegisterPermission(PermImportHomes, this);
            permission.RegisterPermission(PermRadiusHome, this);
            permission.RegisterPermission(PermTp, this);
            permission.RegisterPermission(PermTpB, this);
            permission.RegisterPermission(PermTpR, this);
            permission.RegisterPermission(PermTpConsole, this);
            permission.RegisterPermission(PermTpHome, this);
            permission.RegisterPermission(PermTpTown, this);
            permission.RegisterPermission(PermTpT, this);
            permission.RegisterPermission(PermTpOutpost, this);
            permission.RegisterPermission(PermTpBandit, this);
            permission.RegisterPermission(PermTpN, this);
            permission.RegisterPermission(PermTpL, this);
            permission.RegisterPermission(PermTpRemove, this);
            permission.RegisterPermission(PermTpSave, this);
            permission.RegisterPermission(PermWipeHomes, this);
            permission.RegisterPermission(PermCraftHome, this);
            permission.RegisterPermission(PermCraftTown, this);
            permission.RegisterPermission(PermCraftOutpost, this);
            permission.RegisterPermission(PermCraftBandit, this);
            permission.RegisterPermission(PermCraftTpR, this);
            foreach (var key in config.Home.VIPCooldowns.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.Home.VIPCountdowns.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.Home.VIPDailyLimits.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.Home.VIPHomesLimits.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.TPR.VIPCooldowns.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.TPR.VIPCountdowns.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.TPR.VIPDailyLimits.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.Town.VIPCooldowns.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.Town.VIPCountdowns.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
            foreach (var key in config.Town.VIPDailyLimits.Keys)
                if (!permission.PermissionExists(key, this)) permission.RegisterPermission(key, this);
        }

        private DynamicConfigFile GetFile(string name)
        {
            var file = Interface.Oxide.DataFileSystem.GetFile(name);
            file.Settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            file.Settings.Converters = new JsonConverter[] { new UnityVector3Converter(), new CustomComparerDictionaryCreationConverter<string>(StringComparer.OrdinalIgnoreCase) };
            return file;
        }

        private void CheckNewSave()
        {
            if (!newSave && BuildingManager.server.buildingDictionary.Count == 0)
            {
                newSave = true;
            }

            if (!newSave)
            {
                return;
            }

            if (config.Settings.WipeOnUpgradeOrChange)
            {
                Puts("Rust was upgraded or map changed - clearing homes, town, outpost and bandit!");
                Home.Clear();
                changedHome = True;
                config.Town.Location = Zero;
                config.Outpost.Location = Zero;
                config.Bandit.Location = Zero;
                SaveConfig();
            }
            else
            {
                Puts("Rust was upgraded or map changed - homes, town, outpost and bandit may be invalid!");
            }
        }

        void OnServerInitialized()
        {
            CheckNewSave();
            banditPrefab = StringPool.Get(2074025910);
            banditEnabled = config.Settings.BanditEnabled;
            outpostPrefab = StringPool.Get(1879405026);
            outpostEnabled = config.Settings.OutpostEnabled;

            Subscribe(nameof(OnPlayerSleepEnded));
            Subscribe(nameof(OnPlayerDisconnected));

            boundary = TerrainMeta.Size.x / 2;
            CheckPerms(config.Home.VIPHomesLimits);
            CheckPerms(config.Home.VIPDailyLimits);
            CheckPerms(config.Home.VIPCooldowns);
            CheckPerms(config.TPR.VIPDailyLimits);
            CheckPerms(config.TPR.VIPCooldowns);
            CheckPerms(config.Town.VIPDailyLimits);
            CheckPerms(config.Town.VIPCooldowns);
            CheckPerms(config.Outpost.VIPDailyLimits);
            CheckPerms(config.Outpost.VIPCooldowns);
            CheckPerms(config.Bandit.VIPDailyLimits);
            CheckPerms(config.Bandit.VIPCooldowns);

            foreach (var item in config.Settings.BlockedItems)
            {
                var definition = ItemManager.FindItemDefinition(item.Key);
                if (definition == null)
                {
                    Puts("Blocked item not found: {0}", item.Key);
                    continue;
                }
                ReverseBlockedItems[definition.itemid] = item.Value;
            }

            if (CompoundTeleport == null)
            {
                if (outpostEnabled) AddCovalenceCommand("outpost", nameof(CommandOutpost));
                if (banditEnabled) AddCovalenceCommand("bandit", nameof(CommandBandit));
            }
            if (config.Settings.TownEnabled) AddCovalenceCommand("town", nameof(CommandTown));
            if (config.Settings.TPREnabled) AddCovalenceCommand("tpr", nameof(CommandTeleportRequest));
            if (config.Settings.HomesEnabled)
            {
                AddCovalenceCommand("home", nameof(CommandHome));
                AddCovalenceCommand("sethome", nameof(CommandSetHome));
                AddCovalenceCommand("listhomes", nameof(CommandListHomes));
                AddCovalenceCommand("removehome", nameof(CommandRemoveHome));
                AddCovalenceCommand("radiushome", nameof(CommandHomeRadius));
                AddCovalenceCommand("deletehome", nameof(CommandHomeDelete));
                AddCovalenceCommand("tphome", nameof(CommandHomeAdminTP));
                AddCovalenceCommand("homehomes", nameof(CommandHomeHomes));
            }

            AddCovalenceCommand("tnt", nameof(CommandToggle));
            AddCovalenceCommand("tp", nameof(CommandTeleport));
            AddCovalenceCommand("tpn", nameof(CommandTeleportNear));
            AddCovalenceCommand("tpl", nameof(CommandTeleportLocation));
            AddCovalenceCommand("tpsave", nameof(CommandSaveTeleportLocation));
            AddCovalenceCommand("tpremove", nameof(CommandRemoveTeleportLocation));
            AddCovalenceCommand("tpb", nameof(CommandTeleportBack));
            AddCovalenceCommand("tpt", nameof(CommandTeleportTeam));
            AddCovalenceCommand("tpa", nameof(CommandTeleportAccept));
            AddCovalenceCommand("wipehomes", nameof(CommandWipeHomes));
            AddCovalenceCommand("tphelp", nameof(CommandTeleportHelp));
            AddCovalenceCommand("tpinfo", nameof(CommandTeleportInfo));
            AddCovalenceCommand("tpc", nameof(CommandTeleportCancel));
            AddCovalenceCommand("teleport.toplayer", nameof(CommandTeleportII));
            AddCovalenceCommand("teleport.topos", nameof(CommandTeleportII));
            AddCovalenceCommand("teleport.importhomes", nameof(CommandImportHomes));
            AddCovalenceCommand("spm", nameof(CommandSphereMonuments));
            FindMonuments();  // 1.2.2 location moved from Loaded() to fix outpost and bandit location not being set after a wipe
        }

        List<string> validCommands = new List<string> { "outpost", "bandit", "tp", "home", "sethome", "listhomes", "tpn", "tpl", "tpsave", "tpremove", "tpb", "removehome", "radiushome", "deletehome", "tphome", "homehomes", "tpt", "tpr", "tpa", "wipehomes", "tphelp", "tpinfo", "teleport.toplayer", "teleport.topos", "teleport.importhomes", "town", "spm" };

        void OnNewSave(string strFilename)
        {
            newSave = true;
        }

        void OnServerSave()
        {
            SaveTeleportsAdmin();
            SaveTeleportsHome();
            SaveTeleportsTPR();
            SaveTeleportsTPT();
            SaveTeleportsTown();
            SaveTeleportsOutpost();
            SaveTeleportsBandit();
        }

        void OnServerShutdown() => OnServerSave();

        void Unload() => OnServerSave();

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "Economics")
            {
                Economics = plugin;
            }
            if (plugin.Name == "ServerRewards")
            {
                ServerRewards = plugin;
            }
            if (plugin.Name == "Friends")
            {
                Friends = plugin;
            }
            if (plugin.Name == "Clans")
            {
                Clans = plugin;
            }
            if (plugin.Name == "CompoundTeleport")
            {
                CompoundTeleport = plugin;
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "Economics")
            {
                Economics = null;
            }
            if (plugin.Name == "ServerRewards")
            {
                ServerRewards = null;
            }
            if (plugin.Name == "Friends")
            {
                Friends = null;
            }
            if (plugin.Name == "Clans")
            {
                Clans = null;
            }
            if (plugin.Name == "CompoundTeleport")
            {
                CompoundTeleport = null;
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var player = entity.ToPlayer();
            if (player == null || hitInfo == null) return;
            if (hitInfo.damageTypes.Has(DamageType.Fall) && teleporting.ContainsKey(player.userID))
            {
                hitInfo.damageTypes = new DamageTypeList();
                teleporting.Remove(player.userID);
            }
            TeleportTimer teleportTimer;
            if (!TeleportTimers.TryGetValue(player.userID, out teleportTimer)) return;
            DamageType major = hitInfo.damageTypes.GetMajorityDamageType();
            if (!config.Settings.Interrupt.Hurt) return;
            NextTick(() =>
            {
                if (!player) return;
                if (!hitInfo.hasDamage || hitInfo.damageTypes.Total() <= 0) return;
                // 1.0.84 new checks for cold/heat based on major damage for the player
                if (major == DamageType.Cold && config.Settings.Interrupt.Cold)
                {
                    if (player.metabolism.temperature.value <= config.Settings.MinimumTemp)
                    {
                        PrintMsgL(teleportTimer.OriginPlayer, "TPTooCold");
                        if (teleportTimer.TargetPlayer != null)
                        {
                            PrintMsgL(teleportTimer.TargetPlayer, "InterruptedTarget", teleportTimer.OriginPlayer?.displayName);
                        }
                        teleportTimer.Timer.Destroy();
                        TeleportTimers.Remove(player.userID);
                    }
                }
                else if (major == DamageType.Heat && config.Settings.Interrupt.Hot)
                {
                    if (player.metabolism.temperature.value >= config.Settings.MaximumTemp)
                    {
                        PrintMsgL(teleportTimer.OriginPlayer, "TPTooHot");
                        if (teleportTimer.TargetPlayer != null)
                        {
                            PrintMsgL(teleportTimer.TargetPlayer, "InterruptedTarget", teleportTimer.OriginPlayer?.displayName);
                        }
                        teleportTimer.Timer.Destroy();
                        TeleportTimers.Remove(player.userID);
                    }
                }
                else
                {
                    PrintMsgL(teleportTimer.OriginPlayer, "Interrupted");
                    if (teleportTimer.TargetPlayer != null)
                    {
                        PrintMsgL(teleportTimer.TargetPlayer, "InterruptedTarget", teleportTimer.OriginPlayer?.displayName);
                    }
                    teleportTimer.Timer.Destroy();
                    TeleportTimers.Remove(player.userID);
                }
            });
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!player || !teleporting.ContainsKey(player.userID)) return;
            ulong userID = player.userID;
            timer.Once(3f, () => teleporting.Remove(userID));
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (!player) return;
            Timer reqTimer;
            if (PendingRequests.TryGetValue(player.userID, out reqTimer))
            {
                var originPlayer = PlayersRequests[player.userID];
                if (originPlayer)
                {
                    PlayersRequests.Remove(originPlayer.userID);
                    PrintMsgL(originPlayer, "RequestTargetOff");
                }
                reqTimer.Destroy();
                PendingRequests.Remove(player.userID);
                PlayersRequests.Remove(player.userID);
            }
            TeleportTimer teleportTimer;
            if (TeleportTimers.TryGetValue(player.userID, out teleportTimer))
            {
                teleportTimer.Timer.Destroy();
                TeleportTimers.Remove(player.userID);
            }
            teleporting.Remove(player.userID);
        }

        private void SaveTeleportsAdmin()
        {
            if (Admin == null || !changedAdmin) return;
            dataAdmin.WriteObject(Admin);
            changedAdmin = False;
        }

        private void SaveTeleportsHome()
        {
            if (Home == null || !changedHome) return;
            dataHome.WriteObject(Home);
            changedHome = False;
        }

        private void SaveTeleportsTPR()
        {
            if (TPR == null || !changedTPR) return;
            dataTPR.WriteObject(TPR);
            changedTPR = False;
        }

        private void SaveTeleportsTPT()
        {
            if (TPT == null || !changedTPT) return;
            dataTPT.WriteObject(TPT);
            changedTPT = False;
        }

        private void SaveTeleportsTown()
        {
            if (Town == null || !changedTown) return;
            dataTown.WriteObject(Town);
            changedTown = False;
        }

        private void SaveTeleportsOutpost()
        {
            if (Outpost == null || !changedOutpost) return;
            dataOutpost.WriteObject(Outpost);
            changedOutpost = False;
        }

        private void SaveTeleportsBandit()
        {
            if (Bandit == null || !changedBandit) return;
            dataBandit.WriteObject(Bandit);
            changedBandit = False;
        }

        private void SaveLocation(BasePlayer player)
        {
            if (!IsAllowed(player, PermTpB)) return;
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData))
                Admin[player.userID] = adminData = new AdminData();
            adminData.PreviousLocation = player.transform.position;
            changedAdmin = True;
            PrintMsgL(player, "AdminTPBackSave");
        }

        char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
        private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();

        string RandomString(int minAmount = 5, int maxAmount = 10)
        {
            _sb.Length = 0;

            for (int i = 0; i <= UnityEngine.Random.Range(minAmount, maxAmount); i++)
                _sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);

            return _sb.ToString();
        }

        void FindMonuments()
        {
            var realWidth = 0f;
            string name = null;
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                var monPos = monument.transform.position;
                name = monument.displayPhrase.english.TrimEnd();
                if (string.IsNullOrEmpty(name))
                {
                    if (monument.name.Contains("cave"))
                    {
                        name = (monument.name.Contains("cave_small") ? "Small Cave" : monument.name.Contains("cave_medium") ? "Medium Cave" : "Large Cave") + ":" + RandomString();
                    }
                    else name = monument.name;
                }
                realWidth = monument.name == "OilrigAI" ? 100f : monument.name == "OilrigAI2" ? 200f : 0f;
#if DEBUG
                Puts($"Found {name}, extents {monument.Bounds.extents}");
#endif
                if (realWidth > 0f)
                {
#if DEBUG
                    Puts($"  corrected to {realWidth}");
#endif
                }
                if (monument.name.Contains("cave"))
                {
#if DEBUG
                    Puts("  Adding to cave list");
#endif
                    if (caves.ContainsKey(name)) name += RandomString();
                    caves.Add(name, monPos);
                }
                else if (monument.name == outpostPrefab)
                {
                    if (config.Outpost.Location != Zero && Vector3.Distance(monument.transform.position, config.Outpost.Location) > 100f)
                    {
#if DEBUG
                        Puts("Invalid Outpost location detected");
#endif
                        config.Outpost.Location = Zero;
                    }
                    if (config.Settings.AutoGenOutpost && config.Outpost.Location == Zero)
                    {
#if DEBUG
                        Puts("  Adding Outpost target");
#endif
                        var ents = new List<BaseEntity>();
                        Vis.Entities<BaseEntity>(monPos, 50, ents);
                        foreach (BaseEntity entity in ents)
                        {
                            if (entity.prefabID == 3858860623)
                            {
                                config.Outpost.Location = entity.transform.position + entity.transform.forward + new Vector3(0f, 1f, 0f);
                                SaveConfig();
                                break;
                            }
                            else if (entity is Workbench)
                            {
                                config.Outpost.Location = entity.transform.position + entity.transform.forward + new Vector3(0f, 1f, 0f);
                                SaveConfig();
                                break;
                            }
                            else if (entity is BaseChair)
                            {
                                config.Outpost.Location = entity.transform.position + entity.transform.right + new Vector3(0f, 1f, 0f);
                                SaveConfig();
                                break;
                            }
                        }

                        if (!config.Settings.OutpostEnabled) OutpostTPDisabledMessage = "OutpostTPDisabledConfig";
                        else if (config.Outpost.Location == Zero) OutpostTPDisabledMessage = "OutpostTPDisabledNoLocationAutoGen";
                    }
                }
                else if (monument.name == banditPrefab)
                {
                    if (config.Bandit.Location != Zero && Vector3.Distance(monument.transform.position, config.Bandit.Location) > 100f)
                    {
#if DEBUG
                        Puts("Invalid Bandit location detected");
#endif
                        config.Bandit.Location = Zero;
                    }
                    if (config.Settings.AutoGenBandit && config.Bandit.Location == Zero)
                    {
#if DEBUG
                        Puts("  Adding BanditTown target");
#endif
                        var ents = new List<BaseEntity>();
                        Vis.Entities<BaseEntity>(monPos, 50, ents);
                        foreach (BaseEntity entity in ents)
                        {
                            if (entity.prefabID == 3858860623)
                            {
                                config.Bandit.Location = entity.transform.position + entity.transform.forward + new Vector3(0f, 1f, 0f);
                                SaveConfig();
                                break;
                            }
                            else if (entity is Workbench)
                            {
                                config.Bandit.Location = entity.transform.position + entity.transform.forward + new Vector3(0f, 1f, 0f);
                                SaveConfig();
                                break;
                            }
                            else if (entity is BaseChair)
                            {
                                config.Bandit.Location = entity.transform.position + entity.transform.right + new Vector3(0f, 1f, 0f);
                                SaveConfig();
                                break;
                            }
                        }
                    }

                    if (!config.Settings.BanditEnabled) BanditTPDisabledMessage = "BanditTPDisabledConfig";
                    else if (config.Bandit.Location == Zero) BanditTPDisabledMessage = "BanditTPDisabledNoLocationAutoGen";
                }
                else
                {
                    if (monuments.ContainsKey(name)) name += ":" + RandomString(5, 5);
                    if (monument.name.Contains("power_sub")) name = monument.name.Substring(monument.name.LastIndexOf("/") + 1).Replace(".prefab", "") + ":" + RandomString(5, 5);
                    float radius = GetMonumentFloat(name);
                    monuments[name] = new MonInfo() { Position = monPos, Radius = radius };
#if DEBUG
                    Puts($"Adding Monument: {name}, pos: {monPos}, size: {radius}");
#endif
                }
            }

            if (config.Outpost.Location == Zero)
            {
                if (!config.Settings.AutoGenOutpost) OutpostTPDisabledMessage = "OutpostTPDisabledNoLocation";
                outpostEnabled = False;
            }

            if (config.Bandit.Location == Zero)
            {
                if (!config.Settings.AutoGenBandit) BanditTPDisabledMessage = "BanditTPDisabledNoLocation";
                banditEnabled = False;
            }
        }

        private void CommandToggle(IPlayer p, string command, string[] args)
        {
            if (!p.IsAdmin) return;

            if (args.Length == 0)
            {
                p.Reply("tnt commandname");
                return;
            }

            string arg = args[0].ToLower();

            if (!validCommands.Contains(arg))
            {
                p.Reply("Invalid command name: {0}", null, string.Join(", ", validCommands.ToList()));
                return;
            }

            if (arg == command.ToLower()) return;

            if (!DisabledTPT.DisabledCommands.Contains(arg))
                DisabledTPT.DisabledCommands.Add(arg);
            else DisabledTPT.DisabledCommands.Remove(arg);

            dataDisabled.WriteObject(DisabledTPT);
            p.Reply("{0} {1}", null, DisabledTPT.DisabledCommands.Contains(arg) ? "Disabled:" : "Enabled:", arg);
        }

        private void CommandTeleport(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTp) || !player.IsConnected || player.IsSleeping()) return;
            BasePlayer target;
            float x, y, z;
            switch (args.Length)
            {
                case 1:
                    target = FindPlayersSingle(args[0], player);
                    if (target == null) return;
                    if (target == player)
                    {
#if DEBUG
                        Puts("Debug mode - allowing self teleport.");
#else
                PrintMsgL(player, "CantTeleportToSelf");
                return;
#endif
                    }
                    Teleport(player, target);
                    PrintMsgL(player, "AdminTP", target.displayName);
                    Puts(_("LogTeleport", null, player.displayName, target.displayName));
                    if (config.Admin.AnnounceTeleportToTarget)
                        PrintMsgL(target, "AdminTPTarget", player.displayName);
                    break;
                case 2:
                    var origin = FindPlayersSingle(args[0], player);
                    if (origin == null) return;
                    target = FindPlayersSingle(args[1], player);
                    if (target == null) return;
                    if (target == origin)
                    {
                        PrintMsgL(player, "CantTeleportPlayerToSelf");
                        return;
                    }
                    Teleport(origin, target);
                    PrintMsgL(player, "AdminTPPlayers", origin.displayName, target.displayName);
                    PrintMsgL(origin, "AdminTPPlayer", player.displayName, target.displayName);
                    if (config.Admin.AnnounceTeleportToTarget)
                        PrintMsgL(target, "AdminTPPlayerTarget", player.displayName, origin.displayName);
                    Puts(_("LogTeleportPlayer", null, player.displayName, origin.displayName, target.displayName));
                    break;
                case 3:
                    if (!float.TryParse(args[0], out x) || !float.TryParse(args[1], out y) || !float.TryParse(args[2], out z))
                    {
                        PrintMsgL(player, "InvalidCoordinates");
                        return;
                    }
                    if (config.Settings.CheckBoundaries && !CheckBoundaries(x, y, z)) // added this option because I HATE boundaries
                    {
                        PrintMsgL(player, "AdminTPOutOfBounds");
                        PrintMsgL(player, "AdminTPBoundaries", boundary);
                        return;
                    }
                    Teleport(player, x, y, z);
                    PrintMsgL(player, "AdminTPCoordinates", player.transform.position);
                    Puts(_("LogTeleport", null, player.displayName, player.transform.position));
                    break;
                case 4:
                    target = FindPlayersSingle(args[0], player);
                    if (target == null) return;
                    if (!float.TryParse(args[1], out x) || !float.TryParse(args[2], out y) || !float.TryParse(args[3], out z))
                    {
                        PrintMsgL(player, "InvalidCoordinates");
                        return;
                    }
                    if (!CheckBoundaries(x, y, z))
                    {
                        PrintMsgL(player, "AdminTPOutOfBounds");
                        PrintMsgL(player, "AdminTPBoundaries", boundary);
                        return;
                    }
                    Teleport(target, x, y, z);
                    if (player == target)
                    {
                        PrintMsgL(player, "AdminTPCoordinates", player.transform.position);
                        Puts(_("LogTeleport", null, player.displayName, player.transform.position));
                    }
                    else
                    {
                        PrintMsgL(player, "AdminTPTargetCoordinates", target.displayName, player.transform.position);
                        if (config.Admin.AnnounceTeleportToTarget)
                            PrintMsgL(target, "AdminTPTargetCoordinatesTarget", player.displayName, player.transform.position);
                        Puts(_("LogTeleportPlayer", null, player.displayName, target.displayName, player.transform.position));
                    }
                    break;
                default:
                    PrintMsgL(player, "SyntaxCommandTP");
                    break;
            }
        }

        private void CommandTeleportNear(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpN) || !player.IsConnected || player.IsSleeping()) return;
            switch (args.Length)
            {
                case 1:
                case 2:
                    var target = FindPlayersSingle(args[0], player);
                    if (target == null) return;
                    if (target == player)
                    {
#if DEBUG
                        Puts("Debug mode - allowing self teleport.");
#else
                        PrintMsgL(player, "CantTeleportToSelf");
                        return;
#endif
                    }
                    int distance = config.Admin.TeleportNearDefaultDistance;
                    if (args.Length == 2 && !int.TryParse(args[1], out distance))
                        distance = config.Admin.TeleportNearDefaultDistance;
                    float x = UnityEngine.Random.Range(-distance, distance);
                    var z = (float)System.Math.Sqrt(System.Math.Pow(distance, 2) - System.Math.Pow(x, 2));
                    var destination = target.transform.position;
                    destination.x = destination.x - x;
                    destination.z = destination.z - z;
                    Teleport(player, GetGroundBuilding(destination));
                    PrintMsgL(player, "AdminTP", target.displayName);
                    Puts(_("LogTeleport", null, player.displayName, target.displayName));
                    if (config.Admin.AnnounceTeleportToTarget)
                        PrintMsgL(target, "AdminTPTarget", player.displayName);
                    break;
                default:
                    PrintMsgL(player, "SyntaxCommandTPN");
                    break;
            }
        }

        private void CommandTeleportLocation(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpL) || !player.IsConnected || player.IsSleeping()) return;
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData) || adminData.Locations.Count <= 0)
            {
                PrintMsgL(player, "AdminLocationListEmpty");
                return;
            }
            switch (args.Length)
            {
                case 0:
                    PrintMsgL(player, "AdminLocationList");
                    foreach (var location in adminData.Locations)
                        PrintMsgL(player, $"{location.Key} {location.Value}");
                    break;
                case 1:
                    Vector3 loc;
                    if (!adminData.Locations.TryGetValue(args[0], out loc))
                    {
                        PrintMsgL(player, "LocationNotFound");
                        return;
                    }
                    Teleport(player, loc);
                    PrintMsgL(player, "AdminTPLocation", args[0]);
                    break;
                default:
                    PrintMsgL(player, "SyntaxCommandTPL");
                    break;
            }
        }

        private void CommandSaveTeleportLocation(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpSave) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandTPSave");
                return;
            }
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData))
                Admin[player.userID] = adminData = new AdminData();
            Vector3 location;
            if (adminData.Locations.TryGetValue(args[0], out location))
            {
                PrintMsgL(player, "LocationExists", location);
                return;
            }
            var positionCoordinates = player.transform.position;
            foreach (var loc in adminData.Locations)
            {
                if ((positionCoordinates - loc.Value).magnitude < config.Admin.LocationRadius)
                {
                    PrintMsgL(player, "LocationExistsNearby", loc.Key);
                    return;
                }
            }
            adminData.Locations[args[0]] = positionCoordinates;
            PrintMsgL(player, "AdminTPLocationSave");
            changedAdmin = True;
        }

        private void CommandRemoveTeleportLocation(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpRemove) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandTPRemove");
                return;
            }
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData) || adminData.Locations.Count <= 0)
            {
                PrintMsgL(player, "AdminLocationListEmpty");
                return;
            }
            if (adminData.Locations.Remove(args[0]))
            {
                PrintMsgL(player, "AdminTPLocationRemove", args[0]);
                changedAdmin = True;
                return;
            }
            PrintMsgL(player, "LocationNotFound");
        }

        private void CommandTeleportBack(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpB) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandTPB");
                return;
            }
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData) || adminData.PreviousLocation == Zero)
            {
                PrintMsgL(player, "NoPreviousLocationSaved");
                return;
            }

            Teleport(player, adminData.PreviousLocation);
            adminData.PreviousLocation = Zero;
            changedAdmin = True;
            PrintMsgL(player, "AdminTPBack");
            Puts(_("LogTeleportBack", null, player.displayName));
        }

        private void CommandSetHome(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowed(player, PermHome) || !player.IsConnected || player.IsSleeping()) return;
            if (!config.Settings.HomesEnabled) { p.Reply("Homes are not enabled in the config."); return; }
            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandSetHome");
                return;
            }
            var err = CheckPlayer(player, False, CanCraftHome(player), True, "home");
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            if (!player.CanBuild())
            {
                PrintMsgL(player, "HomeTPBuildingBlocked");
                return;
            }
            if (!args[0].All(char.IsLetterOrDigit))
            {
                PrintMsgL(player, "InvalidCharacter");
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData))
                Home[player.userID] = homeData = new HomeData();
            var limit = GetHigher(player, config.Home.VIPHomesLimits, config.Home.HomesLimit, true);
            if (limit > 0 && homeData.Locations.Count >= limit)
            {
                PrintMsgL(player, "HomeMaxLocations", limit);
                return;
            }
            Vector3 location;
            if (homeData.Locations.TryGetValue(args[0], out location))
            {
                PrintMsgL(player, "HomeExists", location);
                return;
            }
            var positionCoordinates = player.transform.position;
            foreach (var loc in homeData.Locations)
            {
                if ((positionCoordinates - loc.Value).magnitude < config.Home.LocationRadius)
                {
                    PrintMsgL(player, "HomeExistsNearby", loc.Key);
                    return;
                }
            }
            err = CanPlayerTeleport(player);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }

            if (player.IsAdmin && config.Settings.DrawHomeSphere) player.SendConsoleCommand("ddraw.sphere", 30f, Color.blue, GetGround(positionCoordinates), 2.5f);

            err = CheckFoundation(player.userID, positionCoordinates);
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            err = CheckInsideBlock(positionCoordinates);
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            err = CheckInsideBattery(positionCoordinates);
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            homeData.Locations[args[0]] = positionCoordinates;
            changedHome = True;
            PrintMsgL(player, "HomeSave");
            PrintMsgL(player, "HomeQuota", homeData.Locations.Count, limit);
        }

        private void CommandRemoveHome(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            if (!config.Settings.HomesEnabled) { p.Reply("Homes are not enabled in the config."); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowed(player, PermHome) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandRemoveHome");
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData) || homeData.Locations.Count <= 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            if (homeData.Locations.Remove(args[0]))
            {
                changedHome = True;
                PrintMsgL(player, "HomeRemove", args[0]);
            }
            else
                PrintMsgL(player, "HomeNotFound");
        }

        private void CommandHome(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            if (!config.Settings.HomesEnabled) { p.Reply("Homes are not enabled in the config."); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowed(player, PermHome) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length == 0)
            {
                PrintMsgL(player, "SyntaxCommandHome");
                if (IsAllowed(player)) PrintMsgL(player, "SyntaxCommandHomeAdmin");
                return;
            }
            switch (args[0].ToLower())
            {
                case "add":
                    CommandSetHome(p, command, args.Skip(1).ToArray());
                    break;
                case "list":
                    CommandListHomes(p, command, args.Skip(1).ToArray());
                    break;
                case "remove":
                    CommandRemoveHome(p, command, args.Skip(1).ToArray());
                    break;
                case "radius":
                    CommandHomeRadius(p, command, args.Skip(1).ToArray());
                    break;
                case "delete":
                    CommandHomeDelete(p, command, args.Skip(1).ToArray());
                    break;
                case "tp":
                    CommandHomeAdminTP(p, command, args.Skip(1).ToArray());
                    break;
                case "homes":
                    CommandHomeHomes(p, command, args.Skip(1).ToArray());
                    break;
                case "wipe":
                    CommandWipeHomes(p, command, args.Skip(1).ToArray());
                    break;
                default:
                    cmdChatHomeTP(player, command, args);
                    break;
            }
        }

        private void CommandHomeRadius(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermRadiusHome) || !player.IsConnected || player.IsSleeping()) return;
            float radius;
            if (args.Length != 1 || !float.TryParse(args[0], out radius)) radius = 10;
            var found = False;
            foreach (var homeData in Home)
            {
                var toRemove = new List<string>();
                var target = RustCore.FindPlayerById(homeData.Key)?.displayName ?? homeData.Key.ToString();
                foreach (var location in homeData.Value.Locations)
                {
                    if ((player.transform.position - location.Value).magnitude <= radius)
                    {
                        if (CheckFoundation(homeData.Key, location.Value) != null)
                        {
                            toRemove.Add(location.Key);
                            continue;
                        }
                        var entity = GetFoundationOwned(location.Value, homeData.Key);
                        if (entity == null) continue;
                        player.SendConsoleCommand("ddraw.text", 30f, Color.blue, entity.CenterPoint() + new Vector3(0, .5f), $"<size=20>{target} - {location.Key} {location.Value}</size>");
                        DrawBox(player, entity.CenterPoint(), entity.transform.rotation, entity.bounds.size);
                        PrintMsg(player, $"{target} - {location.Key} {location.Value}");
                        found = True;
                    }
                }
                foreach (var loc in toRemove)
                {
                    homeData.Value.Locations.Remove(loc);
                    changedHome = True;
                }
            }
            if (!found)
                PrintMsgL(player, "HomeNoFound");
        }

        private void CommandHomeDelete(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermDeleteHome) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 2)
            {
                PrintMsgL(player, "SyntaxCommandHomeDelete");
                return;
            }
            var userId = FindPlayersSingleId(args[0], player);
            if (userId <= 0) return;
            HomeData targetHome;
            if (!Home.TryGetValue(userId, out targetHome) || !targetHome.Locations.Remove(args[1]))
            {
                PrintMsgL(player, "HomeNotFound");
                return;
            }
            changedHome = True;
            PrintMsgL(player, "HomeDelete", args[0], args[1]);
        }

        private void CommandHomeAdminTP(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpHome) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 2)
            {
                PrintMsgL(player, "SyntaxCommandHomeAdminTP");
                return;
            }
            var userId = FindPlayersSingleId(args[0], player);
            if (userId <= 0) return;
            HomeData targetHome;
            Vector3 location;
            if (!Home.TryGetValue(userId, out targetHome) || !targetHome.Locations.TryGetValue(args[1], out location))
            {
                PrintMsgL(player, "HomeNotFound");
                return;
            }

            Teleport(player, location);
            PrintMsgL(player, "HomeAdminTP", args[0], args[1]);
        }

        // Check that plugins are available and enabled for CheckEconomy()
        private bool UseEconomy()
        {
            if ((config.Settings.UseEconomics && Economics) ||
                (config.Settings.UseServerRewards && ServerRewards))
            {
                return True;
            }
            return False;
        }

        // Check balance on multiple plugins and optionally withdraw money from the player
        private bool CheckEconomy(BasePlayer player, double bypass, bool withdraw = False, bool deposit = False)
        {
            double balance = 0;
            bool foundmoney = False;

            // Check Economics first.  If not in use or balance low, check ServerRewards below
            if (config.Settings.UseEconomics && Economics)
            {
                balance = (double)Economics?.CallHook("Balance", player.UserIDString);
                if (balance >= bypass)
                {
                    foundmoney = True;
                    if (withdraw)
                    {
                        var w = (bool)Economics?.CallHook("Withdraw", player.userID, bypass);
                        return w;
                    }
                    else if (deposit)
                    {
                        Economics?.CallHook("Deposit", player.userID, bypass);
                    }
                }
            }

            // No money via Economics, or plugin not in use.  Try ServerRewards.
            if (config.Settings.UseServerRewards && ServerRewards)
            {
                object bal = ServerRewards?.Call("CheckPoints", player.userID);
                balance = Convert.ToDouble(bal);
                if (balance >= bypass && !foundmoney)
                {
                    foundmoney = True;
                    if (withdraw)
                    {
                        var w = (bool)ServerRewards?.Call("TakePoints", player.userID, (int)bypass);
                        return w;
                    }
                    else if (deposit)
                    {
                        ServerRewards?.Call("AddPoints", player.userID, (int)bypass);
                    }
                }
            }

            // Just checking balance without withdrawal - did we find anything?
            if (foundmoney)
            {
                return True;
            }
            return False;
        }

        private void cmdChatHomeTP(BasePlayer player, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { player.ChatMessage("Disabled command."); return; }
            if (!IsAllowed(player, PermHome) || !player.IsConnected || player.IsSleeping()) return;
            bool paidmoney = False;
            if (!config.Settings.HomesEnabled) { player.ChatMessage("Homes are not enabled in the config."); return; }
            if (args.Length < 1)
            {
                PrintMsgL(player, "SyntaxCommandHome");
                return;
            }
            var err = CheckPlayer(player, config.Home.UsableOutOfBuildingBlocked, CanCraftHome(player), True, "home");
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData) || homeData.Locations.Count <= 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            Vector3 location;
            if (!homeData.Locations.TryGetValue(args[0], out location))
            {
                PrintMsgL(player, "HomeNotFound");
                return;
            }
            err = CheckFoundation(player.userID, location) ?? CheckTargetLocation(player, location, config.Home.UsableIntoBuildingBlocked, config.Home.CupOwnerAllowOnBuildingBlocked);
            if (err != null)
            {
                PrintMsgL(player, "HomeRemovedInvalid", args[0]);
                homeData.Locations.Remove(args[0]);
                changedHome = True;
                return;
            }
            err = CheckInsideBlock(location);
            if (err != null)
            {
                PrintMsgL(player, "HomeRemovedInsideBlock", args[0]);
                homeData.Locations.Remove(args[0]);
                changedHome = True;
                return;
            }
            var timestamp = Facepunch.Math.Epoch.Current;
            var currentDate = DateTime.Now.ToString("d");
            if (homeData.Teleports.Date != currentDate)
            {
                homeData.Teleports.Amount = 0;
                homeData.Teleports.Date = currentDate;
            }
            var cooldown = GetLower(player, config.Home.VIPCooldowns, config.Home.Cooldown);
            if (cooldown > 0 && timestamp - homeData.Teleports.Timestamp < cooldown)
            {
                var cmdSent = "";
                bool foundmoney = CheckEconomy(player, config.Home.Bypass);
                try
                {
                    cmdSent = args[1].ToLower();
                }
                catch { }

                bool payalso = False;
                if (config.Home.Pay > 0)
                {
                    payalso = True;
                }
                if ((config.Settings.BypassCMD != null) && (cmdSent == config.Settings.BypassCMD.ToLower()))
                {
                    if (foundmoney)
                    {
                        CheckEconomy(player, config.Home.Bypass, True);
                        paidmoney = True;
                        PrintMsgL(player, "HomeTPCooldownBypass", config.Home.Bypass);
                        if (payalso)
                        {
                            PrintMsgL(player, "PayToHome", config.Home.Pay);
                        }
                    }
                    else
                    {
                        PrintMsgL(player, "HomeTPCooldownBypassF", config.Home.Bypass);
                        return;
                    }
                }
                else if (UseEconomy())
                {
                    var remain = cooldown - (timestamp - homeData.Teleports.Timestamp);
                    PrintMsgL(player, "HomeTPCooldown", FormatTime(remain));
                    if (config.Home.Bypass > 0 && config.Settings.BypassCMD != null)
                    {
                        PrintMsgL(player, "HomeTPCooldownBypassP", config.Home.Bypass);
                        PrintMsgL(player, "HomeTPCooldownBypassP2", config.Settings.BypassCMD);
                    }
                    return;
                }
                else
                {
                    var remain = cooldown - (timestamp - homeData.Teleports.Timestamp);
                    PrintMsgL(player, "HomeTPCooldown", FormatTime(remain));
                    return;
                }
            }
            var limit = GetHigher(player, config.Home.VIPDailyLimits, config.Home.DailyLimit, true);
            if (limit > 0 && homeData.Teleports.Amount >= limit)
            {
                PrintMsgL(player, "HomeTPLimitReached", limit);
                return;
            }
            if (TeleportTimers.ContainsKey(player.userID))
            {
                PrintMsgL(player, "TeleportPending");
                return;
            }
            err = CanPlayerTeleport(player);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }
            err = CheckItems(player);
            if (err != null)
            {
                PrintMsgL(player, "TPBlockedItem", err);
                return;
            }

            var countdown = GetLower(player, config.Home.VIPCountdowns, config.Home.Countdown);
            TeleportTimers[player.userID] = new TeleportTimer
            {
                OriginPlayer = player,
                Timer = timer.Once(countdown, () =>
                {
#if DEBUG
                    Puts("Calling CheckPlayer from cmdChatHomeTP");
#endif
                    err = CheckPlayer(player, config.Home.UsableOutOfBuildingBlocked, CanCraftHome(player), True, "home");
                    if (err != null)
                    {
                        PrintMsgL(player, "Interrupted");
                        PrintMsgL(player, err);
                        if (paidmoney)
                        {
                            paidmoney = False;
                            CheckEconomy(player, config.Home.Bypass, False, True);
                        }
                        TeleportTimers.Remove(player.userID);
                        return;
                    }
                    err = CanPlayerTeleport(player);
                    if (err != null)
                    {
                        PrintMsgL(player, "Interrupted");
                        PrintMsgL(player, err);
                        if (paidmoney)
                        {
                            paidmoney = False;
                            CheckEconomy(player, config.Home.Bypass, False, True);
                        }
                        TeleportTimers.Remove(player.userID);
                        return;
                    }
                    err = CheckItems(player);
                    if (err != null)
                    {
                        PrintMsgL(player, "Interrupted");
                        PrintMsgL(player, "TPBlockedItem", err);
                        if (paidmoney)
                        {
                            paidmoney = False;
                            CheckEconomy(player, config.Home.Bypass, False, True);
                        }
                        TeleportTimers.Remove(player.userID);
                        return;
                    }
                    err = CheckFoundation(player.userID, location) ?? CheckTargetLocation(player, location, config.Home.UsableIntoBuildingBlocked, config.Home.CupOwnerAllowOnBuildingBlocked);
                    if (err != null)
                    {
                        PrintMsgL(player, "HomeRemovedInvalid", args[0]);
                        homeData.Locations.Remove(args[0]);
                        changedHome = True;
                        if (paidmoney)
                        {
                            paidmoney = False;
                            CheckEconomy(player, config.Home.Bypass, False, True);
                        }
                        return;
                    }
                    err = CheckInsideBlock(location);
                    if (err != null)
                    {
                        PrintMsgL(player, "HomeRemovedInsideBlock", args[0]);
                        homeData.Locations.Remove(args[0]);
                        changedHome = True;
                        if (paidmoney)
                        {
                            paidmoney = False;
                            CheckEconomy(player, config.Home.Bypass, False, True);
                        }
                        return;
                    }

                    if (UseEconomy())
                    {
                        if (config.Home.Pay > 0 && !CheckEconomy(player, config.Home.Pay))
                        {
                            PrintMsgL(player, "Interrupted");
                            PrintMsgL(player, "TPNoMoney", config.Home.Pay);

                            TeleportTimers.Remove(player.userID);
                            return;
                        }
                        else if (config.Home.Pay > 0)
                        {
                            var w = CheckEconomy(player, (double)config.Home.Pay, True);
                            PrintMsgL(player, "TPMoney", (double)config.Home.Pay);
                        }
                    }
                    
                    Teleport(player, location);
                    homeData.Teleports.Amount++;
                    homeData.Teleports.Timestamp = timestamp;
                    changedHome = True;
                    PrintMsgL(player, "HomeTP", args[0]);
                    if (limit > 0) PrintMsgL(player, "HomeTPAmount", limit - homeData.Teleports.Amount);
                    TeleportTimers.Remove(player.userID);
                })
            };
            PrintMsgL(player, "HomeTPStarted", args[0], countdown);
        }

        private void CommandListHomes(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !player.IsConnected || player.IsSleeping()) return;
            if (!config.Settings.HomesEnabled) { p.Reply("Homes are not enabled in the config."); return; }
            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandListHomes");
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData) || homeData.Locations.Count <= 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            PrintMsgL(player, "HomeList");
            if (config.Home.CheckValidOnList)
            {
                var toRemove = new List<string>();
                foreach (var location in homeData.Locations)
                {
                    var err = CheckFoundation(player.userID, location.Value);
                    if (err != null)
                    {
                        toRemove.Add(location.Key);
                        continue;
                    }
                    PrintMsgL(player, $"{location.Key} {location.Value}");
                }
                foreach (var loc in toRemove)
                {
                    PrintMsgL(player, "HomeRemovedInvalid", loc);
                    homeData.Locations.Remove(loc);
                    changedHome = True;
                }
                return;
            }
            foreach (var location in homeData.Locations)
                PrintMsgL(player, $"{location.Key} {location.Value}");
        }

        private void CommandHomeHomes(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermHomeHomes) || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandHomeHomes");
                return;
            }
            var userId = FindPlayersSingleId(args[0], player);
            if (userId <= 0) return;
            HomeData homeData;
            if (!Home.TryGetValue(userId, out homeData) || homeData.Locations.Count <= 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            PrintMsgL(player, "HomeList");
            var toRemove = new List<string>();
            foreach (var location in homeData.Locations)
            {
                var err = CheckFoundation(userId, location.Value);
                if (err != null)
                {
                    toRemove.Add(location.Key);
                    continue;
                }
                PrintMsgL(player, $"{location.Key} {location.Value}");
            }
            foreach (var loc in toRemove)
            {
                PrintMsgL(player, "HomeRemovedInvalid", loc);
                homeData.Locations.Remove(loc);
                changedHome = True;
            }
        }

        private void CommandTeleportTeam(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            if (!config.TPT.UseClans && !config.TPT.UseFriends && !config.TPT.UseTeams)
                return;

            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpT))
                return;

            if (args.Length < 1)
            {
                PrintMsgL(player, "TPTInfo");
                return;
            }

            switch (args[0].ToLower())
            {
                case "friend":
                case "clan":
                case "team":
                    {
                        SetDisabled(player, args[0].ToLower());
                        return;
                    }
            }

            PrintMsgL(player, "TPTInfo");
        }

        public bool IsOnSameTeam(ulong playerId, ulong targetId)
        {
            if (!config.TPT.UseTeams || !IsEnabled(targetId.ToString(), "team"))
            {
                return false;
            }

            RelationshipManager.PlayerTeam team1;
            if (!RelationshipManager.Instance.playerToTeam.TryGetValue(playerId, out team1))
            {
                return false;
            }

            RelationshipManager.PlayerTeam team2;
            if (!RelationshipManager.Instance.playerToTeam.TryGetValue(targetId, out team2))
            {
                return false;
            }

            return team1.teamID == team2.teamID;
        }

        private bool AreFriends(string playerId, string targetId)
        {
            if (!config.TPT.UseFriends || !IsEnabled(targetId, "friend") || !Friends || !Friends.IsLoaded)
            {
                return False;
            }

            return Friends?.Call<bool>("AreFriends", playerId, targetId) ?? False;
        }

        private bool IsInSameClan(string playerId, string targetId)
        {
            if (!config.TPT.UseClans || !IsEnabled(targetId, "clan") || !Clans || !Clans.IsLoaded)
            {
                return false;
            }

            string targetClan = Clans?.Call<string>("GetClanOf", targetId);

            if (targetClan == null)
            {
                return false;
            }

            string playerClan = Clans?.Call<string>("GetClanOf", playerId);

            if (playerClan == null)
            {
                return false;
            }

            return targetClan == playerClan;
        }

        private void OnTeleportRequested(BasePlayer target, BasePlayer player)
        {
            if (IsInSameClan(player.UserIDString, target.UserIDString) || AreFriends(player.UserIDString, target.UserIDString) || IsOnSameTeam(player.userID, target.userID))
            {
                target.SendConsoleCommand("chat.say /tpa");
            }
        }

        bool IsEnabled(string targetId, string value)
        {
            if (TPT.ContainsKey(targetId) && TPT[targetId].Contains(value))
            {
                return false;
            }

            return true;
        }

        void SetDisabled(BasePlayer target, string value)
        {
            List<string> list;
            if (!TPT.TryGetValue(target.UserIDString, out list))
            {
                TPT[target.UserIDString] = list = new List<string>();
            }

            if (list.Contains(value))
            {
                list.Remove(value);
            }
            else
            {
                list.Add(value);
            }

            string status = lang.GetMessage($"TPT_{!list.Contains(value)}", this, target.UserIDString);
            string message = string.Format(lang.GetMessage($"TPT_{value}", this, target.UserIDString), status);

            PrintMsg(target, message);
            changedTPT = True;
        }

        private void CommandTeleportRequest(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermTpR) || !player.IsConnected || player.IsSleeping()) return;
            if (!config.Settings.TPREnabled) { p.Reply("TPR is not enabled in the config."); return; }
            if (args.Length == 0)
            {
                PrintMsgL(player, "SyntaxCommandTPR");
                return;
            }
            var targets = FindPlayersOnline(args[0]);
            if (targets.Count <= 0)
            {
                PrintMsgL(player, "PlayerNotFound");
                return;
            }
            if (targets.Count > 1)
            {
                PrintMsgL(player, "MultiplePlayers", string.Join(", ", targets.Select(x => x.displayName).ToArray()));
                return;
            }
            var target = targets[0];
            if (target == player && !player.IsAdmin)
            {
#if DEBUG
                Puts("Debug mode - allowing self teleport.");
#else
        PrintMsgL(player, "CantTeleportToSelf");
        return;
#endif
            }
#if DEBUG
            Puts("Calling CheckPlayer from cmdChatTeleportRequest");
#endif

            var err = CheckPlayer(player, config.TPR.UsableOutOfBuildingBlocked, CanCraftTPR(player), True, "tpr");
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            err = CheckTargetLocation(target, target.transform.position, config.TPR.UsableIntoBuildingBlocked, config.TPR.CupOwnerAllowOnBuildingBlocked);
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            var timestamp = Facepunch.Math.Epoch.Current;
            var currentDate = DateTime.Now.ToString("d");
            TeleportData tprData;
            if (!TPR.TryGetValue(player.userID, out tprData))
                TPR[player.userID] = tprData = new TeleportData();
            if (tprData.Date != currentDate)
            {
                tprData.Amount = 0;
                tprData.Date = currentDate;
            }

            var cooldown = player.IsAdmin ? 0 : GetLower(player, config.TPR.VIPCooldowns, config.TPR.Cooldown);
            if (cooldown > 0 && timestamp - tprData.Timestamp < cooldown)
            {
                var cmdSent = "";
                bool foundmoney = CheckEconomy(player, config.TPR.Bypass);
                try
                {
                    cmdSent = args[1].ToLower();
                }
                catch { }

                bool payalso = False;
                if (config.TPR.Pay > 0)
                {
                    payalso = True;
                }
                if ((config.Settings.BypassCMD != null) && (cmdSent == config.Settings.BypassCMD.ToLower()))
                {
                    if (foundmoney)
                    {
                        CheckEconomy(player, config.TPR.Bypass, True);
                        PrintMsgL(player, "TPRCooldownBypass", config.TPR.Bypass);
                        if (payalso)
                        {
                            PrintMsgL(player, "PayToTPR", config.TPR.Pay);
                        }
                    }
                    else
                    {
                        PrintMsgL(player, "TPRCooldownBypassF", config.TPR.Bypass);
                        return;
                    }
                }
                else if (UseEconomy())
                {
                    var remain = cooldown - (timestamp - tprData.Timestamp);
                    PrintMsgL(player, "TPRCooldown", FormatTime(remain));
                    if (config.TPR.Bypass > 0 && config.Settings.BypassCMD != null)
                    {
                        PrintMsgL(player, "TPRCooldownBypassP", config.TPR.Bypass);
                        if (payalso)
                        {
                            PrintMsgL(player, "PayToTPR", config.TPR.Pay);
                        }
                        PrintMsgL(player, "TPRCooldownBypassP2a", config.Settings.BypassCMD);
                    }
                    return;
                }
                else
                {
                    var remain = cooldown - (timestamp - tprData.Timestamp);
                    PrintMsgL(player, "TPRCooldown", FormatTime(remain));
                    return;
                }
            }
            var limit = GetHigher(player, config.TPR.VIPDailyLimits, config.TPR.DailyLimit, true);
            if (limit > 0 && tprData.Amount >= limit)
            {
                PrintMsgL(player, "TPRLimitReached", limit);
                return;
            }
            if (TeleportTimers.ContainsKey(player.userID))
            {
                PrintMsgL(player, "TeleportPending");
                return;
            }
            if (TeleportTimers.ContainsKey(target.userID))
            {
                PrintMsgL(player, "TeleportPendingTarget");
                return;
            }
            if (PlayersRequests.ContainsKey(player.userID))
            {
                PrintMsgL(player, "PendingRequest");
                return;
            }
            if (PlayersRequests.ContainsKey(target.userID))
            {
                PrintMsgL(player, "PendingRequestTarget");
                return;
            }
            err = CanPlayerTeleport(player);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }
            err = CanPlayerTeleport(target);
            if (err != null)
            {
                PrintMsgL(player, "TPRTarget");
                return;
            }
            err = CheckItems(player);
            if (err != null)
            {
                PrintMsgL(player, "TPBlockedItem", err);
                return;
            }

            PlayersRequests[player.userID] = target;
            PlayersRequests[target.userID] = player;
            PendingRequests[target.userID] = timer.Once(config.TPR.RequestDuration, () => { RequestTimedOut(player, target); });
            PrintMsgL(player, "Request", target.displayName);
            PrintMsgL(target, "RequestTarget", player.displayName);
            Interface.CallHook("OnTeleportRequested", target, player);
        }

        private void CommandTeleportAccept(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !player.IsConnected || player.IsSleeping()) return;
            if (!config.Settings.TPREnabled) { p.Reply("TPR is not enabled in the config."); return; }
            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandTPA");
                return;
            }
            Timer reqTimer;
            if (!PendingRequests.TryGetValue(player.userID, out reqTimer))
            {
                PrintMsgL(player, "NoPendingRequest");
                return;
            }
#if DEBUG
            Puts("Calling CheckPlayer from cmdChatTeleportAccept");
#endif
            var err = CheckPlayer(player, False, CanCraftTPR(player), False, "tpa");
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            err = CanPlayerTeleport(player);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }
            var originPlayer = PlayersRequests[player.userID];
            err = CheckTargetLocation(originPlayer, player.transform.position, config.TPR.UsableIntoBuildingBlocked, config.TPR.CupOwnerAllowOnBuildingBlocked);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }
            if (config.TPR.BlockTPAOnCeiling)
            {
                if (GetFloor(player.transform.position).Count > 0)
                {
                    PrintMsgL(player, "AcceptOnRoof");
                    return;
                }
            }
            var countdown = GetLower(originPlayer, config.TPR.VIPCountdowns, config.TPR.Countdown);
            PrintMsgL(originPlayer, "Accept", player.displayName, countdown);
            PrintMsgL(player, "AcceptTarget", originPlayer.displayName);
            var timestamp = Facepunch.Math.Epoch.Current;
            TeleportTimers[originPlayer.userID] = new TeleportTimer
            {
                OriginPlayer = originPlayer,
                TargetPlayer = player,
                Timer = timer.Once(countdown, () =>
                {
#if DEBUG
                    Puts("Calling CheckPlayer from cmdChatTeleportAccept timer loop");
#endif
                    err = CheckPlayer(originPlayer, config.TPR.UsableOutOfBuildingBlocked, CanCraftTPR(originPlayer), True, "tpa") ?? CheckPlayer(player, False, CanCraftTPR(player), True, "tpa");
                    if (err != null)
                    {
                        PrintMsgL(player, "InterruptedTarget", originPlayer.displayName);
                        PrintMsgL(originPlayer, "Interrupted");
                        PrintMsgL(originPlayer, err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    err = CheckTargetLocation(originPlayer, player.transform.position, config.TPR.UsableIntoBuildingBlocked, config.TPR.CupOwnerAllowOnBuildingBlocked);
                    if (err != null)
                    {
                        SendReply(player, err);
                        PrintMsgL(originPlayer, "Interrupted");
                        SendReply(originPlayer, err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    err = CanPlayerTeleport(originPlayer) ?? CanPlayerTeleport(player);
                    if (err != null)
                    {
                        SendReply(player, err);
                        PrintMsgL(originPlayer, "Interrupted");
                        SendReply(originPlayer, err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    err = CheckItems(originPlayer);
                    if (err != null)
                    {
                        PrintMsgL(player, "InterruptedTarget", originPlayer.displayName);
                        PrintMsgL(originPlayer, "Interrupted");
                        PrintMsgL(originPlayer, "TPBlockedItem", err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    if (UseEconomy())
                    {
                        if (config.TPR.Pay > 0)
                        {
                            if (!CheckEconomy(originPlayer, config.TPR.Pay))
                            {
                                PrintMsgL(player, "InterruptedTarget", originPlayer.displayName);
                                PrintMsgL(originPlayer, "TPNoMoney", config.TPR.Pay);
                                TeleportTimers.Remove(originPlayer.userID);
                                return;
                            }
                            else
                            {
                                CheckEconomy(originPlayer, config.TPR.Pay, True);
                                PrintMsgL(originPlayer, "TPMoney", (double)config.TPR.Pay);
                            }
                        }
                    }
                    Teleport(originPlayer, player.transform.position, config.TPR.AllowTPB);
                    var tprData = TPR[originPlayer.userID];
                    tprData.Amount++;
                    tprData.Timestamp = timestamp;
                    changedTPR = True;
                    PrintMsgL(player, "SuccessTarget", originPlayer.displayName);
                    PrintMsgL(originPlayer, "Success", player.displayName);
                    var limit = GetHigher(player, config.TPR.VIPDailyLimits, config.TPR.DailyLimit, true);
                    if (limit > 0) PrintMsgL(originPlayer, "TPRAmount", limit - tprData.Amount);
                    TeleportTimers.Remove(originPlayer.userID);
                })
            };
            reqTimer.Destroy();
            PendingRequests.Remove(player.userID);
            PlayersRequests.Remove(player.userID);
            PlayersRequests.Remove(originPlayer.userID);
        }

        private void CommandWipeHomes(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !IsAllowedMsg(player, PermWipeHomes) || !player.IsConnected || player.IsSleeping()) return;
            Home.Clear();
            changedHome = True;
            PrintMsgL(player, "HomesListWiped");
        }

        private void CommandTeleportHelp(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !player.IsConnected || player.IsSleeping()) return;
            if (!config.Settings.HomesEnabled && !config.Settings.TPREnabled && !IsAllowedMsg(player)) return;
            if (args.Length == 1)
            {
                var key = $"TPHelp{args[0].ToLower()}";
                var msg = _(key, player);
                if (key.Equals(msg))
                    PrintMsgL(player, "InvalidHelpModule");
                else
                    PrintMsg(player, msg);
            }
            else
            {
                var msg = _("TPHelpGeneral", player);
                if (IsAllowed(player))
                    msg += NewLine + "/tphelp AdminTP";
                if (config.Settings.HomesEnabled)
                    msg += NewLine + "/tphelp Home";
                if (config.Settings.TPREnabled)
                    msg += NewLine + "/tphelp TPR";
                PrintMsg(player, msg);
            }
        }

        private void CommandTeleportInfo(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            if (!config.Settings.HomesEnabled && !config.Settings.TPREnabled && !config.Settings.TownEnabled) { p.Reply($"{command} is not enabled in the config."); return; }
            var player = p.Object as BasePlayer;
            if (!player || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length == 1)
            {
                var module = args[0].ToLower();
                var msg = _($"TPSettings{module}", player);
                var timestamp = Facepunch.Math.Epoch.Current;
                var currentDate = DateTime.Now.ToString("d");
                TeleportData teleportData;
                int limit;
                int cooldown;
                switch (module)
                {
                    case "home":
                        limit = GetHigher(player, config.Home.VIPDailyLimits, config.Home.DailyLimit, true);
                        cooldown = GetLower(player, config.Home.VIPCooldowns, config.Home.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player), GetLower(player, config.Home.VIPHomesLimits, config.Home.HomesLimit)));
                        HomeData homeData;
                        if (!Home.TryGetValue(player.userID, out homeData))
                            Home[player.userID] = homeData = new HomeData();
                        if (homeData.Teleports.Date != currentDate)
                        {
                            homeData.Teleports.Amount = 0;
                            homeData.Teleports.Date = currentDate;
                        }
                        if (limit > 0) PrintMsgL(player, "HomeTPAmount", limit - homeData.Teleports.Amount);
                        if (cooldown > 0 && timestamp - homeData.Teleports.Timestamp < cooldown)
                        {
                            var remain = cooldown - (timestamp - homeData.Teleports.Timestamp);
                            PrintMsgL(player, "HomeTPCooldown", FormatTime(remain));
                        }
                        break;
                    case "tpr":
                        limit = GetHigher(player, config.TPR.VIPDailyLimits, config.TPR.DailyLimit, true);
                        cooldown = GetLower(player, config.TPR.VIPCooldowns, config.TPR.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!TPR.TryGetValue(player.userID, out teleportData))
                            TPR[player.userID] = teleportData = new TeleportData();
                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0) PrintMsgL(player, "TPRAmount", limit - teleportData.Amount);
                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            var remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "TPRCooldown", FormatTime(remain));
                        }
                        break;
                    case "town":
                        limit = GetHigher(player, config.Town.VIPDailyLimits, config.Town.DailyLimit, true);
                        cooldown = GetLower(player, config.Town.VIPCooldowns, config.Town.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!Town.TryGetValue(player.userID, out teleportData))
                            Town[player.userID] = teleportData = new TeleportData();
                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0) PrintMsgL(player, "TownTPAmount", limit - teleportData.Amount);
                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            var remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "TownTPCooldown", FormatTime(remain));
                            PrintMsgL(player, "TownTPCooldownBypassP", config.Town.Bypass);
                            PrintMsgL(player, "TownTPCooldownBypassP2", config.Settings.BypassCMD);
                        }
                        break;
                    case "outpost":
                        limit = GetHigher(player, config.Outpost.VIPDailyLimits, config.Outpost.DailyLimit, true);
                        cooldown = GetLower(player, config.Outpost.VIPCooldowns, config.Outpost.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!Outpost.TryGetValue(player.userID, out teleportData))
                            Outpost[player.userID] = teleportData = new TeleportData();
                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0) PrintMsgL(player, "OutpostTPAmount", limit - teleportData.Amount);
                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            var remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "OutpostTPCooldown", FormatTime(remain));
                            PrintMsgL(player, "OutpostTPCooldownBypassP", config.Outpost.Bypass);
                            PrintMsgL(player, "OutpostTPCooldownBypassP2", config.Settings.BypassCMD);
                        }
                        break;
                    case "bandit":
                        limit = GetHigher(player, config.Bandit.VIPDailyLimits, config.Bandit.DailyLimit, true);
                        cooldown = GetLower(player, config.Bandit.VIPCooldowns, config.Bandit.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!Bandit.TryGetValue(player.userID, out teleportData))
                            Bandit[player.userID] = teleportData = new TeleportData();
                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0) PrintMsgL(player, "BanditTPAmount", limit - teleportData.Amount);
                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            var remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "BanditTPCooldown", FormatTime(remain));
                            PrintMsgL(player, "BanditTPCooldownBypassP", config.Bandit.Bypass);
                            PrintMsgL(player, "BanditTPCooldownBypassP2", config.Settings.BypassCMD);
                        }
                        break;
                    default:
                        PrintMsgL(player, "InvalidHelpModule");
                        break;
                }
            }
            else
            {
                var msg = _("TPInfoGeneral", player);
                if (config.Settings.HomesEnabled)
                    msg += NewLine + "/tpinfo Home";
                if (config.Settings.TPREnabled)
                    msg += NewLine + "/tpinfo TPR";
                if (config.Settings.TownEnabled)
                    msg += NewLine + "/tpinfo Town";
                if (outpostEnabled)
                    msg += NewLine + "/tpinfo Outpost";
                if (banditEnabled)
                    msg += NewLine + "/tpinfo Bandit";
                PrintMsgL(player, msg);
            }
        }

        private void CommandTeleportCancel(IPlayer p, string command, string[] args)
        {
            var player = p.Object as BasePlayer;
            if (!player || !player.IsConnected || player.IsSleeping()) return;
            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandTPC");
                return;
            }
            TeleportTimer teleportTimer;
            if (TeleportTimers.TryGetValue(player.userID, out teleportTimer))
            {
                teleportTimer.Timer?.Destroy();
                PrintMsgL(player, "TPCancelled");
                PrintMsgL(teleportTimer.TargetPlayer, "TPCancelledTarget", player.displayName);
                TeleportTimers.Remove(player.userID);
                return;
            }
            foreach (var keyValuePair in TeleportTimers)
            {
                if (keyValuePair.Value.TargetPlayer != player) continue;
                keyValuePair.Value.Timer?.Destroy();
                PrintMsgL(keyValuePair.Value.OriginPlayer, "TPCancelledTarget", player.displayName);
                PrintMsgL(player, "TPYouCancelledTarget", keyValuePair.Value.OriginPlayer.displayName);
                TeleportTimers.Remove(keyValuePair.Key);
                return;
            }
            BasePlayer target;
            if (!PlayersRequests.TryGetValue(player.userID, out target))
            {
                PrintMsgL(player, "NoPendingRequest");
                return;
            }
            Timer reqTimer;
            if (PendingRequests.TryGetValue(player.userID, out reqTimer))
            {
                reqTimer.Destroy();
                PendingRequests.Remove(player.userID);
            }
            else if (PendingRequests.TryGetValue(target.userID, out reqTimer))
            {
                reqTimer.Destroy();
                PendingRequests.Remove(target.userID);
                var temp = player;
                player = target;
                target = temp;
            }
            PlayersRequests.Remove(target.userID);
            PlayersRequests.Remove(player.userID);
            PrintMsgL(player, "Cancelled", target.displayName);
            PrintMsgL(target, "CancelledTarget", player.displayName);
        }

        private void CommandOutpost(IPlayer p, string command, string[] args)
        {
            CommandTown(p, "outpost", args);
        }

        private void CommandBandit(IPlayer p, string command, string[] args)
        {
            CommandTown(p, "bandit", args);
        }

        private void CommandTown(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (!player || !player.IsConnected || player.IsSleeping()) return;
#if DEBUG
            Puts($"cmdChatTown: command={command}");
#endif
            switch (command)
            {
                case "outpost":
                    if (!IsAllowedMsg(player, PermTpOutpost)) return;
                    break;
                case "bandit":
                    if (!IsAllowedMsg(player, PermTpBandit)) return;
                    break;
                case "town":
                default:
                    if (!IsAllowedMsg(player, PermTpTown)) return;
                    break;
            }

            // For admin using set command
            if (args.Length == 1 && IsAllowed(player) && args[0].ToLower().Equals("set"))
            {
                switch (command)
                {
                    case "outpost":
                        config.Outpost.Location = player.transform.position;
                        SaveConfig();
                        PrintMsgL(player, "OutpostTPLocation", config.Outpost.Location);
                        break;
                    case "bandit":
                        config.Bandit.Location = player.transform.position;
                        SaveConfig();
                        PrintMsgL(player, "BanditTPLocation", config.Bandit.Location);
                        break;
                    case "town":
                    default:
                        config.Town.Location = player.transform.position;
                        SaveConfig();
                        PrintMsgL(player, "TownTPLocation", config.Town.Location);
                        break;
                }
                return;
            }

            bool paidmoney = False;

            // Is outpost/bandit/town usage enabled?
            if (!outpostEnabled && command == "outpost")
            {
                PrintMsgL(player, OutpostTPDisabledMessage);
                return;
            }
            else if (!banditEnabled && command == "bandit")
            {
                PrintMsgL(player, BanditTPDisabledMessage);
                return;
            }
            else if (!config.Settings.TownEnabled && command == "town")
            {
                PrintMsgL(player, "TownTPDisabled");
                return;
            }

            // Are they trying to bypass cooldown or did they just type something else?
            if (args.Length == 1 && (args[0].ToLower() != config.Settings.BypassCMD.ToLower()))
            {
                string com = command ?? "town";
                string msg = "SyntaxCommand" + char.ToUpper(com[0]) + com.Substring(1);
                PrintMsgL(player, msg);
                if (IsAllowed(player)) PrintMsgL(player, msg + "Admin");
                return;
            }

            // Is outpost/bandit/town location set?
            if (config.Outpost.Location == Zero && command == "outpost")
            {
                PrintMsgL(player, "OutpostTPNotSet");
                return;
            }
            else if (config.Bandit.Location == Zero && command == "bandit")
            {
                PrintMsgL(player, "BanditTPNotSet");
                return;
            }
            else if (config.Town.Location == Zero && command == "town")
            {
                PrintMsgL(player, "TownTPNotSet");
                return;
            }

            TeleportData teleportData = new TeleportData();
            var timestamp = Facepunch.Math.Epoch.Current;
            var currentDate = DateTime.Now.ToString("d");

            string err = null;
            int cooldown = 0;
            int limit = 0;
            int targetPay = 0;
            int targetBypass = 0;
            string msgPay = null;
            string msgCooldown = null;
            string msgCooldownBypass = null;
            string msgCooldownBypassF = null;
            string msgCooldownBypassP = null;
            string msgCooldownBypassP2 = null;
            string msgLimitReached = null;
#if DEBUG
            Puts("Calling CheckPlayer from cmdChatTown");
#endif
            // Setup vars for checks below
            switch (command)
            {
                case "outpost":
                    err = CheckPlayer(player, config.Outpost.UsableOutOfBuildingBlocked, CanCraftOutpost(player), True, "outpost");
                    if (err != null)
                    {
                        PrintMsgL(player, err);
                        if (err == "TPHostile")
                        {
                            double stateHostileTime = Math.Round((player.State.unHostileTimestamp - TimeEx.currentTimestamp) / 60, 0, MidpointRounding.AwayFromZero);
                            PrintMsgL(player, "HostileTimer", stateHostileTime);
                        }
                        return;
                    }
                    cooldown = GetLower(player, config.Outpost.VIPCooldowns, config.Outpost.Cooldown);
                    if (!Outpost.TryGetValue(player.userID, out teleportData))
                    {
                        Outpost[player.userID] = teleportData = new TeleportData();
                    }
                    if (teleportData.Date != currentDate)
                    {
                        teleportData.Amount = 0;
                        teleportData.Date = currentDate;
                    }

                    targetPay = config.Outpost.Pay;
                    targetBypass = config.Outpost.Bypass;

                    msgPay = "PayToOutpost";
                    msgCooldown = "OutpostTPCooldown";
                    msgCooldownBypass = "OutpostTPCooldownBypass";
                    msgCooldownBypassF = "OutpostTPCooldownBypassF";
                    msgCooldownBypassP = "OutpostTPCooldownBypassP";
                    msgCooldownBypassP2 = "OutpostTPCooldownBypassP2";
                    msgLimitReached = "OutpostTPLimitReached";
                    limit = GetHigher(player, config.Outpost.VIPDailyLimits, config.Outpost.DailyLimit, true);
                    break;
                case "bandit":
                    err = CheckPlayer(player, config.Bandit.UsableOutOfBuildingBlocked, CanCraftBandit(player), True, "bandit");
                    if (err != null)
                    {
                        PrintMsgL(player, err);
                        if (err == "TPHostile")
                        {
                            double stateHostileTime = Math.Round((player.State.unHostileTimestamp - TimeEx.currentTimestamp) / 60, 0, MidpointRounding.AwayFromZero);
                            PrintMsgL(player, "HostileTimer", stateHostileTime);
                        }
                        return;
                    }
                    cooldown = GetLower(player, config.Bandit.VIPCooldowns, config.Bandit.Cooldown);
                    if (!Bandit.TryGetValue(player.userID, out teleportData))
                    {
                        Bandit[player.userID] = teleportData = new TeleportData();
                    }
                    if (teleportData.Date != currentDate)
                    {
                        teleportData.Amount = 0;
                        teleportData.Date = currentDate;
                    }
                    targetPay = config.Bandit.Pay;
                    targetBypass = config.Bandit.Bypass;

                    msgPay = "PayToBandit";
                    msgCooldown = "BanditTPCooldown";
                    msgCooldownBypass = "BanditTPCooldownBypass";
                    msgCooldownBypassF = "BanditTPCooldownBypassF";
                    msgCooldownBypassP = "BanditTPCooldownBypassP";
                    msgCooldownBypassP2 = "BanditTPCooldownBypassP2";
                    msgLimitReached = "BanditTPLimitReached";
                    limit = GetHigher(player, config.Bandit.VIPDailyLimits, config.Bandit.DailyLimit, true);
                    break;
                case "town":
                default:
                    err = CheckPlayer(player, config.Town.UsableOutOfBuildingBlocked, CanCraftTown(player), True, "town");
                    if (err != null)
                    {
                        PrintMsgL(player, err);
                        return;
                    }
                    cooldown = GetLower(player, config.Town.VIPCooldowns, config.Town.Cooldown);
                    if (!Town.TryGetValue(player.userID, out teleportData))
                    {
                        Town[player.userID] = teleportData = new TeleportData();
                    }
                    if (teleportData.Date != currentDate)
                    {
                        teleportData.Amount = 0;
                        teleportData.Date = currentDate;
                    }
                    targetPay = config.Town.Pay;
                    targetBypass = config.Town.Bypass;

                    msgPay = "PayToTown";
                    msgCooldown = "TownTPCooldown";
                    msgCooldownBypass = "TownTPCooldownBypass";
                    msgCooldownBypassF = "TownTPCooldownBypassF";
                    msgCooldownBypassP = "TownTPCooldownBypassP";
                    msgCooldownBypassP2 = "TownTPCooldownBypassP2";
                    msgLimitReached = "TownTPLimitReached";
                    limit = GetHigher(player, config.Town.VIPDailyLimits, config.Town.DailyLimit, true);
                    break;
            }

            // Check and process cooldown, bypass, and payment for all modes
            if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
            {
                var cmdSent = "";
                bool foundmoney = CheckEconomy(player, targetBypass);
                try
                {
                    cmdSent = args[0].ToLower();
                }
                catch { }

                bool payalso = False;
                if (targetPay > 0)
                {
                    payalso = True;
                }
                if ((config.Settings.BypassCMD != null) && (cmdSent == config.Settings.BypassCMD.ToLower()))
                {
                    if (foundmoney)
                    {
                        CheckEconomy(player, targetBypass, True);
                        paidmoney = True;
                        PrintMsgL(player, msgCooldownBypass, targetBypass);
                        if (payalso)
                        {
                            PrintMsgL(player, msgPay, targetPay);
                        }
                    }
                    else
                    {
                        PrintMsgL(player, msgCooldownBypassF, targetBypass);
                        return;
                    }
                }
                else if (UseEconomy())
                {
                    var remain = cooldown - (timestamp - teleportData.Timestamp);
                    PrintMsgL(player, msgCooldown, FormatTime(remain));
                    if (targetBypass > 0 && config.Settings.BypassCMD != null)
                    {
                        PrintMsgL(player, msgCooldownBypassP, targetBypass);
                        PrintMsgL(player, msgCooldownBypassP2, config.Settings.BypassCMD);
                    }
                    return;
                }
                else
                {
                    var remain = cooldown - (timestamp - teleportData.Timestamp);
                    PrintMsgL(player, msgCooldown, FormatTime(remain));
                    return;
                }
            }

            if (limit > 0 && teleportData.Amount >= limit)
            {
                PrintMsgL(player, msgLimitReached, limit);
                return;
            }
            if (TeleportTimers.ContainsKey(player.userID))
            {
                PrintMsgL(player, "TeleportPending");
                return;
            }
            err = CanPlayerTeleport(player);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }
            err = CheckItems(player);
            if (err != null)
            {
                PrintMsgL(player, "TPBlockedItem", err);
                return;
            }

            int countdown = 0;
            switch (command)
            {
                case "outpost":
                    countdown = GetLower(player, config.Outpost.VIPCountdowns, config.Outpost.Countdown);
                    TeleportTimers[player.userID] = new TeleportTimer
                    {
                        OriginPlayer = player,
                        Timer = timer.Once(countdown, () =>
                        {
#if DEBUG
                            Puts("Calling CheckPlayer from cmdChatTown outpost timer loop");
#endif
                            err = CheckPlayer(player, config.Outpost.UsableOutOfBuildingBlocked, CanCraftOutpost(player), True, "outpost");
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Outpost.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CanPlayerTeleport(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Outpost.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CheckItems(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, "TPBlockedItem", err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Outpost.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }

                            if (UseEconomy())
                            {
                                if (config.Outpost.Pay > 0 && !CheckEconomy(player, config.Outpost.Pay))
                                {
                                    PrintMsgL(player, "Interrupted");
                                    PrintMsgL(player, "TPNoMoney", config.Outpost.Pay);
                                    TeleportTimers.Remove(player.userID);
                                    return;
                                }
                                else if (config.Outpost.Pay > 0)
                                {
                                    CheckEconomy(player, config.Outpost.Pay, True);
                                    PrintMsgL(player, "TPMoney", (double)config.Outpost.Pay);
                                }
                            }
                            Teleport(player, config.Outpost.Location);
                            teleportData.Amount++;
                            teleportData.Timestamp = timestamp;

                            changedOutpost = True;
                            PrintMsgL(player, "OutpostTP");
                            if (limit > 0) PrintMsgL(player, "OutpostTPAmount", limit - teleportData.Amount);
                            TeleportTimers.Remove(player.userID);
                        })
                    };
                    PrintMsgL(player, "OutpostTPStarted", countdown);
                    break;
                case "bandit":
                    countdown = GetLower(player, config.Bandit.VIPCountdowns, config.Bandit.Countdown);
                    TeleportTimers[player.userID] = new TeleportTimer
                    {
                        OriginPlayer = player,
                        Timer = timer.Once(countdown, () =>
                        {
#if DEBUG
                            Puts("Calling CheckPlayer from cmdChatTown bandit timer loop");
#endif
                            err = CheckPlayer(player, config.Bandit.UsableOutOfBuildingBlocked, CanCraftBandit(player), True, "bandit");
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Bandit.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CanPlayerTeleport(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Bandit.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CheckItems(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, "TPBlockedItem", err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Bandit.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }

                            if (UseEconomy())
                            {
                                if (config.Bandit.Pay > 0 && !CheckEconomy(player, config.Bandit.Pay))
                                {
                                    PrintMsgL(player, "Interrupted");
                                    PrintMsgL(player, "TPNoMoney", config.Bandit.Pay);
                                    TeleportTimers.Remove(player.userID);
                                    return;
                                }
                                else if (config.Bandit.Pay > 0)
                                {
                                    CheckEconomy(player, config.Bandit.Pay, True);
                                    PrintMsgL(player, "TPMoney", (double)config.Bandit.Pay);
                                }
                            }
                            Teleport(player, config.Bandit.Location);
                            teleportData.Amount++;
                            teleportData.Timestamp = timestamp;

                            changedBandit = True;
                            PrintMsgL(player, "BanditTP");
                            if (limit > 0) PrintMsgL(player, "BanditTPAmount", limit - teleportData.Amount);
                            TeleportTimers.Remove(player.userID);
                        })
                    };
                    PrintMsgL(player, "BanditTPStarted", countdown);
                    break;
                case "town":
                default:
                    countdown = GetLower(player, config.Town.VIPCountdowns, config.Town.Countdown);
                    TeleportTimers[player.userID] = new TeleportTimer
                    {
                        OriginPlayer = player,
                        Timer = timer.Once(countdown, () =>
                        {
#if DEBUG
                            Puts("Calling CheckPlayer from cmdChatTown town timer loop");
#endif
                            err = CheckPlayer(player, config.Town.UsableOutOfBuildingBlocked, CanCraftTown(player), True, "town");
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Town.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CanPlayerTeleport(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Town.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CheckItems(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, "TPBlockedItem", err);
                                if (paidmoney)
                                {
                                    paidmoney = False;
                                    CheckEconomy(player, config.Town.Bypass, False, True);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }

                            if (UseEconomy())
                            {
                                if (config.Town.Pay > 0 && !CheckEconomy(player, config.Town.Pay))
                                {
                                    PrintMsgL(player, "Interrupted");
                                    PrintMsgL(player, "TPNoMoney", config.Town.Pay);
                                    TeleportTimers.Remove(player.userID);
                                    return;
                                }
                                else if (config.Town.Pay > 0)
                                {
                                    CheckEconomy(player, config.Town.Pay, True);
                                    PrintMsgL(player, "TPMoney", (double)config.Town.Pay);
                                }
                            }
                            Teleport(player, config.Town.Location);
                            teleportData.Amount++;
                            teleportData.Timestamp = timestamp;

                            changedTown = True;
                            PrintMsgL(player, "TownTP");
                            if (limit > 0) PrintMsgL(player, "TownTPAmount", limit - teleportData.Amount);
                            TeleportTimers.Remove(player.userID);
                        })
                    };
                    PrintMsgL(player, "TownTPStarted", countdown);
                    break;
            }
        }

        private void CommandTeleportII(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;
            if (player != null && (!IsAllowedMsg(player, PermTpConsole) || !player.IsConnected || player.IsSleeping())) return;
            
            List<BasePlayer> players;
            switch (command)
            {
                case "teleport.topos":
                    if (args.Length < 4)
                    {
                        p.Reply(_("SyntaxConsoleCommandToPos", player));
                        return;
                    }
                    players = FindPlayers(args[0]);
                    if (players.Count <= 0)
                    {
                        p.Reply(_("PlayerNotFound", player));
                        return;
                    }
                    if (players.Count > 1)
                    {
                        p.Reply(_("MultiplePlayers", player, string.Join(", ", players.Select(t => t.displayName).ToArray())));
                        return;
                    }
                    var targetPlayer = players.First();
                    players.Clear();
                    float x;
                    if (!float.TryParse(args[1], out x)) x = -10000f;
                    float y;
                    if (!float.TryParse(args[2], out y)) y = -10000f;
                    float z;
                    if (!float.TryParse(args[3], out z)) z = -10000f;
                    if (!CheckBoundaries(x, y, z))
                    {
                        p.Reply(_("AdminTPOutOfBounds", player) + Environment.NewLine + _("AdminTPBoundaries", player, boundary));
                        return;
                    }
                    Teleport(targetPlayer, x, y, z);
                    if (config.Admin.AnnounceTeleportToTarget)
                        PrintMsgL(targetPlayer, "AdminTPConsoleTP", targetPlayer.transform.position);
                    p.Reply(_("AdminTPTargetCoordinates", player, targetPlayer.displayName, targetPlayer.transform.position));
                    Puts(_("LogTeleportPlayer", null, player?.displayName, targetPlayer.displayName, targetPlayer.transform.position));
                    break;
                case "teleport.toplayer":
                    if (args.Length < 2)
                    {
                        p.Reply(_("SyntaxConsoleCommandToPlayer", player));
                        return;
                    }
                    players = FindPlayers(args[0]);
                    if (players.Count <= 0)
                    {
                        p.Reply(_("PlayerNotFound", player));
                        return;
                    }
                    if (players.Count > 1)
                    {
                        p.Reply(_("MultiplePlayers", player, string.Join(", ", players.Select(t => t.displayName).ToArray())));
                        return;
                    }
                    var originPlayer = players.First();
                    players = FindPlayers(args[1]);
                    if (players.Count <= 0)
                    {
                        p.Reply(_("PlayerNotFound", player));
                        return;
                    }
                    if (players.Count > 1)
                    {
                        p.Reply(_("MultiplePlayers", player, string.Join(", ", players.Select(t => t.displayName).ToArray())));
                        players.Clear();
                        return;
                    }
                    targetPlayer = players.First();
                    if (targetPlayer == originPlayer)
                    {
                        players.Clear();
                        p.Reply(_("CantTeleportPlayerToSelf", player));
                        return;
                    }
                    players.Clear();
                    Teleport(originPlayer, targetPlayer);
                    p.Reply(_("AdminTPPlayers", player, originPlayer.displayName, targetPlayer.displayName));
                    PrintMsgL(originPlayer, "AdminTPConsoleTPPlayer", targetPlayer.displayName);
                    if (config.Admin.AnnounceTeleportToTarget)
                        PrintMsgL(targetPlayer, "AdminTPConsoleTPPlayerTarget", originPlayer.displayName);
                    Puts(_("LogTeleportPlayer", null, player?.displayName, originPlayer.displayName, targetPlayer.displayName));
                    break;
            }
        }

        float GetMonumentFloat(string monumentName)
        {
            string name = monumentName.Contains(":") ? monumentName.Substring(0, monumentName.LastIndexOf(":")) : monumentName.TrimEnd();

            switch (name)
            {
                case "Abandoned Cabins":
                    return 24f + 30f;
                case "Abandoned Supermarket":
                    return 50f;
                case "Airfield":
                    return 200f;
                case "Bandit Camp":
                    return 100f + 25f;
                case "Giant Excavator Pit":
                    return 200f + 25f;
                case "Harbor":
                    return 100f + 50f;
                case "HQM Quarry":
                    return 27.5f + 10f;
                case "Large Oil Rig":
                    return 200f;
                case "Launch Site":
                    return 200f + 100f;
                case "Lighthouse":
                    return 24f + 24f;
                case "Military Tunnel":
                    return 100f;
                case "Mining Outpost":
                    return 25f + 15f;
                case "Oil Rig":
                    return 100f;
                case "Outpost":
                    return 100f + 25f;
                case "Oxum's Gas Station":
                    return 50f + 15f;
                case "Power Plant":
                    return 100f + 40f;
                case "power_sub_small_1":
                case "power_sub_small_2":
                case "power_sub_big_1":
                case "power_sub_big_2":
                    return 30f;
                case "Satellite Dish":
                    return 75f + 15f;
                case "Sewer Branch":
                    return 75f + 25f;
                case "Stone Quarry":
                    return 27.5f;
                case "Sulfur Quarry":
                    return 27.5f;
                case "The Dome":
                    return 50f + 20f;
                case "Train Yard":
                    return 100 + 50f;
                case "Water Treatment Plant":
                    return 100f + 85f;
                case "Water Well":
                    return 24f;
                case "Wild Swamp":
                    return 24f;
            }

            return config.Settings.DefaultMonumentSize;
        }

        private void CommandSphereMonuments(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p?.Object as BasePlayer;
            if (!player || !player.IsAdmin || !player.IsConnected || player.IsSleeping()) return;

            foreach (var monument in monuments)
            {
                string name = monument.Key.Contains(":") ? monument.Key.Substring(0, monument.Key.LastIndexOf(":")) : monument.Key.TrimEnd();

                player.SendConsoleCommand("ddraw.sphere", 30f, Color.red, monument.Value.Position, GetMonumentFloat(name));
                player.SendConsoleCommand("ddraw.text", 30f, Color.blue, monument.Value.Position, name);
            }

            foreach (var cave in caves)
            {
                string name = cave.Key.Contains(":") ? cave.Key.Substring(0, cave.Key.LastIndexOf(":")) : cave.Key.TrimEnd();
                float realdistance = cave.Key.Contains("Small") ? config.Settings.CaveDistanceSmall : cave.Key.Contains("Medium") ? config.Settings.CaveDistanceMedium : config.Settings.CaveDistanceLarge;
                realdistance += 50f;

                player.SendConsoleCommand("ddraw.sphere", 30f, Color.black, cave.Value, realdistance);
                player.SendConsoleCommand("ddraw.text", 30f, Color.cyan, cave.Value, name);
            }
        }

        private void CommandImportHomes(IPlayer p, string command, string[] args)
        {
            if (DisabledTPT.DisabledCommands.Contains(command.ToLower())) { p.Reply("Disabled command: " + command); return; }
            var player = p.Object as BasePlayer;

            if (player != null && (!IsAllowedMsg(player, PermImportHomes) || !player.IsConnected || player.IsSleeping()))
            {
                p.Reply(_("NotAllowed", player));
                return;
            }
            var datafile = Interface.Oxide.DataFileSystem.GetFile("m-Teleportation");
            if (!datafile.Exists())
            {
                p.Reply("No m-Teleportation.json exists.");
                return;
            }
            datafile.Load();
            var allHomeData = datafile["HomeData"] as Dictionary<string, object>;
            if (allHomeData == null)
            {
                p.Reply(_("HomeListEmpty", player));
                return;
            }
            var count = 0;
            foreach (var kvp in allHomeData)
            {
                var homeDataOld = kvp.Value as Dictionary<string, object>;
                if (homeDataOld == null) continue;
                if (!homeDataOld.ContainsKey("HomeLocations")) continue;
                var homeList = homeDataOld["HomeLocations"] as Dictionary<string, object>;
                if (homeList == null) continue;
                var userId = Convert.ToUInt64(kvp.Key);
                HomeData homeData;
                if (!Home.TryGetValue(userId, out homeData))
                    Home[userId] = homeData = new HomeData();
                foreach (var kvp2 in homeList)
                {
                    var positionData = kvp2.Value as Dictionary<string, object>;
                    if (positionData == null) continue;
                    if (!positionData.ContainsKey("x") || !positionData.ContainsKey("y") || !positionData.ContainsKey("z")) continue;
                    var position = new Vector3(Convert.ToSingle(positionData["x"]), Convert.ToSingle(positionData["y"]), Convert.ToSingle(positionData["z"]));
                    homeData.Locations[kvp2.Key] = position;
                    changedHome = True;
                    count++;
                }
            }
            p.Reply(string.Format("Imported {0} homes.", count));
        }

        private void RequestTimedOut(BasePlayer player, BasePlayer target)
        {
            PlayersRequests.Remove(player.userID);
            PlayersRequests.Remove(target.userID);
            PendingRequests.Remove(target.userID);
            PrintMsgL(player, "TimedOut", target.displayName);
            PrintMsgL(target, "TimedOutTarget", player.displayName);
        }

        #region Util
        private string FormatTime(long seconds)
        {
            var timespan = TimeSpan.FromSeconds(seconds);
            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
        }

        private double ConvertToRadians(double angle)
        {
            return System.Math.PI / 180 * angle;
        }
        #endregion

        #region Teleport
        public void Teleport(BasePlayer player, BasePlayer target) => Teleport(player, target.transform.position);

        public void Teleport(BasePlayer player, float x, float y, float z) => Teleport(player, new Vector3(x, y, z));

        public void Teleport(BasePlayer player, Vector3 newPosition, bool save = True)
        {
            if (!player.IsValid() || Vector3.Distance(newPosition, Zero) < 5f) return;

            if (save) SaveLocation(player);
            if (!teleporting.ContainsKey(player.userID))
                teleporting.Add(player.userID, newPosition);
            else teleporting[player.userID] = newPosition;

            var oldPosition = player.transform.position;
            
            try
            {
                player.EnsureDismounted(); // 1.1.2 @Def

                if (player.HasParent())
                {
                    player.SetParent(null, True, True);
                }

                if (player.IsConnected) // 1.1.2 @Def
                {
                    player.EndLooting();
                    StartSleeping(player);
                }

                player.RemoveFromTriggers(); // 1.1.2 @Def recommendation to use natural method for issue with triggers
                player.EnableServerFall(True); // redundant, in OnEntityTakeDamage hook
                player.Teleport(newPosition); // 1.1.6

                if (player.IsConnected && !Network.Net.sv.visibility.IsInside(player.net.group, newPosition))
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, True);
                    player.ClientRPCPlayer(null, player, "StartLoading");
                    player.SendEntityUpdate();
                    if (!IsInvisible(player)) // fix for becoming networked briefly with vanish while teleporting
                    {
                        player.UpdateNetworkGroup(); // 1.1.1 building fix @ctv
                        player.SendNetworkUpdateImmediate(False);
                    }
                }
            }
            finally
            {
                player.EnableServerFall(False);
                player.ForceUpdateTriggers(); // 1.1.4 exploit fix for looting sleepers in safe zones
            }

            Interface.CallHook("OnPlayerTeleported", player, oldPosition, newPosition);
        }

        bool IsInvisible(BasePlayer player)
        {
            return Vanish != null && Vanish.Call<bool>("IsInvisible", player);
        }

        public void StartSleeping(BasePlayer player) // custom as to not cancel crafting, or remove player from vanish
        {
            if (!player.IsSleeping())
            {
                Interface.CallHook("OnPlayerSleep", player);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, True);
                player.sleepStartTime = Time.time;
                BasePlayer.sleepingPlayerList.Add(player);
                BasePlayer.bots.Remove(player);
                player.CancelInvoke("InventoryUpdate");
                player.CancelInvoke("TeamUpdate");
            }
        }
        #endregion

        #region Checks
        private string CanPlayerTeleport(BasePlayer player)
        {
            return Interface.Oxide.CallHook("CanTeleport", player) as string;
        }

        private bool CanCraftHome(BasePlayer player)
        {
            return config.Home.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftHome);
        }

        private bool CanCraftTown(BasePlayer player)
        {
            return config.Town.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftTown);
        }

        private bool CanCraftOutpost(BasePlayer player)
        {
            return config.Outpost.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftOutpost);
        }

        private bool CanCraftBandit(BasePlayer player)
        {
            return config.Bandit.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftBandit);
        }

        private bool CanCraftTPR(BasePlayer player)
        {
            return config.TPR.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftTpR);
        }

        public bool AboveWater(BasePlayer player)
        {
            var pos = player.transform.position;
#if DEBUG
            Puts($"Player position: {pos.ToString()}.  Checking for water...");
#endif
            if ((TerrainMeta.HeightMap.GetHeight(pos) - TerrainMeta.WaterMap.GetHeight(pos)) >= 0)
            {
#if DEBUG
                Puts("Player not above water.");
#endif
                return False;
            }
            else
            {
#if DEBUG
                Puts("Player is above water!");
#endif
                return True;
            }
        }

        private string NearMonument(BasePlayer player)
        {
            foreach (var entry in monuments)
            {
                if (entry.Key.ToLower().Contains("power")) continue;

                var pos = entry.Value.Position;
                pos.y = player.transform.position.y;
                float dist = (player.transform.position - pos).magnitude;
#if DEBUG
                Puts($"Checking {entry.Key} dist: {dist}, realdistance: {entry.Value.Radius}");
#endif
                if (dist < entry.Value.Radius)
                {
#if DEBUG
                    Puts($"Player in range of {entry.Key}");
#endif
                    return entry.Key;
                }
            }
            return null;
        }

        private bool belowGround(Vector3 a, Vector3 b)
        {
            return a.y < TerrainMeta.HeightMap.GetHeight(b);
        }

        private string NearCave(BasePlayer player)
        {
            foreach (var entry in caves)
            {
                string caveName = entry.Key.Contains(":") ? entry.Key.Substring(0, entry.Key.LastIndexOf(":")) : entry.Key;
                float realdistance = entry.Key.Contains("Small") ? config.Settings.CaveDistanceSmall : entry.Key.Contains("Medium") ? config.Settings.CaveDistanceMedium : config.Settings.CaveDistanceLarge;

                if (Vector3.Distance(player.transform.position, entry.Value) < realdistance + 50f && !belowGround(player.transform.position, entry.Value))
                {
#if DEBUG
                    Puts($"NearCave: {caveName} nearby.");
#endif
                    return caveName;
                }
                else
                {
#if DEBUG
                    Puts("NearCave: Not near this cave, or above it.");
#endif
                }
            }
            return null;
        }

        private string CheckPlayer(BasePlayer player, bool build = False, bool craft = False, bool origin = True, string mode = "home")
        {
            var onship = player.GetComponentInParent<CargoShip>();
            var onballoon = player.GetComponentInParent<HotAirBalloon>();
            var inlift = player.GetComponentInParent<Lift>();
            var pos = player.transform.position;

            string monname = NearMonument(player);
            if (config.Settings.Interrupt.Monument)
            {
                if (monname != null)
                {
                    return _("TooCloseToMon", player, monname);
                }
            }
            if (config.Settings.Interrupt.Oilrig)
            {
                if (monname != null && monname.Contains("Oil Rig"))
                {
                    return _("TooCloseToMon", player, monname);
                }
            }
            bool allowcave = True;

#if DEBUG
            Puts($"CheckPlayer(): called mode is {mode}");
#endif
            switch (mode)
            {
                case "tpt":
                    allowcave = config.TPT.AllowCave;
                    break;
                case "home":
                    allowcave = config.Home.AllowCave;
                    break;
                case "tpa":
                case "tpr":
                    allowcave = config.TPR.AllowCave;
                    break;
                case "town":
                    allowcave = config.Town.AllowCave;
                    break;
                case "outpost":
                    allowcave = config.Outpost.AllowCave;
                    break;
                case "bandit":
                    allowcave = config.Bandit.AllowCave;
                    break;
                default:
#if DEBUG
                    Puts("Skipping cave check...");
#endif
                    break;
            }
            if (!allowcave)
            {
#if DEBUG
                Puts("Checking cave distance...");
#endif
                string cavename = NearCave(player);
                if (cavename != null)
                {
                    return "TooCloseToCave";
                }
            }

            if (config.Settings.Interrupt.Hostile && (mode == "bandit" || mode == "outpost"))
            {
                if (player.IsHostile())
                {
                    return "TPHostile";
                }
            }
            if (player.isMounted && config.Settings.Interrupt.Mounted)
                return "TPMounted";
            if (!player.IsAlive())
                return "TPDead";
            // Block if hurt if the config is enabled.  If the player is not the target in a tpa condition, allow.
            if ((player.IsWounded() && origin) && config.Settings.Interrupt.Hurt)
                return "TPWounded";

            if (player.metabolism.temperature.value <= config.Settings.MinimumTemp && config.Settings.Interrupt.Cold)
            {
                return "TPTooCold";
            }
            if (player.metabolism.temperature.value >= config.Settings.MaximumTemp && config.Settings.Interrupt.Hot)
            {
                return "TPTooHot";
            }

            if (config.Settings.Interrupt.AboveWater)
                if (AboveWater(player))
                    return "TPAboveWater";
            if (!build && !player.CanBuild())
                return "TPBuildingBlocked";
            if (player.IsSwimming() && config.Settings.Interrupt.Swimming)
                return "TPSwimming";
            // This will have to do until we have a proper parent name for this
            if (monname != null && monname.Contains("Oil Rig") && config.Settings.Interrupt.Oilrig)
                return "TPOilRig";
            if (monname != null && monname.Contains("Excavator") && config.Settings.Interrupt.Excavator)
                return "TPExcavator";
            if (onship && config.Settings.Interrupt.Cargo)
                return "TPCargoShip";
            if (onballoon && config.Settings.Interrupt.Balloon)
                return "TPHotAirBalloon";
            if (inlift && config.Settings.Interrupt.Lift)
                return "TPBucketLift";
            if (GetLift(pos) && config.Settings.Interrupt.Lift)
                return "TPRegLift";
            if (player.InSafeZone() && config.Settings.Interrupt.Safe)
                return "TPSafeZone";
            if (!craft && player.inventory.crafting.queue.Count > 0)
                return "TPCrafting";

            if (config.Settings.BlockZoneFlag && ZoneManager != null)
            {
                var success = ZoneManager?.Call("PlayerHasFlag", player, "notp");

                if (success is bool && (bool)success)
                {
                    return "TPFlagZone";
                }
            }

            if (config.Settings.BlockNoEscape && NoEscape != null)
            {
                var success = NoEscape?.Call("IsBlocked", player);

                if (success is bool && (bool)success)
                {
                    return "TPNoEscapeBlocked";
                }
            }

            return null;
        }

        private string CheckTargetLocation(BasePlayer player, Vector3 targetLocation, bool ubb, bool obb)
        {
            // ubb == UsableIntoBuildingBlocked
            // obb == CupOwnerAllowOnBuildingBlocked
            var entities = Pool.GetList<BuildingBlock>();
            Vis.Entities(targetLocation, 3f, entities, Layers.Mask.Construction, QueryTriggerInteraction.Ignore);
            bool denied = False;

            foreach (var block in entities)
            {
                if (CheckCupboardBlock(block, player, obb))
                {
                    denied = False;
#if DEBUG
                    Puts("Cupboard either owned or there is no cupboard");
#endif
                }
                else if (ubb && (player.userID != block.OwnerID))
                {
                    denied = False;
#if DEBUG
                    Puts("Player does not own block, but UsableIntoBuildingBlocked=true");
#endif
                }
                else if (player.userID == block.OwnerID)
                {
#if DEBUG
                    Puts("Player owns block");
#endif

                    if (!player.IsBuildingBlocked(targetLocation, new Quaternion(), block.bounds))
                    {
#if DEBUG
                        Puts("Player not BuildingBlocked. Likely unprotected building.");
#endif
                        denied = False;
                        break;
                    }
                    else if (ubb)
                    {
#if DEBUG
                        Puts("Player not blocked because UsableIntoBuildingBlocked=true");
#endif
                        denied = False;
                        break;
                    }
                    else
                    {
#if DEBUG
                        Puts("Player owns block but blocked by UsableIntoBuildingBlocked=false");
#endif
                        denied = True;
                        break;
                    }
                }
                else
                {
#if DEBUG
                    Puts("Player blocked");
#endif
                    denied = True;
                    break;
                }
            }
            Pool.FreeList(ref entities);

            return denied ? "TPTargetBuildingBlocked" : null;
        }

        // Check that a building block is owned by/attached to a cupboard, allow tp if not blocked unless allowed by config
        private bool CheckCupboardBlock(BuildingBlock block, BasePlayer player, bool obb)
        {
            // obb == CupOwnerAllowOnBuildingBlocked
            var building = block.GetBuilding();
            if (building != null)
            {
#if DEBUG
                Puts("Found building, checking privileges...");
                Puts($"Building ID: {building.ID}");
#endif
                // cupboard overlap.  Check privs.
                if (building.buildingPrivileges == null)
                {
#if DEBUG
                    Puts("No cupboard found, allowing teleport");
#endif
                    return player.CanBuild();
                }

                foreach (var priv in building.buildingPrivileges)
                {
                    if (priv.IsAuthed(player))
                    {
                        // player is authorized to the cupboard
#if DEBUG
                        Puts("Player owns cupboard with auth");
#endif
                        return True;
                    }
                }

                if (player.userID == block.OwnerID)
                {
                    if (obb)
                    {
#if DEBUG
                        // player set the cupboard and is allowed in by config
                        Puts("Player owns cupboard with no auth, but allowed by CupOwnerAllowOnBuildingBlocked=true");
#endif
                        return True;
                    }
#if DEBUG
                    // player set the cupboard but is blocked by config
                    Puts("Player owns cupboard with no auth, but blocked by CupOwnerAllowOnBuildingBlocked=false");
#endif
                    return False;
                }

#if DEBUG
                // player not authed
                Puts("Player does not own cupboard and is not authorized");
#endif
                return False;
            }
#if DEBUG
            Puts("No cupboard or building found - we cannot tell the status of this block");
#endif
            return True;
        }

        private string CheckInsideBlock(Vector3 targetLocation)
        {
            List<BuildingBlock> blocks = Pool.GetList<BuildingBlock>();
            Vis.Entities(targetLocation + new Vector3(0, 0.25f), 0.1f, blocks, blockLayer);
            bool inside = blocks.Count > 0;
            Pool.FreeList(ref blocks);

            return inside ? "TPTargetInsideBlock" : null;
        }

        private string CheckInsideBattery(Vector3 targetLocation)
        {
            var batteries = new List<ElectricBattery>();
            Vis.Entities(targetLocation, 0.35f, batteries, deployedLayer);
            return batteries.Count > 0 ? "TPTargetInsideBlock" : null;
        }
        
        private string CheckItems(BasePlayer player)
        {
            foreach (var blockedItem in ReverseBlockedItems)
            {
                if (player.inventory.FindItemID(blockedItem.Key) != null)
                {
                    return blockedItem.Value;
                }
            }
            return null;
        }

        private string CheckFoundation(ulong userID, Vector3 position)
        {
            if (CheckInsideBattery(position) != null)
            {
                return "HomeNoFoundation";
            }
            if (!config.Home.ForceOnTopOfFoundation) return null; // Foundation/floor not required
            if (UnderneathFoundation(position))
            {
                return "HomeFoundationUnderneathFoundation";
            }

            var entities = new List<BuildingBlock>();
            if (config.Home.AllowAboveFoundation) // Can set on a foundation or floor
            {
#if DEBUG
                Puts($"CheckFoundation() looking for foundation or floor at {position}");
#endif
                entities = GetFoundationOrFloor(position);
            }
            else // Can only use foundation, not floor/ceiling
            {
#if DEBUG
                Puts($"CheckFoundation() looking for foundation at {position}");
#endif
                entities = GetFoundation(position);
            }

            entities.RemoveAll(x => !x.IsValid() || x.IsDestroyed);
            if (entities.Count == 0) return "HomeNoFoundation";

            if (!config.Home.CheckFoundationForOwner) return null;
            for (var i = 0; i < entities.Count; i++)
            {
                if (entities[i].OwnerID == userID || IsFriend(userID, entities[i].OwnerID)) return null;
            }

            return "HomeFoundationNotFriendsOwned";
        }

        private BuildingBlock GetFoundationOwned(Vector3 position, ulong userID)
        {
#if DEBUG
            Puts("GetFoundationOwned() called...");
#endif
            var entities = GetFoundation(position);
            if (entities.Count == 0) return null;
            if (!config.Home.CheckFoundationForOwner) return entities[0];

            for (var i = 0; i < entities.Count; i++)
            {
                if (entities[i].OwnerID == userID) return entities[i];
                else if (IsFriend(userID, entities[i].OwnerID)) return entities[i];
            }
            return null;
        }

        // Borrowed/modified from PreventLooting and Rewards
        // playerid = active player, ownerid = owner of building block, who may be offline
        bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (config.Home.UseFriends && Friends != null && Friends.IsLoaded)
            {
#if DEBUG
                Puts("Checking Friends...");
#endif
                var fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
#if DEBUG
                    Puts("  IsFriend: true based on Friends plugin");
#endif
                    return True;
                }
            }
            if (config.Home.UseClans && Clans != null && Clans.IsLoaded)
            {
#if DEBUG
                Puts("Checking Clans...");
#endif
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan != null && ownerclan != null && playerclan == ownerclan)
                {
#if DEBUG
                    Puts("  IsFriend: true based on Clans plugin");
#endif
                    return True;
                }
            }
            if (config.Home.UseTeams)
            {
#if DEBUG
                Puts("Checking Rust teams...");
#endif
                BasePlayer player = BasePlayer.FindByID(playerid);
                if (player.currentTeam != (long)0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);
                    if (playerTeam == null) return False;
                    if (playerTeam.members.Contains(ownerid))
                    {
#if DEBUG
                        Puts("  IsFriend: true based on Rust teams");
#endif
                        return True;
                    }
                }
            }
            return False;
        }

        // Check that we are near the middle of a block.  Also check for high wall overlap
        private bool ValidBlock(BaseEntity entity, Vector3 position)
        {
            if (!config.Settings.StrictFoundationCheck)
            {
                return True;
            }
#if DEBUG
            Puts($"ValidBlock() called for {entity.ShortPrefabName}");
#endif
            Vector3 center = entity.CenterPoint();

            List<BaseEntity> ents = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(center, 1.5f, ents);
            foreach (BaseEntity wall in ents)
            {
                if (wall.name.Contains("external.high"))
                {
#if DEBUG
                    Puts($"    Found: {wall.name} @ center {center}, pos {position}");
#endif
                    return False;
                }
            }
#if DEBUG
            Puts($"  Checking block: {entity.name} @ center {center}, pos: {position.ToString()}");
#endif
            if (entity.PrefabName.Contains("triangle.prefab"))
            {
                if (Math.Abs(center.x - position.x) < 0.45f && Math.Abs(center.z - position.z) < 0.45f)
                {
#if DEBUG
                    Puts($"    Found: {entity.ShortPrefabName} @ center: {center}, pos: {position}");
#endif
                    return True;
                }
            }
            else if (entity.PrefabName.Contains("foundation.prefab") || entity.PrefabName.Contains("floor.prefab"))
            {
                if (Math.Abs(center.x - position.x) < 0.7f && Math.Abs(center.z - position.z) < 0.7f)
                {
#if DEBUG
                    Puts($"    Found: {entity.ShortPrefabName} @ center: {center}, pos: {position}");
#endif
                    return True;
                }
            }

            return False;
        }

        private List<BuildingBlock> GetFoundation(Vector3 position)
        {
            RaycastHit hitinfo;
            var entities = new List<BuildingBlock>();

            if (Physics.Raycast(position, Down, out hitinfo, 2.5f, blockLayer) && hitinfo.GetEntity().IsValid())
            {
                var entity = hitinfo.GetEntity();
                if (entity.PrefabName.Contains("foundation") || position.y < entity.WorldSpaceBounds().ToBounds().max.y)
                {
                    if (ValidBlock(entity, position))
                    {
#if DEBUG
                        Puts($"  GetFoundation() found {entity.PrefabName} at {entity.transform.position}");
#endif
                        entities.Add(entity as BuildingBlock);
                    }
                }
            }
            else
            {
#if DEBUG
                Puts("  GetFoundation() none found.");
#endif
            }

            return entities;
        }

        private List<BuildingBlock> GetFloor(Vector3 position)
        {
            RaycastHit hitinfo;
            var entities = new List<BuildingBlock>();

            if (Physics.Raycast(position, Down, out hitinfo, 0.25f, Layers.Mask.Construction, QueryTriggerInteraction.Ignore) && hitinfo.GetEntity().IsValid())
            {
                var entity = hitinfo.GetEntity();

                if (entity.IsValid() && entity.PrefabName.Contains("floor"))
                {
#if DEBUG
                    Puts($"  GetFloor() found {entity.PrefabName} at {entity.transform.position}");
#endif
                    entities.Add(entity as BuildingBlock);
                }
            }
            else
            {
#if DEBUG
                Puts("  GetFloor() none found.");
#endif
            }

            return entities;
        }

        private List<BuildingBlock> GetFoundationOrFloor(Vector3 position)
        {
            RaycastHit hitinfo;
            var entities = new List<BuildingBlock>();

            if (Physics.Raycast(position, Down, out hitinfo, 0.25f, blockLayer) && hitinfo.GetEntity().IsValid())
            {
                var entity = hitinfo.GetEntity();
                if (entity.PrefabName.Contains("floor") || entity.PrefabName.Contains("foundation"))// || position.y < entity.WorldSpaceBounds().ToBounds().max.y))
                {
#if DEBUG
                    Puts($"  GetFoundationOrFloor() found {entity.PrefabName} at {entity.transform.position}");
#endif
                    if (ValidBlock(entity, position))
                    {
                        entities.Add(entity as BuildingBlock);
                    }
                }
            }
            else
            {
#if DEBUG
                Puts("  GetFoundationOrFloor() none found.");
#endif
            }

            return entities;
        }

        private bool CheckBoundaries(float x, float y, float z)
        {
            return x <= boundary && x >= -boundary && y <= 2000 && y >= -100 && z <= boundary && z >= -boundary;
        }

        private Vector3 GetGround(Vector3 sourcePos)
        {
            if (!config.Home.AllowAboveFoundation) return sourcePos;
            var newPos = sourcePos;
            newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            sourcePos.y += .5f;
            RaycastHit hitinfo;
            var done = False;

#if DEBUG
            Puts("GetGround(): Looking for iceberg or cave");
#endif
            //if (Physics.SphereCast(sourcePos, .1f, down, out hitinfo, 250, groundLayer))
            if (Physics.Raycast(sourcePos, Down, out hitinfo, 250f, groundLayer))
            {
                if ((config.Home.AllowIceberg && hitinfo.collider.name.Contains("iceberg")) || (config.Home.AllowCave && hitinfo.collider.name.Contains("cave_")))
                {
#if DEBUG
                    Puts("GetGround():   found iceberg or cave");
#endif
                    sourcePos.y = hitinfo.point.y;
                    done = True;
                }
                else
                {
                    var mesh = hitinfo.collider.GetComponentInChildren<MeshCollider>();
                    if (mesh != null && mesh.sharedMesh.name.Contains("rock_"))
                    {
                        sourcePos.y = hitinfo.point.y;
                        done = True;
                    }
                }
            }
#if DEBUG
            Puts("GetGround(): Looking for cave or rock");
#endif
            //if (!_config.Home.AllowCave && Physics.SphereCast(sourcePos, .1f, up, out hitinfo, 250, groundLayer) && hitinfo.collider.name.Contains("rock_"))
            if (!config.Home.AllowCave && Physics.Raycast(sourcePos, Up, out hitinfo, 250f, groundLayer) && hitinfo.collider.name.Contains("rock_"))
            {
#if DEBUG
                Puts("GetGround():   found cave or rock");
#endif
                sourcePos.y = newPos.y - 10;
                done = True;
            }
            return done ? sourcePos : newPos;
        }

        private bool GetLift(Vector3 position)
        {
            List<ProceduralLift> nearObjectsOfType = new List<ProceduralLift>();
            Vis.Entities<ProceduralLift>(position, 0.5f, nearObjectsOfType);
            if (nearObjectsOfType.Count > 0)
            {
                return True;
            }
            return False;
        }

        private Vector3 GetGroundBuilding(Vector3 sourcePos)
        {
            sourcePos.y = TerrainMeta.HeightMap.GetHeight(sourcePos);
            RaycastHit hitinfo;
            if (Physics.Raycast(sourcePos, Down, out hitinfo, buildingLayer))
            {
                sourcePos.y = Mathf.Max(hitinfo.point.y, sourcePos.y);
                return sourcePos;
            }
            if (Physics.Raycast(sourcePos, Up, out hitinfo, buildingLayer))
                sourcePos.y = Mathf.Max(hitinfo.point.y, sourcePos.y);
            return sourcePos;
        }

        private bool UnderneathFoundation(Vector3 position)
        {
            // Check for foundation half-height above where home was set
            foreach (var hit in Physics.RaycastAll(position, Up, 2f, buildingLayer))
            {
                if (hit.GetCollider().name.Contains("foundation"))
                {
                    return True;
                }
            }
            // Check for foundation full-height above where home was set
            // Since you can't see from inside via ray, start above.
            foreach (var hit in Physics.RaycastAll(position + Up + Up + Up + Up, Down, 2f, buildingLayer))
            {
                if (hit.GetCollider().name.Contains("foundation"))
                {
                    return True;
                }
            }

            return False;
        }

        private bool IsAllowed(BasePlayer player, string perm = null)
        {
            var playerAuthLevel = player.net?.connection?.authLevel;

            int requiredAuthLevel = 3;
            if (config.Admin.UseableByModerators)
            {
                requiredAuthLevel = 1;
            }
            else if (config.Admin.UseableByAdmins)
            {
                requiredAuthLevel = 2;
            }
            if (playerAuthLevel >= requiredAuthLevel) return True;

            return !string.IsNullOrEmpty(perm) && permission.UserHasPermission(player.UserIDString, perm);
        }

        private bool IsAllowedMsg(BasePlayer player, string perm = null)
        {
            if (IsAllowed(player, perm)) return True;
            PrintMsg(player, "NotAllowed");
            return False;
        }

        private int GetHigher(BasePlayer player, Dictionary<string, int> limits, int limit, bool unlimited)
        {
            if (unlimited && limit == 0) return limit;

            foreach (var l in limits)
            {
                if (permission.UserHasPermission(player.UserIDString, l.Key))
                {
                    if (unlimited && l.Value == 0) return l.Value;

                    limit = Math.Max(l.Value, limit);
                }
            }
            return limit;
        }

        private int GetLower(BasePlayer player, Dictionary<string, int> times, int time)
        {
            foreach (var l in times)
            {
                if (permission.UserHasPermission(player.UserIDString, l.Key))
                {
                    time = Math.Min(l.Value, time);
                }
            }
            return time;
        }

        private void CheckPerms(Dictionary<string, int> limits)
        {
            foreach (var limit in limits)
            {
                if (!permission.PermissionExists(limit.Key))
                {
                    permission.RegisterPermission(limit.Key, this);
                }
            }
        }
        #endregion

        #region Message
        private string _(string msgId, BasePlayer player, params object[] args)
        {
            var msg = lang.GetMessage(msgId, this, player?.UserIDString);
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void PrintMsgL(BasePlayer player, string msgId, params object[] args)
        {
            if (player == null) return;
            PrintMsg(player, _(msgId, player, args));
        }

        private void PrintMsg(BasePlayer player, string msg)
        {
            if (player == null) return;
            //SendReply(player, $"{config.Settings.ChatName}{msg}");
            Player.Message(player, $"{config.Settings.ChatName}{msg}", config.Settings.ChatID);
        }
        #endregion

        #region DrawBox
        private static void DrawBox(BasePlayer player, Vector3 center, Quaternion rotation, Vector3 size)
        {
            size = size / 2;
            var point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
            var point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point3);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point5);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point3);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point8);

            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point6);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point7);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point6, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point6);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point7);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point7, point3);
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }
        #endregion

        #region FindPlayer
        private ulong FindPlayersSingleId(string nameOrIdOrIp, BasePlayer player)
        {
            var targets = FindPlayers(nameOrIdOrIp);
            if (targets.Count > 1)
            {
                PrintMsgL(player, "MultiplePlayers", string.Join(", ", targets.Select(p => p.displayName).ToArray()));
                targets.Clear();
                return 0;
            }
            ulong userId;
            if (targets.Count <= 0)
            {
                if (ulong.TryParse(nameOrIdOrIp, out userId)) return userId;
                PrintMsgL(player, "PlayerNotFound");
                return 0;
            }
            else
                userId = targets.First().userID;
            targets.Clear();
            return userId;
        }

        private BasePlayer FindPlayersSingle(string nameOrIdOrIp, BasePlayer player)
        {
            var targets = FindPlayers(nameOrIdOrIp);
            if (targets.Count <= 0)
            {
                PrintMsgL(player, "PlayerNotFound");
                return null;
            }
            if (targets.Count > 1)
            {
                PrintMsgL(player, "MultiplePlayers", string.Join(", ", targets.Select(p => p.displayName).ToArray()));
                targets.Clear();
                return null;
            }
            var t = targets.First();
            targets.Clear();
            return t;
        }

        private static List<BasePlayer> FindPlayers(string nameOrIdOrIp)
        {
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return new List<BasePlayer>();
            return BasePlayer.allPlayerList.Where(p => p && (p.UserIDString == nameOrIdOrIp || p.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase) || (p.IsConnected && p.net.connection.ipaddress.Contains(nameOrIdOrIp)))).ToList();
        }

        private static List<BasePlayer> FindPlayersOnline(string nameOrIdOrIp)
        {
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return new List<BasePlayer>();
            return BasePlayer.activePlayerList.Where(p => p.UserIDString == nameOrIdOrIp || p.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase) || (p.IsConnected && p.net.connection.ipaddress.Contains(nameOrIdOrIp))).ToList();
        }
        #endregion

        #region API
        private Dictionary<string, Vector3> GetHomes(object playerObj)
        {
            if (playerObj == null) return null;
            if (playerObj is string) playerObj = Convert.ToUInt64(playerObj);
            if (!(playerObj is ulong)) throw new ArgumentException("playerObj");
            var playerId = (ulong)playerObj;
            HomeData homeData;
            if (!Home.TryGetValue(playerId, out homeData) || homeData.Locations.Count == 0) return null;
            return homeData.Locations;
        }

        private int GetLimitRemaining(BasePlayer player, string type)
        {
            if (player == null || string.IsNullOrEmpty(type)) return 0;
            var currentDate = DateTime.Now.ToString("d");
            int limit;
            var remaining = -1;
            switch (type.ToLower())
            {
                case "home":
                    limit = GetHigher(player, config.Home.VIPDailyLimits, config.Home.DailyLimit, true);
                    HomeData homeData;
                    if (!Home.TryGetValue(player.userID, out homeData))
                    {
                        Home[player.userID] = homeData = new HomeData();
                    }
                    if (homeData.Teleports.Date != currentDate)
                    {
                        homeData.Teleports.Amount = 0;
                        homeData.Teleports.Date = currentDate;
                    }
                    if (limit > 0)
                    {
                        remaining = limit - homeData.Teleports.Amount;
                    }
                    break;
                case "town":
                    limit = GetHigher(player, config.Town.VIPDailyLimits, config.Town.DailyLimit, true);
                    TeleportData townData;
                    if (!Town.TryGetValue(player.userID, out townData))
                    {
                        Town[player.userID] = townData = new TeleportData();
                    }
                    if (townData.Date != currentDate)
                    {
                        townData.Amount = 0;
                        townData.Date = currentDate;
                    }
                    if (limit > 0)
                    {
                        remaining = limit - townData.Amount;
                    }
                    break;
                case "outpost":
                    limit = GetHigher(player, config.Outpost.VIPDailyLimits, config.Outpost.DailyLimit, true);
                    TeleportData outpostData;
                    if (!Outpost.TryGetValue(player.userID, out outpostData))
                    {
                        Outpost[player.userID] = outpostData = new TeleportData();
                    }
                    if (outpostData.Date != currentDate)
                    {
                        outpostData.Amount = 0;
                        outpostData.Date = currentDate;
                    }
                    if (limit > 0)
                    {
                        remaining = limit - outpostData.Amount;
                    }
                    break;
                case "bandit":
                    limit = GetHigher(player, config.Bandit.VIPDailyLimits, config.Bandit.DailyLimit, true);
                    TeleportData banditData;
                    if (!Bandit.TryGetValue(player.userID, out banditData))
                    {
                        Bandit[player.userID] = banditData = new TeleportData();
                    }
                    if (banditData.Date != currentDate)
                    {
                        banditData.Amount = 0;
                        banditData.Date = currentDate;
                    }
                    if (limit > 0)
                    {
                        remaining = limit - banditData.Amount;
                    }
                    break;
                case "tpr":
                    limit = GetHigher(player, config.TPR.VIPDailyLimits, config.TPR.DailyLimit, true);
                    TeleportData tprData;
                    if (!TPR.TryGetValue(player.userID, out tprData))
                    {
                        TPR[player.userID] = tprData = new TeleportData();
                    }
                    if (tprData.Date != currentDate)
                    {
                        tprData.Amount = 0;
                        tprData.Date = currentDate;
                    }
                    if (limit > 0)
                    {
                        remaining = limit - tprData.Amount;
                    }
                    break;
            }
            return remaining;
        }

        private int GetCooldownRemaining(BasePlayer player, string type)
        {
            if (player == null || string.IsNullOrEmpty(type)) return 0;
            var currentDate = DateTime.Now.ToString("d");
            var timestamp = Facepunch.Math.Epoch.Current;
            int cooldown;
            var remaining = -1;
            switch (type.ToLower())
            {
                case "home":
                    cooldown = GetLower(player, config.Home.VIPCooldowns, config.Home.Cooldown);
                    HomeData homeData;
                    if (!Home.TryGetValue(player.userID, out homeData))
                    {
                        Home[player.userID] = homeData = new HomeData();
                    }
                    if (homeData.Teleports.Date != currentDate)
                    {
                        homeData.Teleports.Amount = 0;
                        homeData.Teleports.Date = currentDate;
                    }
                    if (cooldown > 0 && timestamp - homeData.Teleports.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - homeData.Teleports.Timestamp);
                    }
                    break;
                case "town":
                    cooldown = GetLower(player, config.Town.VIPCooldowns, config.Town.Cooldown);
                    TeleportData townData;
                    if (!Town.TryGetValue(player.userID, out townData))
                    {
                        Town[player.userID] = townData = new TeleportData();
                    }
                    if (townData.Date != currentDate)
                    {
                        townData.Amount = 0;
                        townData.Date = currentDate;
                    }
                    if (cooldown > 0 && timestamp - townData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - townData.Timestamp);
                    }
                    break;
                case "outpost":
                    cooldown = GetLower(player, config.Outpost.VIPCooldowns, config.Outpost.Cooldown);
                    TeleportData outpostData;
                    if (!Outpost.TryGetValue(player.userID, out outpostData))
                    {
                        Outpost[player.userID] = outpostData = new TeleportData();
                    }
                    if (outpostData.Date != currentDate)
                    {
                        outpostData.Amount = 0;
                        outpostData.Date = currentDate;
                    }
                    if (cooldown > 0 && timestamp - outpostData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - outpostData.Timestamp);
                    }
                    break;
                case "bandit":
                    cooldown = GetLower(player, config.Bandit.VIPCooldowns, config.Bandit.Cooldown);
                    TeleportData banditData;
                    if (!Bandit.TryGetValue(player.userID, out banditData))
                    {
                        Bandit[player.userID] = banditData = new TeleportData();
                    }
                    if (banditData.Date != currentDate)
                    {
                        banditData.Amount = 0;
                        banditData.Date = currentDate;
                    }
                    if (cooldown > 0 && timestamp - banditData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - banditData.Timestamp);
                    }
                    break;
                case "tpr":
                    cooldown = GetLower(player, config.TPR.VIPCooldowns, config.TPR.Cooldown);
                    TeleportData tprData;
                    if (!TPR.TryGetValue(player.userID, out tprData))
                    {
                        TPR[player.userID] = tprData = new TeleportData();
                    }
                    if (tprData.Date != currentDate)
                    {
                        tprData.Amount = 0;
                        tprData.Date = currentDate;
                    }
                    if (cooldown > 0 && timestamp - tprData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - tprData.Timestamp);
                    }
                    break;
            }
            return remaining;
        }
        #endregion

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }

        private class CustomComparerDictionaryCreationConverter<T> : CustomCreationConverter<IDictionary>
        {
            private readonly IEqualityComparer<T> comparer;

            public CustomComparerDictionaryCreationConverter(IEqualityComparer<T> comparer)
            {
                if (comparer == null)
                    throw new ArgumentNullException(nameof(comparer));
                this.comparer = comparer;
            }

            public override bool CanConvert(Type objectType)
            {
                return HasCompatibleInterface(objectType) && HasCompatibleConstructor(objectType);
            }

            private static bool HasCompatibleInterface(Type objectType)
            {
                return objectType.GetInterfaces().Where(i => HasGenericTypeDefinition(i, typeof(IDictionary<,>))).Any(i => typeof(T).IsAssignableFrom(i.GetGenericArguments().First()));
            }

            private static bool HasGenericTypeDefinition(Type objectType, Type typeDefinition)
            {
                return objectType.GetTypeInfo().IsGenericType && objectType.GetGenericTypeDefinition() == typeDefinition;
            }

            private static bool HasCompatibleConstructor(Type objectType)
            {
                return objectType.GetConstructor(new[] { typeof(IEqualityComparer<T>) }) != null;
            }

            public override IDictionary Create(Type objectType)
            {
                return Activator.CreateInstance(objectType, comparer) as IDictionary;
            }
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            PrintMsgL(player, "<size=14>NTeleportation</size> by <color=#ce422b>Nogrod</color>\n<color=#ffd479>/sethome NAME</color> - Set home on current foundation\n<color=#ffd479>/home NAME</color> - Go to one of your homes\n<color=#ffd479>/home list</color> - List your homes\n<color=#ffd479>/town</color> - Go to town, if set\n/tpb - Go back to previous location\n/tpr PLAYER - Request teleport to PLAYER\n/tpa - Accept teleport request");
        }

        private bool API_HavePendingRequest(BasePlayer player)
        {
            return PendingRequests.ContainsKey(player.userID) || PlayersRequests.ContainsKey(player.userID) || TeleportTimers.ContainsKey(player.userID);
        }

        private bool API_HaveAvailableHomes(BasePlayer player)
        {
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData))
            {
                Home[player.userID] = homeData = new HomeData();
            }

            var limit = GetHigher(player, config.Home.VIPHomesLimits, config.Home.HomesLimit, true);

            if (limit == 0) return True;

            return homeData.Locations.Count < limit;
        }

        private List<string> API_GetHomes(BasePlayer player)
        {
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData))
            {
                Home[player.userID] = homeData = new HomeData();
            }

            return homeData.Locations.Keys.ToList();
        }
    }
}