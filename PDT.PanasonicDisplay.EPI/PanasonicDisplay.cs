using System;

using System.Collections.Generic;
using System.Linq;
using System.Text;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.Routing;

using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       				// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PepperDash.Essentials;

using PepperDash.Essentials.Core.Config;
using System.Collections;
using PepperDash.Essentials.Core.Bridges;



namespace PDT.PanasonicDisplay.EPI
{
    /// <summary>
    /// 
    /// </summary>
    [Description("Panasonic TH series Display")]
	public class PanasonicDisplay : TwoWayDisplayBase, IBasicVolumeWithFeedback, ICommunicationMonitor, IBridgeAdvanced
	{
        /// <summary>
        /// The communication device
        /// </summary>
		public IBasicCommunication Communication { get; private set; }

        /// <summary>
        /// This class will gather RX data until it finds the specified delimiter.
        /// It then fires an event that contains the entire captured string.
        /// </summary>
        CommunicationGather PortGather;

        /// <summary>
        /// Stores the device configuration 
        /// </summary>
        public DeviceConfig Config { get; private set; }

        /// <summary>
        /// Monitors communication with device
        /// </summary>
		public StatusMonitorBase CommunicationMonitor { get; private set; }

		int InputNumber;
        /// <summary>
        /// The current selected input number
        /// </summary>
		public IntFeedback InputNumberFeedback;
		static List<string> InputKeys = new List<string>();

		#region Command constants
		public const string InputGetCmd = "\x02QMI\x03";
		public const string Hdmi1Cmd = "\x02IMS:HM1\x03";
		public const string Hdmi2Cmd = "\x02IMS:HM2\x03";
		public const string Hdmi3Cmd = "";
		public const string Hdmi4Cmd = "";
		public const string Dp1Cmd = "";
		public const string Dp2Cmd = "";
		public const string Dvi1Cmd = "\x02IMS:DV1\x03";
		public const string Video1Cmd = "";
		public const string VgaCmd = "\x02IMS:PC1\x03";
		public const string RgbCmd = "";

		public const string PowerOnCmd = "\x02PON\x03";
		public const string PowerOffCmd = "\x02POF\x03";
		public const string PowerToggleIrCmd = "";

		public const string MuteOffCmd = "\x02AMT:0\x03";
		public const string MuteOnCmd = "\x02AMT:1\x03";
		public const string MuteToggleIrCmd = "\x02AMT\x03";
		public const string MuteGetCmd = "\x02QAM\x03";

		public const string VolumeGetCmd = "\x02QAV\x03";
		public const string VolumeLevelPartialCmd = "\x02AVL:"; //
		public const string VolumeUpCmd = "\x02AUU\x03";
		public const string VolumeDownCmd = "\x02AUD\x03";

		public const string MenuIrCmd = "";
		public const string UpIrCmd = "";
		public const string DownIrCmd = "";
		public const string LeftIrCmd = "";
		public const string RightIrCmd = "";
		public const string SelectIrCmd = "";
		public const string ExitIrCmd = "";

		public const string PollInput = "\x02QMI\x03";
		#endregion

		bool _PowerIsOn;
		bool _IsWarmingUp;
		bool _IsCoolingDown;
		ushort _VolumeLevel;
		bool _IsMuted;
		

		protected override Func<bool> PowerIsOnFeedbackFunc { get { return () => _PowerIsOn; } }
		protected override Func<bool> IsCoolingDownFeedbackFunc { get { return () => _IsCoolingDown; } }
		protected override Func<bool> IsWarmingUpFeedbackFunc { get { return () => _IsWarmingUp; } }
		protected override Func<string> CurrentInputFeedbackFunc { get { return () => "Not Implemented"; } }



		/// <summary>
		/// Constructor for IBasicCommunication
		/// </summary>
		public PanasonicDisplay(string key, string name, IBasicCommunication comm, DeviceConfig config)
			: base(key, name)
		{
            Config = config; 

            Communication = comm;

			Init();
		}

        /// <summary>
        /// Intial logic to set up instance
        /// </summary>
		void Init()
		{
            // Will gather to the specified delimiter
			PortGather = new CommunicationGather(Communication, '\x03');
			PortGather.LineReceived += this.Port_LineReceived;
			
            // Constuct the CommunicationMonitor
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, "\x02QPW\x03\x02QMI\x03"); // Query Power
			
