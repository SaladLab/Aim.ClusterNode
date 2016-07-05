using System;

namespace Aim.ClusterNode
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ClusterRoleAttribute : Attribute
    {
        public string Role { get; set; }

        public ClusterRoleAttribute(string role)
        {
            Role = role;
        }
    }
}
