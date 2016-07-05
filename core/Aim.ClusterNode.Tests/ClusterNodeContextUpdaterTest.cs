using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.TestKit.TestActors;
using Akka.TestKit.Xunit2;
using Xunit;
using Xunit.Abstractions;
using Akka.Interfaced;

namespace Aim.ClusterNode
{
    public class ClusterNodeContextUpdaterTest : TestKit
    {
        public ClusterNodeContextUpdaterTest(ITestOutputHelper output)
            : base(output: output)
        {
        }

        public class DummyActorRef : InterfacedActorRef
        {
            public DummyActorRef()
                : base(null) { }

            public DummyActorRef(IRequestTarget target)
                : base(target) { }

            public override Type InterfaceType => null;
        }

        public class WrappedActorRef
        {
            public IActorRef ActorRef { get; }

            public WrappedActorRef()
            {
            }

            public WrappedActorRef(IActorRef actorRef)
            {
                ActorRef = actorRef;
            }
        }

        public class Context : ClusterNodeContextBase
        {
            [ClusterActor("Actor1")]
            public IActorRef Actor1;

            [ClusterActor("Actor2")]
            public DummyActorRef Actor2;

            [ClusterActor("Actor3")]
            public WrappedActorRef Actor3;

            [ClusterActor("Actor4", manualUpdate: true)]
            public bool Actor4;
        }

        [Fact]
        private void MonitorAllActorsInContext()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(() => new EchoActor(this, false));

            // Act
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Assert
            var msgs = Enumerable.Range(0, 4).Select(_ => ExpectMsg<ClusterActorDiscoveryMessage.MonitorActor>()).ToList();
            Assert.Equal(new[] { "Actor1", "Actor2", "Actor3", "Actor4" },
                         msgs.Select(m => m.Tag).OrderBy(s => s));
        }

        [Fact]
        private async Task RawActorOn_Updated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            var testActor = ActorOf(BlackHoleActor.Props);
            updater.Tell(new ClusterActorDiscoveryMessage.ActorUp(testActor, "Actor1"));
            await Task.Delay(10);

            // Assert
            Assert.Equal(testActor, context.Actor1);
        }

        [Fact]
        private async Task RawActorOff_Updated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            context.Actor1 = ActorOf(BlackHoleActor.Props);
            updater.Tell(new ClusterActorDiscoveryMessage.ActorDown(context.Actor1, "Actor1"));
            await Task.Delay(10);

            // Assert
            Assert.Null(context.Actor1);
        }

        [Fact]
        private async Task InterfacedActorOn_Updated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            var testActor = ActorOf(BlackHoleActor.Props);
            updater.Tell(new ClusterActorDiscoveryMessage.ActorUp(testActor, "Actor2"));
            await Task.Delay(10);

            // Assert
            Assert.Equal(testActor, context.Actor2.CastToIActorRef());
        }

        [Fact]
        private async Task InterfacedActorOff_Updated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            context.Actor2 = ActorOf(BlackHoleActor.Props).Cast<DummyActorRef>();
            updater.Tell(new ClusterActorDiscoveryMessage.ActorDown(context.Actor2.CastToIActorRef(), "Actor2"));
            await Task.Delay(10);

            // Assert
            Assert.Null(context.Actor2);
        }

        [Fact]
        private async Task WrappedActorOn_Updated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            var testActor = ActorOf(BlackHoleActor.Props);
            updater.Tell(new ClusterActorDiscoveryMessage.ActorUp(testActor, "Actor3"));
            await Task.Delay(10);

            // Assert
            Assert.Equal(testActor, context.Actor3.ActorRef);
        }

        [Fact]
        private async Task WrappedActorOff_Updated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            context.Actor1 = ActorOf(BlackHoleActor.Props);
            updater.Tell(new ClusterActorDiscoveryMessage.ActorDown(context.Actor1, "Actor3"));
            await Task.Delay(10);

            // Assert
            Assert.Null(context.Actor3);
        }

        [Fact]
        private async Task ManualUpdateActorOn_NotUpdated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            var testActor = ActorOf(BlackHoleActor.Props);
            updater.Tell(new ClusterActorDiscoveryMessage.ActorUp(testActor, "Actor4"));
            await Task.Delay(10);

            // Assert
            Assert.False(context.Actor4);
        }

        [Fact]
        private async Task ManualUpdateActorOff_NotUpdated()
        {
            // Arrange
            var context = new Context();
            context.ClusterActorDiscovery = ActorOf(BlackHoleActor.Props);
            var updater = ActorOf(() => new ClusterNodeContextUpdater(context));

            // Act
            context.Actor4 = true;
            updater.Tell(new ClusterActorDiscoveryMessage.ActorDown(ActorOf(BlackHoleActor.Props), "Actor4"));
            await Task.Delay(10);

            // Assert
            Assert.True(context.Actor4);
        }
    }
}
