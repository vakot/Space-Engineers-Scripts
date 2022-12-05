// -------------------------------------------------------------------------------------------------------- \\
// =========== Vakot Ind. Simple Vehicle Controller Script ============ \\
// -------------------------------------------------------------------------------------------------------- \\

// ------------------ DESCRIPTION ------------------ \\

/*
 *  Script use all found wheel's suspension in all subgrid's (exclude connected with connector's).
 *  You also can exclude wheel's suspension from being controlled using "Ignore" keyword.
 *  The keyword is case insensitive.
 *  Exclude example: "Wheel Suspension 3x3 Right iGnOrE"
 *
 *  Script have the light manager. It use the same light source for Brake and Reverse Light's.
 *  You also can include light's to being controlled using [BrakeLightTag] keyword (Set in CONFIGURATION).
 *  The keyword is case insensitive.
 *  Include example: "Interior Light - BrAkE"
 *
 *  To print information to LCD use [LCDTag] keyword (Set in CONFIGURATION) and write to custom data @<display-index>.
 *
 *  Main controller will be choosed by this priority:
 *  1. Controller in under control at moment
 *  2. Controller with [ControllerName] name value
 *  3. Main ship controller
 *  4. First found controller which is not Remote Control
 *  5. First found controller even if it Remote Control
 *
 *  If your main grid does not contain at least 1 wheel suspension - to stop vehicle use "Space" button or "Brake" run argument.
 *  If Safe Mode is enabled, handbrake will be automatically enabled when you leave the cockpit
 *
 *  Installation:
 *  Build a vehicle
 *  Place Programmable Block on it and configurate for yourself
 *  Drive!
 */

//=============================== ARGUMENTS LIST ===================================\\

/*
 *  Up - Set suspension height to [MaxHeight] value
 *  Down - Set suspension height to [MinHeight] value
 *  UpDown - Switch between Up & Down
 *  Friction - Toggle suspension friction between [MinFriction] & [MaxFriction] values
 *  Power - Toggle suspension power-limit between [TargetPower] & [MaxPower] values
 *  Climb - Run Friction & Power arguments in the same time (use to climb a steep slope)
 *  Cruise - Toggle cruise control on/off (you can manage cruise speed with acceleration button's [w, s])
 *  Safe - Toggle safe mode on/off
 *  Brake - Toggle handbrake on/off (work only if your main grid does not contains any wheel's suspension)
 *
 *  LCD CustomData argument's:
 *  - ores (display minimalistic ores stats)
 *  - ingots (display minimalistic ingots stats)
 *  - inventory (display inventory items like in AutoLCD by MMaster)
 *  -- i-[ores/ingots/components/tools/ammos/other] (same as inventory, but display only one type of items at the time)
 *  - powergraph (display power consumption graph)
 *  - inventories (display inventoryes list with status bar)
 *  -- assemblers (display assemblers inventoryes list with status bar)
 *  -- refineries (display refineries inventoryes list with status bar)
 *  -- containers (display containers inventoryes list with status bar)
 *  -- reactors (display reactors inventoryes list with status bar)
 *  - none (set by default and don't display any content)
*/

// ----------------- CONFIGURATION ----------------- \\

/* Change it only before first run of program, rest of time use PB Custom Data */

public readonly static List<string> RunStatus = new List<string>
{
    "[|---]", 
    "[-|--]", 
    "[--|-]", 
    "[---|]", 
    "[--|-]", 
    "[-|--]"
};



// -------------------------------------------------------------------------------------------------------- \\
// ========== !!! DONT CHANGE ANYTHING BELOW THIS LINE !!! =========== \\
// -------------------------------------------------------------------------------------------------------- \\



// Block's
VehicleController _VehicleController;
ShipStatusManager _ShipStatusManager;
SurfaceContentManager _SurfaceContentManager;

// Variables
int Counter = 0;

public const string Version = "2.2",
                    IniSectionGeneral = "General";

// Program
void Status()
{
    string Status = $"Vehicle Controller {RunStatus[Counter % RunStatus.Count]}";
    Status += $"\nNext update in: {10 - Counter / 6}" + "s\n";

    Status += $"\n------ Block's Info ------\n";
    Status += $"Controller: {_VehicleController.ControllerNameSafe}\n";
    Status += $"Suspension's: {_VehicleController.SuspensionsCount}\n";
    Status += $"Light's: {_VehicleController.LightsCount}\n";
    Status += $"Power producer's: {_ShipStatusManager.PowerProducersCount}\n";
    Status += $"Inventories: {_ShipStatusManager.InventoriesCount}\n";
    Status += $"Surfaces: {_SurfaceContentManager.SurfacesCount()}\n";

    Status += $"\n------ Runtime Info ------\n";
    Status += $"Instruction's Count: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}\n";
    Status += $"Call Chain Depth: {Runtime.CurrentCallChainDepth}/{Runtime.MaxCallChainDepth}\n";
    Status += $"Time Since Last Run: {Runtime.TimeSinceLastRun.Milliseconds}ms\n";
    Status += $"Last Runtime Took: {Runtime.LastRunTimeMs}ms\n";
    Echo(Status);
}

void Update()
{
    Counter = 0;

    _SurfaceContentManager.Update();

    _VehicleController.Update();
    _ShipStatusManager.Update();
}

Program() 
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    _SurfaceContentManager = new SurfaceContentManager(this);

    _VehicleController = new VehicleController(this);
    _ShipStatusManager = new ShipStatusManager(this);

    _VehicleController.SetupSurfaces(_SurfaceContentManager);
    _ShipStatusManager.SetupSurfaces(_SurfaceContentManager);
}

void Main(String argument) 
{
    if (++Counter % 60 == 0) Update();

    Status();

    _VehicleController.Control();
    _VehicleController.RunArgument(argument);

    _SurfaceContentManager.DrawContent(6, true);
}

// Last update - 01.12.2022
#region VehicleControllerClass
class VehicleController
{
    private Program _Program;
    private MyIni _ini = new MyIni();

    public const string IniSectionControllerGeneral = "Vehilce Controller - General",
                        IniKeyControllerName = "Controller name",
                        IniKeyAutomaticNewWheelsAdd = "Automatic add wheels (every 10s) (def: true)",
                        IniKeyCruiseAutoOff = "Cruise auto off (def: false)",
                        IniKeySmoothDriving = "Smooth driving (torque) (def: true)",
                        IniKeyUseCustomAirShock = "Custom air shock (def: true)",

                        IniSectionControllerLight = "Vehilce Controller - Light",
                        IniKeyBrakeLightKeyword = "Light keyword",
                        IniKeyReverseManagement = "Reverse light's management (def: true)",
                        IniKeyHabariteManagement = "Habarite light's management (def: true)",

                        IniSectionControllerStrength = "Vehilce Controller - Strength (in %)",
                        IniKeyTargetStrength = "Target strength (def: 10)",

                        IniSectionControllerSpeed = "Vehilce Controller - Speed (in km/h)",
                        IniKeyForwardSpeedLimit = "Forward speed limit (def: 130)",
                        IniKeyBackwardSpeedLimit = "Backward speed limit (def: 20)",

                        IniSectionControllerSteer = "Vehilce Controller - Steer (in deg)",
                        IniKeySteerAngle = "Steer angle (def: 18)",

                        IniSectionControllerPower = "Vehilce Controller - Power (in %)",
                        IniKeyMinPower = "Min power (def: 10)",
                        IniKeyTargetPower = "Target power (def: 40)",
                        IniKeyMaxPower = "Max power (def: 100)",

                        IniSectionControllerFriction = "Vehilce Controller - Friction (in %)",
                        IniKeyMinFriction = "Min friction (def: 35)",
                        IniKeyMaxFriction = "Max friction (def:100)",

                        IniSectionControllerHeight = "Vehilce Controller - Height (in m)",
                        IniKeyMinHeight = "Min height (def: 0)",
                        IniKeyMaxHeight = "Max height (def: 10)";

    private static List<IMyMotorSuspension> _Suspensions = new List<IMyMotorSuspension>();
    private static List<IMyLightingBlock> _Lights = new List<IMyLightingBlock>();
    private static IMyShipController _Controller;

    #region Config
    public string ControllerName { get; private set; } = "";
    public string BrakeLightTag { get; private set; } = "Brake";

    // Make vehicle stop if player leave cockpit
    public bool SafeMode { get; private set; } = true;

    // Reverse light
    public bool ReverseManagement { get; private set; } = true;
    // Don't off light, just make it less intensive
    public bool HabariteManagement { get; private set; } = true;

    // If true will add a new wheel (if old is missin) every 10 seconds (on script update)
    public bool AutomaticNewWheelsAdd { get; private set; } = true;
    // If true - turn Cruise Mode off when cruise speed less then 6m/s
    public bool CruiseAutoOff { get; private set; } = false;
    // Make vehicle accelerate smooth and add the torque bar
    public bool SmoothDriving { get; private set; } = true;
    // Replace vanilla AirShock system
    public bool UseCustomAirShock { get; private set; } = true;

    public float TargetStrength { get; private set; } = 10;

    public static float ForwardSpeedLimit { get; private set; } = 130;
    public static float BackwardSpeedLimit { get; private set; } = 20;

    public float SteerAngle { get; private set; } = 18;

    public float MinPower { get; private set; } = 10;
    public float TargetPower { get; private set; } = 40;
    public float MaxPower { get; private set; } = 100;

    public float MinFriction { get; private set; } = 35;
    public float MaxFriction { get; private set; } = 100;

    public float MinHeight { get; private set; } = 0;
    public float MaxHeight { get; private set; } = 10;
    #endregion

    private static bool _isCustomHandBrake;
    private static bool _isBrakeEnabled = true;
    
    private static bool _CruiseMode = false;
    private static float _CruiseSpeed = 0;

    private static float _CurrentTorque = 0;
    private static float _PropulsionMultiplier = 0;

    private static float _CurrentPowerLimit;
    private static float _CurrentPower => _Suspensions.Count > 0 ? _Suspensions[0].Power : 0f;
    private static float _CurrentPropulsion => _Suspensions.Count > 0 ? Math.Abs(_Suspensions[0].PropulsionOverride) : 0f;
    private static float _CurrentFriction;
    private static float _CurrentHeight;
    private static float _CurrentStrength;

    public VehicleController(Program Program)
    {
        _Program = Program;
        _CurrentPowerLimit = TargetPower;
        _CurrentFriction = MinFriction;
        _CurrentHeight = -MaxHeight;
        _CurrentStrength = TargetStrength;

        Update();
    }

