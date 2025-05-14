using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Text;  //For File Encoding
using System.Windows.Forms;
using System.Windows.Controls;
//using System.Linq; // Needed for Properties().OrderBy
using Newtonsoft.Json.Linq; // Needed for JObject
using System.IO;    // Need for read/write JSON settings file
using SimHub;
//using SimHub.Plugins.InputPlugins;
using System.Net;
using System.Collections.Generic;
using ACToolsUtilities;
using SimHub.Plugins.InputPlugins;   // Needed for Logging

using System.Collections.ObjectModel;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ACToolsUtilities.Input;
using Newtonsoft.Json;
using SimHub.Plugins.Resources;
using WoteverCommon;
using WoteverCommon.Extensions;
using WoteverLocalization;
using static WoteverCommon.JsonExtensions;
using static System.Net.Mime.MediaTypeNames;
using SimHub.Plugins.OutputPlugins.Dash.WPFUI;
using System.Xml.Linq;
using SimHub.Plugins.Devices.DevicesExtensionsDummy;
using System.Windows.Markup;
using SimHub.Plugins.DataPlugins.DataCore;
using System.Linq.Expressions;
using System.Windows.Documents;
using SimHub.Plugins.OutputPlugins.Dash.GLCDTemplating;
using SimHub.Plugins.Devices.UI;

namespace georace.lmuDataPlugin
{
    [PluginName("REDADEG LMU Data plugin")]
    [PluginDescription("plugin for Redadeg SimHub Dashboards\n")]
    [PluginAuthor("Bobokhidze T.B.")]

    //the class name is used as the property headline name in SimHub "Available Properties"
    public class lmuDataPlugin : IPlugin, IDataPlugin,IJoystickPlugin, IWPFSettings
    {
        private List<PitStopDataIndexesClass> PitStopDataIndexes = new List<PitStopDataIndexesClass>();
        

        private ObservableCollection<JoystickDevice> Devices;

        private InputManager im = new InputManager();

        private JoystickManagerSlimDX joystickManager;

        private Thread pollThread;

        private Thread lmu_extendedThread;

        private SettingsControl settingsControlwpf;

        private JoystickPluginSettings settings;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationTokenSource ctsExt = new CancellationTokenSource();

        public bool IsEnded { get; private set; }

        public PluginManager PluginManager { get; set; }

        public bool StopUpdate;
        public int Priority => 1;

        private static int ScreenIndexMax = 7;

        public string LeftMenuTitle => SLoc.GetValue("JoystickPlugin_LeftMenuTitle", "Controllers");
        //input variables
        private string curGame;
        private bool GameInMenu = true;
        private bool GameRunning = true;
        private bool GamePaused = false;
        //private JoystickManagerSlimDX gamepadinput;
        //private string CarModel = "";

        //private float[] TyreRPS = new float[] { 0f, 0f, 0f, 0f };
        int[] lapsForCalculate = new int[] { };
        //private JObject JSONdata_diameters;
        private bool isHybrid = false;
        private bool isDamaged = false;
        private bool isStopAndGo = false;
        private bool haveDriverMenu = false;
        private Guid SessionId;
        //output variables
        private float[] TyreDiameter = new float[] { 0f, 0f, 0f, 0f };   // in meter - FL,FR,RL,RR
        private float[] LngWheelSlip = new float[] { 0f, 0f, 0f, 0f }; // Longitudinal Wheel Slip values FL,FR,RL,RR
        
        private List<double> LapTimes = new List<double>();
        private List<int> EnergyConsuptions = new List<int>();

        //private double energy_AverageConsumptionPer5Lap;
        private int energy_LastLapEnergy = 0;
        private int energy_CurrentIndex = 0;
        private int IsInPit = -1;
        private Guid LastLapId = new Guid();
        
        private int energyPerLastLapRealTime = 0;
        private TimeSpan outFromPitTime = TimeSpan.FromSeconds(0);
        private bool OutFromPitFlag = false;
        private TimeSpan InToPitTime = TimeSpan.FromSeconds(0);
        private bool InToPitFlag = false;
        private int pitStopUpdatePause = -1;
        JObject pitMenuH;
        JObject JSONdata;

