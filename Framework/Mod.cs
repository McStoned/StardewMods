﻿using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

using StardewModdingAPI;
using StardewModdingAPI.Events;

using StardewValley;

namespace Entoarox.Framework
{
    using Core.Content;
    using Extensions;
    internal class ModEntry : Mod
    {
        private static bool CreditsDone = true;
        internal static FrameworkConfig Config;
        private static Version _Version;
        public static Version Version { get => _Version; }
        private static string _PlatformContentDir;
        public static string PlatformContentDir
        {
            get
            {
                if (_PlatformContentDir == null)
                    _PlatformContentDir = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", Game1.content.RootDirectory, "XACT", "FarmerSounds.xgs")) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", Game1.content.RootDirectory) : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
                return _PlatformContentDir;
            }
        }
        public static void VersionRequired(string modRequiring, Version requiringVersion)
        {
            if (Version < requiringVersion)
                Logger.ExitGameImmediately($"The `{modRequiring}` mod requires EntoaroxFramework version [{requiringVersion}] or newer to work.");
        }
        internal static IMonitor Logger;
        internal static bool Repair = false;
        public override void Entry(IModHelper helper)
        {
            _Version = new Version(ModManifest.Version.MajorVersion, ModManifest.Version.MinorVersion, ModManifest.Version.PatchVersion);
            Logger = Monitor;
            Config = helper.ReadConfig<FrameworkConfig>();
            Logger.Log("Registering framework events...",LogLevel.Trace);
            helper.ConsoleCommands.Add("ef_bushreset", "Resets bushes in the whole game, use this if you installed a map mod and want to keep using your old save.", Internal.BushReset.Trigger);
            if(Config.TrainerCommands)
            {
                helper.ConsoleCommands
                    .Add("farm_settype", "farm_settype <type> | Enables you to change your farm type to any of the following: " + string.Join(",",Commands.Commands.Farms), Commands.Commands.farm)
                    .Add("farm_clear", "farm_clear | Removes ALL objects from your farm, this cannot be undone!", Commands.Commands.farm)

                    .Add("player_warp","player_warp <location> <x> <y> | Warps the player to the given position in the game.",Commands.Commands.player)
                ;
            }
            SaveEvents.AfterLoad += SaveEvents_AfterLoad;
            Events.MoreEvents.Setup();
            GameEvents.UpdateTick += TypeRegistry.Update;
            TypeRegistry.Init();
            SaveEvents.AfterReturnToTitle += SaveEvents_AfterReturnToTitle;
            if (Config.SkipCredits)
                GameEvents.UpdateTick += CreditsTick;
            Logger.Log("Framework has finished!",LogLevel.Info);
            VersionChecker.AddCheck("EntoaroxFramework", Version, "https://raw.githubusercontent.com/Entoarox/StardewMods/master/VersionChecker/EntoaroxFramework.json");
            SetupContentManager();
        }
        private WeakReference<ExtendibleContentManager> MainManager;
        private WeakReference<ExtendibleContentManager> TileManager;
        private WeakReference<ExtendibleContentManager> TempManager;
        private xTile.Display.XnaDisplayDevice DisplayDevice;
        private FieldInfo TempContent;
        private string RootDirectory;
        private IServiceProvider ServiceProvider;
        private void SetupContentManager()
        {
            if (TempContent == null)
                TempContent = typeof(Game1).GetField("_temporaryContent", BindingFlags.NonPublic | BindingFlags.Static);
            if (RootDirectory == null)
            {
                ServiceProvider = Game1.content.ServiceProvider;
                RootDirectory = Game1.content.RootDirectory;
                MainManager = new WeakReference<ExtendibleContentManager>(null);
                TileManager = new WeakReference<ExtendibleContentManager>(null);
                TempManager = new WeakReference<ExtendibleContentManager>(null);
            }
            if (!MainManager.TryGetTarget(out ExtendibleContentManager mainManager))
            {
                mainManager = new ExtendibleContentManager(ServiceProvider, RootDirectory);
                MainManager.SetTarget(mainManager);
            }
            if (!TileManager.TryGetTarget(out ExtendibleContentManager tileManager))
            {
                tileManager = new ExtendibleContentManager(ServiceProvider, RootDirectory);
                TileManager.SetTarget(tileManager);
            }
            if (!TempManager.TryGetTarget(out ExtendibleContentManager tempManager))
            {
                tempManager = new ExtendibleContentManager(ServiceProvider, RootDirectory);
                TempManager.SetTarget(tempManager);
            }
            if (DisplayDevice == null)
                DisplayDevice = new xTile.Display.XnaDisplayDevice(mainManager, Game1.game1.GraphicsDevice);
            EnforceContentManager();
            Events.MoreEvents.FireSmartManagerReady();
        }
        private void EnforceContentManager()
        {
            MainManager.TryGetTarget(out ExtendibleContentManager mainManager);
            TileManager.TryGetTarget(out ExtendibleContentManager tileManager);
            TempManager.TryGetTarget(out ExtendibleContentManager tempManager);
            Game1.content = mainManager;
            Game1.mapDisplayDevice = DisplayDevice;
            Game1.game1.xTileContent = tileManager;
            TempContent.SetValue(null, tempManager);
        }
        public static void SaveEvents_AfterReturnToTitle(object s, EventArgs e)
        {
            GameEvents.UpdateTick -= PlayerHelper.Update;
            LocationEvents.CurrentLocationChanged -= PlayerHelper.LocationEvents_CurrentLocationChanged;
            if (Config.GamePatcher)
            {
                GameEvents.UpdateTick -= GamePatcher.Update;
                TimeEvents.DayOfMonthChanged -= GamePatcher.TimeEvents_DayOfMonthChanged;
                Events.MoreEvents.ActionTriggered -= GamePatcher.MoreEvents_ActionTriggered;
            }
        }
        public void CreditsTick(object s, EventArgs e)
        {
            if (!(Game1.activeClickableMenu is StardewValley.Menus.TitleMenu) || Game1.activeClickableMenu == null)
                return;
            if (CreditsDone)
            {
                GameEvents.UpdateTick -= CreditsTick;
                return;
            }
            Game1.playSound("bigDeSelect");
            Helper.Reflection.GetPrivateField<int>(Game1.activeClickableMenu, "logoFadeTimer").SetValue(0);
            Helper.Reflection.GetPrivateField<int>(Game1.activeClickableMenu, "fadeFromWhiteTimer").SetValue(0);
            Game1.delayedActions.Clear();
            Helper.Reflection.GetPrivateField<int>(Game1.activeClickableMenu, "pauseBeforeViewportRiseTimer").SetValue(0);
            Helper.Reflection.GetPrivateField<float>(Game1.activeClickableMenu, "viewportY").SetValue(-999);
            Helper.Reflection.GetPrivateField<float>(Game1.activeClickableMenu, "viewportDY").SetValue(-0.01f);
            Helper.Reflection.GetPrivateField<List<TemporaryAnimatedSprite>>(Game1.activeClickableMenu, "birds").GetValue().Clear();
            Helper.Reflection.GetPrivateField<float>(Game1.activeClickableMenu, "logoSwipeTimer").SetValue(-1);
            Helper.Reflection.GetPrivateField<int>(Game1.activeClickableMenu, "chuckleFishTimer").SetValue(0);
            Game1.changeMusicTrack("MainTheme");
            CreditsDone = true;
        }
        internal static void SaveEvents_AfterLoad(object s, EventArgs e)
        {
            MessageBox.Setup();
            VersionChecker.DoChecks();
            PlayerHelper.ResetForNewGame();
            if (Config.GamePatcher)
            {
                GamePatcher.Patch();
                GameEvents.UpdateTick += GamePatcher.Update;
                TimeEvents.DayOfMonthChanged += GamePatcher.TimeEvents_DayOfMonthChanged;
                Events.MoreEvents.ActionTriggered += GamePatcher.MoreEvents_ActionTriggered;
            }
            GameEvents.UpdateTick += PlayerHelper.Update;
            LocationEvents.CurrentLocationChanged += PlayerHelper.LocationEvents_CurrentLocationChanged;
            Events.MoreEvents.FireWorldReady();
        }
        // Commands

