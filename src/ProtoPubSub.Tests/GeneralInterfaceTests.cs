using System;
using System.Threading;
using Xunit;

namespace ProtoPubSub.Tests
{
    public class GeneralInterfaceTests
    {
        [Fact]
        public void Basic_scenario()
        {
            var r = new ManualResetEvent(false);

            var publisher = new Publisher<Message1>("mem://message1");
            publisher.Start();

            var subscriber = new Subscriber<Message1>("mem://message1");
            subscriber.Subscribe(m =>
                                 {
                                     Console.WriteLine(m.Name);
                                     Console.WriteLine(m.Number);
                                     if (m.Name=="DONE") r.Set();
                                 });

            publisher.Publish(new Message1 { Name = "A", Number = 1 });
            publisher.Publish(new Message1 { Name = "B", Number = 2 });
            publisher.Publish(new Message1 { Name = "C", Number = 3 });
            publisher.Publish(new Message1 { Name = "DONE", Number = 4 });

            r.WaitOne();

            publisher.Stop();
            subscriber.Close();
        }
    }
}
