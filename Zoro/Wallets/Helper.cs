﻿using Zoro.Cryptography;
using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;
using System;
using System.Linq;

namespace Zoro.Wallets
{
    public static class Helper
    {
        public static byte[] Sign(this IVerifiable verifiable, KeyPair key)
        {
            return Crypto.Default.Sign(verifiable.GetHashData(), key.PrivateKey, key.PublicKey.EncodePoint(false).Skip(1).ToArray());
        }

        public static string ToAddress(this UInt160 scriptHash)
        {
            byte[] data = new byte[21];
            data[0] = ProtocolSettings.Default.AddressVersion;
            Buffer.BlockCopy(scriptHash.ToArray(), 0, data, 1, 20);
            return data.Base58CheckEncode();
        }

        public static UInt160 ToScriptHash(this string address)
        {
            byte[] data = address.Base58CheckDecode();
            if (data.Length != 21)
                throw new FormatException();
            if (data[0] != ProtocolSettings.Default.AddressVersion)
                throw new FormatException();
            return new UInt160(data.Skip(1).ToArray());
        }
    }
}
