﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Akka.Actor;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using Akka.Cluster;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Common.Logging;

namespace Aim.ClusterNode
{
    public class ClusterRunner
    {
        private ILog _logger;
        private readonly Config _commonConfig;
        private readonly Dictionary<string, Type> _roleToTypeMap;

        public class Node
        {
            public ClusterNodeContextBase Context;
            public ClusterRoleWorker[] Workers;
        }

        private readonly List<Node> _nodes = new List<Node>();

        public event Action<ActorSystem> SystemCreated;
        public event Action<ActorSystem> SystemTerminating;
        public Func<ClusterNodeContextBase> CreateClusterNodeContext;

        public ClusterRunner(Config commonConfig, IEnumerable<Assembly> assemblies = null)
        {
            _logger = LogManager.GetLogger("ClusterRunner");
            _commonConfig = commonConfig;
            _roleToTypeMap = CollectRoleWorkerTypes(assemblies);
        }

        private Dictionary<string, Type> CollectRoleWorkerTypes(IEnumerable<Assembly> assemblies)
        {
            var map = new Dictionary<string, Type>();
            var asms = assemblies ?? AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in asms)
            {
                foreach (var type in asm.GetTypes())
                {
                    var attr = type.GetCustomAttribute<ClusterRoleAttribute>();
                    if (attr != null)
                        map.Add(attr.Role, type);
                }
            }
            return map;
        }

        public async Task Launch(IList<HoconValue> nodes)
        {
            foreach (var node in nodes)
            {
                var port = node.GetChildObject("port").GetInt();
                var roles = ResolveRoles(node.GetChildObject("roles").GetArray()).ToList();
                await LaunchNode(port, roles);
            }
        }

        private IEnumerable<Tuple<string, Type, HoconValue>> ResolveRoles(IList<HoconValue> roles)
        {
            foreach (var role in roles)
            {
                var arr = role.GetArray();
                if (arr.Count == 0)
                {
                    // "Role"
                    var id = role.GetString();
                    var type = _roleToTypeMap[id];
                    var config = new HoconValue();
                    config.NewValue(new HoconObject());
                    yield return Tuple.Create(id, type, config);
                }
                else
                {
                    // [ "Role", { config } ]
                    var id = arr[0].GetString();
                    var type = _roleToTypeMap[id];
                    var config = arr[1];
                    yield return Tuple.Create(id, type, config);
                }
            }
        }

        public async Task LaunchNode(int port, IEnumerable<Tuple<string, Type, HoconValue>> roles)
        {
            _logger.Info($"LaunchNode(port={port}, roles={string.Join(", ", roles.Select(i => i.Item1))}");

            // setup system

            var config = Config.Empty
                .WithFallback("akka.remote.helios.tcp.port = " + port)
                .WithFallback("akka.cluster.roles = " + "[" + string.Join(",", roles.Select(r => r.Item1)) + "]")
                .WithFallback(_commonConfig);

            var name = config.GetValue("system.name").GetString();
            var system = ActorSystem.Create(name, config);
            DeadRequestProcessingActor.Install(system);

            if (SystemCreated != null)
                SystemCreated(system);

            // configure cluster base utilities

            var cluster = Cluster.Get(system);

            var context = CreateClusterNodeContext();
            context.System = system;
            context.ClusterActorDiscovery = system.ActorOf(Props.Create(() => new ClusterActorDiscovery(cluster)), "ClusterActorDiscovery");
            context.ClusterNodeContextUpdater = system.ActorOf(Props.Create(() => new ClusterNodeContextUpdater(context)), "ClusterNodeContextUpdater");

            // start workers by roles

            var workers = new List<ClusterRoleWorker>();
            foreach (var role in roles)
            {
                _logger.Info($"Start RoleWorker({role.Item2.Name})");

                var worker = (ClusterRoleWorker)Activator.CreateInstance(role.Item2, context, new Config(new HoconRoot(role.Item3)));
                await worker.Start();
                workers.Add(worker);
            }

            _nodes.Add(new Node { Context = context, Workers = workers.ToArray() });
        }

        public async Task Shutdown()
        {
            // stop all workers

            foreach (var node in _nodes.AsEnumerable().Reverse())
            {
                foreach (var worker in node.Workers.Reverse())
                {
                    _logger.Info($"Stop RoleWorker({worker.GetType().Name})");

                    await worker.Stop();
                }
            }

            // stop all actor systems

            if (SystemTerminating != null)
            {
                foreach (var node in _nodes.AsEnumerable().Reverse())
                    SystemTerminating(node.Context.System);
            }

            _logger.Info("Shutdown all systems");

            await Task.WhenAll(_nodes.AsEnumerable().Reverse().Select(n => n.Context.System.Terminate()));
        }
    }
}
