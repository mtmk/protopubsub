using ProtoBuf;

namespace ProtoPubSub
{
    [ProtoContract]
    public class ProtocolHeader
    {
        public const string ProtoIo = "PIO";
        [ProtoMember(1)]
        public string Magic { get; set; }
        [ProtoMember(2)]
        public int? Signal { get; set; }
    }
}