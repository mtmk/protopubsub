using ProtoBuf;

namespace ProtoPubSub.Tests
{
    [ProtoContract]
    public class SimpleHeaderMessage
    {
        [ProtoMember(1)]
        public string TypeName { get; set; }
    }
}