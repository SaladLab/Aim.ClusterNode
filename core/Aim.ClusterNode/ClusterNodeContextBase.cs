using Akka.Actor;

namespace Aim.ClusterNode
{
    public class ClusterNodeContextBase
    {
        public ActorSystem System;
        public IActorRef ClusterActorDiscovery;
        public IActorRef ClusterNodeContextUpdater;

        protected internal virtual object OnActorUp(string tag, IActorRef actor, object value)
        {
            return value;
        }

        protected internal virtual void OnActorDown(string tag, IActorRef actor)
        {
        }
    }
}
