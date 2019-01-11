using System.Collections.Generic;

namespace Zoro.Network.P2P
{
    internal class MessageFlagContainer
    {
        private readonly Dictionary<string, MessageFlag> msgFlags = new Dictionary<string, MessageFlag>();

        public void SetFlag(string command, MessageFlag flag)
        {
            if (msgFlags.TryGetValue(command, out MessageFlag flags))
            {
                msgFlags[command] = flags | flag;
            }
            else
            {
                msgFlags.Add(command, flag);
            }
        }

        public bool HasFlag(string command, MessageFlag flag)
        {
            if (msgFlags.TryGetValue(command, out MessageFlag flags))
                return flags.HasFlag(flag);

            return false;
        }
    }
}
