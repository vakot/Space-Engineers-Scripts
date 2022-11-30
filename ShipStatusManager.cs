// ------------------------------------------------------------------------------------------------------- \\
// ========== Vakot Ind. Advanced Ship Status Manager Class =========== \\
// ------------------------------------------------------------------------------------------------------- \\

// ------------------ DESCRIPTION ------------------ \\

/* 
 * Simple class that provides posibility to collect information about ship status.
 * (power consumption, capacity, inventory fill level, etc.)
 * And display them to LCD panel's
 */

// ---------------- ARGUMENTS LIST ----------------- \\

/* 
 * Current version does not take any argument's
 */

// ----------------- CONFIGURATION ----------------- \\

/* Change it only before first run of program, rest of time use PB Custom Data */

public string LCDTag { get; private set; } = "[LCD]";

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
ShipStatusManager _ShipStatusManager;

static SurfaceContentManager _SurfaceContentManager;

// Variables
int Counter = 0;

public const string Version = "1.2",
                    IniSectionGeneral = "General",
                    IniKeyLCDTag = "LCD name tag",

                    IniSectionLCD = "Screen Settings",
                    IniKeyContentType = "Surface";
MyIni _ini = new MyIni();

// Program
void Status()
{
    string Status = $"Ship Status {RunStatus[Counter % RunStatus.Count]}";
    Status += $"\nNext update in: {10 - Counter / 6}" + "s\n";

    Status += $"\n------ Block's Info ------\n";
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

    ParseIni();

    _SurfaceContentManager.Update();

    _ShipStatusManager.Update();
}

void ParseIni()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        LCDTag = _ini.Get(IniSectionGeneral, IniKeyLCDTag).ToString(LCDTag);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(IniSectionGeneral, IniKeyLCDTag, LCDTag);

    string Output = _ini.ToString();
    if (Output != Me.CustomData)
    {
        Me.CustomData = Output;
    }
}

Program() 
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    ParseIni();
    
    _SurfaceContentManager = new SurfaceContentManager(this);

    _ShipStatusManager = new ShipStatusManager(this);

    _ShipStatusManager.SetupSurfaces(_SurfaceContentManager);
}

void Main(String argument) 
{
    if (++Counter % 60 == 0) Update();

    Status();

    _SurfaceContentManager.DrawContent(60, Counter % 6 == 0);
}

// Last update - 29.11.2022
#region ShipStatusManagerClass
class ShipStatusManager
{
    private Program _Program;

    private static List<IMyTerminalBlock> _PowerProducers = new List<IMyTerminalBlock>();
    private static List<IMyTerminalBlock> _Inventories = new List<IMyTerminalBlock>();

    private static List<float> _PowerConsumptionStory = new List<float>();

    public Dictionary<string, float> TargetOresCount = new Dictionary<string, float>() {
        { "Iron", 100000f },
        { "Nickel", 25000f },
        { "Cobalt", 10000f },
        { "Magnesium", 10000f },
        { "Silicon", 10000f },
        { "Uranium", 5000f },
        { "Platinum", 5000f },
        { "Silver", 10000f },
        { "Gold", 10000f },
        { "Stone", 10000f },
        { "Ice", 10000f },
        { "Scrap", 10000f }
    };

    public Dictionary<string, float> TargetIngotsCount = new Dictionary<string, float>() {
        { "Iron", 10000f },
        { "Nickel", 2500f },
        { "Cobalt", 1000f },
        { "Magnesium", 1000f },
        { "Silicon", 1000f },
        { "Uranium", 500f },
        { "Platinum", 500f },
        { "Silver", 1000f },
        { "Gold", 1000f },
        { "Gravel", 1000f },
    };

    public ShipStatusManager(Program Program)
    {
        _Program = Program;

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
        SurfaceContentManager.AddContentType("powergraph", DrawPowerGraph);
        SurfaceContentManager.AddContentType("ores", DrawOresStats);
        SurfaceContentManager.AddContentType("ingots", DrawIngotsStats);
        SurfaceContentManager.AddContentType("cargolist", DrawCargoList);
    }

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

