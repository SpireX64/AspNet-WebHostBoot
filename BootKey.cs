using System;

namespace SpireX.AspNetCore.Boot
{
    public struct BootKey
    {
        public Guid Id { get; }
        public string Name { get; }

        public BootKey(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
        }

        public static bool operator ==(BootKey lhs, BootKey rhs) => lhs.Equals(rhs);

        public static bool operator !=(BootKey lhs, BootKey rhs) => !lhs.Equals(rhs);

        public override bool Equals(object obj)
        {
            if(obj is BootKey bootKey)
            {
                return Id == bootKey.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}