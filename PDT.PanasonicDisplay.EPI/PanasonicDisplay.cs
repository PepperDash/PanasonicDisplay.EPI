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
using PepperDash.Essentials.Bridges;



namespace PDT.PanasonicDisplay.EPI
{
	public class PdtPanasonicDisplay : TwoWayDisplayBase, IBasicVolumeWithFeedback, ICommunicationMonitor, IBridge
	{
		public static void LoadPlugin()
		{
			PepperDash.Essentials.Core.DeviceFactory.AddFactoryForType("panasonicdisplay", PdtPanasonicDisplay.BuildDevice);
		}

        public static string MinimumEssentialsFrameworkVersion = "1.4.23";

		public static PdtPanasonicDisplay BuildDevice(DeviceConfig dc)
		{
			var config = JsonConvert.DeserializeObject<DeviceConfig>(dc.Properties.ToString());
			var newMe = new PdtPanasonicDisplay(dc.Key, dc.Name, dc);
			return newMe;
		}


		public IBasicCommunication Communication { get; private set; }
		public CommunicationGather PortGather { get; private set; }
		public StatusMonitorBase CommunicationMonitor { get; private set; }

		public int InputNumber;
		public IntFeedback InputNumberFeedback;
		public static List<string> InputKeys = new List<string>();

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
		public PdtPanasonicDisplay(string key, string name, DeviceConfig config)
			: base(key, name)
		{
			Communication = CommFactory.CreateCommForDevice(config);

			Init();
		}


		/// <summary>
		/// Constructor for COM
		/// </summary>
		/*
		public PdtPanasonicDisplay(string key, string name, ComPort port, ComPort.ComPortSpec spec)
			: base(key, name)
		{
			Communication = new ComPortController(key + "-com", port, spec);
			Init();
		}
		*/
		void Init()
		{
			PortGather = new CommunicationGather(Communication, '\x03');
			PortGather.LineReceived += this.Port_LineReceived;
			CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, "\x02QPW\x03\x02QMI\x03"); // Query Power
			
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this));
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this));
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.DviIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Dvi, new Action(InputDvi1), this));
			InputPorts.Add(new RoutingInputPort(RoutingPortNames.VgaIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Vga, new Action(InputVga), this));


			VolumeLevelFeedback = new IntFeedback(() => { return _VolumeLevel; });
			MuteFeedback = new BoolFeedback(() => _IsMuted);
			InputNumberFeedback = new IntFeedback(() => { Debug.Console(2, this, "CHange Input number {0}", InputNumber); return InputNumber; });
				

			//    new BoolCueActionPair(CommonBoolCue.Menu, b => { if(b) Send(MenuIrCmd); }),
			//    new BoolCueActionPair(CommonBoolCue.Up, b => { if(b) Send(UpIrCmd); }),
			//    new BoolCueActionPair(CommonBoolCue.Down, b => { if(b) Send(DownIrCmd); }),
			//    new BoolCueActionPair(CommonBoolCue.Left, b => { if(b) Send(LeftIrCmd); }),
			//    new BoolCueActionPair(CommonBoolCue.Right, b => { if(b) Send(RightIrCmd); }),
			//    new BoolCueActionPair(CommonBoolCue.Select, b => { if(b) Send(SelectIrCmd); }),
			//    new BoolCueActionPair(CommonBoolCue.Exit, b => { if(b) Send(ExitIrCmd); }),

			//};
			WarmupTime = 17000;
		}

		~PdtPanasonicDisplay()
		{
			PortGather = null;
		}

		public override bool CustomActivate()
		{
			Communication.Connect();

			CommunicationMonitor.StatusChange += (o, a) => { Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status); };
			CommunicationMonitor.Start();
			return true;
		}

		void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
		{
			port.FeedbackMatchObject = fbMatch;
			InputPorts.Add(port);
		}

		/*
		public override FeedbackCollection<Feedback> Feedbacks
		{
			get
			{
				var list = base.Feedbacks;
				
				list.AddRange(new list<Feedback>
				{

				});
				return list;
			}
		}
		*/

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

		void Send(string s)
		{
			if (Debug.Level == 2)
				Debug.Console(2, this, "Send: '{0}'", ComTextHelper.GetEscapedText(s));
			Communication.SendText(s);
		}


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

		public override void PowerToggle()
		{
			if (PowerIsOnFeedback.BoolValue && !IsWarmingUpFeedback.BoolValue)
				PowerOff();
			else if (!PowerIsOnFeedback.BoolValue && !IsCoolingDownFeedback.BoolValue)
				PowerOn();
		}

		public void InputHdmi1()
		{
			Send(Hdmi1Cmd);
			Send(PollInput);
		}

		public void InputHdmi2()
		{

			Send(Hdmi2Cmd);
			Send(PollInput);
		}

		public void InputHdmi3()
		{

			Send(Hdmi3Cmd);
			Send(PollInput);
		}

		public void InputHdmi4()
		{
			Send(Hdmi4Cmd);
			Send(PollInput);
		}

		public void InputDisplayPort1()
		{
			Send(Dp1Cmd);
			Send(PollInput);
		}

		public void InputDisplayPort2()
		{
			Send(Dp2Cmd);
			Send(PollInput);
		}

		public void InputDvi1()
		{
			Send(Dvi1Cmd);
			Send(PollInput);
		}

		public void InputVideo1()
		{
			Send(Video1Cmd);
			Send(PollInput);
		}

		public void InputVga()
		{
			Send(VgaCmd);
			Send(PollInput);
		}

		public void InputRgb()
		{
			Send(RgbCmd); 
			Send(PollInput);
		}

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

			}//Send((string)selector);
		}

		void SetVolume(ushort level)
		{
			var levelString = string.Format("{0}{1:X3}\x03", VolumeLevelPartialCmd, level);

			//Debug.Console(2, this, "Volume:{0}", ComTextHelper.GetEscapedText(levelString));
			_VolumeLevel = level;
			VolumeLevelFeedback.FireUpdate();
		}

		#region IBasicVolumeWithFeedback Members

		public IntFeedback VolumeLevelFeedback { get; private set; }

		public BoolFeedback MuteFeedback { get; private set; }

		public void MuteOff()
		{
			Send(MuteOffCmd);
		}

		public void MuteOn()
		{
			Send(MuteOnCmd);
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

		public void VolumeDown(bool pressRelease)
		{
			//throw new NotImplementedException();
			//#warning need incrementer for these
			SetVolume(_VolumeLevel++);
		}

		public void VolumeUp(bool pressRelease)
		{
			//throw new NotImplementedException();
			SetVolume(_VolumeLevel--);
		}




		#endregion
		#region IBridge Members

		public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey)
		{
			this.LinkToApiExt(trilist, joinStart, joinMapKey);
		}

		#endregion
	}
}