using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using System;

namespace TONE.Modules.Rpc
{
    public abstract class BaseModdedRpc : BaseGameDataMessage
    {

        public BaseModdedRpc(IntPtr ptr) : base(ptr) { }

        public override GameDataTypes GameDataType { get; } = GameDataTypes.RpcFlag;

        [HideFromIl2Cpp]
        public abstract byte RpcType { get; }

        public BaseModdedRpc(uint rpcObjectNetId) : base(ClassInjector.DerivedConstructorPointer<BaseModdedRpc>())
        {
            ClassInjector.DerivedConstructorBody(this);
            this.rpcObjectNetId = rpcObjectNetId;
        }

        public override void SerializeValues(MessageWriter msg)
        {
            msg.WritePacked(this.rpcObjectNetId);
            msg.Write(this.RpcType);

            SerializeRpcValues(msg);
        }

        [HideFromIl2Cpp]
        public abstract void SerializeRpcValues(MessageWriter msg);

        private readonly uint rpcObjectNetId;
    }
}
