﻿using System.IO;

namespace Zoro.Consensus
{
    internal class PrepareResponse : ConsensusMessage
    {
        public byte[] Signature;

        public override int Size => base.Size + Signature.Length;

        public PrepareResponse()
            : base(ConsensusMessageType.PrepareResponse)
        {
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Signature = reader.ReadBytes(64);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Signature);
        }
    }
}