            // Define the input ports 
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this));
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this));
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.DviIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Dvi, new Action(InputDvi1), this));
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.VgaIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Vga, new Action(InputVga), this));

            // Define the feedback Funcs
			VolumeLevelFeedback = new IntFeedback(() => { return _VolumeLevel; });
			MuteFeedback = new BoolFeedback(() => _IsMuted);
			InputNumberFeedback = new IntFeedback(() => { Debug.Console(2, this, "CHange Input number {0}", InputNumber); return InputNumber; });
				
            // Set the warmup time
			WarmupTime = 17000;
		}

		~PanasonicDisplay()
		{
			PortGather = null;
		}

        /// <summary>
        /// This will run during the Activation phase
        /// </summary>
        /// <returns></returns>
		public override bool CustomActivate()
		{
			Communication.Connect();

			CommunicationMonitor.StatusChange += (o, a) => { Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status); };
			CommunicationMonitor.Start();

            // Call the base method in case any steps need to happen there
            return base.CustomActivate();
		}

        /// <summary>
        /// Adds a routing port to the InputPorts collection
        /// </summary>
        /// <param name="port">port to add</param>
        /// <param name="fbMatch">matching response</param>
		void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
		{
			port.FeedbackMatchObject = fbMatch;
			InputPorts.Add(port);
		}

        /// <summary>
        /// This method will run when the PortGather is satisfied.  Parse responses here.
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="args"></param>
		void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
		{
			if (Debug.Level == 2)
				Debug.Console(2, this, "Received: '{0}'", ComTextHelper.GetEscapedText(args.Text));
			char[] trimChars = { '\x02', '\x03' };
			var FB = args.Text.Trim(trimChars);
			Debug.Console(2, this, "Received cmd: '{0}'", FB);
			switch (FB)
			{
				case "PON":
					{
						_PowerIsOn = true;
						PowerIsOnFeedback.FireUpdate();
						InputNumberFeedback.FireUpdate();
						break;
					}
				case "POF":
					{
						_PowerIsOn = false;
						PowerIsOnFeedback.FireUpdate();
						InputNumber = 102;
						InputNumberFeedback.FireUpdate();
						break;
					}
				case "QPW:1":
					{
						_PowerIsOn = true;
						PowerIsOnFeedback.FireUpdate();
						InputNumberFeedback.FireUpdate();
						break;
					}
				case "QPW:0":
					{
						_PowerIsOn = false;
						PowerIsOnFeedback.FireUpdate();
						InputNumber = 102;
						InputNumberFeedback.FireUpdate();
						break;
					}
				case "QMI:HM1":
					{
						if (_PowerIsOn)
						{
							InputNumber = 1;
							InputNumberFeedback.FireUpdate();
						}
						break;
					}
				case "QMI:HM2":
					{
						if (_PowerIsOn)
						{
							InputNumber = 2;
							InputNumberFeedback.FireUpdate();
						}
						break;
					}
				case "QMI:DV1":
					{
						if (_PowerIsOn)
						{
							InputNumber = 3;
							InputNumberFeedback.FireUpdate();
						}
						break;
					}
				case "QMI:PC1":
					{
						if (_PowerIsOn)
						{
							InputNumber = 4;
							InputNumberFeedback.FireUpdate();
						}
						break;
					}
			}
		}

        /// <summary>
        /// Sends data to the device
        /// </summary>
        /// <param name="s"></param>
		void Send(string s)
		{
			if (Debug.Level == 2)
				Debug.Console(2, this, "Send: '{0}'", ComTextHelper.GetEscapedText(s));
			Communication.SendText(s);
		}

        /// <summary>
        /// Power on the display
        /// </summary>
		public override void PowerOn()
		{
			Send(PowerOnCmd);
			if (!PowerIsOnFeedback.BoolValue && !_IsWarmingUp && !_IsCoolingDown)
			{
				_IsWarmingUp = true;
				IsWarmingUpFeedback.FireUpdate();
				// Fake power-up cycle
				WarmupTimer = new Crestron.SimplSharp.CTimer(o =>
					{
						_IsWarmingUp = false;
						_PowerIsOn = true;
						IsWarmingUpFeedback.FireUpdate();
						PowerIsOnFeedback.FireUpdate();
					}, WarmupTime);
			}
		}

        /// <summary>
        /// Power off the display
        /// </summary>
		public override void PowerOff()
		{
			// If a display has unreliable-power off feedback, just override this and
			// remove this check.
			if (PowerIsOnFeedback.BoolValue && !_IsWarmingUp && !_IsCoolingDown)
			{
				Send(PowerOffCmd);
				_IsCoolingDown = true;
				_PowerIsOn = false;
				PowerIsOnFeedback.FireUpdate();
				IsCoolingDownFeedback.FireUpdate();
				// Fake cool-down cycle
				CooldownTimer = new CTimer(o =>
					{
						Debug.Console(2, this, "Cooldown timer ending");
						_IsCoolingDown = false;
						IsCoolingDownFeedback.FireUpdate();
					}, CooldownTime);
			}
		}

        /// <summary>
        /// Toggle the display power
        /// </summary>
		public override void PowerToggle()
		{
			if (PowerIsOnFeedback.BoolValue && !IsWarmingUpFeedback.BoolValue)
				PowerOff();
			else if (!PowerIsOnFeedback.BoolValue && !IsCoolingDownFeedback.BoolValue)
				PowerOn();
		}

        /// <summary>
        /// Set the input to Hdmi1
        /// </summary>
		public void InputHdmi1()
		{
			Send(Hdmi1Cmd);
			Send(PollInput);
		}

        /// <summary>
        /// Set the input to Hdmi2
        /// </summary>
		public void InputHdmi2()
		{

			Send(Hdmi2Cmd);
			Send(PollInput);
		}

        /// <summary>
        /// Set the input to Hdmi3
        /// </summary>
		public void InputHdmi3()
		{

			Send(Hdmi3Cmd);
			Send(PollInput);
		}

        /// <summary>
        /// Set the input to Hdmi4
        /// </summary>
		public void InputHdmi4()
		{
			Send(Hdmi4Cmd);
			Send(PollInput);
		}


        /// <summary>
        /// Set the input to DP1
        /// </summary>
		public void InputDisplayPort1()
		{
			Send(Dp1Cmd);
			Send(PollInput);
		}

        /// <summary>
        /// Set the input to DP2
        /// </summary>
		public void InputDisplayPort2()
		{
			Send(Dp2Cmd);
			Send(PollInput);
		}


        /// <summary>
        /// Set the input to DVI1
        /// </summary>
		public void InputDvi1()
		{
			Send(Dvi1Cmd);
			Send(PollInput);
		}


        /// <summary>
        /// Set the input to Video1
        /// </summary>
		public void InputVideo1()
		{
			Send(Video1Cmd);
			Send(PollInput);
		}


        /// <summary>
        /// Set the input to VGA
        /// </summary>
		public void InputVga()
		{
			Send(VgaCmd);
			Send(PollInput);
		}

        /// <summary>
        /// Set the input to RGB
        /// </summary>
		public void InputRgb()
		{
			Send(RgbCmd); 
			Send(PollInput);
		}

        /// <summary>
        /// Executes an input switch
        /// </summary>
        /// <param name="selector"></param>
		public override void ExecuteSwitch(object selector)
		{
			if (!_PowerIsOn)
			{
				PowerOn();
				 var tempSelector = selector as Action; 
				
				
				var inpuptTimer = new Crestron.SimplSharp.CTimer(o =>
				{
					if (tempSelector != null)
						(tempSelector).Invoke();
					else { Debug.Console(1, this, "WARNING: ExecuteSwitch cannot handle type {0}", selector.GetType()); }
				}, WarmupTime);
			}
			else
			{
				if (selector is Action)
					(selector as Action).Invoke();
				else
					Debug.Console(1, this, "WARNING: ExecuteSwitch cannot handle type {0}", selector.GetType());

			}
		}

        /// <summary>
        /// Set the volume level
        /// </summary>
        /// <param name="level">level to set</param>
		void SetVolume(ushort level)
		{
			var levelString = string.Format("{0}{1:X3}\x03", VolumeLevelPartialCmd, level);

			//Debug.Console(2, this, "Volume:{0}", ComTextHelper.GetEscapedText(levelString));
			_VolumeLevel = level;
			VolumeLevelFeedback.FireUpdate();
		}

		#region IBasicVolumeWithFeedback Members

        /// <summary>
        /// Provides feedback of current volume level
        /// </summary>
		public IntFeedback VolumeLevelFeedback { get; private set; }

        /// <summary>
        /// Provides feedback of current mute state
        /// </summary>
		public BoolFeedback MuteFeedback { get; private set; }

        /// <summary>
        /// Unmutes the display
        /// </summary>
		public void MuteOff()
		{
			Send(MuteOffCmd);
            _IsMuted = true;
            MuteFeedback.FireUpdate();
		}

        /// <summary>
        /// Mutes the display
        /// </summary>
		public void MuteOn()
		{
			Send(MuteOnCmd);
            _IsMuted = false;
            MuteFeedback.FireUpdate();
		}

		void IBasicVolumeWithFeedback.SetVolume(ushort level)
		{
			SetVolume(level);
		}

		#endregion

		#region IBasicVolumeControls Members

		public void MuteToggle()
		{
			Send(MuteToggleIrCmd);
		}

        /// <summary>
        /// Decrements the volume level
        /// </summary>
        /// <param name="pressRelease"></param>
		public void VolumeDown(bool pressRelease)
		{
			SetVolume(_VolumeLevel--);
		}

        /// <summary>
        /// Increments the volume level
        /// </summary>
        /// <param name="pressRelease"></param>
		public void VolumeUp(bool pressRelease)
		{
			SetVolume(_VolumeLevel++);
		}

		#endregion
		#region IBridge Members

        /// <summary>
        /// Calls the extension method to bridge the device to an EiscApi class (SIMPL Bridge)
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
		public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
            DisplayControllerJoinMap joinMap = new DisplayControllerJoinMap(joinStart, typeof(DisplayControllerJoinMap));

            var JoinMapSerialized = JoinMapHelper.GetJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(JoinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<DisplayControllerJoinMap>(JoinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Display: {0}", Name);

            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;


            var commMonitor = this as ICommunicationMonitor;
            if (commMonitor != null)
            {
                commMonitor.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            }

            InputNumberFeedback.LinkInputSig(trilist.UShortInput[joinMap.InputSelect.JoinNumber]);

            // Two way feedbacks
            var twoWayDisplay = this as PepperDash.Essentials.Core.TwoWayDisplayBase;
            if (twoWayDisplay != null)
            {
                trilist.SetBool(joinMap.IsTwoWayDisplay.JoinNumber, true);

                twoWayDisplay.CurrentInputFeedback.OutputChange += new EventHandler<FeedbackEventArgs>(CurrentInputFeedback_OutputChange);



            }

            // Power Off
            trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, () =>
            {
                PowerOff();
            });

            PowerIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

            // PowerOn
            trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, () =>
            {
                PowerOn();
            });


            PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);

            int count = 1;
            var displayBase = this as PepperDash.Essentials.Core.DisplayBase;
            foreach (var input in InputPorts)
            {
                //displayDevice.InputKeys.Add(input.Key.ToString());
                //var tempKey = InputKeys.ElementAt(count - 1);
                trilist.SetSigTrueAction((ushort)(joinMap.InputSelectOffset.JoinNumber + count), () => { ExecuteSwitch(InputPorts[input.Key.ToString()].Selector); });
                Debug.Console(2, this, "Setting Input Select Action on Digital Join {0} to Input: {1}", joinMap.InputSelectOffset.JoinNumber + count, InputPorts[input.Key.ToString()].Key.ToString());
                trilist.StringInput[(ushort)(joinMap.InputNamesOffset.JoinNumber + count)].StringValue = input.Key.ToString();
                count++;
            }

            Debug.Console(2, this, "Setting Input Select Action on Analog Join {0}", joinMap.InputSelect);
            trilist.SetUShortSigAction(joinMap.InputSelect.JoinNumber, (a) =>
            {
                if (a == 0)
                {
                    PowerOff();
                }
                else if (a > 0 && a < InputPorts.Count)
                {
                    ExecuteSwitch(InputPorts.ElementAt(a - 1).Selector);
                }
                else if (a == 102)
                {
                    PowerToggle();

                }
                Debug.Console(2, this, "InputChange {0}", a);


            });


            var volumeDisplay = this as IBasicVolumeControls;
            if (volumeDisplay != null)
            {
                trilist.SetBoolSigAction(joinMap.VolumeUp.JoinNumber, (b) => volumeDisplay.VolumeUp(b));
                trilist.SetBoolSigAction(joinMap.VolumeDown.JoinNumber, (b) => volumeDisplay.VolumeDown(b));
                trilist.SetSigTrueAction(joinMap.VolumeMute.JoinNumber, () => volumeDisplay.MuteToggle());

                var volumeDisplayWithFeedback = volumeDisplay as IBasicVolumeWithFeedback;
                if (volumeDisplayWithFeedback != null)
                {
                    volumeDisplayWithFeedback.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.VolumeLevel.JoinNumber]);
                    volumeDisplayWithFeedback.MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VolumeMute.JoinNumber]);
                }
            }
		}

        void CurrentInputFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {

            Debug.Console(0, this, "CurrentInputFeedback_OutputChange {0}", e.StringValue);

        }

		#endregion
	}
}