    public void Update()
    {
        ParseIni();

        _Lights.Clear();
        _Program.GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(
            _Lights,
            x => x.CustomName.ToLower().Contains(BrakeLightTag.ToLower())
            && x.IsSameConstructAs(_Program.Me)
        );

        _Suspensions.Clear();
        _Program.GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(
            _Suspensions,
            x => !x.CustomName.ToLower().Contains("ignore")
            && x.IsSameConstructAs(_Program.Me)
        );

        if (AutomaticNewWheelsAdd) AddWheels();

        _Controller = GetShipController();
        if (!IsValid(_Controller)) return;

        _isCustomHandBrake = false;
        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            if (Suspension.CubeGrid != _Controller.CubeGrid)
            {
                _isCustomHandBrake = true;
                break;
            }
            Suspension.IsParkingEnabled = true;
            Suspension.Brake = true;
        }
        if (_isCustomHandBrake)
        {
            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                Suspension.IsParkingEnabled = false;
                Suspension.Brake = false;
            }
        }
    }

    private void ParseIni()
    {
        IMyTerminalBlock Block = _Program.Me as IMyTerminalBlock;

        _ini.Clear();

        if (_ini.TryParse(Block.CustomData))
        {
            ControllerName = _ini.Get(IniSectionControllerGeneral, IniKeyControllerName).ToString(ControllerName);
            AutomaticNewWheelsAdd = _ini.Get(IniSectionControllerGeneral, IniKeyAutomaticNewWheelsAdd).ToBoolean(AutomaticNewWheelsAdd);
            CruiseAutoOff = _ini.Get(IniSectionControllerGeneral, IniKeyCruiseAutoOff).ToBoolean(CruiseAutoOff);
            SmoothDriving = _ini.Get(IniSectionControllerGeneral, IniKeySmoothDriving).ToBoolean(SmoothDriving);
            UseCustomAirShock = _ini.Get(IniSectionControllerGeneral, IniKeyUseCustomAirShock).ToBoolean(UseCustomAirShock);

            BrakeLightTag = _ini.Get(IniSectionControllerLight, IniKeyBrakeLightKeyword).ToString(BrakeLightTag);
            ReverseManagement = _ini.Get(IniSectionControllerGeneral, IniKeyReverseManagement).ToBoolean(ReverseManagement);
            HabariteManagement = _ini.Get(IniSectionControllerGeneral, IniKeyHabariteManagement).ToBoolean(HabariteManagement);

            TargetStrength = _ini.Get(IniSectionControllerStrength, IniKeyTargetStrength).ToSingle(TargetStrength);

            ForwardSpeedLimit = _ini.Get(IniSectionControllerSpeed, IniKeyForwardSpeedLimit).ToSingle(ForwardSpeedLimit);
            BackwardSpeedLimit = _ini.Get(IniSectionControllerSpeed, IniKeyBackwardSpeedLimit).ToSingle(BackwardSpeedLimit);

            SteerAngle = _ini.Get(IniSectionControllerSteer, IniKeySteerAngle).ToSingle(SteerAngle);

            MinPower = _ini.Get(IniSectionControllerPower, IniKeyMinPower).ToSingle(MinPower);
            TargetPower = _ini.Get(IniSectionControllerPower, IniKeyTargetPower).ToSingle(TargetPower);
            MaxPower = _ini.Get(IniSectionControllerPower, IniKeyMaxPower).ToSingle(MaxPower);

            MinFriction = _ini.Get(IniSectionControllerFriction, IniKeyMinFriction).ToSingle(MinFriction);
            MaxFriction = _ini.Get(IniSectionControllerFriction, IniKeyMaxFriction).ToSingle(MaxFriction);

            MinHeight = _ini.Get(IniSectionControllerHeight, IniKeyMinHeight).ToSingle(MinHeight);
            MaxHeight = _ini.Get(IniSectionControllerHeight, IniKeyMaxHeight).ToSingle(MaxHeight);
        }
        else if (!string.IsNullOrWhiteSpace(Block.CustomData))
        {
            _ini.EndContent = Block.CustomData;
        }

        _ini.Set(IniSectionControllerGeneral, IniKeyControllerName, ControllerName);
        _ini.Set(IniSectionControllerGeneral, IniKeyAutomaticNewWheelsAdd, AutomaticNewWheelsAdd);
        _ini.Set(IniSectionControllerGeneral, IniKeyCruiseAutoOff, CruiseAutoOff);
        _ini.Set(IniSectionControllerGeneral, IniKeySmoothDriving, SmoothDriving);
        _ini.Set(IniSectionControllerGeneral, IniKeyUseCustomAirShock, UseCustomAirShock);

        _ini.Set(IniSectionControllerLight, IniKeyBrakeLightKeyword, BrakeLightTag);
        _ini.Set(IniSectionControllerLight, IniKeyReverseManagement, ReverseManagement);
        _ini.Set(IniSectionControllerLight, IniKeyHabariteManagement, HabariteManagement);

        _ini.Set(IniSectionControllerStrength, IniKeyTargetStrength, TargetStrength);

        _ini.Set(IniSectionControllerSpeed, IniKeyForwardSpeedLimit, ForwardSpeedLimit);
        _ini.Set(IniSectionControllerSpeed, IniKeyBackwardSpeedLimit, BackwardSpeedLimit);

        _ini.Set(IniSectionControllerSteer, IniKeySteerAngle, SteerAngle);

        _ini.Set(IniSectionControllerPower, IniKeyMinPower, MinPower);
        _ini.Set(IniSectionControllerPower, IniKeyTargetPower, TargetPower);
        _ini.Set(IniSectionControllerPower, IniKeyMaxPower, MaxPower);
        
        _ini.Set(IniSectionControllerFriction, IniKeyMinFriction, MinFriction);
        _ini.Set(IniSectionControllerFriction, IniKeyMaxFriction, MaxFriction);

        _ini.Set(IniSectionControllerHeight, IniKeyMinHeight, MinHeight);
        _ini.Set(IniSectionControllerHeight, IniKeyMaxHeight, MaxHeight);

        string Output = _ini.ToString();
        if (Output != Block.CustomData)
        {
            Block.CustomData = Output;
        }
    }

    public void Control()
    {
        if (!IsValid(_Controller))
        {
            _Controller = GetShipController();
            return;
        }
        // if (!_Controller.IsUnderControl) _Controller = GetShipController();

        ManageSafeMode();
        ManageTorque();
        ManageLights();
        ManageCruise();
        ManageWheels();
    }

    public void SetupSurfaces(SurfaceContentManager SurfaceContentManager)
    {
        SurfaceContentManager.AddContentType("dashboard", DrawDashboard);
        SurfaceContentManager.AddContentType("speed", DrawSpeed);
    }

    public string ControllerNameSafe => IsValid(_Controller) ? _Controller.CustomName : "Controller is not exist";
    public int SuspensionsCount => _Suspensions.Count;
    public int LightsCount => _Lights.Count;
    
    #region Helpers
    private bool IsValid(IMyTerminalBlock Block)
    {
        return (Block != null) && !Block.Closed;
    }

    private IMyShipController GetShipController()
    {
        List<IMyShipController> ControllerList = new List<IMyShipController>();
        _Program.GridTerminalSystem.GetBlocksOfType<IMyShipController>(ControllerList);

        foreach (IMyShipController Controller in ControllerList)
            if (Controller.ControlWheels && Controller.CanControlShip && Controller.CustomName.ToLower() == ControllerName.ToLower())
                return Controller;

        foreach (IMyShipController Controller in ControllerList)
            if (Controller.ControlWheels && Controller.CanControlShip && Controller.IsMainCockpit)
                return Controller;
        
        foreach (IMyShipController Controller in ControllerList)
            if (Controller.ControlWheels && Controller.CanControlShip && !(Controller is IMyRemoteControl))
                return Controller;

        foreach (IMyShipController Controller in ControllerList)
            if (Controller.ControlWheels && Controller.CanControlShip)
                return Controller;

        return null;
    }

    private static float GetShipVelocityForward()
    {
        Vector3D Velocity = _Controller.GetShipVelocities().LinearVelocity;
        return (float)Velocity.Dot(_Controller.WorldMatrix.Forward);
    }
    private static float GetShipVelocityDown()
    {
        Vector3D Velocity = _Controller.GetShipVelocities().LinearVelocity;
        return (float)Velocity.Dot(_Controller.WorldMatrix.Down);
    }
    private float GetTorque()
    {
        float Torque = _CurrentTorque * _CurrentPowerLimit / 100;
        Torque = Math.Min(Torque, _CurrentPowerLimit);
        return Math.Min(Math.Max(MinPower, Torque), Math.Max(TargetPower, _CurrentPowerLimit));
    }

    private Vector3D GetAverageWheelsPosition()
    {
        Vector3D Sum = Vector3D.Zero;
        int Count = 0;

        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            if (Suspension.IsAttached)
            {
                Sum += Suspension.GetPosition();
                Count++;
            }
        }

        if (Count == 0) return Vector3D.Zero;
        return Sum / Count;
    }
    #endregion

    #region Changers
    private void Setup()
    {
        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            Suspension.Friction = _CurrentFriction;
            Suspension.Height = _CurrentHeight;
        }
    }
    public void RunArgument(string argument)
    {
        if (argument.ToLower() == "safe")
        {
            SafeMode = !SafeMode;
        }
        else if (argument.ToLower() == "power")
        {
            _CurrentPowerLimit = Math.Round(_CurrentPowerLimit) == TargetPower ? MaxPower : TargetPower;
        }
        else if (argument.ToLower() == "friction")
        {
            _CurrentFriction = Math.Round(_CurrentFriction) == MinFriction ? MaxFriction : MinFriction;
        }
        else if (argument.ToLower() == "climb")
        {
            _CurrentPowerLimit = Math.Round(_CurrentPowerLimit) == TargetPower ? MaxPower : TargetPower;
            _CurrentFriction = Math.Round(_CurrentFriction) == MinFriction ? MaxFriction : MinFriction;
        }
        else if (argument.ToLower() == "up")
        {
            _CurrentHeight = -MaxHeight;
        }
        else if (argument.ToLower() == "down")
        {
            _CurrentHeight = MinHeight;
        }
        else if (argument.ToLower() == "updown")
        {
            _CurrentHeight = Math.Round(_CurrentHeight) == MinHeight ? -MaxHeight : MinHeight;
        }
        else if (argument.ToLower() == "brake")
        {
            if (_isCustomHandBrake)
                _isBrakeEnabled = !_isBrakeEnabled;
            else
                _isBrakeEnabled = false;
        }
        else if (argument.ToLower() == "cruise")
        {
            _CruiseMode = !_CruiseMode;
            _CruiseSpeed = (float)Math.Round(GetShipVelocityForward());
        }
    }
    #endregion

    #region Controllers
    private void ManageSafeMode()
    {
        if (!SafeMode || _Controller.IsUnderControl) return;

        _CruiseMode = false;
        _CruiseSpeed = 0;

        if (!_isCustomHandBrake)
            _Controller.HandBrake = true;
        else
            _isBrakeEnabled = true;
    }
    private void ManageCruise()
    {
        bool isBrake =  _Controller.HandBrake
                        || (_isCustomHandBrake && _isBrakeEnabled);

        if (isBrake) _CruiseMode = false;
        if (!_CruiseMode)
        {
            _CruiseSpeed = 0;
            return;
        }

        _CruiseSpeed = (float)Math.Round(Math.Min(_CruiseSpeed - _Controller.MoveIndicator.Z, ForwardSpeedLimit / 3.6f));

        if (_CruiseSpeed < 6 && CruiseAutoOff)
        {
            _CruiseMode = false;
            _CruiseSpeed = 0;
            return;
        }

        _CruiseSpeed = Math.Max(_CruiseSpeed, 6);
    }
    private void ManageTorque()
    {
        if (!SmoothDriving)
        {
            _CurrentTorque = 100;
            _PropulsionMultiplier = 1;
            return;
        }

        float Velocity = Math.Abs(GetShipVelocityForward());

        bool isAccelerate = _Controller.MoveIndicator.Z != 0 || _CruiseMode;
        bool isForwardOffset = Velocity * 3.6f < ForwardSpeedLimit * 0.9f;
        bool isBackwardOffset = Velocity * 3.6f < BackwardSpeedLimit * 0.9f;

        _CurrentTorque = isAccelerate
            ? Math.Min(_CurrentTorque * (_Controller.MoveIndicator.Y > 0 || Velocity <= 6 ? 1.15f : 1.05f), 100)
            : Math.Max(_CurrentTorque * 0.95f, 5);

        _PropulsionMultiplier = isForwardOffset && _Controller.MoveIndicator.Z < 0
            ? Math.Min(_PropulsionMultiplier * 1.01f, 360 / ForwardSpeedLimit)
            : ( isBackwardOffset && _Controller.MoveIndicator.Z > 0
            ? Math.Min(_PropulsionMultiplier * 1.01f, 360 / BackwardSpeedLimit)
            : Math.Max(_PropulsionMultiplier * 0.9f, 1)
            );
    }
    private void ManageLights()
    {
        bool isReverse = ReverseManagement
                        && GetShipVelocityForward() < -0.5f
                        && _Controller.MoveIndicator.Z > 0;

        bool isBrake =  _Controller.HandBrake
                        || _Controller.MoveIndicator.Z > 0
                        || _Controller.MoveIndicator.Y > 0
                        || (_isCustomHandBrake && _isBrakeEnabled);

        foreach (IMyLightingBlock Light in _Lights)
        {
            Light.Enabled = true;

            Light.Intensity = isBrake || isReverse ? 2.5f : 0.5f;
            Light.Radius = isBrake || isReverse ? 2.5f : 0.5f;
            Light.Color = isReverse ? new Color(250, 230, 180) : new Color(255, 30, 30);
        }
    }
    private void ManageWheels()
    {
        Setup();
        SteerControl();
        StrengthControl();

        if (_CruiseMode)
            CruiseControl();
        else
            ManualControl();

        BrakeControl();

        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            if (UseCustomAirShock)
                Suspension.AirShockEnabled = false;
            else
                Suspension.AirShockEnabled = true;
        }
    }

    private void AddWheels()
    {
        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            for(int i = 0; i <= 10; ++i)
            {
                if (!Suspension.IsAttached) Suspension.ApplyAction("Add Top Part");
                else break;
            }
        }
    }
    private void ManualControl()
    {
        float Velocity = GetShipVelocityForward();
        float Torque = GetTorque();
        Vector3D AverageWheelsPosition = GetAverageWheelsPosition();

        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
            Vector3D Left = _Controller.WorldMatrix.Left;
            Vector3D Right = _Controller.WorldMatrix.Right;

            bool isLeft = Vector3D.Dot(Difference, Left) > 0;

            float Sign = Math.Sign(Vector3D.Dot(Suspension.WorldMatrix.Up, isLeft ? Left : Right));

            Suspension.SetValueFloat("Speed Limit", _Controller.MoveIndicator.Z > 0 && ReverseManagement ?  BackwardSpeedLimit : ForwardSpeedLimit);

            Suspension.Power = Torque;

            float Propulsion = _Controller.MoveIndicator.Z;
            Propulsion *= Suspension.Power * _PropulsionMultiplier / 100;

            if (Velocity > ForwardSpeedLimit / 3.6f)
                Propulsion = (Velocity - ForwardSpeedLimit / 3.6f) / _Suspensions.Count;
            else if (Math.Abs(Velocity) > BackwardSpeedLimit / 3.6f && Velocity < 0)
                Propulsion = (Velocity + BackwardSpeedLimit / 3.6f) / _Suspensions.Count;
            
            if (_CurrentPowerLimit / 100 <= Math.Abs(Propulsion))
                Propulsion = _CurrentPowerLimit / 100 * (Propulsion > 0 ? 1 : -1);

            Suspension.PropulsionOverride = Math.Max(Math.Min(Propulsion * Sign, 1), -1) * (isLeft ? -1 : 1);
        }

    }
    private void CruiseControl()
    {
        float Velocity = GetShipVelocityForward();
        float Torque =  GetTorque();
        Vector3D AverageWheelsPosition = GetAverageWheelsPosition();

        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
            Vector3D Left = _Controller.WorldMatrix.Left;
            Vector3D Right = _Controller.WorldMatrix.Right;

            bool isLeft = Vector3D.Dot(Difference, Left) > 0;

            float Sign = Math.Sign(Vector3D.Dot(Suspension.WorldMatrix.Up, isLeft ? Left : Right));

            Suspension.SetValueFloat("Speed Limit", Convert.ToSingle(Math.Round(_CruiseSpeed * 3.6f)));

            Suspension.Power = Torque;

            float Propulsion = (Velocity - _CruiseSpeed) / (_Suspensions.Count / 2);
            Propulsion *= (Suspension.Power * _PropulsionMultiplier / 100);
            Suspension.PropulsionOverride = Math.Max(Math.Min(Propulsion * Sign, 1), -1) * (isLeft ? -1 : 1);
        }
    }
    private void SteerControl()
    {
        Vector3D AverageWheelsPosition = GetAverageWheelsPosition();

        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
            Vector3D Backward = _Controller.WorldMatrix.Backward;

            bool isBackward = Vector3D.Dot(Difference, Backward) > 0;

            float Dot = (float)Math.Abs(Vector3D.Dot(Vector3D.Normalize(Difference), Suspension.WorldMatrix.Up));

            Suspension.MaxSteerAngle = SteerAngle * (float)Math.PI / 180.0f * (1 - Dot);
            Suspension.SteeringOverride = _Controller.MoveIndicator.X * (isBackward ? -1 : 1);
        }
    }
    private void BrakeControl()
    {
        if (!_Controller.HandBrake && !(_isCustomHandBrake && _isBrakeEnabled) && !(_Controller.MoveIndicator.Y > 0)) return;

        Vector3D AverageWheelsPosition = GetAverageWheelsPosition();
        float Velocity = GetShipVelocityForward();

        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
            Vector3D Left = _Controller.WorldMatrix.Left;

            bool isLeft = Vector3D.Dot(Difference, Left) > 0;

            Suspension.SetValueFloat("Speed Limit", 0);

            float Propulsion = Velocity / 2.5f;

            Suspension.PropulsionOverride = Math.Max(Math.Min(Propulsion, 1), -1) * (isLeft ? -1 : 1);
        }
    }
    private void StrengthControl() 
    {
        Vector3D AverageWheelsPosition = GetAverageWheelsPosition();
        float VelocityForward = GetShipVelocityForward();
        float VelocityDown = GetShipVelocityDown();

        foreach (IMyMotorSuspension Suspension in _Suspensions)
        {
            Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
            Vector3D Left = _Controller.WorldMatrix.Left;
            Vector3D Backward = _Controller.WorldMatrix.Backward;

            float Multiplier = (float)Math.Abs(Vector3D.Dot(Difference, Backward));
            bool isLeft = Vector3D.Dot(Difference, Left) > 0;
            float VelocityDifference = (VelocityForward > 0 ? ForwardSpeedLimit : BackwardSpeedLimit) / 3.6f - Math.Abs(VelocityForward);

            float StrengthDelta = TargetStrength / (_Suspensions.Count * 2);
            float Strength;

            if (Math.Pow(VelocityDown, 2) > Math.Abs(VelocityForward))
                Strength = Math.Max(TargetStrength * (float)Math.Pow(VelocityDown * 0.1f, 2), TargetStrength);
            else
                Strength = Math.Max(Math.Min(TargetStrength * Math.Abs(VelocityForward * 0.1f) * 0.5f, _CurrentStrength + StrengthDelta), TargetStrength);
            
            _CurrentStrength = Math.Min(Strength, 100);

            // tilt on steering
            float MinStrength = TargetStrength * 0.75f;
            if (_Controller.MoveIndicator.X > 0 && isLeft)
                Strength -= Math.Abs(VelocityDifference) * _Controller.MoveIndicator.X;
            else if (_Controller.MoveIndicator.X < 0 && !isLeft)
                Strength += Math.Abs(VelocityDifference) * _Controller.MoveIndicator.X;
            Strength = Math.Max(Math.Min(Strength, 100), MinStrength);

            Suspension.Strength = Math.Max(Strength * Multiplier / 4, Strength);
        }
    }
    #endregion
    
    #region ContentManager
    private readonly Dictionary<float, Color> Colors = new Dictionary<float, Color>() {
        { 0.0f, new Color(220, 30, 30) },
        { 0.1f, new Color(201, 49, 30) },
        { 0.2f, new Color(182, 68, 30) },
        { 0.3f, new Color(163, 87, 30) },
        { 0.4f, new Color(144, 106, 30) },
        { 0.5f, new Color(125, 125, 30) },
        { 0.6f, new Color(106, 114, 30) },
        { 0.7f, new Color(87, 163, 30) },
        { 0.8f, new Color(68, 182, 30) },
        { 0.9f, new Color(49, 201, 30) },
        { 1.0f, new Color(30, 220, 30) }
    };
    private void DrawDashboard(SurfaceContentManager.SurfaceManager Manager)
    {
        // Cruise
        Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1f, 0.15f), new Vector2(Math.Min(1f - _CruiseSpeed / (ForwardSpeedLimit / 3.6f), 0.85f), 0));
        Manager.AddTextBuilder(
            "Cruise speed:",
            new Vector2(0f, 0f), new Vector2(1f, 0.15f),
            FontSize: 1.5f,
            Alignment: TextAlignment.LEFT, ExtraPadding: true
        );
        Manager.AddTextBuilder(
            _CruiseMode ? $"{_CruiseSpeed}m/s" : "Off",
            new Vector2(0f, 0f), new Vector2(1f, 0.15f),
            FontSize: 1.5f,
            Alignment: TextAlignment.RIGHT, ExtraPadding: true
        );

        // Speed
        float Velocity = Math.Abs(GetShipVelocityForward());
        Manager.AddCircleProgressBarBuilder(
            Velocity / (ForwardSpeedLimit / 3.6f), 0.15f,
            new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f),
            225, -45, Reverse: true
        );
        Manager.AddTextBuilder(
            String.Format("{0:0.00}", Velocity),
            new Vector2(0f, 0f), new Vector2(1f, 0.85f),
            FontSize: 2.75f
        );
        Manager.AddTextBuilder(
            "m/s",
            new Vector2(0f, 0.275f), new Vector2(1f, 0.375f),
            Alignment: TextAlignment.CENTER,
            FontSize: 1.2f
        );

        // Torque
        float Torque = _CurrentTorque * 0.01f;
        Manager.AddCircleProgressBarBuilder(
            Torque, 1f,
            new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.85f),
            360, 90, Reverse: true
        );
        Manager.AddTextBuilder(
            $"{Math.Round(Torque * 6000)}\nRPM",
            new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.85f),
            color: Colors[(float)Math.Round(1f - Torque, 1, MidpointRounding.AwayFromZero)]
        );

        // Power & Friction
        float Power = _CurrentPower * 0.01f;
        float Friction = _CurrentFriction * 0.01f;
        Manager.AddBorderBuilder(new Vector2(0f, 0.15f), new Vector2(0.15f, 0.5f));
        Manager.AddBorderBuilder(new Vector2(0f, 0.5f), new Vector2(0.15f, 0.85f));
        Manager.AddSquareProgressBarBuilder(Power, new Vector2(0f, 0.15f), new Vector2(0.15f, 0.5f));
        Manager.AddSquareProgressBarBuilder(Friction, new Vector2(0f, 0.5f), new Vector2(0.15f, 0.85f));
        Manager.AddTextBuilder(
            "Power",
            new Vector2(0f, 0.3f), new Vector2(0.15f, 0.5f),
            FontSize: 1.5f,
            color: Colors[(float)Math.Round(Power, 1, MidpointRounding.AwayFromZero)]
        );
        Manager.AddTextBuilder(
            "Friction",
            new Vector2(0f, 0.65f), new Vector2(0.15f, 0.85f),
            FontSize: 1.5f,
            color: Colors[(float)Math.Round(Friction, 1, MidpointRounding.AwayFromZero)]
        );
        
        // EMPTY RIGHT
        Manager.AddBorderBuilder(new Vector2(0.85f, 0.15f), new Vector2(1, 0.5f));
        Manager.AddBorderBuilder(new Vector2(0.85f, 0.5f), new Vector2(1, 0.85f));
        Manager.AddSquareProgressBarBuilder(0, new Vector2(0.85f, 0.15f), new Vector2(1, 0.5f));
        Manager.AddSquareProgressBarBuilder(0, new Vector2(0.85f, 0.5f), new Vector2(1, 0.85f));
        Manager.AddTextBuilder("Empty", new Vector2(0.85f, 0.3f), new Vector2(1, 0.5f), FontSize: 1.5f);
        Manager.AddTextBuilder("Empty", new Vector2(0.85f, 0.65f), new Vector2(1, 0.85f), FontSize: 1.5f);

        // EMPTY BOTTOM
        Manager.AddBorderBuilder(new Vector2(0f, 0.85f), new Vector2(1f, 1f));
        Manager.AddTextBuilder("Empty", new Vector2(0f, 0.85f), new Vector2(1f, 1f), FontSize: 1.5f);
    }
    private void DrawSpeed(SurfaceContentManager.SurfaceManager Manager)
    {
        // Speed
        float Velocity = Math.Abs(GetShipVelocityForward());
        Manager.AddCircleProgressBarBuilder(
            Velocity / (ForwardSpeedLimit / 3.6f), 0.15f,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            225, -45, Reverse: true
        );
        Manager.AddTextBuilder(
            String.Format("{0:0.00}", Velocity),
            new Vector2(0f, 0f), new Vector2(1f, 0.85f),
            FontSize: 4.5f
        );
        Manager.AddTextBuilder(
            "m/s",
            new Vector2(0f, 0.225f), new Vector2(1f, 0.325f),
            Alignment: TextAlignment.CENTER, FontSize: 2f
        );

        // Torque
        float Torque = _CurrentTorque * 0.01f;
        Manager.AddCircleProgressBarBuilder(
            _CurrentTorque * 0.01f, 1f,
            new Vector2(0.25f, 0.65f), new Vector2(0.75f, 1f),
            360, 90, Reverse: true);
        Manager.AddTextBuilder(
            $"{Math.Round(Torque * 6000)}\nRPM",
            new Vector2(0.25f, 0.65f), new Vector2(0.75f, 1f),
            FontSize: 1.2f,
            color: Colors[(float)Math.Round(1f - Torque, 1, MidpointRounding.AwayFromZero)]
        );
    }
    #endregion
}
#endregion