    public float ItemCount(string typeId, string subTypeId)
    {
        float Amount = 0f;
        foreach(IMyTerminalBlock Inventory in _Inventories)
        {
            try
            {
                for (int i = 0; i < Inventory.InventoryCount; i++)
                {
                    Amount += Inventory.GetInventory(i).GetItemAmount(new MyItemType(typeId, subTypeId)).ToIntSafe();
                }
            }
            catch(Exception exception) { }
        }
        return Amount;
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
        Manager.AddTextBuilder("--- Ores Stash ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
        Manager.SaveLine();

        Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1, 0.65f));

        Manager.AddTextBuilder("Fe", new Vector2(0f, 0.025f), new Vector2(0.333f, 0.225f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Iron") / TargetOresCount["Iron"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Ni", new Vector2(0.333f, 0.025f), new Vector2(0.666f, 0.225f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Nickel") / TargetOresCount["Nickel"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Co", new Vector2(0.666f, 0.025f), new Vector2(1f, 0.225f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Cobalt") / TargetOresCount["Cobalt"],
            1, MidpointRounding.AwayFromZero), 1)]);

        Manager.AddTextBuilder("Mg", new Vector2(0f, 0.225f), new Vector2(0.333f, 0.425f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Magnesium") / TargetOresCount["Magnesium"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Si", new Vector2(0.333f, 0.225f), new Vector2(0.666f, 0.425f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Silicon") / TargetOresCount["Silicon"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Ur", new Vector2(0.666f, 0.225f), new Vector2(1f, 0.425f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Uranium") / TargetOresCount["Uranium"],
            1, MidpointRounding.AwayFromZero), 1)]);

        Manager.AddTextBuilder("Pt", new Vector2(0f, 0.425f), new Vector2(0.333f, 0.625f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Platinum") / TargetOresCount["Platinum"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Ag", new Vector2(0.333f, 0.425f), new Vector2(0.666f, 0.625f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Silver") / TargetOresCount["Silver"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Au", new Vector2(0.666f, 0.425f), new Vector2(1f, 0.625f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Gold") / TargetOresCount["Gold"],
            1, MidpointRounding.AwayFromZero), 1)]);


        Manager.SaveLine();


        Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1f, 0.2f), new Vector2(0.5f, 0f));
        Manager.AddTextBuilder("Ice", new Vector2(0f, 0f), new Vector2(0.33f, 0.2f), FontSize: 1.5f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Ice") / TargetOresCount["Ice"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("!Stone", new Vector2(0.33f, 0f), new Vector2(0.66f, 0.2f), FontSize: 1.5f,
        Color: Colors[
            1f - Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Stone") / TargetOresCount["Stone"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("!Scrap", new Vector2(0.66f, 0f), new Vector2(1f, 0.2f), FontSize: 1.5f,
        Color: Colors[
            1f - Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ore", "Scrap") / TargetOresCount["Scrap"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.SaveLine();
    }

    private void DrawIngotsStats(SurfaceContentManager.SurfaceManager Manager)
    {
        Manager.AddTextBuilder("--- Ingot's Stash ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
        Manager.SaveLine();

        Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1, 0.65f));

        Manager.AddTextBuilder("Fe", new Vector2(0f, 0.025f), new Vector2(0.3f, 0.225f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Iron") / TargetIngotsCount["Iron"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Ni", new Vector2(0.3f, 0.025f), new Vector2(0.7f, 0.225f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Nickel") / TargetIngotsCount["Nickel"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Co", new Vector2(0.7f, 0.025f), new Vector2(1f, 0.225f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Cobalt") / TargetIngotsCount["Cobalt"],
            1, MidpointRounding.AwayFromZero), 1)]);

        Manager.AddTextBuilder("Mg", new Vector2(0f, 0.225f), new Vector2(0.3f, 0.425f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Magnesium") / TargetIngotsCount["Magnesium"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Si", new Vector2(0.3f, 0.225f), new Vector2(0.7f, 0.425f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Silicon") / TargetIngotsCount["Silicon"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Ur", new Vector2(0.7f, 0.225f), new Vector2(1f, 0.425f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Uranium") / TargetIngotsCount["Uranium"],
            1, MidpointRounding.AwayFromZero), 1)]);

        Manager.AddTextBuilder("Pt", new Vector2(0f, 0.425f), new Vector2(0.3f, 0.625f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Platinum") / TargetIngotsCount["Platinum"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Ag", new Vector2(0.3f, 0.425f), new Vector2(0.7f, 0.625f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Silver") / TargetIngotsCount["Silver"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.AddTextBuilder("Au", new Vector2(0.7f, 0.425f), new Vector2(1f, 0.625f), FontSize: 2f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Gold") / TargetIngotsCount["Gold"],
            1, MidpointRounding.AwayFromZero), 1)]);
        
        Manager.SaveLine();

        Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1, 0.2f), new Vector2(0.5f, 0f));
        Manager.AddTextBuilder("Gravel", new Vector2(0, 0f), new Vector2(1, 0.2f), FontSize: 1.5f,
        Color: Colors[
            Math.Min((float)Math.Round(ItemCount("MyObjectBuilder_Ingot", "Stone") / TargetIngotsCount["Gravel"],
            1, MidpointRounding.AwayFromZero), 1)]);
        Manager.SaveLine();
    }

    private void DrawCargoList(SurfaceContentManager.SurfaceManager Manager)
    {
        Manager.AddTextBuilder("--- Cargo List ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
        Manager.SaveLine();

        foreach (IMyTerminalBlock Inventory in _Inventories)
        {
            for (int i = 0; i < Inventory.InventoryCount; i++)
            {
                float FillLevel = GetInventoryFillLevel(Inventory.GetInventory(i));

                Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(0.8f, 0.1f));
                
                Manager.AddSquareProgressBarBuilder(FillLevel, new Vector2(0f, 0f), new Vector2(0.8f, 0.1f), 270);
                Manager.AddTextBuilder(String.Format("{0:0.0}%", FillLevel * 100f), new Vector2(0.75f, 0f), new Vector2(1f, 0.1f), Alignment: TextAlignment.RIGHT);
                Manager.AddTextBuilder($"[{i}] - {Inventory.CustomName}", new Vector2(0f, 0f), new Vector2(0.85f, 0.095f), Alignment: TextAlignment.LEFT, ExtraPadding: true, Color: SurfaceContentManager.SurfaceManager.BackgroundColor, FontSize: 0.7f);

                Manager.SaveLine();
            }
        }
    }
    #endregion
}
#endregion

