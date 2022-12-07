// ------------------------------------------------------------------------------------------------------ \\
// ========= Vakot Ind. Advanced Piston Elevator Manager Class ========= \\
// ------------------------------------------------------------------------------------------------------ \\

// ------------------ DESCRIPTION ------------------ \\

/*
 *  Piston group will include all parts of your elevator
 *  - Piston's
 *  - Door's (optionally)
 *  - Light's (optionally)
 *  - Sound Block's (optionally)
 *  Script will automatically manage their state to prevent most of posible issues
 *  
 *  Installation:
 *  Build a piston elevator
 *  Place Programmable Block and configurate for yourself
 *  Use!
 */

// ---------------- ARGUMENTS LIST ----------------- \\

/*
 *  Up - Get 1 floor up
 *  Down - Get 1 floor down
 *  [number] - Get to a specific floor
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
ElevatorManager _ElevatorManager;

// Variables
int Counter = 0;

public const string Version = "1.0",
                    IniSectionGeneral = "General";

// Program
void Status()
{
    string Status = $"Elevator is {_ElevatorManager.Status} {RunStatus[Counter % RunStatus.Count]}";
    Status += $"\nNext update in: {10 - Counter / 6}" + "s\n";

    Status += $"\n------ Block's Info ------\n";
    Status += $"Piston's: {_ElevatorManager.PistonsCount}\n";
    Status += $"Velocity: {_ElevatorManager.CurrentVelocity}\n";
    Status += $"Current Position: {_ElevatorManager.CurrentPosition()}\n";
    Status += $"Target Position: {_ElevatorManager.TargetPosition()}\n";

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

    _ElevatorManager.Update();
}

Program() 
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    
    _ElevatorManager = new ElevatorManager(this);
}

void Main(string argument) 
{
    if (++Counter % 60 == 0) Update();

    Status();

    _ElevatorManager.Run(argument);
}

// Last update - 07.12.2022
#region ElevatorManagerClass
class ElevatorManager
{
    private Program _Program;

    MyIni _ini = new MyIni();
    public const string IniSectionElevatorGeneral = "Elevator Manager - General",
                        IniKeyGroupName = "Elevator group name",
                        IniKeyConnectionPoints = "Piston's connection point's",
                        IniSectionElevatorMovement = "Elevator Manager - Movement",
                        IniKeyAcceleration = "Acceleration (m/s^2)",
                        IniKeyTargetVelocity = "Target velocity (m/s)",
                        IniSectionElevatorLight = "Elevator Manager - Light",
                        IniKeyIdleColor = "Idle color",
                        IniKeyActiveColor = "Active color",
                        IniSectionElevatorFloors = "Elevator Manager - Floors",
                        IniKeyFloors = "Floors list";

    // Name of ElevatorGroup
    public string ElevatorGroupName { get; private set; } = "Elevator";
    
    // Elevator acceleration m/s^2 (every second increase velocity by Acceleration = [value]f)
    private float _Acceleration = 1f;
    // Elevator target speed m/s (max elevator up/down velocity)
    private float _TargetVelocity = 5f;

    // Count of piston connection points to elevator
    private int _PistonConnectionPoints = 1;
    
    // Default light color (when the elevator is idle)
    private Color _IdleColor = new Color(255, 255, 255);
    // Active light color (when the elevator is moving)
    private Color _ActiveColor = new Color(255, 160, 0);

    // Elevator floors list (to set the floor - move all your pistons at the same time to height
    // where you want the floor to be and write [Piston Current Position] value to the list)
    // Be careful and dont use urecheable values, pistons can't move higher or lower than they can
    // Example: { 0f, 2.5f, 5f, 7.5f, 10f }
    private List<float> _Floors = new List<float>() { 0f, 2.5f, 5f, 7.5f, 10f };

    private List<IMyPistonBase> _Pistons = new List<IMyPistonBase>();
    private List<IMyDoor> _Doors = new List<IMyDoor>();
    private List<IMyLightingBlock> _Lights = new List<IMyLightingBlock>();
    private List<IMySoundBlock> _SoundBlocks = new List<IMySoundBlock>();

    private int _Count;
    public int CurrentFloor { get; private set; } = 0;
    public float CurrentVelocity { get; private set; } = 0f;
    private bool IsBreaking = false;

    private const string Active = "Active",
                         Idle = "Idle";

    public string Status { get; private set; } = Idle;


    public ElevatorManager(Program Program)
    {
        _Program = Program;

        Update();
    }

    public void Update()
    {
        ParseIni();

        _Pistons.Clear();
        _Doors.Clear();
        _Lights.Clear();
        _SoundBlocks.Clear();

        IMyBlockGroup ElevatorGroup = _Program.GridTerminalSystem.GetBlockGroupWithName(ElevatorGroupName);

        if (ElevatorGroup == null) return;

        ElevatorGroup.GetBlocksOfType<IMyPistonBase>(_Pistons);
        ElevatorGroup.GetBlocksOfType<IMyDoor>(_Doors);
        ElevatorGroup.GetBlocksOfType<IMyLightingBlock>(_Lights);
        ElevatorGroup.GetBlocksOfType<IMySoundBlock>(_SoundBlocks);

        _Count = _Pistons.Count / Math.Max(_PistonConnectionPoints, 1);
    }

    private void ParseIni()
    {
        _ini.Clear();

        string IdleColor = _IdleColor.ToString();
        string ActiveColor = _ActiveColor.ToString();
        string Floors = string.Join(",", _Floors);

        if (_ini.TryParse(_Program.Me.CustomData))
        {
            ElevatorGroupName = _ini.Get(IniSectionElevatorGeneral, IniKeyGroupName).ToString(ElevatorGroupName);
            _PistonConnectionPoints = _ini.Get(IniSectionElevatorGeneral, IniKeyConnectionPoints).ToInt32(_PistonConnectionPoints);

            _Acceleration = _ini.Get(IniSectionElevatorMovement, IniKeyAcceleration).ToSingle(_Acceleration);
            _TargetVelocity = _ini.Get(IniSectionElevatorMovement, IniKeyTargetVelocity).ToSingle(_TargetVelocity);

            IdleColor = _ini.Get(IniSectionElevatorLight, IniKeyIdleColor).ToString(IdleColor);
            ActiveColor = _ini.Get(IniSectionElevatorLight, IniKeyActiveColor).ToString(ActiveColor);

            Floors = _ini.Get(IniSectionElevatorFloors, IniKeyFloors).ToString(Floors);
        }
        else if (!string.IsNullOrWhiteSpace(_Program.Me.CustomData))
        {
            _ini.EndContent = _Program.Me.CustomData;
        }

        _ini.Set(IniSectionElevatorGeneral, IniKeyGroupName, ElevatorGroupName);
        _ini.Set(IniSectionElevatorGeneral, IniKeyConnectionPoints, _PistonConnectionPoints);

        _ini.Set(IniSectionElevatorMovement, IniKeyAcceleration, _Acceleration);
        _ini.Set(IniSectionElevatorMovement, IniKeyTargetVelocity, _TargetVelocity);

        _ini.Set(IniSectionElevatorLight, IniKeyIdleColor, IdleColor);
        _ini.Set(IniSectionElevatorLight, IniKeyActiveColor, ActiveColor);

        _ini.Set(IniSectionElevatorFloors, IniKeyFloors, Floors);

        string Output = _ini.ToString();
        if (Output != _Program.Me.CustomData)
        {
            _Program.Me.CustomData = Output;
        }

        _IdleColor = TryParseColor(IdleColor);
        _ActiveColor = TryParseColor(ActiveColor);
        _Floors = TryParseList(Floors);
    }

    #region Control
    private void RunArguments(string argument)
    {
        if (!IsIdle) return;

        if (argument.ToLower() == "up")
        {
            Status = Active;
            SetFloor(CurrentFloor + 1);
        }
        else if (argument.ToLower() == "down")
        {
            Status = Active;
            SetFloor(CurrentFloor - 1);
        }
        else if (argument != "")
        {
            int number;
            Int32.TryParse(argument, out number);

            if (number >= 0 || number < _Floors.Count)
            {
                Status = Active;
                SetFloor(number);
            }
        }
    }

    public void Run(string argument)
    {
        if (_Pistons.Count <= 0
        || _Count <= 0
        || _PistonConnectionPoints <= 0
        || _Acceleration <= 0
        || _TargetVelocity <= 0) return;

        if (argument != "") RunArguments(argument);

        if (Status != Active || !Prepare()) return;

        if (Math.Abs(CurrentPosition() - TargetPosition()) > 0.01f)
        {
            Move();
        }
        else
        {
            Stop();
        }
    }

    private void Move()
    {
        CurrentVelocity = GetVelocity();

        int Direction = CurrentPosition() > TargetPosition() ? -1 : 1;

        foreach (IMyPistonBase Piston in _Pistons)
        {
            Piston.Velocity = CurrentVelocity / _Count * Direction;
        }
    }

    private void Stop()
    {
        Status = Idle;

        CurrentVelocity = 0;
        IsBreaking = false;

        foreach (IMyPistonBase Piston in _Pistons)
        {
            Piston.Velocity = 0;
        }

        foreach (IMyDoor Door in _Doors)
        {
            Door.ApplyAction("OnOff_On");
            Door.OpenDoor();
        }

        foreach (IMyLightingBlock Light in _Lights)
        {
            Light.Color = _IdleColor;
            if (Light.CustomData.ToLower().Contains("off")) Light.ApplyAction("OnOff_Off");
        }

        foreach (IMySoundBlock SoundBlock in _SoundBlocks)
        {
            SoundBlock.Volume = 0;
            SoundBlock.Stop();
        }
    }
    #endregion

    #region Helpers
    public int PistonsCount => _Pistons.Count;
    private bool IsIdle => Status == Idle;

    public float CurrentPosition()
    {
        float Sum = 0;
        foreach (IMyPistonBase Piston in _Pistons)
        {
            Sum += Piston.CurrentPosition;
        }
        return Sum;
    }
    public float TargetPosition()
    {
        return _Floors[CurrentFloor] * _Count;
    }

    private float GetVelocity()
    {
        float RemainingDistance = Math.Abs(TargetPosition() - CurrentPosition());
        float StoppingDistance = (float)Math.Pow(CurrentVelocity, 2) / (_Acceleration * 2);

        if (RemainingDistance <= StoppingDistance * 1.2f) IsBreaking = true;

        CurrentVelocity = Math.Abs(CurrentVelocity + _Acceleration / 6 * (IsBreaking ? -1 : 1));
        
        return Math.Max(Math.Min(CurrentVelocity, _TargetVelocity), 0.2f);
    }

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

    private List<float> TryParseList(string Str)
    {
        try
        {
            string[] Split = Str.Split(',');

            List<float> Floors = new List<float>();

            foreach (string str in Split) Floors.Add(Convert.ToSingle(str));

            if (Floors.Count <= 0) throw new Exception();
            return Floors;
        }
        catch (Exception exception) { return _Floors; }
    }
    #endregion

    #region Changers
    private void SetFloor(int value) => CurrentFloor = Math.Max(Math.Min(value, _Floors.Count - 1), 0);
    
    private bool Prepare()
    {
        bool isReady = true;
        foreach (IMyPistonBase Piston in _Pistons)
        {
            Piston.MaxLimit = TargetPosition() / _Count;
            Piston.MinLimit = TargetPosition() / _Count;
        }

        foreach (IMyDoor Door in _Doors)
        {
            Door.CloseDoor();
            if (Door.OpenRatio == 0)
            {
                Door.ApplyAction("OnOff_Off");
            }
            else
            {
                isReady = false;
                Door.ApplyAction("OnOff_On");
            }
        }

        foreach (IMyLightingBlock Light in _Lights)
        {
            Light.ApplyAction("OnOff_On");
            Light.Color = _ActiveColor;
        }

        foreach (IMySoundBlock SoundBlock in _SoundBlocks)
        {
            SoundBlock.ApplyAction("OnOff_On");
            if (SoundBlock.Volume == 0 && SoundBlock.IsSoundSelected)
            {
                SoundBlock.LoopPeriod = 1800;
                SoundBlock.Volume = 50;
                SoundBlock.Play();
            }
        }
        return isReady;
    }
    #endregion
}
#endregion