// Last update - 06.12.2022
#region ShipStatusManagerClass
class ShipStatusManager
{
    private Program _Program;

    private static List<IMyTerminalBlock> _PowerProducers = new List<IMyTerminalBlock>();
    private static List<IMyTerminalBlock> _Inventories = new List<IMyTerminalBlock>();

    private static List<float> _PowerConsumptionStory = new List<float>();

    private ItemsQuotas _Quotas = new ItemsQuotas();

    public ShipStatusManager(Program Program)
    {
        _Program = Program;

        SetupQuotas();

        Update();
    }

    public void Update()
    {
        _PowerProducers.Clear();
        _Inventories.Clear();

        List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
        _Program.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(Blocks, x => x.IsSameConstructAs(_Program.Me));

        _PowerProducers = Blocks.Where(x => x is IMyPowerProducer).ToList();
        _Inventories = Blocks.Where(x => x.HasInventory).ToList();
    }

    public void SetupSurfaces(SurfaceContentManager SurfaceContentManager)
    {
        SurfaceContentManager.AddContentType("debug", Debug);

        SurfaceContentManager.AddContentType("powergraph", DrawPowerGraph);

        SurfaceContentManager.AddContentType("ores", DrawOresStats);
        SurfaceContentManager.AddContentType("ingots", DrawIngotsStats);

        SurfaceContentManager.AddContentType("assemblers", DrawAssemblersList);
        SurfaceContentManager.AddContentType("refineries", DrawRefineriesList);
        SurfaceContentManager.AddContentType("containers", DrawCargoContainersList);
        SurfaceContentManager.AddContentType("reactors", DrawReactorList);
        SurfaceContentManager.AddContentType("inventories", DrawInventoriesList);

        SurfaceContentManager.AddContentType("inventory", DrawInventoryList);
        SurfaceContentManager.AddContentType("i-ores", DrawOresInventoryList);
        SurfaceContentManager.AddContentType("i-ingots", DrawIngotsInventoryList);
        SurfaceContentManager.AddContentType("i-components", DrawComponentsInventoryList);
        SurfaceContentManager.AddContentType("i-tools", DrawToolsInventoryList);
        SurfaceContentManager.AddContentType("i-ammos", DrawAmmosInventoryList);
        SurfaceContentManager.AddContentType("i-other", DrawOtherInventoryList);
    }