        internal static List<string> Farms = new List<string>() { "standard", "river", "forest", "hilltop", "wilderniss" };
        private static string Verify;
        internal void farm(string command, string[] args)
        {
            if (!Game1.hasLoadedGame)
            {
                Logger.Log("You need to load a game before you can use this command.", LogLevel.Error);
                return;
            }
            switch (command)
            {
                case "farm_settype":
                    if (args.Length == 0)
                        Monitor.Log("Please provide the type you wish to change your farm to.", LogLevel.Error);
                    else if (Farms.Contains(args[0]))
                    {
                        Game1.whichFarm = Farms.IndexOf(args[0]);
                        Logger.Log($"Changed farm type to `{args[0]}`, please sleep in a bed then quit&restart to finalize this change.", LogLevel.Alert);
                    }
                    else
                        Logger.Log("Unknown farm type: " + args[0], LogLevel.Error);
                    break;
                case "farm_clear":
                    if (Verify == null || args.Length == 0 || !args[0].Equals(Verify))
                    {
                        Verify = new Random().Next().ToString();
                        Logger.Log($"This will remove all objects, natural and user-made from your farm, use `farm_clear {Verify}` to verify that you actually want to do this.", LogLevel.Alert);
                        return;
                    }
                    Farm farm = Game1.getFarm();
                    farm.objects.Clear();
                    break;
            }
        }
        internal void player(string command, string[] args)
        {
            if (!Game1.hasLoadedGame)
            {
                Monitor.Log("You need to load a game before you can use this command.", LogLevel.Error);
                return;
            }
            switch (command)
            {
                case "player_warp":
                    try
                    {
                        int x = Convert.ToInt32(args[1]);
                        int y = Convert.ToInt32(args[2]);
                        Game1.warpFarmer(args[0], x, y, false);
                        Monitor.Log("Player warped.", LogLevel.Alert);
                    }
                    catch (Exception err)
                    {
                        Monitor.Log("A error occured trying to warp: ", LogLevel.Error, err);
                    }
                    break;
            }
        }
    }
}