// Last update - 01.12.2022
#region SurfaceContentManagerClass
class SurfaceContentManager
{
    private Program _Program;

    private List<SurfaceProvider> _Providers = new List<SurfaceProvider>();
    private static Dictionary<string, Action<SurfaceManager>> _ContentTypes = new Dictionary<string, Action<SurfaceManager>>();

    public SurfaceContentManager(Program Program)
    {
        _Program = Program;

        Update();
    }

    public void Update()
    {
        // remove provider's that don't contain's [LCDTag] or isn't valid
        _Providers = _Providers.Where(x => x.CustomName.Contains(_Program.LCDTag) && IsValid(x)).ToList();

        List<IMyTextSurfaceProvider> Providers = new List<IMyTextSurfaceProvider>();
        _Program.GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(
            Providers,
            x => (x as IMyTerminalBlock).IsSameConstructAs(_Program.Me)
            && (x as IMyTerminalBlock).CustomName.Contains(_Program.LCDTag)
            && x.SurfaceCount > 0
        );

        // add new provider's
        foreach (IMyTextSurfaceProvider Provider in Providers) if (!IsExist(Provider)) _Providers.Add(new SurfaceProvider(Provider));

        // update provider's content
        foreach (SurfaceProvider _Provider in _Providers) _Provider.Update();
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
            ParseIni();
        }

        private void ParseIni()
        {
            IMyTerminalBlock Block = _Provider as IMyTerminalBlock;

            for (int i = 0; i < _Provider.SurfaceCount; i++)
            {
                _ini.Clear();
                string[] Content = new string[] { "none" };

                if (_ini.TryParse(Block.CustomData))
                {
                    Content = _ini.Get(IniSectionLCD, $"{IniKeyContentType} ({i})").ToString("none").Split(',');
                }
                else if (!string.IsNullOrWhiteSpace(Block.CustomData))
                {
                    _ini.EndContent = Block.CustomData;
                }

                _ini.Set(IniSectionLCD, $"{IniKeyContentType} ({i})", string.Join(",", Content));

                string Output = _ini.ToString();
                if (Output != Block.CustomData)
                {
                    Block.CustomData = Output;
                }

                if (Content.Length == 1 && Content[0] == "none") continue;

                SurfaceManager Manager = new SurfaceManager(_Provider.GetSurface(i));

                if (!IsExist(Manager))
                {
                    _Surfaces.Add(Manager);
                    _Contents.Add(Content);
                }
                else
                {
                    int index = IndexOf(Manager);
                    if (index != -1)
                    {
                        if (!IsEquals(Content, _Contents[index])) _Surfaces[index].Reset();
                        _Contents[index] = Content;
                    }
                }
            }
        }

