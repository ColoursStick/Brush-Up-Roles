namespace TONE.Modules;

public static class DoorsReset
{
    private static bool isEnabled = false;
    private static ResetModeList mode;
    private static DoorsSystemType DoorsSystem => ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out var system) ? system.CastFast<DoorsSystemType>() : null;
    private static readonly LogHandler logger = Logger.Handler(nameof(DoorsReset));

    public static void Initialize()
    {
        if (GameStates.IsHideNSeek)
        {
            isEnabled = false;
            return;
        }

        if ((Utils.GetActiveMapName() is not (MapNames.Airship or MapNames.Polus or MapNames.Fungle)) || Options.DisableCloseDoor.GetBool())
        {
            isEnabled = false;
            return;
        }
        isEnabled = Options.ResetDoorsEveryTurns.GetBool();
        mode = (ResetModeList)Options.DoorsResetMode.GetValue();
        Logger.Info($"initialization: [ {isEnabled}, {mode} ]", "Reset Doors");
    }

    public static void ResetDoors()
    {
        if (!isEnabled || DoorsSystem == null)
        {
            return;
        }
        Logger.Info("Reset", "Reset Doors");

        switch (mode)
        {
            case ResetModeList.AllOpen: OpenAllDoors(); break;
            case ResetModeList.AllClosed: CloseAllDoors(); break;
            case ResetModeList.RandomByDoor: OpenOrCloseAllDoorsRandomly(); break;
            default: Logger.Warn($"Invalid Reset Doors Mode: {mode}", "Reset Doors"); break;
        }
    }

    public static void OpenAllDoors()
    {
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            SetDoorOpenState(door, true);
        }
        DoorsSystem.IsDirty = true;
    }

    public static void CloseAllDoors()
    {
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            SetDoorOpenState(door, false);
        }
        DoorsSystem.IsDirty = true;
    }

    public static void OpenOrCloseAllDoorsRandomly()
    {
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            var isOpen = IRandom.Instance.Next(2) > 0;
            SetDoorOpenState(door, isOpen);
        }
        DoorsSystem.IsDirty = true;
    }

    private static void SetDoorOpenState(OpenableDoor door, bool isOpen)
    {
        if (IsValidDoor(door))
        {
            door.SetDoorway(isOpen);
        }
    }

    private static bool IsValidDoor(OpenableDoor door)
    {

        return door.Room is not (SystemTypes.Lounge or SystemTypes.Decontamination);
    }
    [Obfuscation(Exclude = true)]
    public enum ResetModeList { AllOpen, AllClosed, RandomByDoor, }
}
