using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq; // Needed for JObject 
using System.IO;    // Needed for read/write JSON settings file
using SimHub;   // Needed for Logging
using System.Net;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using MahApps.Metro.Controls;   // Needed for Logging
using System.Windows.Markup;
using SimHub.Plugins.OutputPlugins.Dash.WPFUI;
using System.Diagnostics.Eventing.Reader;

namespace Georace.lmuDataPlugin
{
    /// <summary>
    /// Logique d'interaction pour SettingsControlDemo.xaml
    /// </summary>

    public partial class SettingsControl : UserControl, IComponentConnector
    {


        //public void InitializeComponent()
        //{
        //    if (!_contentLoaded)
        //    {
        //        _contentLoaded = true;
        //        Uri resourceLocator = new Uri("/SimHub.Plugins;component/inputplugins/joystick/joystickpluginsettingscontrolwpf.xaml", UriKind.Relative);
        //        Application.LoadComponent(this, resourceLocator);
        //    }
        //}

        internal Delegate _CreateDelegate(Type delegateType, string handler)
        {
            return Delegate.CreateDelegate(delegateType, this, handler);
        }

        //void IComponentConnector.Connect(int connectionId, object target)
        //{
        //    if (connectionId == 1)
        //    {
        //        ((Button)target).Click += clearLogging_Click;
        //    }
        //    else
        //    {
        //        _contentLoaded = true;
        //    }
        //}
        public SettingsControl()
        {
            InitializeComponent();


        }

        //private bool value_changed = false;

        //private delegate void UpdateDataThreadSafeDelegate<TResult>(void Refresh);

        //public static void UpdateDataThreadSafe<TResult>(this Control @this)
        //{
        //   @this.Update;
        //}


