using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Systems.GameEventSystem;
using BTKSANameplateMod.Config;
using BTKSASelfPortrait.Config;
using BTKUILib;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using HarmonyLib;
using Semver;
using UnityEngine;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace BTKSANameplateMod
{
    public static class BuildInfo
    {
        public const string Name = "BTKSANameplateMod"; // Name of the Mod.  (MUST BE SET)
        public const string Author = "DDAkebono#0001"; // Author of the Mod.  (Set as null if none)
        public const string Company = "BTK-Development"; // Company that made the Mod.  (Set as null if none)
        public const string Version = "3.0.2"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateMod/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateMod : MelonMod
    {
        internal static MelonLogger.Instance Logger;
        internal static readonly List<BTKBaseConfig> BTKConfigs = new(); 
        
        public static BTKBoolConfig ScalingEnable = new(nameof(BTKSANameplateMod), "Nameplate Scaling", "Enables the nameplate size scaling", true, null, false);
        public static BTKBoolConfig CloseRangeFade = new(nameof(BTKSANameplateMod), "Close Range Fade", "Enables the nameplate close range fade mode", true, null, false);
        public static BTKBoolConfig CloseRangeFadeFriends = new(nameof(BTKSANameplateMod), "Close Range Fade Friends", "Sets Close Range Fade to only affect friends", false, null, false);
        public static BTKBoolConfig HideFriendNameplates = new(nameof(BTKSANameplateMod), "Hide Friends Nameplates", "This hides the nameplates of your friends but shows all others", false, null, false);
        public static BTKFloatConfig CloseRangeFadeMinDist = new(nameof(BTKSANameplateMod), "Close Range Distance Min", "Configure the minimum distance for close range fade, at this point the nameplate will be completely faded out", 2f, 0f, 10f, null, false);
        public static BTKFloatConfig CloseRangeFadeMaxDist = new(nameof(BTKSANameplateMod), "Close Range Distance Max", "Configure the maximum distance for close range fade, at this point the nameplate will be completely visible", 3f, 0f, 10f, null, false);
        public static BTKFloatConfig ScalerMinSize = new(nameof(BTKSANameplateMod), "Nameplate Scale Min Size", "Minimum size that a nameplate can be scaled down to", 0.2f, 0f, 1f, null, false);
        public static BTKFloatConfig ScalerMaxDist = new(nameof(BTKSANameplateMod), "Nameplate Scale Start Range", "Distance that scaling begins at", 3f, 0f, 10f, null, false);
        public static List<NameplateAdjuster> ActiveAdjusters = new();

        private static MethodInfo _btkGetCreatePageAdapter;

        private bool _isMenuOpen;
        private readonly List<string> _hiddenNameplateUserIDs = new();
        private bool _hasSetupUI;

        public override void OnInitializeMelon()
        {
            Logger = LoggerInstance;
            
            Logger.Msg("BTK Standalone: Nameplate Mod - Starting up");

            if (RegisteredMelons.Any(x => x.Info.Name.Equals("BTKCompanionLoader", StringComparison.OrdinalIgnoreCase)))
            {
                MelonLogger.Msg("Hold on a sec! Looks like you've got BTKCompanion installed, this mod is built in and not needed!");
                MelonLogger.Error("BTKSANameplateMod has not started up! (BTKCompanion Running)");
                return;
            }
            
            if (!RegisteredMelons.Any(x => x.Info.Name.Equals("BTKUILib") && x.Info.SemanticVersion != null && x.Info.SemanticVersion.CompareTo(new SemVersion(1)) >= 0))
            {
                Logger.Error("BTKUILib was not detected or it outdated! BTKCompanion cannot function without it!");
                Logger.Error("Please download an updated copy for BTKUILib!");
                return;
            }

            if (RegisteredMelons.Any(x => x.Info.Name.Equals("BTKUILib") && x.Info.SemanticVersion.CompareTo(new SemVersion(2, 0, 0)) <= 0))
            {
                //We're working with UILib 2.0.0, let's reflect the get create page function
                _btkGetCreatePageAdapter = typeof(Page).GetMethod("GetOrCreatePage", BindingFlags.Public | BindingFlags.Static);
                Logger.Msg($"BTKUILib 2.0.0 detected, attempting to grab GetOrCreatePage function: {_btkGetCreatePageAdapter != null}");
            }

            //Apply patches
            ApplyPatches(typeof(NameplatePatches));
            
            NameplatePatches.OnNameplateRebuild += OnNameplateRebuild;

            CVRGameEventSystem.QuickMenu.OnOpen.AddListener(() =>
            {
                try
                {
                    _isMenuOpen = true;
                    foreach(var adjuster in ActiveAdjusters)
                    {
                        adjuster.MenuToggled(true);
                    }
                }
                catch(Exception e)
                {
                    Logger.Error(e);
                }
            });
            
            CVRGameEventSystem.QuickMenu.OnClose.AddListener(() =>
            {
                try
                {
                    _isMenuOpen = false;
                    foreach(var adjuster in ActiveAdjusters)
                    {
                        adjuster.MenuToggled(false);
                    }
                }
                catch(Exception e)
                {
                    Logger.Error(e);
                }
            });

            Logger.Msg("Loading HiddenNameplateUserIDs from file", true);
            LoadHiddenNameplateFromFile();

            QuickMenuAPI.OnMenuRegenerate += SetupUI;
        }

        private void OnNameplateRebuild(PlayerNameplate obj)
        {
            var adjusterCheck = obj.GetComponent<NameplateAdjuster>();
            if (adjusterCheck != null) return;
            var adjuster = obj.gameObject.AddComponent<NameplateAdjuster>();
            adjuster.isFriend = Friends.FriendsWith(obj.player.ownerId);
            adjuster.SetHidden((adjuster.isFriend && HideFriendNameplates.BoolValue) || _hiddenNameplateUserIDs.Contains(obj.player.ownerId));
            adjuster.playerNameplate = obj;
            adjuster.MenuToggled(_isMenuOpen);
        }

        private void ApplyPatches(Type type)
        {
            try
            {
                HarmonyInstance.PatchAll(type);
            }
            catch(Exception e)
            {
                Logger.Msg($"Failed while patching {type.Name}!");
                Logger.Error(e);
            }
        }

        public bool ToggleNameplateVisibility()
        {
            bool state = false;
            
            if (!_hiddenNameplateUserIDs.Contains(QuickMenuAPI.SelectedPlayerID))
            {
                _hiddenNameplateUserIDs.Add(QuickMenuAPI.SelectedPlayerID);
                state = true;
            }
            else
            {
                _hiddenNameplateUserIDs.Remove(QuickMenuAPI.SelectedPlayerID);
            }

            SaveHiddenNameplateFile();

            var adjuster = ActiveAdjusters.FirstOrDefault(x => x.playerNameplate.player.ownerId.Equals(QuickMenuAPI.SelectedPlayerID));
            if (adjuster == null) return state;
            
            adjuster.SetHidden(state);
            return state;
        }

        private void SaveHiddenNameplateFile()
        {
            StringBuilder builder = new StringBuilder();
            foreach (string id in _hiddenNameplateUserIDs)
            {
                builder.Append(id);
                builder.AppendLine();
            }
            File.WriteAllText("UserData\\BTKHiddenNameplates.txt", builder.ToString());
        }

        private void LoadHiddenNameplateFromFile()
        {
            if (File.Exists("UserData\\BTKHiddenNameplates.txt"))
            {
                _hiddenNameplateUserIDs.Clear();

                string[] lines = File.ReadAllLines("UserData\\BTKHiddenNameplates.txt");

                foreach (string line in lines)
                {
                    if (!String.IsNullOrWhiteSpace(line))
                        _hiddenNameplateUserIDs.Add(line);
                }
            }
        }
        
        private void SetupUI(CVR_MenuManager unused)
        {
            if(_hasSetupUI) return;
            _hasSetupUI = true;
            
            QuickMenuAPI.PrepareIcon("BTKStandalone", "BTKIcon", Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSANameplateMod.Images.BTKIcon.png"));
            QuickMenuAPI.PrepareIcon("BTKStandalone", "Settings", Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSANameplateMod.Images.Settings.png"));
            QuickMenuAPI.PrepareIcon("BTKStandalone", "TurnOff", Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSANameplateMod.Images.TurnOff.png"));

            Page rootPage;

            if (_btkGetCreatePageAdapter != null)
                rootPage = (Page)_btkGetCreatePageAdapter.Invoke(null, new object[] { "BTKStandalone", "MainPage", true, "BTKIcon", null, false });
            else
                rootPage = new Page("BTKStandalone", "MainPage", true, "BTKIcon");

            rootPage.MenuTitle = "BTK Standalone Mods";
            rootPage.MenuSubtitle = "Toggle and configure your BTK Standalone mods here!";

            var functionToggles = rootPage.AddCategory("Nameplate Mod");

            var playerSelectCat = QuickMenuAPI.PlayerSelectPage.AddCategory("Nameplate Mod", "BTKStandalone");
            var toggleNP = playerSelectCat.AddButton("Toggle Nameplate", "TurnOff", "Toggles this users nameplate off and on, this is saved");
            toggleNP.OnPress += () =>
            {
                var state = ToggleNameplateVisibility();
                QuickMenuAPI.ShowAlertToast(state ? $"{QuickMenuAPI.SelectedPlayerName}'s nameplate has been hidden!" : $"{QuickMenuAPI.SelectedPlayerName}'s nameplate has been shown!");
            };
            var settingsPage = functionToggles.AddPage("NP Settings", "Settings", "Change settings related to NameplateMod", "BTKStandalone");

            var configCategories = new Dictionary<string, Category>();
            
            foreach (var config in BTKConfigs)
            {
                if (!configCategories.ContainsKey(config.Category)) 
                    configCategories.Add(config.Category, settingsPage.AddCategory(config.Category));

                var cat = configCategories[config.Category];

                switch (config.Type)
                {
                    case { } boolType when boolType == typeof(bool):
                        ToggleButton toggle = null;
                        var boolConfig = (BTKBoolConfig)config;
                        toggle = cat.AddToggle(config.Name, config.Description, boolConfig.BoolValue);
                        toggle.OnValueUpdated += b =>
                        {
                            if (!ConfigDialogs(config))
                                toggle.ToggleValue = boolConfig.BoolValue;

                            boolConfig.BoolValue = b;
                        };
                        break;
                    case {} floatType when floatType == typeof(float):
                        SliderFloat slider = null;
                        var floatConfig = (BTKFloatConfig)config;
                        slider = settingsPage.AddSlider(floatConfig.Name, floatConfig.Description, Convert.ToSingle(floatConfig.FloatValue), floatConfig.MinValue, floatConfig.MaxValue);
                        slider.OnValueUpdated += f =>
                        {
                            if (!ConfigDialogs(config))
                            {
                                slider.SetSliderValue(floatConfig.FloatValue);
                                return;
                            }

                            floatConfig.FloatValue = f;

                        };
                        break;
                }
            }
        }
        private bool ConfigDialogs(BTKBaseConfig config)
        {
            if (config.DialogMessage != null)
            {
                QuickMenuAPI.ShowNotice("Notice", config.DialogMessage);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerNameplate))]
    class NameplatePatches
    {
        internal static Action<PlayerNameplate> OnNameplateRebuild;
        private static MethodInfo _cameraGetMain = typeof(Camera).GetProperty(nameof(Camera.main), BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();

        [HarmonyPatch(nameof(PlayerNameplate.UpdateNamePlate))]
        [HarmonyPostfix]
        static void UpdateNameplate(PlayerNameplate __instance)
        {
            try
            {
                OnNameplateRebuild?.Invoke(__instance);
            }
            catch (Exception e)
            {
                BTKSANameplateMod.Logger.Error(e);
            }
        }
        
        [HarmonyPatch("Update")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> NameplateTranspilerTest(IEnumerable<CodeInstruction> instructions)
        {
            var instArray = instructions.ToArray();

            for (int i = 0; i < instArray.Length; i++)
            {
                var inst = instArray[i];

                if (inst.opcode == OpCodes.Call && ReferenceEquals(inst.operand, _cameraGetMain))
                {
                    instArray[i] = new CodeInstruction(OpCodes.Ret);

                    Array.Resize(ref instArray, i + 1);

                    break;
                }
            }

            return instArray;
        }
    }
}
