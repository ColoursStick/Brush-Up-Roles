using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using System;

namespace TONE.Modules.Rpc
{

    public abstract class CustomModdedData : BaseGameDataMessage
    {

        public CustomModdedData(IntPtr ptr) : base(ptr) { }

        public override GameDataTypes GameDataType => GameDataTypes.Invalid;

        [HideFromIl2Cpp]
        public abstract GameDataTypes FirstDataType { get; }

        public CustomModdedData() : base(ClassInjector.DerivedConstructorPointer<CustomModdedData>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        public override void SerializeValues(MessageWriter msg)
        {
            msg.CancelMessage();
            msg.StartMessage((byte)this.FirstDataType);

            SerializeCustomValues(msg);
        }

        [HideFromIl2Cpp]
        public abstract void SerializeCustomValues(MessageWriter msg);

    }
}