        void OnLoad(object sender, RoutedEventArgs e)
        {
            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
                ButtonBindSettings.AntiFlickPitMenuTimeout = JSONSettingsdata["AntiFlickPitMenuTimeout"] != null ? (int)JSONSettingsdata["AntiFlickPitMenuTimeout"] : 10;
                
                ButtonBindSettings.UP = JSONSettingsdata["KeyMapUp"] != null ? JSONSettingsdata["KeyMapUp"].ToString() : "";
                ButtonBindSettings.DOWN = JSONSettingsdata["KeyMapDown"] != null ? JSONSettingsdata["KeyMapDown"].ToString() : "";
                ButtonBindSettings.LEFT = JSONSettingsdata["KeyMapLeft"] != null ? JSONSettingsdata["KeyMapLeft"].ToString() : "";
                ButtonBindSettings.RIGHT = JSONSettingsdata["KeyMapRight"] != null ? JSONSettingsdata["KeyMapRight"].ToString() : "";
                ButtonBindSettings.UseLongPressLeftAndRight = JSONSettingsdata["UseLongPressLeftAndRight"] != null ? (bool)JSONSettingsdata["UseLongPressLeftAndRight"] : false;
                ButtonBindSettings.NEXTSCREEN = JSONSettingsdata["NextScreen"] != null ? JSONSettingsdata["NextScreen"].ToString() : "";
                ButtonBindSettings.PREVSCREEN = JSONSettingsdata["PrevScreen"] != null ? JSONSettingsdata["PrevScreen"].ToString() : "";
            
            }
            catch { }
            clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;
            RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;
            GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;
            DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;
            AntiFlickPitMenuTimeout.Value = ButtonBindSettings.AntiFlickPitMenuTimeout;
            Up_button_text.Text = ButtonBindSettings.UP.Split('_')[ButtonBindSettings.UP.Split('_').Count() - 1]; ;
            Down_button_text.Text = ButtonBindSettings.DOWN.Split('_')[ButtonBindSettings.DOWN.Split('_').Count() - 1]; ;
            Left_button_text.Text = ButtonBindSettings.LEFT.Split('_')[ButtonBindSettings.LEFT.Split('_').Count() - 1]; ;
            Right_button_text.Text = ButtonBindSettings.RIGHT.Split('_')[ButtonBindSettings.RIGHT.Split('_').Count() - 1]; ;
            prev_screen_button_text.Text = ButtonBindSettings.PREVSCREEN.Split('_')[ButtonBindSettings.PREVSCREEN.Split('_').Count() - 1]; ;
            next_screen_button_text.Text = ButtonBindSettings.NEXTSCREEN.Split('_')[ButtonBindSettings.NEXTSCREEN.Split('_').Count() - 1]; ;
            useLogPress.IsChecked = ButtonBindSettings.UseLongPressLeftAndRight;
        }

   
        public  void Refresh(string _Key)
        {
            bool changedBind = false;
            string MessageText = "";
            string bindedKeysString = "UP:" + ButtonBindSettings.UP + ";DOWN:" + ButtonBindSettings.DOWN + ";LEFT:" + ButtonBindSettings.LEFT + ";RIGHT:" + ButtonBindSettings.RIGHT + ";NEXTSCREEN:" + ButtonBindSettings.NEXTSCREEN + ";PREVSCREEN:" + ButtonBindSettings.PREVSCREEN;

            if (ButtonBindSettings.UP == "Wait Or Click to Clear")
            {
                if (bindedKeysString.Contains(_Key))
                {
                    MessageText = "Key: " + _Key + " binded to button other KEY";
                    ButtonBindSettings.UP = "...Bind...";
                }
                else
                {
                    ButtonBindSettings.UP = _Key;
                    changedBind = true;
                }

            }
            else if (ButtonBindSettings.DOWN == "Wait Or Click to Clear")
            {
                if (bindedKeysString.Contains(_Key))
                {
                    MessageText = "Key: " + _Key + " binded to button other KEY";
                    ButtonBindSettings.DOWN = "...Bind...";
                }
                else
                {
                    ButtonBindSettings.DOWN = _Key;
                    changedBind = true;
                }
            }
            else if (ButtonBindSettings.LEFT == "Wait Or Click to Clear")
            {
                if (bindedKeysString.Contains(_Key))
                {
                    MessageText = "Key: " + _Key + " binded to button other KEY";
                    ButtonBindSettings.LEFT = "...Bind...";
                }
                else
                {
                    ButtonBindSettings.LEFT = _Key;
                    changedBind = true;
                }
            }
            else if (ButtonBindSettings.RIGHT == "Wait Or Click to Clear")
            {
                if (bindedKeysString.Contains(_Key))
                {
                    MessageText = "Key: " + _Key + " binded to button other KEY";
                    ButtonBindSettings.RIGHT = "...Bind...";
                }
                else
                {
                    ButtonBindSettings.RIGHT = _Key;
                    changedBind = true;
                }
            }
            else if (ButtonBindSettings.NEXTSCREEN == "Wait Or Click to Clear")
            {

                if (bindedKeysString.Contains(_Key))
                {
                    MessageText = "Key: " + _Key + " binded to button other KEY";
                    ButtonBindSettings.NEXTSCREEN = "...Bind...";
                }
                else
                {
                    ButtonBindSettings.NEXTSCREEN = _Key;
                    changedBind = true;
                }
            }
            else if (ButtonBindSettings.PREVSCREEN == "Wait Or Click to Clear")
            {
                if (bindedKeysString.Contains(_Key))
                {
                    MessageText = "Key: " + _Key + " binded to button other KEY";
                    ButtonBindSettings.PREVSCREEN = "...Bind...";
                }
                else
                {
                    ButtonBindSettings.PREVSCREEN = _Key;
                    changedBind = true;
                }
            }


            ButtonBindSettings.waitinput = false;
            if (changedBind)
            {
                SaveSetting();
            }
            //try
            //{
            //    JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
            //    ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
            //    ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
            //    ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
            //    ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
            //    ButtonBindSettings.AntiFlickPitMenuTimeout = JSONSettingsdata["AntiFlickPitMenuTimeout"] != null ? (int)JSONSettingsdata["AntiFlickPitMenuTimeout"] : 10;

            //    ButtonBindSettings.UP = JSONSettingsdata["KeyMapUp"] != null ? JSONSettingsdata["KeyMapUp"].ToString() : "";
            //    ButtonBindSettings.DOWN = JSONSettingsdata["KeyMapDown"] != null ? JSONSettingsdata["KeyMapDown"].ToString() : "";
            //    ButtonBindSettings.LEFT = JSONSettingsdata["KeyMapLeft"] != null ? JSONSettingsdata["KeyMapLeft"].ToString() : "";
            //    ButtonBindSettings.RIGHT = JSONSettingsdata["KeyMapRight"] != null ? JSONSettingsdata["KeyMapRight"].ToString() : "";
            //    ButtonBindSettings.UseLongPressLeftAndRight = JSONSettingsdata["UseLongPressLeftAndRight"] != null ? (bool)JSONSettingsdata["UseLongPressLeftAndRight"] : false;
            //    ButtonBindSettings.NEXTSCREEN = JSONSettingsdata["NextScreen"] != null ? JSONSettingsdata["NextScreen"].ToString() : "";
            //    ButtonBindSettings.PREVSCREEN = JSONSettingsdata["PrevScreen"] != null ? JSONSettingsdata["PrevScreen"].ToString() : "";
            //}
            //catch { }
            base.Dispatcher.InvokeAsync(delegate
            {
                lock (Up_button_text)
                {
                    Up_button_text.Text = ButtonBindSettings.UP.Split('_')[ButtonBindSettings.UP.Split('_').Count() - 1];

                }
                lock (Down_button_text)
                {
                    Down_button_text.Text = ButtonBindSettings.DOWN.Split('_')[ButtonBindSettings.DOWN.Split('_').Count() - 1];

                }
                lock (Left_button_text)
                {
                    Left_button_text.Text = ButtonBindSettings.LEFT.Split('_')[ButtonBindSettings.LEFT.Split('_').Count() - 1];

                }
                lock (Right_button_text)
                {
                    Right_button_text.Text = ButtonBindSettings.RIGHT.Split('_')[ButtonBindSettings.RIGHT.Split('_').Count() - 1];

                }

                lock (next_screen_button_text)
                {
                    next_screen_button_text.Text = ButtonBindSettings.NEXTSCREEN.Split('_')[ButtonBindSettings.NEXTSCREEN.Split('_').Count() - 1];

                }

                lock (prev_screen_button_text)
                {
                    prev_screen_button_text.Text = ButtonBindSettings.PREVSCREEN.Split('_')[ButtonBindSettings.PREVSCREEN.Split('_').Count() - 1];

                }

                lock (useLogPress)
                {
                    useLogPress.IsChecked = ButtonBindSettings.UseLongPressLeftAndRight;

                }

                lock (clock_format24)
                {
                    clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;

                }
                lock (RealTimeClock)
                {
                    RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;

                }

                lock (DataUpdateThreadTimeout)
                {
                    DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;

                }

                lock (GetMemoryDataThreadTimeout)
                {
                    GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;

                }

                lock (AntiFlickPitMenuTimeout)
                {
                    AntiFlickPitMenuTimeout.Value = ButtonBindSettings.AntiFlickPitMenuTimeout;

                }

                lock (message_text)
                {
                    message_text.Text = MessageText;

                }
            }
        );
        }