    #region Quotas
    public void SetupQuotas()
    {
        // ORES
        _Quotas.Add(new ItemsQuotas.ItemQuota("Iron", "Ore", "Iron Ore", "Fe", 100000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Nickel", "Ore", "Nickel Ore", "Ni", 50000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Cobalt", "Ore", "Cobalt Ore", "Co", 25000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Magnesium", "Ore", "Magnesium Ore", "Mg", 25000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Silicon", "Ore", "Silicon Ore", "Si", 50000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Silver", "Ore", "Silver Ore", "Ag", 15000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Gold", "Ore", "Gold Ore", "Au", 15000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Platinum", "Ore", "Platinum Ore", "Pt", 7500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Uranium", "Ore", "Uranium Ore", "Ur", 7500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Ice", "Ore", "", "", 100000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Stone", "Ore", "", "", -25000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Scrap", "Ore", "", "", -25000));
        // INGOTS
        _Quotas.Add(new ItemsQuotas.ItemQuota("Iron", "Ingot", "Iron Ingot", "Fe", 100000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Nickel", "Ingot", "Nickel Ingot", "Ni", 50000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Cobalt", "Ingot", "Cobalt Ingot", "Co", 25000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Magnesium", "Ingot", "Magnesium Ingot", "Mg", 25000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Silicon", "Ingot", "Silicon Ingot", "Si", 50000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Silver", "Ingot", "Silver Ingot", "Ag", 15000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Gold", "Ingot", "Gold Ingot", "Au", 15000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Platinum", "Ingot", "Platinum Ingot", "Pt", 7500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Uranium", "Ingot", "Uranium Ingot", "Ur", 7500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Stone", "Ingot", "Gravel", "", 50000));
        // COMPONENTS
        _Quotas.Add(new ItemsQuotas.ItemQuota("Construction", "Component", "", "", 50000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("MetalGrid", "Component", "Metal Grid", "", 15500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("InteriorPlate", "Component", "Interior Plate", "", 55000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("SteelPlate", "Component", "Steel Plate", "", 300000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Girder", "Component", "Steel Plate", "", 8500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("SmallTube", "Component", "Small Tube", "", 26000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("LargeTube", "Component", "Large Tube", "", 6000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Motor", "Component", "", "", 16000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Display", "Component", "", "", 500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("BulletproofGlass", "Component", "Bull. Glass", "", 12000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Computer", "Component", "", "", 6500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Reactor", "Component", "Reactor Comp.", "", 2800));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Thrust", "Component", "Thruster Comp.", "", 5600));
        _Quotas.Add(new ItemsQuotas.ItemQuota("GravityGenerator", "Component", "Gravity Comp.", "", 250));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Medical", "Component", "Medical Comp.", "", 120));
        _Quotas.Add(new ItemsQuotas.ItemQuota("RadioCommunication", "Component", "Radio Comp.", "", 250));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Detector", "Component", "Detector Comp.", "", 400));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Explosives", "Component", "", "", 500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("SolarCell", "Component", "Solar Cell", "", 3200));
        _Quotas.Add(new ItemsQuotas.ItemQuota("PowerCell", "Component", "Power Cell", "", 3200));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Superconductor", "Component", "", "", 3000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Canvas", "Component", "", "", 300));
        _Quotas.Add(new ItemsQuotas.ItemQuota("ZoneChip", "Component", "Zone Chip", "", 100));
        // WEAPONS
        _Quotas.Add(new ItemsQuotas.ItemQuota("SemiAutoPistolItem", "PhysicalGunObject", "S-10 Pistol", "S-10"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("ElitePistolItem", "PhysicalGunObject", "S-10E Pistol", "S-10E"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("FullAutoPistolItem", "PhysicalGunObject", "S-20A Pistol", "S-20A"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("AutomaticRifleItem", "PhysicalGunObject", "MR-20 Rifle", "MR-20"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("PreciseAutomaticRifleItem", "PhysicalGunObject", "MR-8P Rifle", "MR-8P"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("RapidFireAutomaticRifleItem", "PhysicalGunObject", "MR-50A Rifle", "MR-50A"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("UltimateAutomaticRifleItem", "PhysicalGunObject", "MR-30E Rifle", "MR-30E"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("BasicHandHeldLauncherItem", "PhysicalGunObject", "RO-1 Launcher", "RO-1"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("AdvancedHandHeldLauncherItem", "PhysicalGunObject", "PRO-1 Launcher", "PRO-1"));
        // AMMOS
        _Quotas.Add(new ItemsQuotas.ItemQuota("NATO_5p56x45mm", "AmmoMagazine", "5.56x45mm", "", 8000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("SemiAutoPistolMagazine", "AmmoMagazine", "S-10 Mag.", "", 500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("ElitePistolMagazine", "AmmoMagazine", "S-10E Mag.", "", 500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("FullAutoPistolMagazine", "AmmoMagazine", "S-20A Mag.", "", 500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("AutomaticRifleGun_Mag_20rd", "AmmoMagazine", "MR-20 Mag.", "", 1000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("PreciseAutomaticRifleGun_Mag_5rd", "AmmoMagazine", "MR-8P Mag.", "", 1000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("RapidFireAutomaticRifleGun_Mag_50rd", "AmmoMagazine", "MR-50A Mag.", "", 8000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("UltimateAutomaticRifleGun_Mag_30rd", "AmmoMagazine", "MR-30E Mag.", "", 1000));
        _Quotas.Add(new ItemsQuotas.ItemQuota("NATO_25x184mm", "AmmoMagazine", "25x184mm", "", 2500));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Missile200mm", "AmmoMagazine", "200mm Missile", "", 1600));
        // TOOLS
        _Quotas.Add(new ItemsQuotas.ItemQuota("WelderItem", "PhysicalGunObject", "Welder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Welder2Item", "PhysicalGunObject", "* Enh. Welder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Welder3Item", "PhysicalGunObject", "** Prof. Welder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Welder4Item", "PhysicalGunObject", "*** Elite Welder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinderItem", "PhysicalGunObject", "Angle Grinder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinder2Item", "PhysicalGunObject", "* Enh. Grinder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinder3Item", "PhysicalGunObject", "** Prof. Grinder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinder4Item", "PhysicalGunObject", "*** Elite Grinder"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrillItem", "PhysicalGunObject", "Hand Drill"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrill2Item", "PhysicalGunObject", "* Enh. Drill"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrill3Item", "PhysicalGunObject", "** Prof. Drill"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrill4Item", "PhysicalGunObject", "*** Elite Drill"));
        // OTHER (ConsumableItem, OxygenContainerObject, GasContainerObject, PhysicalObject, Datapad)
        _Quotas.Add(new ItemsQuotas.ItemQuota("Datapad", "Datapad"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Medkit", "ConsumableItem"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Package", "Package"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("Powerkit", "ConsumableItem"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("ClangCola", "ConsumableItem", "Clang Cola"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("CosmicCoffee", "ConsumableItem", "Cosmic Coffee"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("SpaceCredit", "PhysicalObject", "Space Credit"));
        _Quotas.Add(new ItemsQuotas.ItemQuota("OxygenBottle", "OxygenContainerObject", "Oxygen Bottle", "", 5));
        _Quotas.Add(new ItemsQuotas.ItemQuota("HydrogenBottle", "GasContainerObject", "Hydrogen Bottle", "", 20));
    }

    private class ItemsQuotas
    {
        private List<ItemQuota> _Quotas = new List<ItemQuota>();

        public void Add(string name, string type, string customName = "", string shortName = "", float quota = 0)
        {
            _Quotas.Add(new ItemQuota(name, type, customName, shortName, quota));
        }
        public void Add(ItemQuota itemQuota)
        {
            _Quotas.Add(itemQuota);
        }

        public ItemQuota GetByName(string name, string type) {
            return _Quotas.Find(
                x => x.Name == name
                && (x.Type == type || $"MyObjectBuilder_{x.Type}" == type)
            );
        }
        public List<ItemQuota> GetByType(string type) => _Quotas.Where(x => x.Type == type).ToList();

        public class ItemQuota
        {
            public string Name { get; private set; }
            public string Type { get; private set; }
            public string CustomName { get; private set; }
            public string ShortName { get; private set; }
            public float Quota { get; private set; }

            public ItemQuota(string name, string type, string customName = "", string shortName = "", float quota = 0)
            {
                Name = name;
                Type = type;
                CustomName = customName != "" ? customName : name;
                ShortName = shortName;
                Quota = quota;
            }
        }
    }
    #endregion

    #region Power
    public int PowerProducersCount => _PowerProducers.Count;
    public int ReactorsCount => _PowerProducers.Where(x => x is IMyReactor).ToList().Count;
    public int BatteriesCount => _PowerProducers.Where(x => x is IMyBatteryBlock).ToList().Count;
    public int SolarPanelsCount => _PowerProducers.Where(x => x is IMySolarPanel).ToList().Count;

    public static float CurrentPowerOutput
    {
        get {
            try
            {
                float Sum = 0;
                foreach (IMyPowerProducer Producer in _PowerProducers)
                {
                    if (Producer.IsWorking)
                    {
                        Sum += Producer.CurrentOutput;
                    }
                }
                return Sum;
            }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    public static float MaxPowerOutput
    {
        get {
            try
            {
                float Sum = 0;
                foreach (IMyPowerProducer Producer in _PowerProducers)
                {
                    if (Producer.IsWorking)
                    {
                        Sum += Producer.MaxOutput;
                    }
                }
                return Sum;
            }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    public static float CurrentStoredPower
    {
        get {
            try
            {
                float Sum = 0;
                foreach (IMyBatteryBlock Battery in _PowerProducers.Where(x => x is IMyBatteryBlock).ToList())
                {
                    if (Battery.IsWorking)
                    {
                        Sum += Battery.CurrentStoredPower;
                    }
                }
                return Sum;
            }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    public static float MaxStoredPower
    {
        get {
            try
            {
                float Sum = 0;
                foreach (IMyBatteryBlock Battery in _PowerProducers.Where(x => x is IMyBatteryBlock).ToList())
                {
                    if (Battery.IsWorking)
                    {
                        Sum += Battery.MaxStoredPower;
                    }
                }
                return Sum;
            }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    public static float AvgStoredPower
    {
        get {
            try { return CurrentStoredPower / _PowerProducers.Where(x => x is IMyBatteryBlock).ToList().Count; }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    public static float AvgPowerOutput
    {
        get {
            try { return CurrentPowerOutput / _PowerProducers.Count; }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    #endregion

    #region Inventory
    public int InventoriesCount => _Inventories.Count;

    public float CargoCurrentVolume
    {
        get {
            try
            {
                float Sum = 0;
                foreach (IMyCargoContainer Inventory in _Inventories)
                {
                    for (int i = 0; i < Inventory.InventoryCount; i++)
                    {
                        Sum += (float)Inventory.GetInventory(i).CurrentVolume;
                    }
                }
                return Sum;
            }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    public float CargoMaxVolume
    {   
        get {
            try
            {
                float Sum = 0;
                foreach (IMyCargoContainer Inventory in _Inventories)
                {
                    for (int i = 0; i < Inventory.InventoryCount; i++)
                    {
                        Sum += (float)Inventory.GetInventory(i).MaxVolume;
                    }
                }
                return Sum;
            }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }
    public float AvgCargoCurrentVolume
    {
        get {
            try { return CargoCurrentVolume / _Inventories.Count; }
            catch (Exception exception) { return 0; }
        }
        private set {}
    }

    private float GetCargoMaxVolume(IMyInventoryOwner Inventory)
    {
        try
        {
            float Sum = 0;
            for (int i = 0; i < Inventory.InventoryCount; i++)
            {
                Sum += (float)Inventory.GetInventory(i).MaxVolume;
            }
            return Sum;
        }
        catch (Exception exception) { return 0; }
    }
    private float GetCargoCurrentVolume(IMyInventoryOwner Inventory)
    {
        try
        {
            float Sum = 0;
            for (int i = 0; i < Inventory.InventoryCount; i++)
            {
                Sum += (float)Inventory.GetInventory(i).CurrentVolume;
            }
            return Sum;
        }
        catch (Exception exception) { return 0; }
    }
    private float GetCargoFillLevel(IMyInventoryOwner Inventory)
    {
        float Max = GetCargoMaxVolume(Inventory);
        if (Max != 0) return GetCargoCurrentVolume(Inventory) / Max;
        else return 0;
    }

    private float GetInventoryMaxVolume(IMyInventory Inventory)
    {
        try
        {
            return (float)Inventory.MaxVolume;
        }
        catch (Exception exception) { return 0; }
    }
    private float GetInventoryCurrentVolume(IMyInventory Inventory)
    {
        try
        {
            return (float)Inventory.CurrentVolume;
        }
        catch (Exception exception) { return 0; }
    }
    private float GetInventoryFillLevel(IMyInventory Inventory)
    {
        float Max = GetInventoryMaxVolume(Inventory);
        if (Max > 0) {
            float Current = GetInventoryCurrentVolume(Inventory);
            return Current / Max;
        }
        else return 0;
    }

    private Dictionary<MyItemType, float> GetInventoryItems(string[] TypeId = null, string SubtypeId = null)
    {
        Dictionary<MyItemType, float> Items = new Dictionary<MyItemType, float>();

        foreach (IMyTerminalBlock Block in _Inventories)
        {
            for (int i = 0; i < Block.InventoryCount; i++)
            {
                try
                {
                    List<MyInventoryItem> ItemsList = new List<MyInventoryItem>();

                    Block.GetInventory(i).GetItems(ItemsList);

                    foreach (MyInventoryItem Item in ItemsList)
                    {
                        if (TypeId != null && !TypeId.Contains(Item.Type.TypeId.ToString())) continue;
                        if (SubtypeId != null && Item.Type.SubtypeId.ToString() != SubtypeId) continue;

                        if (Item.Amount <= 0) continue;
                        
                        // if exist - change amount
                        if (Items.ContainsKey(Item.Type))
                        {
                            Items[Item.Type] += (float)Item.Amount;
                        }
                        // if not exist - add and set amount
                        else
                        {
                            Items.Add(Item.Type, (float)Item.Amount);
                        }
                        
                    }
                }
                catch(Exception exception) { }
            }
        }

        return Items;
    }
    #endregion

    #region ContentManager
    private readonly Dictionary<float, Color> Colors = new Dictionary<float, Color>() {
        { 0.0f, new Color(220, 30, 30) },
        { 0.1f, new Color(201, 49, 30) },
        { 0.2f, new Color(182, 68, 30) },
        { 0.3f, new Color(163, 87, 30) },
        { 0.4f, new Color(144, 106, 30) },
        { 0.5f, new Color(125, 125, 30) },
        { 0.6f, new Color(106, 114, 30) },
        { 0.7f, new Color(87, 163, 30) },
        { 0.8f, new Color(68, 182, 30) },
        { 0.9f, new Color(49, 201, 30) },
        { 1.0f, new Color(30, 220, 30) }
    };

    private void Debug(SurfaceContentManager.SurfaceManager Manager)
    {
        Manager.AddTextBuilder("--- Debug ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
        Manager.SaveLine();
    }

    private void DrawPowerGraph(SurfaceContentManager.SurfaceManager Manager)
    {
        _PowerConsumptionStory.Add(CurrentPowerOutput / MaxPowerOutput);
        if (_PowerConsumptionStory.Count > 30) _PowerConsumptionStory.RemoveAt(0);

        Manager.AddTextBuilder("--- Power Consumption ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
        Manager.AddBorderBuilder(new Vector2(0f, 0.15f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
        Manager.AddGraphBuilder(_PowerConsumptionStory, new Vector2(0f, 0.2f), new Vector2(1f, 1f));
    }

    private void DrawOresStats(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawStash(Manager, new string[] { "MyObjectBuilder_Ore" }, "Ore", "Ores");
    }
    private void DrawIngotsStats(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawStash(Manager, new string[] { "MyObjectBuilder_Ingot" }, "Ingot", "Ingot's");
    }
    private void DrawStash(SurfaceContentManager.SurfaceManager Manager, string[] TypeId, string Type, string Title)
    {
        Dictionary<MyItemType, float> Items = GetInventoryItems(TypeId: TypeId);
        List<ItemsQuotas.ItemQuota> Quotas = _Quotas.GetByType(Type);

        Manager.AddTextBuilder($"--- {Title} Stash ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
        Manager.SaveLine();

        Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1, (float)Math.Ceiling(Quotas.Count / 3f) * 0.2f + 0.05f));

        float Vertical = 0.025f;
        float Horizontal = 0f;

        foreach (ItemsQuotas.ItemQuota Quota in Quotas)
        {
            bool isReverse = Quota.Quota < 0;
            string Lable = (isReverse ? "!" : "") + (Quota.ShortName != "" ? Quota.ShortName : Quota.Name);

            foreach (string typeId in TypeId)
            {
                MyItemType ItemType = new MyItemType(typeId, Quota.Name);

                float Amount = Items.ContainsKey(ItemType) ? Items[ItemType] : 0;
                float TargetAmount = Math.Abs(Quota.Quota);

                float Percentage = Amount / TargetAmount;
                Percentage = isReverse ? 1f - Percentage : Percentage;
                Percentage = (float)Math.Round(Percentage, 1, MidpointRounding.AwayFromZero);
                Percentage = Math.Max(Math.Min(Percentage, 1), 0);

                Manager.AddTextBuilder(Lable, new Vector2(Horizontal, Vertical), new Vector2(Horizontal + 1f / 3f, Vertical + 0.2f), FontSize: 2f, color: Colors[Percentage]);

                Horizontal += 1f / 3f;
                if (Horizontal >= 1f)
                {
                    Horizontal = 0f;
                    Vertical += 0.2f;
                }
            }
        }
        Manager.SaveLine();
    }

    private void DrawAssemblersList(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawCargoList(Manager, _Inventories.Where(x => x is IMyAssembler).ToList(), "Assembler's List");
    }
    private void DrawRefineriesList(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawCargoList(Manager, _Inventories.Where(x => x is IMyRefinery).ToList(), "Refineries List");
    }
    private void DrawCargoContainersList(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawCargoList(Manager, _Inventories.Where(x => x is IMyCargoContainer).ToList(), "Container's List");
    }
    private void DrawReactorList(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawCargoList(Manager, _Inventories.Where(x => x is IMyReactor).ToList(), "Reactor's List");
    }
    private void DrawInventoriesList(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawCargoList(Manager, _Inventories, "All Inventories List");
    }
    private void DrawCargoList(SurfaceContentManager.SurfaceManager Manager, List<IMyTerminalBlock> Inventories, string Title)
    {
        Manager.AddTextBuilder($"--- {Title} ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
        Manager.SaveLine();

        int Index = 0;

        foreach (IMyTerminalBlock Inventory in Inventories)
        {
            for (int i = 0; i < Inventory.InventoryCount; i++)
            {
                float FillLevel = GetInventoryFillLevel(Inventory.GetInventory(i));

                Manager.AddBorderBuilder(new Vector2(0.1f, 0f), new Vector2(0.8f, 0.1f));
                
                Manager.AddTextBuilder($"{++Index} -", new Vector2(-0.1f, 0f), new Vector2(0.1f, 0.1f), Alignment: TextAlignment.RIGHT);
                Manager.AddSquareProgressBarBuilder(FillLevel, new Vector2(0.1f, 0f), new Vector2(0.8f, 0.1f), 270);
                Manager.AddTextBuilder(String.Format("{0:0.0}%", FillLevel * 100f), new Vector2(0.75f, 0f), new Vector2(1f, 0.1f), Alignment: TextAlignment.RIGHT);
                Manager.AddTextBuilder($"[{i}] - {Inventory.CustomName}", new Vector2(0.1f, 0f), new Vector2(0.8f, 0.095f), Alignment: TextAlignment.LEFT, ExtraPadding: true, color: Manager.BackgroundColor, FontSize: 0.7f);

                Manager.SaveLine();
            }
        }
    }

    private void DrawOresInventoryList(SurfaceContentManager.SurfaceManager Manager)
    {
        Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
            { "Ores", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ore" }) }
        };
        DrawInventoryItemsList(Manager, Groups);
    }
    private void DrawIngotsInventoryList(SurfaceContentManager.SurfaceManager Manager)
    {
        Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
            { "Ingot's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ingot" }) }
        };
        DrawInventoryItemsList(Manager, Groups);
    }
    private void DrawComponentsInventoryList(SurfaceContentManager.SurfaceManager Manager)
    {
        Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
            { "Component's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Component" }) }
        };
        DrawInventoryItemsList(Manager, Groups);
    }
    private void DrawToolsInventoryList(SurfaceContentManager.SurfaceManager Manager)
    {
        Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
            { "Tools", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_PhysicalGunObject" }) }
        };
        DrawInventoryItemsList(Manager, Groups);
    }
    private void DrawAmmosInventoryList(SurfaceContentManager.SurfaceManager Manager)
    {
        Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
            { "Ammos", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_AmmoMagazine" }) }
        };
        DrawInventoryItemsList(Manager, Groups);
    }
    private void DrawOtherInventoryList(SurfaceContentManager.SurfaceManager Manager)
    {
        Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
            { "Other", GetInventoryItems(TypeId: new string[] {
                "MyObjectBuilder_Datapad",
                "MyObjectBuilder_ConsumableItem",
                "MyObjectBuilder_PhysicalObject",
                "MyObjectBuilder_OxygenContainerObject",
                "MyObjectBuilder_GasContainerObject"
            })}
        };
        DrawInventoryItemsList(Manager, Groups);
    }
    private void DrawInventoryList(SurfaceContentManager.SurfaceManager Manager)
    {
        DrawInventoryItemsList(Manager);
    }
    private void DrawInventoryItemsList(SurfaceContentManager.SurfaceManager Manager, Dictionary<string, Dictionary<MyItemType, float>> Groups = null)
    {
        Dictionary<MyItemType, float> Items = GetInventoryItems();

        if (Groups == null) Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
            { "Ores", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ore" }) },
            { "Ingot's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ingot" }) },
            { "Component's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Component" }) },
            { "Ammos", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_AmmoMagazine" }) },
            { "Tools", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_PhysicalGunObject" }) },
            { "Other", GetInventoryItems(TypeId: new string[] {
                "MyObjectBuilder_Datapad",
                "MyObjectBuilder_ConsumableItem",
                "MyObjectBuilder_PhysicalObject",
                "MyObjectBuilder_OxygenContainerObject",
                "MyObjectBuilder_GasContainerObject"
            })}
        };

        foreach (string Key in Groups.Keys)
        {
            Manager.AddTextBuilder($"<<{Key} summary>>", new Vector2(0f, 0f), new Vector2(1f, 0.1f));
            Manager.SaveLine();

            foreach (MyItemType ItemType in Groups[Key].Keys)
            {
                ItemsQuotas.ItemQuota Quota = _Quotas.GetByName(ItemType.SubtypeId, ItemType.TypeId);

                float Amount = Groups[Key][ItemType];
                float TargetAmount = Quota != null ? Quota.Quota : 0;
                string Lable = Quota != null ? Quota.CustomName : ItemType.SubtypeId.ToString();
                string[] Suffix = { "", "" };
                if (Amount >= 1000f)
                {
                    Amount *= 0.001f;
                    Suffix[0] = "k";
                }
                if (TargetAmount >= 1000f)
                {
                    TargetAmount *= 0.001f;
                    Suffix[1] = "k";
                }
                if (TargetAmount < 0) TargetAmount = 0;
                
                string Value = "";
                if (ItemType.TypeId == "MyObjectBuilder_Component") Value = "";
                else if (ItemType.TypeId == "MyObjectBuilder_Ingot") Value = " (kg)";
                else if (ItemType.TypeId == "MyObjectBuilder_Ore") Value = " (kg)";

                string Total = TargetAmount > 0
                ? $"{Math.Round(Amount, 1)}{Suffix[0]} / {Math.Round(TargetAmount, 1)}{Suffix[1]}{Value}"
                : $"{Math.Round(Amount, 1)}{Suffix[0]}{Value}";

                Manager.AddTextBuilder(Lable, new Vector2(0f, 0f), new Vector2(0.75f, 0.05f), FontSize: 0.8f, Alignment: TextAlignment.LEFT);
                Manager.AddTextBuilder(Total, new Vector2(0.25f, 0f), new Vector2(1f, 0.05f), FontSize: 0.8f, Alignment: TextAlignment.RIGHT);
                Manager.SaveLine();
            }
        }
    }
    #endregion
}
#endregion

// Last update - 06.12.2022
#region SurfaceContentManagerClass
class SurfaceContentManager
{   
    private Program _Program;

    MyIni _ini = new MyIni();
    public const string IniSectionSurfaceGeneral = "Surface Manager - General",
                        IniKeyLCDTag = "LCD name tag";

    public string LCDTag { get; private set; } = "[LCD]";

    private List<SurfaceProvider> _Providers = new List<SurfaceProvider>();
    private static Dictionary<string, Action<SurfaceManager>> _ContentTypes = new Dictionary<string, Action<SurfaceManager>>();

    public SurfaceContentManager(Program Program)
    {
        _Program = Program;

        Update();
    }

    public void Update()
    {
        ParseIni();

        // remove provider's that don't contain's [LCDTag] or isn't valid
        _Providers = _Providers.Where(x => x.CustomName.Contains(LCDTag) && IsValid(x)).ToList();

        List<IMyTextSurfaceProvider> Providers = new List<IMyTextSurfaceProvider>();
        _Program.GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(
            Providers,
            x => (x as IMyTerminalBlock).IsSameConstructAs(_Program.Me)
            && (x as IMyTerminalBlock).CustomName.Contains(LCDTag)
            && x.SurfaceCount > 0
        );

        // add new provider's
        foreach (IMyTextSurfaceProvider Provider in Providers) if (!IsExist(Provider)) _Providers.Add(new SurfaceProvider(Provider));

        // update provider's content
        foreach (SurfaceProvider _Provider in _Providers) _Provider.Update();
    }

    private void ParseIni()
    {
        _ini.Clear();
        if (_ini.TryParse(_Program.Me.CustomData))
        {
            LCDTag = _ini.Get(IniSectionSurfaceGeneral, IniKeyLCDTag).ToString(LCDTag);
        }
        else if (!string.IsNullOrWhiteSpace(_Program.Me.CustomData))
        {
            _ini.EndContent = _Program.Me.CustomData;
        }

        _ini.Set(IniSectionSurfaceGeneral, IniKeyLCDTag, LCDTag);

        string Output = _ini.ToString();
        if (Output != _Program.Me.CustomData)
        {
            _Program.Me.CustomData = Output;
        }
    }

    #region Draw
    public void DrawContent(int PixelsToScroll = 0, bool IsScroll = false)
    {
        foreach (SurfaceProvider _Provider in _Providers)
        {
            _Provider.DrawContent(PixelsToScroll, IsScroll);
        }
    }
    #endregion

    #region Helpers
    public void AddContentType(string tag, Action<SurfaceManager> action)
    {
        try
        {
            _ContentTypes.Add(tag, action);
        }
        catch (Exception exception) { return; }
    }

    private bool IsExist(IMyTextSurfaceProvider Provider)
    {
        int Hash = Provider.GetHashCode();
        foreach (SurfaceProvider _Provider in _Providers)
        {
            if (Hash == _Provider.GetHashCode()) return true;
        }
        return false;
    }
    private bool IsValid(SurfaceProvider Provider)
    {
        return Provider != null && !Provider.Closed;
    }

    public int SurfacesCount()
    {
        int Count = 0;
        foreach (SurfaceProvider _Provider in _Providers)
        {
            Count += _Provider.SurfaceCount;
        }
        return Count;
    }
    #endregion

    #region SurfaceProviderClass
    public class SurfaceProvider
    {
        private MyIni _ini = new MyIni();
        public const string IniSectionLCD = "Screen",
                            IniKeyContentType = "Content type",
                            IniKeyBackgroundColor = "Background color",
                            IniKeyDefaultColor = "Default color";

        private IMyTextSurfaceProvider _Provider;

        private List<SurfaceManager> _Surfaces = new List<SurfaceManager>();
        private List<string[]> _Contents = new List<string[]>();

        public string CustomName => (_Provider as IMyTerminalBlock).CustomName;
        public int SurfaceCount => _Provider.SurfaceCount;
        public override int GetHashCode() => _Provider.GetHashCode();
        public bool Closed => (_Provider as IMyTerminalBlock).Closed;

        public SurfaceProvider(IMyTextSurfaceProvider Provider)
        {
            _Provider = Provider;
        }

        public void Update()
        {
            for (int i = 0; i < _Provider.SurfaceCount; i++)
            {
                ParseIni(i);
            }
        }

        private void ParseIni(int i = 0)
        {
            IMyTerminalBlock Block = _Provider as IMyTerminalBlock;
            SurfaceManager Manager = new SurfaceManager(_Provider.GetSurface(i));

            _ini.Clear();

            string backgroundColor = Manager.BackgroundColor.ToString();
            string defaultColor = Manager.DefaultColor.ToString();

            string[] Content = new string[] { "none" };

            if (_ini.TryParse(Block.CustomData))
            {
                Content = _ini.Get($"{IniSectionLCD} ({i})", IniKeyContentType).ToString("none").Split(',');
                backgroundColor = _ini.Get($"{IniSectionLCD} ({i})", IniKeyBackgroundColor).ToString(backgroundColor);
                defaultColor = _ini.Get($"{IniSectionLCD} ({i})", IniKeyDefaultColor).ToString(defaultColor);
            }
            else if (!string.IsNullOrWhiteSpace(Block.CustomData))
            {
                _ini.EndContent = Block.CustomData;
            }

            _ini.Set($"{IniSectionLCD} ({i})", IniKeyContentType, string.Join(",", Content));
            _ini.Set($"{IniSectionLCD} ({i})", IniKeyBackgroundColor, backgroundColor);
            _ini.Set($"{IniSectionLCD} ({i})", IniKeyDefaultColor, defaultColor);

            string Output = _ini.ToString();
            if (Output != Block.CustomData)
            {
                Block.CustomData = Output;
            }

            if (Content.Length == 1 && Content[0] == "none") return;

            if (!IsExist(Manager))
            {
                Manager.SetColors(TryParseColor(backgroundColor), TryParseColor(defaultColor));
                _Surfaces.Add(Manager);
                _Contents.Add(Content);
            }
            else
            {
                int index = IndexOf(Manager);
                if (index == -1) return;
                
                if (!IsEquals(Content, _Contents[index]))_Surfaces[index].Reset();
                _Surfaces[index].SetColors(TryParseColor(backgroundColor), TryParseColor(defaultColor));
                _Contents[index] = Content;
            }
        }

        #region Helpers
        private Color TryParseColor(string Str)
        {
            try
            {
                if (Str[0] != '{' || Str[Str.Length - 1] != '}') throw new Exception();

                string[] Split = Str.Substring(1, Str.Length - 2).Split(' ');
                if (Split.Length != 4) throw new Exception();

                int[] RGBA = new int[] { 0, 0, 0, 255 };
                for(int i = 0; i < Split.Length; i++)
                {
                    RGBA[i] = int.Parse(Split[i].Substring(2, Split[i].Length - 2));
                }

                return new Color(RGBA[0], RGBA[1], RGBA[2], RGBA[3]);
            }
            catch (Exception exception) { return Color.Transparent; }
        }
        private bool IsExist(SurfaceManager Manager)
        {
            int Hash = Manager.GetHashCode();

            foreach (SurfaceManager manager in _Surfaces)
            {
                if (manager.GetHashCode() == Hash) return true;
            }

            return false;
        }

        private int IndexOf(SurfaceManager Manager)
        {
            for (int i = 0; i < _Surfaces.Count; i++)
            {
                if (Manager.GetHashCode() == _Surfaces[i].GetHashCode()) return i;
            }
             
            return -1;
        }
        
        private bool IsEquals(string[] Array1, string[] Array2)
        {
            if (Array1.Length != Array2.Length) return false;

            for(int i = 0; i < Array1.Length; i++)
            {
                if (Array1[i] != Array2[i]) return false;
            }

            return true;
        }
        #endregion

        #region Draw
        public void DrawContent(int PixelsToScroll, bool IsScroll)
        {
            for (int i = 0; i < _Surfaces.Count && i < _Contents.Count; i++)
            {
                SurfaceManager Manager = _Surfaces[i];
                string[] Content = _Contents[i];

                try
                {
                    Manager.Clear();
                    foreach(string Type in Content)
                    {
                        _ContentTypes[Type](Manager);
                        Manager.SaveLine();
                    }
                    Manager.Render(IsScroll ? PixelsToScroll : 0);
                }
                catch (Exception exception)
                {
                    DrawBSOD(Manager, exception);
                }

            }
        }

        private void DrawBSOD(SurfaceManager Manager, Exception exception)
        {
            Manager.Clear();

            Manager.AddTextBuilder(":(", new Vector2(0f, 0f), new Vector2(1f, 0.25f), Alignment: TextAlignment.LEFT, FontSize: 6f);

            Manager.AddTextBuilder(exception.Message, new Vector2(0f, 0.25f), new Vector2(1f, 0.9f), Alignment: TextAlignment.LEFT, FontSize: 1.1f, Multiline: true);

            Manager.AddTextBuilder("Please type correct setting's to Custom Data", new Vector2(0f, 0.9f), new Vector2(1f, 1f), Alignment: TextAlignment.LEFT, FontSize: 0.8f);

            Manager.Render(0);
        }
        #endregion
    }
    #endregion

    #region SurfaceManagerClass
    public class SurfaceManager
    {
        private readonly IMyTextSurface _Surface;

        private readonly RectangleF _Viewport;
        private readonly Vector2 _Padding;
        private readonly float _Scale;

        private List<MySprite> _Sprites = new List<MySprite>();
        private List<List<MySprite>> _Lines = new List<List<MySprite>>();

        private int _ScrollDirection = -6;
        private float _ScrollValue = 0f;

        public Color BackgroundColor { get; private set; } = new Color(0, 88, 151);
        public Color DefaultColor { get; private set; } = new Color(179, 237, 255);

        public override int GetHashCode() => _Surface.GetHashCode(); 

        public SurfaceManager(IMyTextSurface Surface)
        {
            _Surface = Surface;
            _Viewport = new RectangleF((Surface.TextureSize - Surface.SurfaceSize) * 0.5f, Surface.SurfaceSize);

            Vector2 VScale = _Viewport.Size / 512f;
            _Scale = Math.Min(VScale.X, VScale.Y);

            _Padding = new Vector2(10f, 10f) * _Scale;

            _Viewport.Size -= _Padding * 4f;
            _Viewport.Position += _Padding * 2f;
        }

        #region Builders
        public void AddTextBuilder(
            string Text,
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            Color? color = null,
            TextAlignment Alignment = TextAlignment.CENTER,
            bool ExtraPadding = false,
            bool Multiline = false,
            float FontSize = 1f
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            FontSize *= _Scale;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding - (ExtraPadding ? _Padding.X : 0);
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;
            
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

            if (Alignment == TextAlignment.RIGHT) Position.X += ContentSize.X * 0.5f - (ExtraPadding ? _Padding.X : 0);
            if (Alignment == TextAlignment.LEFT) Position.X -= ContentSize.X * 0.5f - (ExtraPadding ? _Padding.X : 0);

            string _Text = "";
            if (Multiline) 
            {
                int j = 0;
                for (int i = 0; i < Text.Length; i++)
                {
                    Vector2 textSize = _Surface.MeasureStringInPixels(new StringBuilder(Text.Substring(j, i - j)), "Debug", FontSize);
                    if (textSize.X > ContentSize.X - _Padding.X * 2f)
                    {
                        _Text += "\n";
                        j = i;
                    }
                    _Text += Text[i];
                }
            }
            else
            {
                _Text = Text;

                Vector2 textSize = _Surface.MeasureStringInPixels(new StringBuilder(_Text), "Debug", FontSize);
                while (textSize.X >= ContentSize.X - _Padding.X * 2f)
                {
                    _Text = _Text.Remove(_Text.Length - 1);
                    textSize = _Surface.MeasureStringInPixels(new StringBuilder(_Text), "Debug", FontSize);
                }
            }
            

            Vector2 TextSize = _Surface.MeasureStringInPixels(new StringBuilder(_Text), "Debug", FontSize);
            Position = new Vector2(Position.X, Position.Y - TextSize.Y * 0.5f);

            _Sprites.Add(new MySprite(SpriteType.TEXT, _Text, Position, ContentSize - _Padding * 2f, color ?? DefaultColor, "Debug", Alignment, FontSize));
        }
        public void AddCircleProgressBarBuilder(
            float Percentage,
            float Size,
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            float Sector = 270,
            float Rotation = 0,
            int Cells = 1,
            Color? color = null,
            bool Reverse = false
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Percentage = Math.Max(Math.Min(Percentage, 1f), 0f);

            Color _Color = color ?? DefaultColor;
            Color _GhostColor = new Color(_Color, 0.1f);

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

            float CircleSize = Math.Min(ContentSize.X, ContentSize.Y) - 2f * Math.Min(_Padding.X, _Padding.Y);
            float Radius = CircleSize * 0.5f;
            
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

            Vector2 Offset = new Vector2(0f, 0f);
            float SeparatorWidth = 2f * (float)Math.PI * Radius / 180;

            // Unfilled
            for (int i = 0; i <= Sector; i++)
            {
                float Angle = Sector - i;
                if (Reverse) Angle = Sector - Angle;

                Offset = new Vector2(
                    -(float)Math.Cos(MathHelper.ToRadians(Angle + Rotation)) * Radius,
                    -(float)Math.Sin(MathHelper.ToRadians(Angle + Rotation)) * Radius
                );

                DrawLine((Position + Offset * (1 - Size)), Position + Offset, _GhostColor, SeparatorWidth);
            }

            // Filled
            for (int i = 0; i <= Sector * Percentage; i++)
            {            
                float Angle = Sector - i;
                if (Reverse) Angle = Sector - Angle;

                Offset = new Vector2(
                    -(float)Math.Cos(MathHelper.ToRadians(Angle + Rotation)) * Radius,
                    -(float)Math.Sin(MathHelper.ToRadians(Angle + Rotation)) * Radius
                );

                DrawLine((Position + Offset * (1 - Size)), Position + Offset, _Color, SeparatorWidth);
            }

            if (Cells <= 1) return;

            // Cells
            for (int i = 0; i < Cells; i++)
            {
                float Angle = Sector / Cells * i;
                if (!Reverse) Angle = Sector - Angle;
                
                Offset = new Vector2(
                    -(float)Math.Cos(MathHelper.ToRadians(Angle + Rotation)) * Radius,
                    -(float)Math.Sin(MathHelper.ToRadians(Angle + Rotation)) * Radius
                );

                DrawLine((Position + Offset * (1 - Size)), Position + Offset, BackgroundColor, SeparatorWidth);
            }
        }
        public void AddSquareProgressBarBuilder(
            float Percentage,
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            int Rotation = 0,
            int Cells = 1,
            Color? color = null
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Percentage = Math.Max(Math.Min(Percentage, 1f), 0f);
            
            Color _Color = color ?? DefaultColor;
            Color _GhostColor = new Color(_Color, 0.1f);

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

            Vector2 BarSize = ContentSize - _Padding * 2f;
            Vector2 ActiveSize, BarPosition;

            Vector2 StartPos, EndPos, SeparatorSize;

            switch (Rotation)
            {
                case 90:
                    ActiveSize = BarSize * new Vector2(Percentage, 1f);
                    BarPosition = new Vector2(Position.X + (ContentSize.X - ActiveSize.X) * 0.5f - _Padding.X, Position.Y);
                    StartPos = new Vector2(Position.X - ContentSize.X * 0.5f + _Padding.Y * 0.5f, Position.Y);
                    EndPos = new Vector2(Position.X + ContentSize.X * 0.5f - _Padding.Y * 0.5f, Position.Y);
                    SeparatorSize = new Vector2(Math.Min(_Padding.X, _Padding.Y) * 0.5f, BarSize.Y);
                    break;

                case 180:
                    ActiveSize = BarSize * new Vector2(1f, Percentage);
                    BarPosition = new Vector2(Position.X, Position.Y + (ActiveSize.Y - ContentSize.Y) * 0.5f + _Padding.Y);
                    StartPos = new Vector2(Position.X, Position.Y - ContentSize.Y * 0.5f + _Padding.Y * 0.5f);
                    EndPos = new Vector2(Position.X, Position.Y + ContentSize.Y * 0.5f - _Padding.Y * 0.5f);
                    SeparatorSize = new Vector2(BarSize.X, Math.Min(_Padding.X, _Padding.Y) * 0.5f);
                    break;

                case 270:
                    ActiveSize = BarSize * new Vector2(Percentage, 1f);
                    BarPosition = new Vector2(Position.X + (ActiveSize.X - ContentSize.X) * 0.5f + _Padding.X, Position.Y);
                    StartPos = new Vector2(Position.X - ContentSize.X * 0.5f + _Padding.Y * 0.5f, Position.Y);
                    EndPos = new Vector2(Position.X + ContentSize.X * 0.5f - _Padding.Y * 0.5f, Position.Y);
                    SeparatorSize = new Vector2(Math.Min(_Padding.X, _Padding.Y) * 0.5f, BarSize.Y);
                    break;

                default:
                    ActiveSize = BarSize * new Vector2(1f, Percentage);
                    BarPosition = new Vector2(Position.X, Position.Y + (ContentSize.Y - ActiveSize.Y) * 0.5f - _Padding.Y);
                    StartPos = new Vector2(Position.X, Position.Y - ContentSize.Y * 0.5f + _Padding.Y * 0.5f);
                    EndPos = new Vector2(Position.X, Position.Y + ContentSize.Y * 0.5f - _Padding.Y * 0.5f);
                    SeparatorSize = new Vector2(BarSize.X, Math.Min(_Padding.X, _Padding.Y) * 0.5f);
                    break;
            }
            
            // Unfilled
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BarSize, _GhostColor));
            // Body
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", BarPosition, ActiveSize, _Color));

            if (Cells <= 1) return;

            Vector2 Offset = (EndPos - StartPos) / Cells;
            for (int i = 1; i < Cells; i++)
            {
                Vector2 SeparatorPosition = StartPos + Offset * i;
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", SeparatorPosition, SeparatorSize, BackgroundColor));
            }
        }
        public void AddGraphBuilder(
            List<float> Values,
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            Color? color = null,
            bool DisplayPercentage = true,
            bool Filled = false
        ) {
            if (Values.Count <= 0) return;
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            float FontSize = 1.25f * _Scale;

            Color _Color = color ?? DefaultColor;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 GraphBoxSize = ContentSize - _Padding;

            Vector2 TextSize = new Vector2(0f, 0f);
            if (DisplayPercentage)
            {
                TextSize = _Surface.MeasureStringInPixels(new StringBuilder("000.0%"), "Debug", FontSize);
                TextSize.X += _Padding.X;
            }
            GraphBoxSize -= TextSize;

            float Offset = GraphBoxSize.X / (Values.Count - 1);
            float Value = Math.Max(Math.Min(Values[0], 1), 0);
            Vector2 ZeroPoint = new Vector2(Position.X - (GraphBoxSize.X + TextSize.X) * 0.5f, Position.Y + GraphBoxSize.Y * 0.5f);
            Vector2 StartPoint = new Vector2(ZeroPoint.X, ZeroPoint.Y - GraphBoxSize.Y * Value);

            float Size = Math.Max(_Padding.X, _Padding.Y) * 0.5f;

            // Graph
            for (int i = 1; i < Values.Count; i++)
            {
                Value = Math.Max(Math.Min(Values[i], 1), 0);
                Vector2 EndPoint = new Vector2(ZeroPoint.X + i * Offset, ZeroPoint.Y - GraphBoxSize.Y * Value);

                DrawLine(StartPoint, EndPoint, _Color, Size);
                if (i == 1) _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", StartPoint, new Vector2(Size, Size), _Color));
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", EndPoint, new Vector2(Size, Size), _Color));

                // Fill
                if (Filled)
                {
                    Vector2 Difference = EndPoint - StartPoint;

                    float X = StartPoint.X;
                    while (X <= EndPoint.X + _Scale)
                    {
                        float Y = (X - StartPoint.X) / Difference.X * Difference.Y;
                        DrawLine(new Vector2(X, ZeroPoint.Y), new Vector2(X, StartPoint.Y + Y), _Color, Size * 0.5f);
                        X++;
                    }
                }

                StartPoint = EndPoint;
            }
            // Fill smooth bottom
            if (Filled)
            {
                Vector2 Start = ZeroPoint;
                Vector2 End = new Vector2(ZeroPoint.X + GraphBoxSize.X, ZeroPoint.Y);

                DrawLine(Start, End, _Color, Size);
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", Start, new Vector2(Size, Size), _Color));
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", End, new Vector2(Size, Size), _Color));
            }
            // Running Percentage 
            if (DisplayPercentage) _Sprites.Add(new MySprite(
                SpriteType.TEXT,
                String.Format("{0:0.0}%", Values[Values.Count - 1] * 100f),
                new Vector2(StartPoint.X + TextSize.X - _Padding.X * 0.25f, StartPoint.Y - TextSize.Y * 0.5f),
                null,
                _Color,
                "Debug",
                TextAlignment.RIGHT,
                FontSize
            ));
        }
        public void AddBorderBuilder(
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            Vector2? Gaps = null,
            Color? color = null
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;
            
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

            // Border
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, ContentSize, color ?? DefaultColor));

            if (Gaps != null)
            {
                // Vertical Gaps
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, new Vector2(ContentSize.X, Gaps.Value.Y * ContentSize.Y), BackgroundColor));
                // Horizontal Gaps
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, new Vector2(Gaps.Value.X * ContentSize.X, ContentSize.Y), BackgroundColor));
            }

            // Inner
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, ContentSize - _Padding * 0.75f, BackgroundColor));
        }
        public void AddSpriteBuilder(
            string Type,
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            Color? color = null,
            TextAlignment Alignment = TextAlignment.CENTER,
            bool KeepAspectRatio = true
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;
                    
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, Color.Transparent));

            float Size = Math.Min(ContentSize.X, ContentSize.Y);

            if (Alignment == TextAlignment.RIGHT) Position.X += (ContentSize.X - Size) * 0.5f;
            if (Alignment == TextAlignment.LEFT) Position.X -= (ContentSize.X - Size) * 0.5f;

            if (KeepAspectRatio) ContentSize = new Vector2(Size, Size);

            _Sprites.Add(new MySprite(SpriteType.TEXTURE, Type, Position, ContentSize - _Padding * 2f, color ?? DefaultColor));
        }
        #endregion

        #region Changers
        public void SetColors(Color backgroundColor, Color defaultColor)
        {
            BackgroundColor = backgroundColor;
            DefaultColor = defaultColor;
        }
        public void Reset()
        {
            _ScrollDirection = 1;
            _ScrollValue = 0f;
        }
        public void SaveLine() 
        {
            if (_Sprites.Count <= 0) return;

            _Lines.Add(new List<MySprite>(_Sprites));
            _Sprites.Clear();
        }
        public void Clear()
        {
            _Lines.Clear();
            _Sprites.Clear();
        }
        private void SetupDrawSurface()
        {
            _Surface.ContentType = ContentType.SCRIPT;
            _Surface.Script = "";
            _Surface.ScriptBackgroundColor = BackgroundColor;
        }
        #endregion

        #region Render
        public void Render(int PixelsToScroll = 0)
        {
            if (_Sprites.Count > 0) SaveLine();

            SetupDrawSurface();

            if (PixelsToScroll > 0) RunScroll(PixelsToScroll);

            MySpriteDrawFrame Frame = _Surface.DrawFrame();

            float Offset = 0f;
            for (int i = 0; i < _Lines.Count; i++)
            {
                DrawSprites(ref Frame, _Lines[i], (Offset - _ScrollValue));
                Offset += GetLineHeight(_Lines[i]);
            }
            Frame.Dispose();
        }
        private void DrawSprites(ref MySpriteDrawFrame Frame, List<MySprite> Sprites, float Offset)
        {
            foreach (MySprite Sprite in Sprites)
            {
                MySprite sprite = Sprite;
                sprite.Position += new Vector2(0f, Offset);
                Frame.Add(sprite);
            }
        }
        private void RunScroll(int Offset)
        {
            float Difference = GetFrameHeight() - _Viewport.Size.Y + _Viewport.Position.Y - _Padding.Y * 2f;

            if (Difference > 0)
            {
                float LowerLimit = 0f;
                float UpperLimit = Difference;

                _ScrollValue = Math.Max(Math.Min(
                    _ScrollValue + Offset * _Scale * (_ScrollDirection > 0 ? 1 : (_ScrollDirection < 0 ? -1 : 0)),
                UpperLimit), LowerLimit);

                if (_ScrollValue <= LowerLimit && _ScrollDirection <= 0) _ScrollDirection++;
                else if (_ScrollValue >= UpperLimit && _ScrollDirection >= 0) _ScrollDirection--;

                if (_ScrollDirection < 0 && !(_ScrollValue <= LowerLimit)) _ScrollDirection = -6;
                if (_ScrollDirection > 0 && !(_ScrollValue >= UpperLimit)) _ScrollDirection = 6;
            }
        }
        #endregion

        #region Helpers
        private void DrawLine(Vector2 Point1, Vector2 Point2, Color color, float Width)
        {
            Vector2 Position = 0.5f * (Point1 + Point2);
            Vector2 Difference = (Point1 - Point2);

            float Length = Difference.Length();
            if (Length != 0)
                Difference /= Length;

            Vector2 Size = new Vector2(Length, Width);

            float Angle = (float)Math.Acos(Vector2.Dot(Difference, Vector2.UnitX));
            Angle *= Math.Sign(Vector2.Dot(Difference, Vector2.UnitY));

            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, Size, color, null, TextAlignment.CENTER, Angle));
        }
        private float GetLineHeight(List<MySprite> Line)
        {
            float LineHeight = 0;
            foreach (MySprite Sprite in Line)
            {
                if (Sprite.Size != null && Sprite.Position != null)
                {
                    LineHeight = Math.Max(Sprite.Position.Value.Y + Sprite.Size.Value.Y * 0.5f, LineHeight);
                }
            }
            return LineHeight - _Viewport.Position.Y;
        }
        private float GetFrameHeight()
        {
            float TotalLinesHeight = 0f;
            foreach (List<MySprite> Line in _Lines)
            {
                TotalLinesHeight += GetLineHeight(Line);
            }
            return TotalLinesHeight;
        }
        #endregion
    }
    #endregion
}
#endregion
