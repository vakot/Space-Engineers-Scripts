public readonly static List<string> RunStatus = new List<string>
{
    "[|---]", 
    "[-|--]", 
    "[--|-]", 
    "[---|]", 
    "[--|-]", 
    "[-|--]"
};

// Variables
int Counter = 0;
JSON json;
string Content = @"{
    ""status-bar"":{
        ""value"":0.25,
        ""position"":{
            ""x"":0,
            ""y"":0
        }
        ""size"":{
            ""x"":256,
            ""y"":64
        }
    }
}";

public const string Version = "1.5",
                    IniSectionGeneral = "General";

// Program
void Status()
{
    string Status = $"I'm tired, Boss {RunStatus[Counter % RunStatus.Count]}";
    Status += $"\nNext update in: {10 - Counter / 6}" + "s\n";

    Status += $"\n------ TEST ------\n";
    foreach (KeyValuePair<string, string> kvp in json.Deserialized)
    {
        Status += $"{kvp.Key}:{kvp.Value}\n";
    }

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

    json = new JSON(Content);
    json.Convert();
}

Program() 
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    Update();
}

void Main(string argument)
{
    if (++Counter % 60 == 0) Update();

    Status();
}

// Last update - 15.03.2023
#region JSON
class JSON
{
    private char Separator = '.';

    public string Serialized { get; private set; }
    public Dictionary<string, string> Deserialized = new Dictionary<string, string>();

    public JSON(string serialized)
    {
        Serialized = serialized;
    }

    public void Convert()
    {
        string _Serialized = String.Concat(Serialized.Where(c => !Char.IsWhiteSpace(c)));
        
        bool isKey = true;

        List<string> PreffixList = new List<string>();

        string CurrentKey = "";
        string CurrentValue = "";

        for (int i = 0; i < Serialized.Length; i++)
        {
            string Preffix = string.Join(Separator.ToString(), PreffixList.ToArray()) + Separator;

            switch (Serialized[i])
            {
                case '{':
                    isKey = true;
                    if (!String.IsNullOrWhiteSpace(CurrentKey)) PreffixList.Add(CurrentKey);
                    CurrentKey = "";
                    CurrentValue = "";
                    break;
                case '}':
                    isKey = true;
                    Save(Preffix + CurrentKey, CurrentValue);
                    if (PreffixList.Count > 0) PreffixList.Pop();
                    CurrentKey = "";
                    CurrentValue = "";
                    break;
                case ',':
                    isKey = true;
                    Save(Preffix + CurrentKey, CurrentValue);
                    CurrentKey = "";
                    CurrentValue = "";
                    break;
                case ':':
                    isKey = false;
                    break;
                default:
                    if (isKey)
                        CurrentKey += Serialized[i].ToString();
                    else
                        CurrentValue += Serialized[i].ToString();
                    break;
            }
        }
    }

    private void Save(string Key, string Value)
    {
        if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Value)) return;
        if (Deserialized.ContainsKey(Key)) return;
        Deserialized.Add(Key, Value);
    }
}
#endregion
