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
    /// <summary>
    /// This class is responsible for defining the minimum requirements of the plugin and define the necessary requirement for Essentails to load this plugin.
    /// </summary>
    public class PanasonicDisplayFactory : EssentialsPluginDeviceFactory<PanasonicDisplay>
    {
        /// <summary>
        /// Load the types to the factory
        /// </summary>
        public PanasonicDisplayFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.5.0";

             TypeNames = new List<string>() {"panasonicDisplay", "panasonicThDisplay"};
        }

        /// <summary>
        /// Builds an instance of the device and returns it
        /// </summary>
        /// <param name="dc">Device Configuration</param>
        /// <returns>Instance of Device</returns>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
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