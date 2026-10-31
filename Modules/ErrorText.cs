using UnityEngine;
using static TONE.Translator;

namespace TONE;

[Obfuscation(Exclude = true, Feature = "renaming", ApplyToMembers = true)]
public class ErrorText : MonoBehaviour
{
    #region Singleton
    public static ErrorText Instance
    {
        get
        {
            return _instance;
        }
    }
    private static ErrorText _instance;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this);
        }
    }
    #endregion
    public static void Create(TMPro.TextMeshPro baseText)
    {
        var Text = Instantiate(baseText);
        var instance = Text.gameObject.AddComponent<ErrorText>();
        instance.Text = Text;
        instance.name = "ErrorText";

        Text.enabled = false;
        Text.text = "NO ERROR";
        Text.color = Color.red;
        Text.outlineColor = Color.black;
        Text.alignment = TMPro.TextAlignmentOptions.Top;
    }

    public TMPro.TextMeshPro Text;
    public Camera Camera;
    public List<ErrorData> AllErrors = [];
    public Vector3 TextOffset = new(0, 0.3f, -1000f);
    public void Update()
    {
        foreach (var error in AllErrors.ToArray())
        {
            error.IncreaseTimer();
        }

        var ToRemove = AllErrors.Where(err => err.ErrorLevel <= 1 && 30f < err.Timer);
        if (ToRemove.Any())
        {
            AllErrors.RemoveAll(err => ToRemove.Contains(err));
            UpdateText();
            if (HnSFlag)
                Destroy(this.gameObject);
        }
    }
    public void LateUpdate()
    {
        if (!Text.enabled) return;

        if (Camera == null) Camera = !HudManager.InstanceExists ? Camera.main : HudManager.Instance.PlayerCam.GetComponent<Camera>();

        if (Camera != null)
        {
            transform.position = AspectPosition.ComputeWorldPosition(Camera, AspectPosition.EdgeAlignments.Top, TextOffset);
        }
    }
    public void AddError(ErrorCode code)
    {
        var error = new ErrorData(code);
        if (0 < error.ErrorLevel)
            Logger.Error($"エラー発生: {error}: {error.Message}", "ErrorText");

        if (!AllErrors.Any(e => e.Code == code))
        {

            AllErrors.Add(error);
        }
        UpdateText();
    }
    public void UpdateText()
    {
        string text = "";
        int maxLevel = 0;
        foreach (var err in AllErrors.ToArray())
        {
            text += $"{err}: {err.Message}\n";
            if (maxLevel < err.ErrorLevel) maxLevel = err.ErrorLevel;
        }
        if (maxLevel == 0)
        {
            Text.enabled = false;
        }
        else
        {
            if (!HnSFlag)
                text += $"{GetString($"ErrorLevel{maxLevel}")}";
            if (CheatDetected)
                text = SBDetected ? GetString("EAC.CheatDetected.HighLevel") : GetString("EAC.CheatDetected.LowLevel");
            Text.enabled = true;
        }
        if (GameStates.IsInGame && maxLevel != 3 && !CheatDetected)
            text += $"\n{GetString("TerminateCommand")}: Shift+L+Enter";
        Text.text = text;
    }
    public void Clear()
    {
        AllErrors.RemoveAll(err => err.ErrorLevel != 3);
        UpdateText();
    }

    public class ErrorData
    {
        public readonly ErrorCode Code;
        public readonly int ErrorType1;
        public readonly int ErrorType2;
        public readonly int ErrorLevel;
        public float Timer { get; private set; }
        public string Message => GetString(this.ToString());
        public ErrorData(ErrorCode code)
        {
            this.Code = code;
            this.ErrorType1 = (int)code / 10000;
            this.ErrorType2 = (int)code / 10 - ErrorType1 * 1000;
            this.ErrorLevel = (int)code - (int)code / 10 * 10;
            this.Timer = 0f;
        }
        public override string ToString()
        {

            return $"ERR-{ErrorType1:000}-{ErrorType2:000}-{ErrorLevel:0}";
        }
        public void IncreaseTimer() => Timer += Time.deltaTime;
    }

    public bool HnSFlag;
    public bool CheatDetected;
    public bool SBDetected;
}
[Obfuscation(Exclude = true)]
public enum ErrorCode
{

    Main_DictionaryError = 0010003,

    UnsupportedVersion = 002_000_1,

    NoError = 0000000,
    TestError0 = 0009000,
    TestError1 = 0009101,
    TestError2 = 0009202,
    TestError3 = 0009303,
    HnsUnload = 000_804_1,
    CheatDetected = 000_666_2,
    SBDetected = 000_666_1,
}
