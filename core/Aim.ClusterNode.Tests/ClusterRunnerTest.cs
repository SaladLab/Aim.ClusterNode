using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Configuration;
using Akka.Interfaced;
using Akka.TestKit.TestActors;
using Akka.TestKit.Xunit2;
using Xunit;
using Xunit.Abstractions;
using Akka.Configuration.Hocon;

namespace Aim.ClusterNode
{
    public class ClusterRunnerTest : TestKit
    {
        public ClusterRunnerTest(ITestOutputHelper output)
            : base(output: output)
        {
        }

        public class ClusterNodeContext : ClusterNodeContextBase
        {
            public IProducerConsumerCollection<string> LogBoard;
        }

        [ClusterRole("One")]
        public class OneWorker : ClusterRoleWorker
        {
            private ClusterNodeContext _context;
            private IActorRef _actor;

            public OneWorker(ClusterNodeContext context, Config config)
            {
                _context = context;
            }

            public override Task Start()
            {
                _context.LogBoard.TryAdd("OneWorker.Start");
                _actor = _context.System.ActorOf(BlackHoleActor.Props, "Actor1");
                return Task.FromResult(true);
            }

            public override async Task Stop()
            {
                _context.LogBoard.TryAdd("OneWorker.Stop");
                await _actor.GracefulStop(TimeSpan.FromMinutes(1));
            }
        }

        [ClusterRole("Two")]
        public class TwoWorker : ClusterRoleWorker
        {
            private ClusterNodeContext _context;
            private int _id;
            private IActorRef _actor;

            public TwoWorker(ClusterNodeContext context, Config config)
            {
                _context = context;
                _id = config.GetInt("id");
            }

            public override Task Start()
            {
                _context.LogBoard.TryAdd($"TwoWorker({_id}).Start");
                _actor = _context.System.ActorOf(BlackHoleActor.Props, "Actor2");
                return Task.FromResult(true);
            }

            public override async Task Stop()
            {
                _context.LogBoard.TryAdd($"TwoWorker({_id}).Stop");
                await _actor.GracefulStop(TimeSpan.FromMinutes(1));
            }
        }

        private const string CommonConfig = @"
            system {
              name = TestCluster
            }
            akka {
              actor {
                provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                #serializers {
                #  wire = ""Akka.Serialization.WireSerializer, Akka.Serialization.Wire""
                #}
                #serialization-bindings {
                #  ""System.Object"" = wire
                #}
              }
              remote {
                helios.tcp {
                  hostname = ""127.0.0.1""
                }
              }
              cluster {
                seed-nodes = [""akka.tcp://TestCluster@127.0.0.1:3001""]
                auto-down-unreachable-after = 30s
              }
            }";

        [Fact]
        private async Task StartAndStopRunnerInSingleCluster()
        {
            // Arrange
            var logBoard = new ConcurrentQueue<string>();
            var runner = new ClusterRunner(ConfigurationFactory.ParseString(CommonConfig), new[] { GetType().Assembly });
            runner.CreateClusterNodeContext = () => new ClusterNodeContext { LogBoard = logBoard };

            // Act
            await runner.Launch(new[]
            {
                Parser.Parse("{ port=3001, roles=[ \"One\", [ \"Two\", { id = 10 } ] ] }", null).Value
            });
            await runner.Shutdown();

            // Assert
            Assert.Equal(new[] { "OneWorker.Start", "TwoWorker(10).Start", "TwoWorker(10).Stop", "OneWorker.Stop" },
                         logBoard.ToArray());
        }

        [Fact]
        private async Task StartAndStopRunnerInMultipleCluster()
        {
            // Arrange
            var logBoard = new ConcurrentQueue<string>();
            var runner = new ClusterRunner(ConfigurationFactory.ParseString(CommonConfig), new[] { GetType().Assembly });
            runner.CreateClusterNodeContext = () => new ClusterNodeContext { LogBoard = logBoard };

            // Act
            await runner.Launch(new[]
            {
                Parser.Parse("{ port=3001, roles=[ \"One\" ] }", null).Value,
                Parser.Parse("{ port=3002, roles=[ [ \"Two\", { id = 10 } ] ] }", null).Value
            });
            await runner.Shutdown();

            // Assert
            Assert.Equal(new[] { "OneWorker.Start", "TwoWorker(10).Start", "TwoWorker(10).Stop", "OneWorker.Stop" }.OrderBy(x => x),
                         logBoard.ToArray().OrderBy(x => x));
        }
    }
}