        #region Helpers
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
                    DrawBSOD(Manager);
                }

            }
        }

        private void DrawBSOD(SurfaceManager Manager)
        {
            Manager.Clear();

            Manager.AddTextBuilder(":)", new Vector2(0f, 0f), new Vector2(1f, 0.5f), Alignment: TextAlignment.LEFT, FontSize: 6f);

            Manager.AddTextBuilder("Your PROGRAM is perfectly stable and is running", new Vector2(0f, 0f), new Vector2(1f, 1f), Alignment: TextAlignment.LEFT, FontSize: 1.2f);
            Manager.AddTextBuilder("with absolutely no problems whatsoever.", new Vector2(0f, 0.15f), new Vector2(1f, 1f), Alignment: TextAlignment.LEFT, FontSize: 1.2f);

            Manager.AddTextBuilder("Please type correct setting's to Custom Data O_o", new Vector2(0f, 0.45f), new Vector2(1f, 1f), Alignment: TextAlignment.LEFT, FontSize: 0.8f);

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

        private int _ScrollDirection = 1;
        private float _ScrollValue = 0f;

        public readonly static Color BackgroundColor = new Color(0, 88, 151);
        public readonly static Color DefaultColor = new Color(179, 237, 255);
        public readonly static Color GhostColor = new Color(134, 195, 226);

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
            Color? Color = null,
            TextAlignment Alignment = TextAlignment.CENTER,
            bool ExtraPadding = false,
            float FontSize = 1f
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            FontSize *= _Scale;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;
            
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, new Color(0, 0, 0, 0)));

            if (Alignment == TextAlignment.RIGHT) Position.X += ContentSize.X * 0.5f - (ExtraPadding ? _Padding.X * 2f: 0);
            if (Alignment == TextAlignment.LEFT) Position.X -= ContentSize.X * 0.5f - (ExtraPadding ? _Padding.X * 2f : 0);

            Vector2 TextSize = _Surface.MeasureStringInPixels(new StringBuilder(Text), "Debug", FontSize);
            while (TextSize.X >= ContentSize.X - _Padding.X * 2f)
            {
                Text = Text.Remove(Text.Length-1);
                TextSize = _Surface.MeasureStringInPixels(new StringBuilder(Text), "Debug", FontSize);
            }
            Position = new Vector2(Position.X, Position.Y - TextSize.Y * 0.5f);

            _Sprites.Add(new MySprite(SpriteType.TEXT, Text, Position, ContentSize - _Padding * 2f, Color ?? DefaultColor, "Debug", Alignment, FontSize));
        }
        public void AddCircleProgressBarBuilder(
            float Percentage,
            float Size,
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            float Sector = 270,
            float Rotation = 0,
            int Cells = 1,
            Color? Color = null,
            bool Reverse = false
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Percentage = Math.Max(Math.Min(Percentage, 1f), 0f);

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

            float CircleSize = Math.Min(ContentSize.X, ContentSize.Y) - 2f * Math.Min(_Padding.X, _Padding.Y);
            float Radius = CircleSize * 0.5f;
            
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, new Color(0, 0, 0, 0)));

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

                DrawLine((Position + Offset * (1 - Size)), Position + Offset, GhostColor, SeparatorWidth);
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

                DrawLine((Position + Offset * (1 - Size)), Position + Offset, Color ?? DefaultColor, SeparatorWidth);
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
            Color? Color = null
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Percentage = Math.Max(Math.Min(Percentage, 1f), 0f);

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, new Color(0, 0, 0, 0)));

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
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BarSize, GhostColor));
            // Body
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", BarPosition, ActiveSize, Color ?? DefaultColor));

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
            Color? Color = null,
            bool DisplayPercentage = true,
            bool Filled = false
        ) {
            if (Values.Count <= 0) return;
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            float FontSize = 1.25f * _Scale;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;

            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, new Color(0, 0, 0, 0)));

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

                DrawLine(StartPoint, EndPoint, Color ?? DefaultColor, Size);
                if (i == 1) _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", StartPoint, new Vector2(Size, Size), Color ?? DefaultColor));
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", EndPoint, new Vector2(Size, Size), Color ?? DefaultColor));

                // Fill
                if (Filled)
                {
                    Vector2 Difference = EndPoint - StartPoint;

                    float X = StartPoint.X;
                    while (X <= EndPoint.X + _Scale)
                    {
                        float Y = (X - StartPoint.X) / Difference.X * Difference.Y;
                        DrawLine(new Vector2(X, ZeroPoint.Y), new Vector2(X, StartPoint.Y + Y), Color ?? DefaultColor, Size * 0.5f);
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

                DrawLine(Start, End, Color ?? DefaultColor, Size);
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", Start, new Vector2(Size, Size), Color ?? DefaultColor));
                _Sprites.Add(new MySprite(SpriteType.TEXTURE, "Circle", End, new Vector2(Size, Size), Color ?? DefaultColor));
            }
            // Running Percentage 
            if (DisplayPercentage) _Sprites.Add(new MySprite(
                SpriteType.TEXT,
                String.Format("{0:0.0}%", Values[Values.Count - 1] * 100f),
                new Vector2(StartPoint.X + TextSize.X - _Padding.X * 0.25f, StartPoint.Y - TextSize.Y * 0.5f),
                null,
                Color ?? DefaultColor,
                "Debug",
                TextAlignment.RIGHT,
                FontSize
            ));
        }
        public void AddBorderBuilder(
            Vector2 TopLeftCorner,
            Vector2 BottomRightCorner,
            Vector2? Gaps = null,
            Color? Color = null
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;
            
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, new Color(0, 0, 0, 0)));

            // Border
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, ContentSize, Color ?? DefaultColor));

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
            Color? Color = null,
            TextAlignment Alignment = TextAlignment.CENTER,
            bool KeepAspectRatio = true
        ) {
            if (BottomRightCorner.X <= TopLeftCorner.X || BottomRightCorner.Y <= TopLeftCorner.Y) return;

            Vector2 BlockSize = _Viewport.Size * (BottomRightCorner - TopLeftCorner);
            Vector2 ContentSize = BlockSize - _Padding;
            Vector2 Position = _Viewport.Position + _Viewport.Size * (BottomRightCorner + TopLeftCorner) * 0.5f;
                    
            // Fix Size
            _Sprites.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Position, BlockSize, new Color(0, 0, 0, 0)));

            float Size = Math.Min(ContentSize.X, ContentSize.Y);

            if (Alignment == TextAlignment.RIGHT) Position.X += (ContentSize.X - Size) * 0.5f;
            if (Alignment == TextAlignment.LEFT) Position.X -= (ContentSize.X - Size) * 0.5f;

            if (KeepAspectRatio)
            {
                ContentSize = new Vector2(Size, Size);
            }

            _Sprites.Add(new MySprite(SpriteType.TEXTURE, Type, Position, ContentSize - _Padding * 2f, Color ?? DefaultColor));
        }
        #endregion

        #region Changers
        public void Reset()
        {
            _ScrollDirection = 1;
            _ScrollValue = 0f;

            SetupDrawSurface();
            Clear();
        }
        public void SaveLine() 
        {
            _Lines.Add(_Sprites);
            _Sprites = new List<MySprite>();
        }
        public void Clear()
        {
            _Lines = new List<List<MySprite>>();
            _Sprites = new List<MySprite>();
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

            RunScroll(PixelsToScroll);

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
            float Difference = GetFrameHeight() - _Viewport.Size.Y;

            if (Difference > 0)
            {
                _ScrollValue += Offset * _ScrollDirection;

                float LowerLimit = 0f;
                float UpperLimit = Difference;

                if (_ScrollValue <= LowerLimit  && _ScrollDirection <= 0)
                {
                    _ScrollValue = LowerLimit;
                    _ScrollDirection++;
                }
                else if (_ScrollValue >= UpperLimit  && _ScrollDirection >= 0)
                {
                    _ScrollValue = UpperLimit;
                    _ScrollDirection--;
                }
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
