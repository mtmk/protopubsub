using ProtoBuf;

namespace ProtoPubSub.Tests
{
    [ProtoContract]
    public class Message1
    {
        [ProtoMember(1)]
        public string Name { get; set; }

        [ProtoMember(2)]
        public int Number { get; set; }
    }
}