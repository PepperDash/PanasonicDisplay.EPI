using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

using Newtonsoft.Json;



namespace PDT.PanasonicDisplay.EPI
{
    public class PluginFactory
    {
        /// <summary>
        /// Load the types to the factory
        /// </summary>
        public static void LoadPlugin()
        {
            PepperDash.Essentials.Core.DeviceFactory.AddFactoryForType("panasonicdisplay", PluginFactory.BuildDevice);
        }

        /// <summary>
        /// Must use this version of Essentials as minimum
        /// </summary>
        public static string MinimumEssentialsFrameworkVersion = "1.4.32";

        /// <summary>
        /// Builds an instance of the device and returns it
        /// </summary>
        /// <param name="dc">Device Configuration</param>
        /// <returns>Instance of Device</returns>
        public static PanasonicDisplay BuildDevice(DeviceConfig dc)
        {
            var comm = CommFactory.CreateCommForDevice(dc);

            // No use in creating the display if we can't communicate with it
            if (comm != null)
            {
                // Deserialize the Properties object so we can access any custom properties
                var config = JsonConvert.DeserializeObject<PanasonicDisplayConfigObject>(dc.Properties.ToString());
                var newDisplay = new PanasonicDisplay(dc.Key, dc.Name, comm, dc);
                return newDisplay;
            }
            else
            {
                Debug.Console(0, Debug.ErrorLogLevel.Warning, "Unable to create Communication device for device with key '{0}'", dc.Key);
                return null;
            }
        }

    }
}