        private void SHSection_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //Trigger for saving JSON file. Event is fired if you enter or leave the Plugin Settings View or if you close SimHub

            //Saving on leaving Settings View only
            if (!SHSectionPluginOptions.IsVisible)
            {
                try
                {
           
                  
                }
                catch (Exception ext)
                {
                    Logging.Current.Info("INNIT ERROR: " + ext.ToString());
                }


            }
        }

       

        private void refresh_button_Click(object sender, RoutedEventArgs e)
        {

            clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;
            RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;
            GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;
            DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;
            AntiFlickPitMenuTimeout.Value = ButtonBindSettings.AntiFlickPitMenuTimeout;
           
            Up_button_text.Text = ButtonBindSettings.UP.Split('_')[ButtonBindSettings.UP.Split('_').Count() - 1];
            Down_button_text.Text = ButtonBindSettings.DOWN.Split('_')[ButtonBindSettings.DOWN.Split('_').Count() - 1];
            Left_button_text.Text = ButtonBindSettings.LEFT.Split('_')[ButtonBindSettings.LEFT.Split('_').Count() - 1];
            Right_button_text.Text = ButtonBindSettings.RIGHT.Split('_')[ButtonBindSettings.RIGHT.Split('_').Count() - 1];
            prev_screen_button_text.Text = ButtonBindSettings.PREVSCREEN.Split('_')[ButtonBindSettings.PREVSCREEN.Split('_').Count() - 1];
            next_screen_button_text.Text = ButtonBindSettings.NEXTSCREEN.Split('_')[ButtonBindSettings.NEXTSCREEN.Split('_').Count() - 1];
            useLogPress.IsChecked = ButtonBindSettings.UseLongPressLeftAndRight;

            message_text.Text = "";
        }

        private void SaveSetting()
         {
            JObject JSONdata = new JObject(
                  new JProperty("Clock_Format24", ButtonBindSettings.Clock_Format24),
                   new JProperty("RealTimeClock", ButtonBindSettings.RealTimeClock),
                   new JProperty("GetMemoryDataThreadTimeout", ButtonBindSettings.GetMemoryDataThreadTimeout),
                   new JProperty("DataUpdateThreadTimeout", ButtonBindSettings.DataUpdateThreadTimeout),
                   new JProperty("AntiFlickPitMenuTimeout", ButtonBindSettings.AntiFlickPitMenuTimeout),
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
                Logging.Current.Info("Plugin georace.lmuDataPlugin - Settings file saved to : " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);
            }
            catch
            {
                //A MessageBox creates graphical glitches after closing it. Search another way, maybe using the Standard Log in SimHub\Logs
                //MessageBox.Show("Cannot create or write the following file: \n" + System.Environment.CurrentDirectory + "\\" + AccData.path, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logging.Current.Error("Plugin georace.lmuDataPlugin - Cannot create or write settings file: " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);


            }
        }
       
      

        private void clock_format24_Checked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.Clock_Format24 = true;
            message_text.Text = "";
            SaveSetting();
        }
        private void clock_format24_unChecked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.Clock_Format24 = false;
            message_text.Text = "";
            SaveSetting();
        }

        private void RealTimeClock_Checked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.RealTimeClock = true;
            message_text.Text = "";
            SaveSetting();
        }
        private void RealTimeClock_unChecked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.RealTimeClock = false;
            message_text.Text = "";
            SaveSetting();
        }

        private void GetMemoryDataThreadTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ButtonBindSettings.GetMemoryDataThreadTimeout = (int)GetMemoryDataThreadTimeout.Value;
            SaveSetting();
        }
        
        private void DataUpdateThreadTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ButtonBindSettings.DataUpdateThreadTimeout = (int)DataUpdateThreadTimeout.Value;
            SaveSetting();
        }

        private void AntiFlickPitMenuTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ButtonBindSettings.AntiFlickPitMenuTimeout = (int)AntiFlickPitMenuTimeout.Value;
            SaveSetting();
        }


        private void Up_button_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonBindSettings.UP.Equals("Wait Or Click to Clear"))
            {
                ButtonBindSettings.UP = "...Bind...";
                Up_button_text.Text = ButtonBindSettings.UP;
                ButtonBindSettings.waitinput = false;
                message_text.Text = "";
                SaveSetting();
            }
            else
            {
                // Up_button_text.Text = "Wait input";
                ButtonBindSettings.UP = "Wait Or Click to Clear";
                ButtonBindSettings.waitinput = true;
                Up_button_text.Text = ButtonBindSettings.UP;
                message_text.Text = "";
            }
        }

        private void Down_button_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonBindSettings.DOWN.Equals("Wait Or Click to Clear"))
            {
                ButtonBindSettings.DOWN = "...Bind...";
                Down_button_text.Text = ButtonBindSettings.DOWN;
                ButtonBindSettings.waitinput = false;
                message_text.Text = "";
                SaveSetting();
            }
            else
            {
                // Down_button_text.Text = "Wait input";
                ButtonBindSettings.DOWN = "Wait Or Click to Clear";
                ButtonBindSettings.waitinput = true;
                Down_button_text.Text = ButtonBindSettings.DOWN;
                message_text.Text = "";
            }
        }


        private void Right_button_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonBindSettings.RIGHT.Equals("Wait Or Click to Clear"))
            {
                ButtonBindSettings.RIGHT = "...Bind...";
                Right_button_text.Text = ButtonBindSettings.RIGHT;
                ButtonBindSettings.waitinput = false;
                message_text.Text = "";
                SaveSetting();
            }
            else
            {
                // Right_button_text.Text = "Wait input";
                ButtonBindSettings.RIGHT = "Wait Or Click to Clear";
                ButtonBindSettings.waitinput = true;
                Right_button_text.Text = ButtonBindSettings.RIGHT;
                message_text.Text = "";
            }
        }

        private void Left_button_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonBindSettings.LEFT.Equals("Wait Or Click to Clear"))
            {
                ButtonBindSettings.LEFT = "...Bind...";
                Left_button_text.Text = ButtonBindSettings.LEFT;
                ButtonBindSettings.waitinput = false;
                message_text.Text = "";
                SaveSetting();
            }
            else
            {
                //Left_button_text.Text = "Wait input";
                ButtonBindSettings.LEFT = "Wait Or Click to Clear";
                ButtonBindSettings.waitinput = true;
                Left_button_text.Text = ButtonBindSettings.LEFT;
                message_text.Text = "";

            }
        }

        private void useLogPress_Checked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.UseLongPressLeftAndRight = true;
            message_text.Text = "";
            SaveSetting();
        }
        private void useLogPress_unChecked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.UseLongPressLeftAndRight = false;
            message_text.Text = "";
            SaveSetting();
        }

        private void next_screen_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonBindSettings.NEXTSCREEN.Equals("Wait Or Click to Clear"))
            {
                ButtonBindSettings.NEXTSCREEN = "...Bind...";
                next_screen_button_text.Text = ButtonBindSettings.NEXTSCREEN;
                ButtonBindSettings.waitinput = false;
                message_text.Text = "";
                SaveSetting();
            }
            else
            {
                //  next_screen_button_text.Text = "Wait input";
                ButtonBindSettings.NEXTSCREEN = "Wait Or Click to Clear";
                ButtonBindSettings.waitinput = true;
                next_screen_button_text.Text = ButtonBindSettings.NEXTSCREEN;
                message_text.Text = "";
            }
        }

        private void prev_screen_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonBindSettings.PREVSCREEN.Equals("Wait Or Click to Clear"))
            {
                ButtonBindSettings.PREVSCREEN = "...Bind...";
                prev_screen_button_text.Text = ButtonBindSettings.PREVSCREEN;
                ButtonBindSettings.waitinput = false;
                message_text.Text = "";
                SaveSetting();
            }
            else
            {
                //  prev_screen_button_text.Text = "Wait input";
                ButtonBindSettings.PREVSCREEN = "Wait Or Click to Clear";
                ButtonBindSettings.waitinput = true;
                prev_screen_button_text.Text = ButtonBindSettings.PREVSCREEN;
                message_text.Text = "";
            }
        }
    }


   public class LMU_EnegryAndFuelCalculation
    {
        public static double lastLapEnegry { get; set; }
        public static int lapIndex = 0;
        public static bool runned = false;
        public static double LastLapUsed = 0;
        public static bool inPit = true;
        public static double AvgOfFive = 0;

    }
    public class ButtonKeyValues
    {
         string _key { get; set; }
         string _value { get; set; }
    }


        public class ButtonBindSettings
    {
        public static bool RealTimeClock { get; set; }
        public static bool Clock_Format24 { get; set; }
        public static int DataUpdateThreadTimeout { get; set; }
        public static int GetMemoryDataThreadTimeout { get; set; }
        public static int AntiFlickPitMenuTimeout{ get; set; }
        
        public static string UP { get; set; }
        public static string DOWN { get; set; }
        public static string LEFT { get; set; }
        public static string RIGHT { get; set; }
        public static string NEXTSCREEN { get; set; }
        public static string PREVSCREEN { get; set; }
        public static bool UseLongPressLeftAndRight { get; set; }
        public static bool waitinput { get; set; }
        public static bool inputAdded { get; set; }
    }


    /*public class AccSpeed - old way
    {*/
    /*private static int Speed = 20;
    public static int Value
    {
        get { return Speed; }
        set { Speed = value; }
    }*/
    /*public static int Value { get; set; }
}*/
}
