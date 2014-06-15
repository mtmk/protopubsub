using ProtoBuf;

namespace ProtoPubSub
{
    [ProtoContract]
    public class SimpleHeaderMessage
    {
        [ProtoMember(1)]
        public string TypeName { get; set; }
    }
}