        MappedBuffer<LMU_Extended> extendedBuffer = new MappedBuffer<LMU_Extended>(LMU_Constants.MM_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Scoring> scoringBuffer = new MappedBuffer<rF2Scoring>(LMU_Constants.MM_SCORING_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Rules> rulesBuffer = new MappedBuffer<rF2Rules>(LMU_Constants.MM_RULES_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);

        LMU_Extended lmu_extended;
        rF2Scoring scoring;
        rF2Rules rules;

        bool lmu_extended_connected = false;
        bool rf2_score_connected = false;
        public IEnumerable<JoystickDevice> GetDevices()
        {
            JoystickManagerSlimDX obj = joystickManager;
            if (obj == null)
            {
                return null;
            }
           
            return obj.GetDevices();
        }

     

        private void ComputeEnergyData(int CurrentLap, double CurrentLapTime, int pitState ,bool IsLapValid, PluginManager pluginManager)
        {
           // pluginManager.SetPropertyValue("georace.lmu.NewLap", this.GetType(), CurrentLap + " - PitState " + pitState);
           

            //if (pitState > 0)
            //{
            //    energy_LastLapEnergy = currentVirtualEnergy;
            // pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), energy_LastLapEnergy + " - 112" + LMURepairAndRefuelData.currentVirtualEnergy);
            //}
             
            if (energy_LastLapEnergy > LMURepairAndRefuelData.currentVirtualEnergy)
            {
                int energyPerLastLapRaw = energy_LastLapEnergy - LMURepairAndRefuelData.currentVirtualEnergy;
               
                if (OutFromPitFlag) energyPerLastLapRaw = energyPerLastLapRealTime;
;

                //pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(),  energyPerLastLapRaw);

                if ((pitState != CurrentLap && IsLapValid) || OutFromPitFlag || InToPitFlag)
                {
                    IsInPit = -1;
                    if (LapTimes.Count < 5)
                    {
                        energy_CurrentIndex++;
                        LapTimes.Add(CurrentLapTime);
                        EnergyConsuptions.Add(energyPerLastLapRaw);
    
                    }
                    else if (LapTimes.Count == 5)
                    {
                        energy_CurrentIndex++;
                        if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
                        LapTimes[energy_CurrentIndex] = CurrentLapTime;
                        EnergyConsuptions[energy_CurrentIndex] = energyPerLastLapRaw;
                    }
                }
                LMURepairAndRefuelData.energyPerLastLap = (double)(energyPerLastLapRaw);
                LMURepairAndRefuelData.energyPerLast5Lap = EnergyConsuptions.Average() / LMURepairAndRefuelData.maxVirtualEnergy;
                    
                    //pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), LapTimes.Average() + " - " + EnergyConsuptions.Average() / LMURepairAndRefuelData.maxVirtualEnergy);
                }
         

            energy_LastLapEnergy = LMURepairAndRefuelData.currentVirtualEnergy;
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            //curGame = pluginManager.GetPropertyValue("DataCorePlugin.CurrentGame").ToString();
            curGame = data.GameName;
            GameInMenu = data.GameInMenu;
            GameRunning = data.GameRunning;
            GamePaused = data.GamePaused;

            if (data.GameRunning && !data.GameInMenu && !data.GamePaused && !StopUpdate)
            {
                
                if ( curGame == "LMU")   //TODO: check a record where the game was captured from startup on
                {

                   
                    
                    try
                    {
                        WebClient wc = new WebClient();

                        JSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel"));
                        JObject TireMagagementJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/TireManagement"));
                        JObject GameStateJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/sessions/GetGameState"));
                      //  JObject StandingsJSONdata = JObject.Parse(wc.DownloadString(" http://localhost:6397/rest/garage/UIScreen/Standings"));
                   




                        JObject fuelInfo = JObject.Parse(JSONdata["fuelInfo"].ToString());
                        JObject pitStopLength = JObject.Parse(JSONdata["pitStopLength"].ToString());

                        if (pitStopUpdatePause == -1)
                        {
                            pitMenuH = JObject.Parse(JSONdata["pitMenu"].ToString());
                        }
                        else
                        {
                            if (pitStopUpdatePause == 0) // Update pit data if pitStopUpdatePauseCounter is 0
                            {
                                //wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                                //string HtmlResult = wc.UploadString("http://localhost:6397/rest/garage/PitMenu/loadPitMenu", pitMenuH["pitMenu"].ToString());
                                pitStopUpdatePause = -1;
                            }
                            pitStopUpdatePause--;
                        }


                        JObject tireInventory = JObject.Parse(TireMagagementJSONdata["tireInventory"].ToString());
                       // JObject Standings = JObject.Parse(StandingsJSONdata["standings"].ToString());

                        LMURepairAndRefuelData.maxAvailableTires = tireInventory["maxAvailableTires"] != null ?(int)tireInventory["maxAvailableTires"]:0;
                        LMURepairAndRefuelData.newTires = tireInventory["newTires"] != null ? (int)tireInventory["newTires"]: 0;

                        LMURepairAndRefuelData.currentBattery = fuelInfo["currentBattery"] != null ? (int)fuelInfo["currentBattery"]: 0;
                        LMURepairAndRefuelData.currentFuel = fuelInfo["currentFuel"] != null ? (int)fuelInfo["currentFuel"] : 0;
                        LMURepairAndRefuelData.timeOfDay = GameStateJSONdata["timeOfDay"] != null ? (double)GameStateJSONdata["timeOfDay"]: 0;
                        try
                        {
                            JObject InfoForEventJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/sessions/GetSessionsInfoForEvent"));
                            JObject scheduledSessions = JObject.Parse(InfoForEventJSONdata.ToString());

                            foreach (JObject Sesstions in scheduledSessions["scheduledSessions"])
                            {
                                if (Sesstions["name"].ToString().ToUpper().Equals(data.NewData.SessionTypeName.ToUpper())) LMURepairAndRefuelData.rainChance = Sesstions["rainChance"] != null ? (int)Sesstions["rainChance"] : 0;

                            }
                        }
                        catch
                        { 
                        }


                        try
                        {
                            LMURepairAndRefuelData.currentVirtualEnergy = (int)fuelInfo["currentVirtualEnergy"];
                            LMURepairAndRefuelData.maxVirtualEnergy = (int)fuelInfo["maxVirtualEnergy"];
                        }
                        catch
                        {
                            LMURepairAndRefuelData.currentVirtualEnergy = 0;
                            LMURepairAndRefuelData.maxVirtualEnergy = 0;
                        }
                       
                        LMURepairAndRefuelData.maxBattery = (int)fuelInfo["maxBattery"];
                        LMURepairAndRefuelData.maxFuel = (int)fuelInfo["maxFuel"];
                       
                        LMURepairAndRefuelData.pitStopLength = (int)pitStopLength["timeInSeconds"];
                        int Virtual_Energy = 0;
                        int idx = 0;
                        PitStopDataIndexes.Clear();
                        haveDriverMenu = false;
                        isStopAndGo = false;
                        isDamaged = false;

                        //pitStopUpdatePause area
                      
                            foreach (JObject PMCs in pitMenuH["pitMenu"])
                            {



                                if ((int)PMCs["PMC Value"] == 0)
                                {
                                    LMURepairAndRefuelData.passStopAndGo = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    isStopAndGo = true;
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }

                                if ((int)PMCs["PMC Value"] == 1)
                                {
                                    if (idx == 0)
                                    {
                                        isStopAndGo = false;
                                        LMURepairAndRefuelData.passStopAndGo = "";
                                    }
                                    LMURepairAndRefuelData.RepairDamage = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    if (LMURepairAndRefuelData.RepairDamage.Equals("N/A"))
                                    { isDamaged = false; }
                                    else
                                    {
                                        isDamaged = true;
                                        PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                    }

                                }
                                if ((int)PMCs["PMC Value"] == 4)
                                {
                                    LMURepairAndRefuelData.Driver = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    haveDriverMenu = true;
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                try
                                {


                                    if ((int)PMCs["PMC Value"] == 5)
                                    {
                                        LMURepairAndRefuelData.addVirtualEnergy = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                        Virtual_Energy = (int)PMCs["currentSetting"];
                                        pluginManager.SetPropertyValue("georace.lmu.Virtual_Energy", this.GetType(), Virtual_Energy);
                                        PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                    }
                                    if ((int)PMCs["PMC Value"] == 6)
                                    {
                                        if (PMCs["name"].ToString().Equals("FUEL:"))
                                        {
                                            LMURepairAndRefuelData.addFuel = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                            PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                            isHybrid = false;
                                        }
                                        else
                                        {
                                            LMURepairAndRefuelData.FuelRatio = (double)PMCs["settings"][(int)PMCs["currentSetting"]]["text"];
                                            LMURepairAndRefuelData.addFuel = string.Format("{0:f1}", LMURepairAndRefuelData.FuelRatio * Virtual_Energy) + "L" + LMURepairAndRefuelData.addVirtualEnergy.Split('%')[1];
                                            PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                            isHybrid = true;
                                        }
                                    }
                                }
                                catch
                                {
                                    LMURepairAndRefuelData.FuelRatio = 0;
                                }


                                try
                                {
                                    if ((int)PMCs["PMC Value"] == 32)
                                    {
                                        LMURepairAndRefuelData.Grille = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                        PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                    }
                                    if ((int)PMCs["PMC Value"] == 30)
                                    {
                                        LMURepairAndRefuelData.Wing = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                        PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                    }
                                }
                                catch
                                {
                                }

                                if ((int)PMCs["PMC Value"] == 12)
                                {
                                    LMURepairAndRefuelData.fl_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                if ((int)PMCs["PMC Value"] == 13)
                                {
                                    LMURepairAndRefuelData.fr_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                if ((int)PMCs["PMC Value"] == 14)
                                {
                                    LMURepairAndRefuelData.rl_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                if ((int)PMCs["PMC Value"] == 15)
                                {
                                    LMURepairAndRefuelData.rr_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }

                                if ((int)PMCs["PMC Value"] == 35)
                                {
                                    LMURepairAndRefuelData.fl_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                if ((int)PMCs["PMC Value"] == 36)
                                {
                                    LMURepairAndRefuelData.fr_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                if ((int)PMCs["PMC Value"] == 37)
                                {
                                    LMURepairAndRefuelData.rl_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                if ((int)PMCs["PMC Value"] == 38)
                                {
                                    LMURepairAndRefuelData.rr_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }

                                if ((int)PMCs["PMC Value"] == 43)
                                {
                                    LMURepairAndRefuelData.replaceBrakes = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                                }
                                idx++;
                            }
                       

                      
                                           
                        try
                        {
                            if (pluginManager.GetPropertyValue("georace.lmu.ScreenIndex") != null)
                            {
                                LMU_MenuPositions.ScreenIndex = (int)pluginManager.GetPropertyValue("georace.lmu.ScreenIndex");
                                //Logging.Current.Info("ScreenIndex: " + LMU_MenuPositions.ScreenIndex.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Current.Info("ScreenIndex Error: " + ex.ToString());
                        }


                        LMU_MenuPositions.MenuMaxIndex = 11;

                        if (isStopAndGo) LMU_MenuPositions.MenuMaxIndex++;

                        if (isHybrid) LMU_MenuPositions.MenuMaxIndex++;

                        if(isDamaged) LMU_MenuPositions.MenuMaxIndex++;

                        if (haveDriverMenu) LMU_MenuPositions.MenuMaxIndex++;

                        if (isStopAndGo)
                        {
                            pluginManager.AddProperty("georace.lmu.isStopAndGo", this.GetType(), 1);
                        }
                        else
                        {
                            pluginManager.AddProperty("georace.lmu.isStopAndGo", this.GetType(), 0);
                        }
                       
                        if (isHybrid)
                        {
                            pluginManager.SetPropertyValue("georace.lmu.isHyper", this.GetType(), 1);
                        }
                        else
                        {
                            pluginManager.SetPropertyValue("georace.lmu.isHyper", this.GetType(), 0);
                        }

                        if (isDamaged)
                        {
                            pluginManager.SetPropertyValue("georace.lmu.isDamage", this.GetType(), 1);
                        }
                        else
                        {
                            pluginManager.SetPropertyValue("georace.lmu.isDamage", this.GetType(), 0);
                        }
                        if (haveDriverMenu)
                        {
                            pluginManager.SetPropertyValue("georace.lmu.haveDriverMenu", this.GetType(), 1);
                        }
                        else
                        {
                            pluginManager.SetPropertyValue("georace.lmu.haveDriverMenu", this.GetType(), 0);
                        }
                        

                        //data.NewData.SessionOdo < 50 || 
                        if (data.OldData.SessionTypeName != data.NewData.SessionTypeName || data.OldData.IsSessionRestart != data.NewData.IsSessionRestart || !data.SessionId.Equals(SessionId))
                        {
                            SessionId = data.SessionId;
                            Logging.Current.Info("SectorChange: " + data.OldData.IsSessionRestart.ToString() + " - " + data.NewData.IsSessionRestart.ToString());
                            LapTimes.Clear();
                            EnergyConsuptions.Clear();
                            energy_LastLapEnergy = 0;
                            energy_CurrentIndex = 0;
                            LMURepairAndRefuelData.energyPerLast5Lap = 0;
                            LMURepairAndRefuelData.energyPerLastLap = 0;
                            LMURepairAndRefuelData.energyTimeElapsed = 0;


                            LMURepairAndRefuelData.mPlayerBestSector1 = 0;
                            LMURepairAndRefuelData.mPlayerBestSector2 = 0;
                            LMURepairAndRefuelData.mPlayerBestSector3 = 0;

                            LMURepairAndRefuelData.mPlayerCurSector1 = 0;
                            LMURepairAndRefuelData.mPlayerCurSector2 = 0;
                            LMURepairAndRefuelData.mPlayerCurSector3 = 0;

                            LMURepairAndRefuelData.mSessionBestSector1 = 0;
                            LMURepairAndRefuelData.mSessionBestSector2 = 0;
                            LMURepairAndRefuelData.mSessionBestSector3 = 0;

                            LMURepairAndRefuelData.mPlayerBestLapTime = 0;
                            LMURepairAndRefuelData.mPlayerBestLapSector1 = 0;
                            LMURepairAndRefuelData.mPlayerBestLapSector2 = 0;
                            LMURepairAndRefuelData.mPlayerBestLapSector3 = 0;

                            scoringBuffer.ClearStats();
                        }
                     

                        if (isHybrid)
                        {

                            if (energy_LastLapEnergy == 0)
                            {
                                energy_LastLapEnergy = LMURepairAndRefuelData.currentVirtualEnergy;
                            }
                            string mPitStatus = "0";
                            try
                            {
                                mPitStatus = pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentPlayer.mPitState").ToString(); }
                            catch { }

                            pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), mPitStatus + " SetPit Int " + data.NewData.IsInPitSince);

                            if (!mPitStatus.Contains("4") && !mPitStatus.Contains("5")) energyPerLastLapRealTime = energy_LastLapEnergy - LMURepairAndRefuelData.currentVirtualEnergy;
                            pluginManager.SetPropertyValue("georace.lmu.energyPerLastLapRealTime", this.GetType(), energyPerLastLapRealTime);


                            if (mPitStatus.Contains("4") || mPitStatus.Contains("5"))
                            {
                                pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), mPitStatus + " In Pit Lane Remot " + data.NewData.IsInPitSince);
                                energy_LastLapEnergy = LMURepairAndRefuelData.currentVirtualEnergy;
                            }

                            if (data.OldData.IsInPit > 0) IsInPit = data.OldData.CurrentLap;
                           
                          

                         
                            if (data.OldData.IsInPit > data.NewData.IsInPit)
                            {
                                OutFromPitFlag = true;
                                outFromPitTime = data.NewData.CurrentLapTime;
                             //   pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), outFromPitTime.ToString() + " SetPit Out " + data.NewData.IsInPit.ToString());
                            }
                           
                           
                            if (data.OldData.IsInPit < data.NewData.IsInPit)
                            {
                                InToPitFlag = true;
                                InToPitTime = data.NewData.CurrentLapTime;
                              //  pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), InToPitTime + " SetPit Int " + data.NewData.IsInPit.ToString());
                            }
                            if (data.OldData.CurrentLap < data.NewData.CurrentLap)
                            {
                                if (OutFromPitFlag && InToPitFlag)
                                {
                                    ComputeEnergyData(data.OldData.CurrentLap, InToPitTime.TotalSeconds - outFromPitTime.TotalSeconds, IsInPit, data.OldData.IsLapValid, pluginManager);
                                   // pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), data.OldData.IsInPit.ToString() + " OutFromPitFlag_1 " + data.NewData.IsInPit.ToString());
                                    OutFromPitFlag = false;
                                    InToPitFlag = false;
                                }
                                else if (OutFromPitFlag)
                                {
                                    ComputeEnergyData(data.OldData.CurrentLap, data.OldData.CurrentLapTime.TotalSeconds - outFromPitTime.TotalSeconds, IsInPit, data.OldData.IsLapValid, pluginManager);
                                    //pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), data.OldData.IsInPit.ToString() + " OutFromPitFlag_2 " + data.NewData.IsInPit.ToString());
                                  
                                }
                                else
                                {
                                    ComputeEnergyData(data.OldData.CurrentLap, InToPitFlag ? InToPitTime.TotalSeconds : data.OldData.CurrentLapTime.TotalSeconds, IsInPit, data.OldData.IsLapValid, pluginManager);
                                   // pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), data.OldData.IsInPit.ToString() + " cLEARlAP " + data.NewData.IsInPit.ToString());
                                }
                                OutFromPitFlag = false;
                                InToPitFlag = false;
                                outFromPitTime = TimeSpan.FromSeconds(0);
                                InToPitTime = TimeSpan.FromSeconds(0);
                                if (mPitStatus.Contains("4") || mPitStatus.Contains("5")) energy_LastLapEnergy = LMURepairAndRefuelData.currentVirtualEnergy;
                            }
                            else if (LastLapId != data.LapId)
                            {
                                if (OutFromPitFlag && InToPitFlag)
                                {
                                    ComputeEnergyData(data.OldData.CurrentLap, InToPitTime.TotalSeconds - outFromPitTime.TotalSeconds, IsInPit, data.OldData.IsLapValid, pluginManager);
                                   // pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), data.OldData.IsInPit.ToString() + " OutFromPitFlag_1 " + data.NewData.IsInPit.ToString());
                                    OutFromPitFlag = false;
                                    InToPitFlag = false;
                                    outFromPitTime = TimeSpan.FromSeconds(0);
                                    InToPitTime = TimeSpan.FromSeconds(0);
                                } else if (OutFromPitFlag)
                                {
                                    ComputeEnergyData(data.OldData.CurrentLap, data.OldData.CurrentLapTime.TotalSeconds - outFromPitTime.TotalSeconds, IsInPit, data.OldData.IsLapValid, pluginManager);
                                   // pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), data.OldData.IsInPit.ToString() + " OutFromPitFlag_1 " + data.NewData.IsInPit.ToString());
                                    OutFromPitFlag = false;
                                    InToPitFlag = false;
                                    outFromPitTime = TimeSpan.FromSeconds(0);
                                    InToPitTime = TimeSpan.FromSeconds(0);
                                }
                              
                                energy_LastLapEnergy = LMURepairAndRefuelData.currentVirtualEnergy;
                                //ComputeEnergyData(data.OldData.CurrentLap, data.OldData.CurrentLapTime.TotalSeconds, IsInPit, LapOldNew, data.OldData.IsLapValid, pluginManager);

                            }
                           

                            LastLapId = data.LapId;

                            pluginManager.SetPropertyValue("georace.lmu.energyPerLast5Lap", this.GetType(), LMURepairAndRefuelData.energyPerLast5Lap);
                            pluginManager.SetPropertyValue("georace.lmu.energyPerLastLap", this.GetType(), LMURepairAndRefuelData.energyPerLastLap);

                            if (OutFromPitFlag)
                            {
                                
                            }

                            if (EnergyConsuptions.Count() > 0 && LapTimes.Count() > 0)
                            {
                                LMURepairAndRefuelData.energyTimeElapsed = LapTimes.Average() * LMURepairAndRefuelData.currentVirtualEnergy / EnergyConsuptions.Average(); 
                            }
                            else
                            {
                                //real time calculation
                                double energyLapsRealTimeElapsed = data.OldData.TrackPositionPercent * (double)LMURepairAndRefuelData.currentVirtualEnergy / (double)energyPerLastLapRealTime;
                                LMURepairAndRefuelData.energyTimeElapsed = (double)LMURepairAndRefuelData.currentVirtualEnergy * (double)(data.OldData.CurrentLapTime.TotalSeconds - outFromPitTime.TotalSeconds) / (double)energyPerLastLapRealTime;
                                pluginManager.SetPropertyValue("georace.lmu.energyLapsRealTimeElapsed", this.GetType(), energyLapsRealTimeElapsed);
                            }
                            pluginManager.SetPropertyValue("georace.lmu.energyTimeElapsed", this.GetType(), LMURepairAndRefuelData.energyTimeElapsed);
                          


                        }

                        if (!this.rf2_score_connected)
                        {
                            try
                            {
                                // Extended buffer is the last one constructed, so it is an indicator RF2SM is ready.
                                this.scoringBuffer.Connect();
                                this.rf2_score_connected = true;
                            }
                            catch (Exception)
                            {

                                this.rf2_score_connected = false;
                                LMURepairAndRefuelData.mPlayerBestSector1 = 0;
                                LMURepairAndRefuelData.mPlayerBestSector2 = 0;
                                LMURepairAndRefuelData.mPlayerBestSector3 = 0;

                                LMURepairAndRefuelData.mPlayerCurSector1 = 0;
                                LMURepairAndRefuelData.mPlayerCurSector2 = 0;
                                LMURepairAndRefuelData.mPlayerCurSector3 = 0;

                                LMURepairAndRefuelData.mSessionBestSector1 = 0;
                                LMURepairAndRefuelData.mSessionBestSector2 = 0;
                                LMURepairAndRefuelData.mSessionBestSector3 = 0;

                                LMURepairAndRefuelData.mPlayerBestLapTime = 0;
                                LMURepairAndRefuelData.mPlayerBestLapSector1 = 0;
                                LMURepairAndRefuelData.mPlayerBestLapSector2 = 0;
                                LMURepairAndRefuelData.mPlayerBestLapSector3 = 0;
                                //Logging.Current.Info("Extended data update service not connectded.");
                            }
                        }
                        //Calc current times
                        if (data.OldData.CurrentSectorIndex != data.NewData.CurrentSectorIndex)
                        {


                            if (this.rf2_score_connected)
                            {
                                scoringBuffer.GetMappedData(ref scoring);
                                rF2VehicleScoring playerScoring = GetPlayerScoring(ref this.scoring);

                                //double mPlayerLastSector1 = 0.0;
                                //double mPlayerLastSector2 = 0.0;
                                //double mPlayerLastSector3 = 0.0;

                                double mSessionBestSector1 = 0.0;
                                double mSessionBestSector2 = 0.0;
                                double mSessionBestSector3 = 0.0;

                                List<rF2VehicleScoring> OpenentsScoring = GetOpenentsScoring(ref this.scoring);
                                foreach (rF2VehicleScoring OpenentScore in OpenentsScoring)
                                {
                                    if (GetStringFromBytes(playerScoring.mVehicleClass).Equals(GetStringFromBytes(OpenentScore.mVehicleClass)))
                                    {

                                        if (OpenentScore.mCurSector1 > 0) mSessionBestSector1 = OpenentScore.mCurSector1;

                                        if (LMURepairAndRefuelData.mSessionBestSector1 == 0 && OpenentScore.mBestLapSector1 > 0)
                                        {
                                            LMURepairAndRefuelData.mSessionBestSector1 = OpenentScore.mBestLapSector1;
                                        }

                                        if (OpenentScore.mCurSector1 > 0 && OpenentScore.mCurSector2 > 0) mSessionBestSector2 = OpenentScore.mCurSector2 - OpenentScore.mCurSector1;

                                        if (LMURepairAndRefuelData.mSessionBestSector2 == 0 && OpenentScore.mBestLapSector2 > 0 && OpenentScore.mBestLapSector1 > 0)
                                        {
                                            LMURepairAndRefuelData.mSessionBestSector2 = OpenentScore.mBestLapSector2 - OpenentScore.mBestLapSector1;
                                        }

                                        if (OpenentScore.mCurSector2 > 0 && OpenentScore.mLastLapTime > 0) mSessionBestSector3 = OpenentScore.mLastLapTime - OpenentScore.mCurSector2;

                                        if (LMURepairAndRefuelData.mSessionBestSector3 == 0 && OpenentScore.mBestLapTime > 0 && OpenentScore.mBestLapSector2 > 0)
                                        {
                                            LMURepairAndRefuelData.mSessionBestSector3 = OpenentScore.mBestLapTime - OpenentScore.mBestLapSector2;
                                        }


                                        if (LMURepairAndRefuelData.mSessionBestSector1 > mSessionBestSector1 && mSessionBestSector1 > 0) LMURepairAndRefuelData.mSessionBestSector1 = mSessionBestSector1;
                                        if (LMURepairAndRefuelData.mSessionBestSector2 > mSessionBestSector2 && mSessionBestSector2 > 0) LMURepairAndRefuelData.mSessionBestSector2 = mSessionBestSector2;
                                        if (LMURepairAndRefuelData.mSessionBestSector3 > mSessionBestSector3 && mSessionBestSector3 > 0) LMURepairAndRefuelData.mSessionBestSector3 = mSessionBestSector3;

                                    }
                                }
                            }
                            //Logging.Current.Info("SectorChange: " + data.OldData.CurrentSectorIndex.ToString() + " - " + data.NewData.CurrentSectorIndex.ToString());

                            if (data.NewData.Sector1Time.HasValue) LMURepairAndRefuelData.mPlayerCurSector1 = data.NewData.Sector1Time.Value.TotalSeconds;
                            //Logging.Current.Info("Print sector 1: " + data.OldData.Sector1Time.Value.TotalSeconds.ToString() + " - " + data.NewData.Sector1Time.Value.TotalSeconds.ToString());

                        if (data.NewData.Sector2Time.HasValue) LMURepairAndRefuelData.mPlayerCurSector2 = data.NewData.Sector2Time.Value.TotalSeconds;
                        if (data.NewData.Sector3LastLapTime.HasValue) LMURepairAndRefuelData.mPlayerCurSector3 = data.NewData.Sector3LastLapTime.Value.TotalSeconds;

                        if ((LMURepairAndRefuelData.mPlayerBestSector1 > LMURepairAndRefuelData.mPlayerCurSector1 || LMURepairAndRefuelData.mPlayerBestSector1 == 0) && LMURepairAndRefuelData.mPlayerCurSector1 > 0.0) LMURepairAndRefuelData.mPlayerBestSector1 = LMURepairAndRefuelData.mPlayerCurSector1;
                        if ((LMURepairAndRefuelData.mPlayerBestSector2 > LMURepairAndRefuelData.mPlayerCurSector2 || LMURepairAndRefuelData.mPlayerBestSector2 == 0) && LMURepairAndRefuelData.mPlayerCurSector2 > 0.0) LMURepairAndRefuelData.mPlayerBestSector2 = LMURepairAndRefuelData.mPlayerCurSector2;
                        if ((LMURepairAndRefuelData.mPlayerBestSector3 > LMURepairAndRefuelData.mPlayerCurSector3 || LMURepairAndRefuelData.mPlayerBestSector3 == 0) && LMURepairAndRefuelData.mPlayerCurSector3 > 0.0) LMURepairAndRefuelData.mPlayerBestSector3 = LMURepairAndRefuelData.mPlayerCurSector3;

                        LMURepairAndRefuelData.mPlayerBestLapTime = data.NewData.BestLapTime.TotalSeconds > 0 ? data.NewData.BestLapTime.TotalSeconds : 0;
                        LMURepairAndRefuelData.mPlayerBestLapSector1 = data.NewData.Sector1BestLapTime.Value.TotalSeconds > 0 ? data.NewData.Sector1BestLapTime.Value.TotalSeconds : 0;
                        LMURepairAndRefuelData.mPlayerBestLapSector2 = data.NewData.Sector2BestLapTime.Value.TotalSeconds > 0 ? data.NewData.Sector2BestLapTime.Value.TotalSeconds : 0;
                        LMURepairAndRefuelData.mPlayerBestLapSector3 = data.NewData.Sector3BestLapTime.Value.TotalSeconds > 0 ? data.NewData.Sector3BestLapTime.Value.TotalSeconds : 0;
                        }
                        //Calc current times end

                        pluginManager.SetPropertyValue("georace.lmu.passStopAndGo", this.GetType(), LMURepairAndRefuelData.passStopAndGo);
                        pluginManager.SetPropertyValue("georace.lmu.RepairDamage", this.GetType(), LMURepairAndRefuelData.RepairDamage);
                        pluginManager.SetPropertyValue("georace.lmu.Driver", this.GetType(), LMURepairAndRefuelData.Driver);
                        pluginManager.SetPropertyValue("georace.lmu.rainChance", this.GetType(), LMURepairAndRefuelData.rainChance);
                        pluginManager.SetPropertyValue("georace.lmu.timeOfDay", this.GetType(), LMURepairAndRefuelData.timeOfDay);
                        pluginManager.SetPropertyValue("georace.lmu.FuelRatio", this.GetType(), LMURepairAndRefuelData.FuelRatio);
                        pluginManager.SetPropertyValue("georace.lmu.currentFuel", this.GetType(), LMURepairAndRefuelData.currentFuel);
                        pluginManager.SetPropertyValue("georace.lmu.addFuel", this.GetType(), LMURepairAndRefuelData.addFuel);
                        pluginManager.SetPropertyValue("georace.lmu.addVirtualEnergy", this.GetType(), LMURepairAndRefuelData.addVirtualEnergy);
                        pluginManager.SetPropertyValue("georace.lmu.Wing", this.GetType(), LMURepairAndRefuelData.Wing);
                        pluginManager.SetPropertyValue("georace.lmu.Grille", this.GetType(), LMURepairAndRefuelData.Grille);

                        pluginManager.SetPropertyValue("georace.lmu.maxAvailableTires", this.GetType(), LMURepairAndRefuelData.maxAvailableTires);
                        pluginManager.SetPropertyValue("georace.lmu.newTires", this.GetType(), LMURepairAndRefuelData.newTires);

                        pluginManager.SetPropertyValue("georace.lmu.currentBattery", this.GetType(), LMURepairAndRefuelData.currentBattery);
                        pluginManager.SetPropertyValue("georace.lmu.currentVirtualEnergy", this.GetType(), LMURepairAndRefuelData.currentVirtualEnergy);
                        pluginManager.SetPropertyValue("georace.lmu.pitStopLength", this.GetType(), LMURepairAndRefuelData.pitStopLength);

                        pluginManager.SetPropertyValue("georace.lmu.fl_TyreChange", this.GetType(), LMURepairAndRefuelData.fl_TyreChange);
                        pluginManager.SetPropertyValue("georace.lmu.fr_TyreChange", this.GetType(), LMURepairAndRefuelData.fr_TyreChange);
                        pluginManager.SetPropertyValue("georace.lmu.rl_TyreChange", this.GetType(), LMURepairAndRefuelData.rl_TyreChange);
                        pluginManager.SetPropertyValue("georace.lmu.rr_TyreChange", this.GetType(), LMURepairAndRefuelData.rr_TyreChange);

                        pluginManager.SetPropertyValue("georace.lmu.fl_TyrePressure", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure);
                        pluginManager.SetPropertyValue("georace.lmu.fr_TyrePressure", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure);
                        pluginManager.SetPropertyValue("georace.lmu.rl_TyrePressure", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure);
                        pluginManager.SetPropertyValue("georace.lmu.rr_TyrePressure", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure);
                        pluginManager.SetPropertyValue("georace.lmu.replaceBrakes", this.GetType(), LMURepairAndRefuelData.replaceBrakes);

                        pluginManager.SetPropertyValue("georace.lmu.maxBattery", this.GetType(), LMURepairAndRefuelData.maxBattery);
                        pluginManager.SetPropertyValue("georace.lmu.maxFuel", this.GetType(), LMURepairAndRefuelData.maxFuel);
                        pluginManager.SetPropertyValue("georace.lmu.maxVirtualEnergy", this.GetType(), LMURepairAndRefuelData.maxVirtualEnergy);
                        pluginManager.SetPropertyValue("georace.lmu.selectedMenuIndex", this.GetType(), LMU_MenuPositions.selectedMenuIndex);
                        pluginManager.SetPropertyValue("georace.lmu.ScreenIndex", this.GetType(), LMU_MenuPositions.ScreenIndex);

                        pluginManager.SetPropertyValue("georace.lmu.Extended.Cuts", this.GetType(), LMURepairAndRefuelData.Cuts);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.CutsMax", this.GetType(), LMURepairAndRefuelData.CutsMax);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.PenaltyLeftLaps", this.GetType(), LMURepairAndRefuelData.PenaltyLeftLaps);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.PenaltyType", this.GetType(), LMURepairAndRefuelData.PenaltyType);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.PenaltyCount", this.GetType(), LMURepairAndRefuelData.PenaltyCount);
                        
                        pluginManager.SetPropertyValue("georace.lmu.Extended.PendingPenaltyType1", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType1);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.PendingPenaltyType2", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType2);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.PendingPenaltyType3", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType3);

                        pluginManager.SetPropertyValue("georace.lmu.Extended.TractionControl", this.GetType(), LMURepairAndRefuelData.mpTractionControl);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.BrakeMigration", this.GetType(), LMURepairAndRefuelData.mpBrakeMigration);
                        pluginManager.SetPropertyValue("georace.lmu.Extended.BrakeMigrationMax", this.GetType(), LMURepairAndRefuelData.mpBrakeMigrationMax);

                        pluginManager.SetPropertyValue("georace.lmu.mPlayerBestSector1", this.GetType(), LMURepairAndRefuelData.mPlayerBestSector1);
                        pluginManager.SetPropertyValue("georace.lmu.mPlayerBestSector2", this.GetType(), LMURepairAndRefuelData.mPlayerBestSector2);
                        pluginManager.SetPropertyValue("georace.lmu.mPlayerBestSector3", this.GetType(), LMURepairAndRefuelData.mPlayerBestSector3);

                        pluginManager.SetPropertyValue("georace.lmu.mPlayerCurSector1", this.GetType(), LMURepairAndRefuelData.mPlayerCurSector1);
                        pluginManager.SetPropertyValue("georace.lmu.mPlayerCurSector2", this.GetType(), LMURepairAndRefuelData.mPlayerCurSector2);
                        pluginManager.SetPropertyValue("georace.lmu.mPlayerCurSector3", this.GetType(), LMURepairAndRefuelData.mPlayerCurSector3);

                        pluginManager.SetPropertyValue("georace.lmu.mSessionBestSector1", this.GetType(), LMURepairAndRefuelData.mSessionBestSector1);
                        pluginManager.SetPropertyValue("georace.lmu.mSessionBestSector2", this.GetType(), LMURepairAndRefuelData.mSessionBestSector2);
                        pluginManager.SetPropertyValue("georace.lmu.mSessionBestSector3", this.GetType(), LMURepairAndRefuelData.mSessionBestSector3);

                        pluginManager.SetPropertyValue("georace.lmu.mPlayerBestLapTime", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapTime);
                        pluginManager.SetPropertyValue("georace.lmu.mPlayerBestLapSector1", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapSector1);
                        pluginManager.SetPropertyValue("georace.lmu.mPlayerBestLapSector2", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapSector2);
                        pluginManager.SetPropertyValue("georace.lmu.mPlayerBestLapSector3", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapSector3);

                        pluginManager.SetPropertyValue("georace.lmu.Clock_Format24", this.GetType(), ButtonBindSettings.Clock_Format24);
                        pluginManager.SetPropertyValue("georace.lmu.RealTimeClock", this.GetType(), ButtonBindSettings.RealTimeClock);

                        //DataCorePlugin.GameRawData.CurrentPlayerTelemetry.mRearBrakeBias
                        double mRearBrakeBias = 0.0;
                        try
                        { 
                            mRearBrakeBias = (double)pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentPlayerTelemetry.mRearBrakeBias"); 
                        }
                        catch
                        { }
                       
                        pluginManager.SetPropertyValue("georace.lmu.elecMenuValue6", this.GetType(), data.OldData.BrakeBias.ToString("0.0") + ":" + (mRearBrakeBias*100).ToString("0.0"));

                        //if (lmu_extended_connected)
                        //{
                        //    pluginManager.SetPropertyValue("georace.lmu.mMessage", this.GetType(), GetStringFromBytes( rules.mTrackRules.mMessage));

                        //}

                        isStopAndGo = false;
                        LMURepairAndRefuelData.passStopAndGo = "";
                    }
                    // if there is no settings file, use the following defaults
                    catch (Exception ex)
                    {
                        LMURepairAndRefuelData.currentBattery = 50;
                        LMURepairAndRefuelData.currentFuel = 0;
                        LMURepairAndRefuelData.currentVirtualEnergy = 5;
                        LMURepairAndRefuelData.maxBattery = 486000000;
                        LMURepairAndRefuelData.maxVirtualEnergy = 920000000;
                        LMURepairAndRefuelData.pitStopLength = 0;
                        Logging.Current.Info("Plugin georace.lmuDataPlugin: " + ex.ToString());
                        
                    }
                 }
                StopUpdate = false;
            }
            
        }





        /// <summary>
        /// Called at plugin manager stop, close/displose anything needed here !
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            IsEnded = true;
            cts.Cancel();
            pollThread.Join();
            joystickManager.End();
            // try to read complete data file from disk, compare file data with new data and write new file if there are diffs
            try
            {
                if (rf2_score_connected) this.scoringBuffer.Disconnect();
                if(lmu_extended_connected) this.extendedBuffer.Disconnect();
                if (lmu_extended_connected) this.rulesBuffer.Disconnect();
               
                //WebClient wc = new WebClient();
                //JObject JSONcurGameData = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel"));

            }
            // if there is not already a settings file on disk, create new one and write data for current game
            catch (FileNotFoundException)
            {
                // try to write data file
               
            }
            // other errors like Syntax error on JSON parsing, data file will not be saved
            catch (Exception ex)
            {
                Logging.Current.Info("Plugin Viper.PluginCalcLngWheelSlip - data file not saved. The following error occured: " + ex.Message);
            }
        }

        /// <summary>
        /// Return you winform settings control here, return null if no settings control
        /// 
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <returns></returns>
        public System.Windows.Forms.Control GetSettingsControl(PluginManager pluginManager)
        {
            return null;
        }

        public  System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            if (settingsControlwpf == null)
            {
                settingsControlwpf = new SettingsControl();
            }

            return settingsControlwpf;
        }

        private void LoadSettings(PluginManager pluginManager)
        {
            //IL_006a: Unknown result type (might be due to invalid IL or missing references)
            //IL_006f: Unknown result type (might be due to invalid IL or missing references)
            //IL_007c: Unknown result type (might be due to invalid IL or missing references)
            //IL_008e: Expected O, but got Unknown
            string commonStoragePath = pluginManager.GetCommonStoragePath("JoystickPluginSettings.json");
            if (!File.Exists(commonStoragePath))
            {
                Dictionary<Guid, string> obj = JsonExtensions.FromJsonFileWithVersionning<Dictionary<Guid, string>>(pluginManager.GetCommonStoragePath("JoystickNames.json"), 5, (JsonSerializerSettings)null) ?? new Dictionary<Guid, string>();
                settings = new JoystickPluginSettings();
                {
                    foreach (KeyValuePair<Guid, string> item in obj)
                    {
                        settings.JoystickSettings.Add(new JoystickSettings
                        {
                            InstanceId = item.Key,
                            Name = item.Value
                        });
                    }

                    return;
                }
            }

            settings = JsonExtensions.FromJsonFileWithVersionning<JoystickPluginSettings>(commonStoragePath, 5, (JsonSerializerSettings)null) ?? new JoystickPluginSettings();
        }


        private void JoystickManager_JoystickConnected(object sender, JoystickDevice e)
        {
            //foreach (KeyValuePair<string, Func<int>> item in e.GetAxis(0))
            //{
            //    KeyValuePair<string, Func<int>> entryTmp = item;
            //    string key = entryTmp.Key;
            //    Func<int?> valueProvider = () => (!e.Connected) ? null : new int?(entryTmp.Value());
            //    PluginManager.AttachDelegate(e.Name + "_" + key, GetType(), valueProvider);
            //}
        }

        //public void SaveSettings()
        //{
        //    JsonExtensions.ToJsonFileWithVersionning((object)settings, PluginManager.GetCommonStoragePath("JoystickPluginSettings.json"), 5, (JsonSerializerSettings)null, (AutoBackupStrategy)0, true);
        //}

        private void JoystickManager_JoystickChanged(object sender, IList<JoystickDevice> e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(delegate
            {
                Devices.Clear();
                foreach (JoystickDevice item in e)
                {
                    Devices.Add(item);
                }
            });
        }

        private void lmu_extendedReadThread()
        {
            try
            {
                Task.Delay(500, cts.Token).Wait();
            
                while (!IsEnded)
                {
                    if (!this.lmu_extended_connected)
                    {
                        try
                        {
                            // Extended buffer is the last one constructed, so it is an indicator RF2SM is ready.
                            this.extendedBuffer.Connect();
                            this.rulesBuffer.Connect();
                            
                            this.lmu_extended_connected = true; 
                        }
                        catch (Exception)
                        {
                            LMURepairAndRefuelData.Cuts = 0;
                            LMURepairAndRefuelData.CutsMax = 0;
                            LMURepairAndRefuelData.PenaltyLeftLaps = 0;
                            LMURepairAndRefuelData.PenaltyType = 0;
                            LMURepairAndRefuelData.PenaltyCount = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType1 = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType2 = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType3 = 0;
                            LMURepairAndRefuelData.mpBrakeMigration = 0;
                            LMURepairAndRefuelData.mpBrakeMigrationMax = 0;
                            LMURepairAndRefuelData.mpTractionControl = 0;
                            this.lmu_extended_connected = false;
                            //Logging.Current.Info("Extended data update service not connectded.");
                        }
                    }
                    else
                    {
                        extendedBuffer.GetMappedData(ref lmu_extended);
                        rulesBuffer.GetMappedData(ref rules);
                        LMURepairAndRefuelData.Cuts = lmu_extended.mCuts;
                        LMURepairAndRefuelData.CutsMax = lmu_extended.mCutsPoints;
                        LMURepairAndRefuelData.PenaltyLeftLaps  = lmu_extended.mPenaltyLeftLaps;
                        LMURepairAndRefuelData.PenaltyType = lmu_extended.mPenaltyType;
                        LMURepairAndRefuelData.PenaltyCount = lmu_extended.mPenaltyCount;
                        LMURepairAndRefuelData.mPendingPenaltyType1 = lmu_extended.mPendingPenaltyType1;
                        LMURepairAndRefuelData.mPendingPenaltyType2 = lmu_extended.mPendingPenaltyType2;
                        LMURepairAndRefuelData.mPendingPenaltyType3 = lmu_extended.mPendingPenaltyType3;
                        LMURepairAndRefuelData.mpBrakeMigration = lmu_extended.mpBrakeMigration;
                        LMURepairAndRefuelData.mpBrakeMigrationMax = lmu_extended.mpBrakeMigrationMax;
                        LMURepairAndRefuelData.mpTractionControl = lmu_extended.mpTractionControl;

                        
                       

                        //Logging.Current.Info(("Extended data update service connectded. " +  lmu_extended.mCutsPoints.ToString() + " Penalty laps" + lmu_extended.mPenaltyLeftLaps).ToString());
                    }





                 Thread.Sleep(50);

                
                }

            }
            catch (AggregateException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static string GetStringFromBytes(byte[] bytes)
        {
            if (bytes == null)
                return "";

            var nullIdx = Array.IndexOf(bytes, (byte)0);

            return nullIdx >= 0
              ? Encoding.Default.GetString(bytes, 0, nullIdx)
              : Encoding.Default.GetString(bytes);
        }
        public static rF2VehicleScoring GetPlayerScoring(ref rF2Scoring scoring)
        {
            var playerVehScoring = new rF2VehicleScoring();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = scoring.mVehicles[i];
                switch ((LMU_Constants.rF2Control)vehicle.mControl)
                {
                    case LMU_Constants.rF2Control.AI:
                    case LMU_Constants.rF2Control.Player:
                    case LMU_Constants.rF2Control.Remote:
                        if (vehicle.mIsPlayer == 1)
                            playerVehScoring = vehicle;

                        break;

                    default:
                        continue;
                }

                if (playerVehScoring.mIsPlayer == 1)
                    break;
            }

            return playerVehScoring;
        }

        public static List<rF2VehicleScoring> GetOpenentsScoring(ref rF2Scoring scoring)
        {
            List<rF2VehicleScoring> playersVehScoring  = new List<rF2VehicleScoring>();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = scoring.mVehicles[i];
                switch ((LMU_Constants.rF2Control)vehicle.mControl)
                {
                    case LMU_Constants.rF2Control.AI:
                        //if (vehicle.mIsPlayer != 1)
                            playersVehScoring.Add(vehicle);
                        break;
                    case LMU_Constants.rF2Control.Player:
                    case LMU_Constants.rF2Control.Remote:
                        //if (vehicle.mIsPlayer != 1)
                            playersVehScoring.Add(vehicle);

                        break;

                    default:
                        continue;
                }

             }

            return playersVehScoring;
        }

        private void PollControllers()
        {
            try
            {
                Task.Delay(500, cts.Token).Wait();
                string currentPressInput = "press";
                string currentReleaseInput = "release";
                int count = 0;
                const int LongInputTime = 100;
                while (!IsEnded)
                {
                   

                    List<string> state = joystickManager.GetState();
                    im.SetCurrentInputs(state);
                    foreach (string newInput in im.GetNewInputs())
                    {
                        if (PluginManager != null)
                        {
                            PluginManager.TriggerInputPress(newInput, typeof(JoystickPlugin));
                        }
                        //Logging.Current.Info("Joystick: " + newInput + " pressed");
                        //SimHub.Logging.Current.Info("selectedMenuIndex changed " + LMU_MenuPositions.selectedMenuIndex.ToString());
                        currentPressInput = newInput;
                        currentReleaseInput = "ButtonPressed";
                      //  Logging.Current.Info("GameInMenu - " + GameInMenu.ToString() + " Game paused: " + GamePaused);

                    }

                    foreach (string releasedInput in im.GetReleasedInputs())
                        {
                            if (PluginManager != null)
                            {
                                PluginManager.TriggerInputRelease(releasedInput, typeof(JoystickPlugin));
                            }
                            currentReleaseInput = releasedInput;
                            
                            //Logging.Current.Info("Joystick: " + releasedInput + " released");
                        }

                        Thread.Sleep(5);

                    if (currentPressInput.Equals(currentReleaseInput) )
                    {
                        if (count < LongInputTime)
                        {
                            //Logging.Current.Info("Joystick: " + currentPressInput + " ShortInput");
                            
                            ShortInput(currentPressInput);
                        }

                        if (count > LongInputTime)
                        {
                            LongInputEnd(currentPressInput);

                            //Logging.Current.Info("Joystick: " + currentPressInput + " LongInput END" + count);
                        }
                        currentPressInput = "press";
                        currentReleaseInput = "release";
                        count = 0;
                    }
                    else if (currentReleaseInput.Equals("ButtonPressed"))
                    {
                        count++;
                        //Logging.Current.Info("Joystick: " + currentPressInput + " LongInput " + count);
                    }

              
                    if (count > LongInputTime)
                    {
                        LongInput(currentPressInput);
                       // Logging.Current.Info("Joystick: " + currentPressInput + " LongInput" + count);
                    }

                }

               

            }
            catch (AggregateException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void LongInputEnd(string newInput)
        {
            //Logging.Current.Info("Joystick: " + newInput + " LongInput END" + ButtonBindSettings.UseLongPressLeftAndRight);
            if (ButtonBindSettings.UseLongPressLeftAndRight)
            {
                if (newInput.Contains(ButtonBindSettings.RIGHT))
                {
                    IncrementScreenValue();
                }
                if (newInput.Contains(ButtonBindSettings.LEFT))
                {
                    DecrementScreenValue();
                }
            }
           
        }
        private void LongInput(string newInput)
        {
           // Logging.Current.Info("Joystick: " + newInput + " LongInput ");
            if (!ButtonBindSettings.UseLongPressLeftAndRight)
            {
                if (newInput.Contains(ButtonBindSettings.RIGHT))
                {
                    IncrementParameterValue();
                }

                if (newInput.Contains(ButtonBindSettings.LEFT))
                {
                    DecrementParameterValue();
                }
            }
        }
        private void ShortInput(string newInput)
        {

            if (ButtonBindSettings.waitinput)
            {
               if (settingsControlwpf != null) settingsControlwpf.Refresh(newInput);
            }
            //if (changedBind)
            //{
            //    ButtonBindSettings.waitinput = false;
            //    SaveJSonSetting();
            //    System.Windows.Controls.Control settingsControlwpf = GetWPFSettingsControl(PluginManager);
            //    //if (settingsControlwpf != null)
            //    //{

            //    //    settingsControlwpf.UpdateLayout();
            //    //}
            //}
            if (LMU_MenuPositions.ScreenIndex == ScreenIndexMax)
            {
            if (newInput.Equals(ButtonBindSettings.UP))
            {
                if (LMU_MenuPositions.selectedMenuIndex > 0)
                {
                    LMU_MenuPositions.selectedMenuIndex--;
                }
                else
                {
                    if (LMU_MenuPositions.selectedMenuIndex == 0) LMU_MenuPositions.selectedMenuIndex = LMU_MenuPositions.MenuMaxIndex;
                }
            }

            if (newInput.Equals(ButtonBindSettings.DOWN))
            {
                if (LMU_MenuPositions.selectedMenuIndex < LMU_MenuPositions.MenuMaxIndex)
                {
                    LMU_MenuPositions.selectedMenuIndex++;
                }
                else
                {
                    if (LMU_MenuPositions.selectedMenuIndex == LMU_MenuPositions.MenuMaxIndex) LMU_MenuPositions.selectedMenuIndex = 0;
                }
                //SimHub.Logging.Current.Info("selectedMenuIndex changed " + LMU_MenuPositions.selectedMenuIndex.ToString());

            }

            if (newInput.Equals(ButtonBindSettings.RIGHT))
            {
                IncrementParameterValue();
            }

            if (newInput.Equals(ButtonBindSettings.LEFT))
            {
                DecrementParameterValue();
            }
            }
            try
            {
                //Logging.Current.Info("new Input: " + newInput);
                if (!ButtonBindSettings.UseLongPressLeftAndRight)
                {
                    if (newInput.Equals(ButtonBindSettings.NEXTSCREEN))
                    {
                        
                        IncrementScreenValue();
                      
                    }
                    if (newInput.Equals(ButtonBindSettings.PREVSCREEN))
                    {
                        DecrementScreenValue();
                   
                    }
                }
            }
            catch(Exception ex) { Logging.Current.Info("ERROR Change value: " + ex.ToString()); }

        }

        private void SaveJSonSetting()
        {
            JObject JSONdata = new JObject(
                   new JProperty("KeyMapUp", ButtonBindSettings.UP),
                   new JProperty("KeyMapDown", ButtonBindSettings.DOWN),
                   new JProperty("KeyMapLeft", ButtonBindSettings.LEFT),
                   new JProperty("KeyMapRight", ButtonBindSettings.RIGHT),
                   new JProperty("NextScreen", ButtonBindSettings.NEXTSCREEN),
                   new JProperty("PrevScreen", ButtonBindSettings.PREVSCREEN),
                   new JProperty("UseLongPressLeftAndRight", ButtonBindSettings.UseLongPressLeftAndRight),
                   new JProperty("LastScreenIndex", LMU_MenuPositions.ScreenIndex));
            //string settings_path = AccData.path;
            try
            {
                // create/write settings file
                File.WriteAllText(LMURepairAndRefuelData.path, JSONdata.ToString());
                //Logging.Current.Info("Plugin Viper.PluginCalcLngWheelSlip - Settings file saved to : " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);
            }
            catch
            {
                //A MessageBox creates graphical glitches after closing it. Search another way, maybe using the Standard Log in SimHub\Logs
                //MessageBox.Show("Cannot create or write the following file: \n" + System.Environment.CurrentDirectory + "\\" + AccData.path, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //Logging.Current.Error("Plugin Viper.PluginCalcLngWheelSlip - Cannot create or write settings file: " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);


            }
        }
        private void IncrementScreenValue()
        {
            if (LMU_MenuPositions.ScreenIndex >= ScreenIndexMax)
            {
                //Logging.Current.Info("new Input:  increment set 0");
                LMU_MenuPositions.ScreenIndex = 0;
            }
            else
            {
                LMU_MenuPositions.ScreenIndex++;
                //Logging.Current.Info("new Input:  increment " + LMU_MenuPositions.ScreenIndex);
            }
            SaveJSonSetting();

        }
        private void DecrementScreenValue()
        {
            if (LMU_MenuPositions.ScreenIndex <= 0)
            {
                LMU_MenuPositions.ScreenIndex = ScreenIndexMax;
            }
            else
            {
                LMU_MenuPositions.ScreenIndex--;
            }
            SaveJSonSetting();
        }

        private void IncrementParameterValue()
        {
            pitStopUpdatePause = 3000;
            if (GameRunning && !GameInMenu && !GamePaused && LMU_MenuPositions.ScreenIndex == ScreenIndexMax)
            {
                try
                {
                    pitMenuH = JObject.Parse(JSONdata["pitMenu"].ToString());
                    //foreach (PitStopDataIndexesClass PitStopindex in PitStopDataIndexes)
                    //{
                    //    Logging.Current.Info("Index: " + PitStopindex.index + " Max: " + PitStopindex.maxvalue + " Name:" + PitStopindex.name);

                    //}
                    int CurrentSetting = (int)pitMenuH["pitMenu"][PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].index]["currentSetting"];
                    if (CurrentSetting < PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].maxvalue)
                    {
                        pitMenuH["pitMenu"][PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].index]["currentSetting"] = CurrentSetting + 1;
                        // Logging.Current.Info("Index: CHangdf value " + PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].index + "\r\n");
                    }
                    else
                    {
                        pitMenuH["pitMenu"][PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].index]["currentSetting"] = 0;
                    }

                }
                catch (Exception ex)
                {
                    Logging.Current.Info("ERROR Change value: " + ex.ToString());
                }
            }
         }

