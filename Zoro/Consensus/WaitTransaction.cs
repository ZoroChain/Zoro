using System;
using System.Collections.Generic;
using System.Text;

namespace Zoro.Consensus
{
    internal class WaitTransaction : ConsensusMessage
    {
        public WaitTransaction()
            : base(ConsensusMessageType.WaitTransaction)
        {
        }
    }
}
