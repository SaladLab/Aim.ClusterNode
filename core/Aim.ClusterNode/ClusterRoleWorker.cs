using System;
using System.Threading.Tasks;

namespace Aim.ClusterNode
{
    public abstract class ClusterRoleWorker
    {
        public abstract Task Start();
        public abstract Task Stop();
    }
}