        private void DecrementParameterValue()
        {
            pitStopUpdatePause = 3000;
            if (GameRunning && !GameInMenu && !GamePaused && LMU_MenuPositions.ScreenIndex == ScreenIndexMax)
            {
                
                try
                {
                    pitMenuH = JObject.Parse(JSONdata["pitMenu"].ToString());

                    int CurrentSetting = (int)pitMenuH["pitMenu"][PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].index]["currentSetting"];
                    if (CurrentSetting > 0)
                    {
                        pitMenuH["pitMenu"][PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].index]["currentSetting"] = CurrentSetting - 1;
                        //Logging.Current.Info("Changed value: " + pitMenuH["pitMenu"][0].ToString());
                    }
                    else if (CurrentSetting == 0)
                    {
                        pitMenuH["pitMenu"][PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].index]["currentSetting"] = PitStopDataIndexes[LMU_MenuPositions.selectedMenuIndex].maxvalue;
                    }
                }
                catch (Exception ex)
                {
                    Logging.Current.Info("ERROR Change value: " + ex.ToString());
                }
            }
        }
        public void Init(PluginManager pluginManager)
        {
            // set path/filename for settings file
            LMURepairAndRefuelData.path = PluginManager.GetCommonStoragePath("georace.lmuDataPlugin.json");
            string path_data = PluginManager.GetCommonStoragePath("georace.lmuDataPlugin.data.json");
            LMU_MenuPositions.MenuMaxIndex = 11;
            LMU_MenuPositions.selectedMenuIndex = 0;
            LMU_MenuPositions.ScreenIndex = ScreenIndexMax;
            //List<PitStopDataIndexesClass> PitStopDataIndexes = new List<PitStopDataIndexesClass>();
            // try to read settings file




            LoadSettings(pluginManager);
            joystickManager = new JoystickManagerSlimDX((IList<JoystickSettings>)settings.JoystickSettings);
            joystickManager.JoystickChanged += JoystickManager_JoystickChanged;
            joystickManager.JoystickConnected += JoystickManager_JoystickConnected;
            Devices = new ObservableCollection<JoystickDevice>();
            pollThread = new Thread(PollControllers)
            {
                Name = "JoystickPollThread"
            };
            pollThread.Start();

            lmu_extendedThread = new Thread(lmu_extendedReadThread)
            {
                Name = "ExtendedDataUpdateThread"
            };
            lmu_extendedThread.Start();

            Logging.Current.Info("Plugin georace.lmuDataPlugin - try devices update.");

            try
            {
                JObject JSONdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.UP = JSONdata["KeyMapUp"] != null ? JSONdata["KeyMapUp"].ToString(): "";
                ButtonBindSettings.DOWN = JSONdata["KeyMapDown"] != null ? JSONdata["KeyMapDown"].ToString(): "";
                ButtonBindSettings.LEFT = JSONdata["KeyMapLeft"] != null ? JSONdata["KeyMapLeft"].ToString(): "";
                ButtonBindSettings.RIGHT = JSONdata["KeyMapRight"] != null ? JSONdata["KeyMapRight"].ToString(): "";
                ButtonBindSettings.UseLongPressLeftAndRight = JSONdata["UseLongPressLeftAndRight"] != null ?  (bool)JSONdata["UseLongPressLeftAndRight"]: false;
                ButtonBindSettings.Clock_Format24 = JSONdata["Clock_Format24"] != null ? (bool)JSONdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONdata["RealTimeClock"] != null ? (bool)JSONdata["RealTimeClock"] : false;
                ButtonBindSettings.NEXTSCREEN = JSONdata["NextScreen"] != null ? JSONdata["NextScreen"].ToString() : "";
                ButtonBindSettings.PREVSCREEN = JSONdata["PrevScreen"] != null ? JSONdata["PrevScreen"].ToString() : "";
                LMU_MenuPositions.ScreenIndex = JSONdata["LastScreenIndex"] != null ? (int)JSONdata["LastScreenIndex"]: ScreenIndexMax;
                //                Logging.Current.Info("Plugin Viper.PluginCalcLngWheelSlip - Settings file " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path + " loaded.");
            }
            catch { }

            //var joystickDevices = GetDevices();
            //if (joystickDevices != null)
            //{ 
            //    foreach (JoystickDevice joy in GetDevices())
            //    {
            //        Logging.Current.Info("Joystic: " + joy.Name);
            //    }
            //}

            pluginManager.AddProperty("georace.lmu.energyPerLast5Lap", this.GetType(), LMURepairAndRefuelData.energyPerLast5Lap);
            pluginManager.AddProperty("georace.lmu.energyPerLastLap", this.GetType(), LMURepairAndRefuelData.energyPerLastLap);
            pluginManager.AddProperty("georace.lmu.energyPerLastLapRealTime", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.energyLapsRealTimeElapsed", this.GetType(), 0);

            pluginManager.AddProperty("georace.lmu.energyTimeElapsed", this.GetType(), LMURepairAndRefuelData.energyTimeElapsed);

            pluginManager.AddProperty("georace.lmu.NewLap", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.rainChance", this.GetType(), LMURepairAndRefuelData.rainChance);
            pluginManager.AddProperty("georace.lmu.timeOfDay", this.GetType(), LMURepairAndRefuelData.timeOfDay);
            pluginManager.AddProperty("georace.lmu.passStopAndGo", this.GetType(), LMURepairAndRefuelData.passStopAndGo);
            pluginManager.AddProperty("georace.lmu.RepairDamage", this.GetType(), LMURepairAndRefuelData.RepairDamage);
            pluginManager.AddProperty("georace.lmu.Driver", this.GetType(), LMURepairAndRefuelData.Driver);
            pluginManager.AddProperty("georace.lmu.FuelRatio", this.GetType(), LMURepairAndRefuelData.FuelRatio);
            pluginManager.AddProperty("georace.lmu.currentFuel", this.GetType(), LMURepairAndRefuelData.currentFuel);
            pluginManager.AddProperty("georace.lmu.addFuel", this.GetType(), LMURepairAndRefuelData.addFuel);
            pluginManager.AddProperty("georace.lmu.addVirtualEnergy", this.GetType(), LMURepairAndRefuelData.addVirtualEnergy);
            pluginManager.AddProperty("georace.lmu.Wing", this.GetType(), LMURepairAndRefuelData.Wing);
            pluginManager.AddProperty("georace.lmu.Grille", this.GetType(), LMURepairAndRefuelData.Grille);
            pluginManager.AddProperty("georace.lmu.Virtual_Energy", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.currentBattery", this.GetType(), LMURepairAndRefuelData.currentBattery);
            pluginManager.AddProperty("georace.lmu.currentVirtualEnergy", this.GetType(), LMURepairAndRefuelData.currentVirtualEnergy);
            pluginManager.AddProperty("georace.lmu.pitStopLength", this.GetType(), LMURepairAndRefuelData.pitStopLength);

            pluginManager.AddProperty("georace.lmu.maxAvailableTires", this.GetType(), LMURepairAndRefuelData.maxAvailableTires);
            pluginManager.AddProperty("georace.lmu.newTires", this.GetType(), LMURepairAndRefuelData.newTires);

            pluginManager.AddProperty("georace.lmu.fl_TyreChange", this.GetType(), LMURepairAndRefuelData.fl_TyreChange);
            pluginManager.AddProperty("georace.lmu.fr_TyreChange", this.GetType(), LMURepairAndRefuelData.fr_TyreChange);
            pluginManager.AddProperty("georace.lmu.rl_TyreChange", this.GetType(), LMURepairAndRefuelData.rl_TyreChange);
            pluginManager.AddProperty("georace.lmu.rr_TyreChange", this.GetType(), LMURepairAndRefuelData.rr_TyreChange);

            pluginManager.AddProperty("georace.lmu.fl_TyrePressure", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure);
            pluginManager.AddProperty("georace.lmu.fr_TyrePressure", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure);
            pluginManager.AddProperty("georace.lmu.rl_TyrePressure", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure);
            pluginManager.AddProperty("georace.lmu.rr_TyrePressure", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure);
            pluginManager.AddProperty("georace.lmu.replaceBrakes", this.GetType(), LMURepairAndRefuelData.replaceBrakes);

            pluginManager.AddProperty("georace.lmu.maxBattery", this.GetType(), LMURepairAndRefuelData.maxBattery);
            pluginManager.AddProperty("georace.lmu.selectedMenuIndex", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.maxFuel", this.GetType(), LMURepairAndRefuelData.maxFuel);
            pluginManager.AddProperty("georace.lmu.maxVirtualEnergy", this.GetType(), LMURepairAndRefuelData.maxVirtualEnergy);

            pluginManager.AddProperty("georace.lmu.isStopAndGo", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.isDamage", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.haveDriverMenu", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.isHyper", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.ScreenIndex", this.GetType(), 0);


            pluginManager.AddProperty("georace.lmu.Extended.Cuts", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.CutsMax", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.PenaltyLeftLaps", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.PenaltyType", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.PenaltyCount", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.PendingPenaltyType1", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.PendingPenaltyType2", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.PendingPenaltyType3", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.Extended.TractionControl", this.GetType(),0);
            pluginManager.AddProperty("georace.lmu.Extended.BrakeMigration", this.GetType(),0);
            pluginManager.AddProperty("georace.lmu.Extended.BrakeMigrationMax", this.GetType(), 0);

            pluginManager.AddProperty("georace.lmu.mPlayerBestSector1", this.GetType(),0);
            pluginManager.AddProperty("georace.lmu.mPlayerBestSector2", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mPlayerBestSector3", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mSessionBestSector1", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mSessionBestSector2", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mSessionBestSector3", this.GetType(), 0);

            pluginManager.AddProperty("georace.lmu.mPlayerCurSector1", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mPlayerCurSector2", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mPlayerCurSector3", this.GetType(), 0);

            pluginManager.AddProperty("georace.lmu.mPlayerBestLapTime", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mPlayerBestLapSector1", this.GetType(),0);
            pluginManager.AddProperty("georace.lmu.mPlayerBestLapSector2", this.GetType(), 0);
            pluginManager.AddProperty("georace.lmu.mPlayerBestLapSector3", this.GetType(), 0);

            pluginManager.AddProperty("georace.lmu.elecMenuTitle1", this.GetType(), "Traction control");
            pluginManager.AddProperty("georace.lmu.elecMenuTitle2", this.GetType(), "TC power cut");
            pluginManager.AddProperty("georace.lmu.elecMenuTitle3", this.GetType(), "TC slip angle");
            pluginManager.AddProperty("georace.lmu.elecMenuTitle4", this.GetType(), "Front ARB");
            pluginManager.AddProperty("georace.lmu.elecMenuTitle5", this.GetType(), "Rear ARB");
            pluginManager.AddProperty("georace.lmu.elecMenuTitle6", this.GetType(), "Brake bias");
            pluginManager.AddProperty("georace.lmu.elecMenuTitle7", this.GetType(), "Brake migration");

            pluginManager.AddProperty("georace.lmu.elecMenuValue1", this.GetType(), "7");
            pluginManager.AddProperty("georace.lmu.elecMenuValue2", this.GetType(), "6");
            pluginManager.AddProperty("georace.lmu.elecMenuValue3", this.GetType(), "10");
            pluginManager.AddProperty("georace.lmu.elecMenuValue4", this.GetType(), "P4");
            pluginManager.AddProperty("georace.lmu.elecMenuValue5", this.GetType(), "P2");
            pluginManager.AddProperty("georace.lmu.elecMenuValue6", this.GetType(), "54.6:45.4");
            pluginManager.AddProperty("georace.lmu.elecMenuValue7", this.GetType(), "1.0% F");

            pluginManager.AddProperty("georace.lmu.Clock_Format24", this.GetType(), ButtonBindSettings.Clock_Format24);
            pluginManager.AddProperty("georace.lmu.RealTimeClock", this.GetType(), ButtonBindSettings.RealTimeClock);

            pluginManager.AddProperty("georace.lmu.mMessage", this.GetType(), "");


            //this.AddAction("georace.lmu.MenuUP", (a, b) =>
            //{
            //if (LMU_MenuPositions.selectedMenuIndex > 0)
            //    {
            //        LMU_MenuPositions.selectedMenuIndex--;
            //    }
            //else
            //    {
            //        if (LMU_MenuPositions.selectedMenuIndex == 0) LMU_MenuPositions.selectedMenuIndex = LMU_MenuPositions.MenuMaxIndex;
            //    }
          
            //    SimHub.Logging.Current.Info("selectedMenuIndex changed " + LMU_MenuPositions.selectedMenuIndex.ToString());
            //});

            //this.AddAction("georace.lmu.MenuDOWN", (a, b) =>
            //{
            //    if (LMU_MenuPositions.selectedMenuIndex < LMU_MenuPositions.MenuMaxIndex)
            //    {
            //        LMU_MenuPositions.selectedMenuIndex++;
            //    }
            //    else
            //    {
            //        if (LMU_MenuPositions.selectedMenuIndex == LMU_MenuPositions.MenuMaxIndex) LMU_MenuPositions.selectedMenuIndex = 0;
            //    }
            //    SimHub.Logging.Current.Info("selectedMenuIndex changed " + LMU_MenuPositions.selectedMenuIndex.ToString());
            //});

            //this.AddAction("georace.lmu.MenuIncrement", (a, b) =>
            //{
                
            //    SimHub.Logging.Current.Info("selectedMenuIndex changed 3");
            //});
            //this.AddAction("georace.lmu.MenuDecrement", (a, b) =>
            //{
               
            //    SimHub.Logging.Current.Info("selectedMenuIndex changed 4");
            //});

        }
    }
}
