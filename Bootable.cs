using System;
using System.Threading.Tasks;

namespace SpireX.AspNetCore.Boot
{
    public abstract class Bootable
    {
        public BootKey[] Dependencies { get; set; } = Array.Empty<BootKey>();
        public bool IsCritical { get; set; } = false;
        public abstract BootKey Key { get; }
        public abstract Task Boot();
    }
}