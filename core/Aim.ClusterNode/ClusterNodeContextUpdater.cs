using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;

namespace Aim.ClusterNode
{
    public class ClusterNodeContextUpdater : ReceiveActor
    {
        private readonly ClusterNodeContextBase _clusterContext;

        private class ActorFieldItem
        {
            public Func<IActorRef, object> FieldValueConstructor;
            public Action<ClusterNodeContextBase, object> FieldValueSetter;
        }

        private Dictionary<string, ActorFieldItem> _fieldMap;

        public ClusterNodeContextUpdater(ClusterNodeContextBase clusterContext)
        {
            if (clusterContext == null)
                throw new ArgumentNullException(nameof(clusterContext));

            _clusterContext = clusterContext;
            _fieldMap = BuildClusterActorFieldMap(_clusterContext);

            Receive<ClusterActorDiscoveryMessage.ActorUp>(m => OnMessage(m));
            Receive<ClusterActorDiscoveryMessage.ActorDown>(m => OnMessage(m));
        }

        private static Dictionary<string, ActorFieldItem> BuildClusterActorFieldMap(ClusterNodeContextBase clusterContext)
        {
            var map = new Dictionary<string, ActorFieldItem>();

            foreach (var member in clusterContext.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attr = member.GetCustomAttribute<ClusterActorAttribute>();
                if (attr == null)
                    continue;

                var item = new ActorFieldItem();

                var fieldType = (member.MemberType == MemberTypes.Field)
                    ? ((FieldInfo)member).FieldType
                    : ((PropertyInfo)member).PropertyType;

                if (fieldType == typeof(IActorRef))
                {
                    item.FieldValueConstructor = o => o;
                }
                else if (typeof(InterfacedActorRef).IsAssignableFrom(fieldType))
                {
                    item.FieldValueConstructor = o => Activator.CreateInstance(fieldType, new AkkaReceiverTarget(o));
                }
                else
                {
                    var ctor = fieldType.GetConstructors().FirstOrDefault(c => c.GetParameters().Count() == 1 &&
                                                                               c.GetParameters()[0].ParameterType == typeof(IActorRef));
                    if (ctor != null)
                    {
                        item.FieldValueConstructor = o => Activator.CreateInstance(fieldType, o);
                    }
                }

                if (attr.ManualUpdate == false)
                {
                    item.FieldValueSetter = (member.MemberType == MemberTypes.Field)
                        ? (Action<ClusterNodeContextBase, object>)((obj, value) => ((FieldInfo)member).SetValue(obj, value))
                        : (Action<ClusterNodeContextBase, object>)((obj, value) => ((PropertyInfo)member).SetValue(obj, value));
                }
                map.Add(attr.Tag, item);
            }

            return map;
        }

        protected override void PreStart()
        {
            foreach (var i in _fieldMap)
            {
                _clusterContext.ClusterActorDiscovery.Tell(
                    new ClusterActorDiscoveryMessage.MonitorActor(i.Key), Self);
            }
        }

        private void OnMessage(ClusterActorDiscoveryMessage.ActorUp m)
        {
            ActorFieldItem field;
            if (_fieldMap.TryGetValue(m.Tag, out field) == false)
                return;

            // construct field value from actor

            object fieldValue = null;
            if (field.FieldValueConstructor != null)
                fieldValue = field.FieldValueConstructor(m.Actor);

            // invoke OnActorUp event handler

            fieldValue = _clusterContext.OnActorUp(m.Tag, m.Actor, fieldValue);

            // set field value

            if (fieldValue != null && field.FieldValueSetter != null)
                field.FieldValueSetter(_clusterContext, fieldValue);
        }

        private void OnMessage(ClusterActorDiscoveryMessage.ActorDown m)
        {
            ActorFieldItem field;
            if (_fieldMap.TryGetValue(m.Tag, out field) == false)
                return;

            // invoke OnActorDown event handler

            _clusterContext.OnActorDown(m.Tag, m.Actor);

            // set field value

            if (field.FieldValueSetter != null)
                field.FieldValueSetter(_clusterContext, null);
        }
    }
}
