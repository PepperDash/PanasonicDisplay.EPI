using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using Newtonsoft.Json;

using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PDT.PanasonicDisplay.EPI
{
    /// <summary>
    /// Defines any custom properties necessary for the device
    /// </summary>
	public class PanasonicDisplayConfigObject
    {    
        /// <summary>
        /// Contains the necessary properties to communicate with the device
        /// </summary>
        [JsonProperty("control", Required = Required.Always)]
        EssentialsControlPropertiesConfig Control { get; set; }

        // Add any additional custom properties here
	}
}