using System;
using Akka.Actor;

namespace Aim.ClusterNode
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ClusterActorAttribute : Attribute
    {
        public string Tag { get; set; }
        public bool ManualUpdate { get; set; }

        public ClusterActorAttribute(string tag, bool manualUpdate = false)
        {
            Tag = tag;
            ManualUpdate = manualUpdate;
        }
    }
}
