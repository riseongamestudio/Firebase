using System;
using System.Security.Cryptography;

namespace RiseOn.Firebase.Editor {
    /// <summary>
    /// Reads the RSA key of a service account file.<br/>
    /// The Editor's Mono throws PlatformNotSupportedException from RSA.ImportPkcs8PrivateKey, so the DER is read here.
    /// </summary>
    internal static class Pkcs8 {
        private const byte SequenceTag    = 0x30;
        private const byte IntegerTag     = 0x02;
        private const byte OctetStringTag = 0x04;

        public static RSAParameters ReadRsaPem(string pem) {
            var body = pem
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\\n", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();
            return ReadRsa(Convert.FromBase64String(body));
        }

        // PrivateKeyInfo ::= SEQUENCE { version, AlgorithmIdentifier, OCTET STRING RSAPrivateKey }
        // RSAPrivateKey  ::= SEQUENCE { version, n, e, d, p, q, dp, dq, qinv }
        public static RSAParameters ReadRsa(byte[] der) {
            var info = new Reader(new Reader(der).Read(SequenceTag));
            info.Read(IntegerTag);
            info.Read(SequenceTag);

            var key = new Reader(new Reader(info.Read(OctetStringTag)).Read(SequenceTag));
            key.Read(IntegerTag);
            var modulus  = key.Integer();
            var exponent = key.Integer();
            var d        = key.Integer();
            var p        = key.Integer();
            var q        = key.Integer();
            var dp       = key.Integer();
            var dq       = key.Integer();
            var inverseQ = key.Integer();

            // RSACryptoServiceProvider wants D as long as the modulus and the CRT values half of it.
            var half = (modulus.Length + 1) / 2;
            return new RSAParameters {
                Modulus  = modulus
              , Exponent = exponent
              , D        = Pad(d, modulus.Length)
              , P        = Pad(p, half)
              , Q        = Pad(q, half)
              , DP       = Pad(dp, half)
              , DQ       = Pad(dq, half)
              , InverseQ = Pad(inverseQ, half)
            };
        }

        private static byte[] Pad(byte[] value, int length) {
            if (value.Length >= length) return value;

            var padded = new byte[length];
            Buffer.BlockCopy(value, 0, padded, length - value.Length, value.Length);
            return padded;
        }

        private sealed class Reader {
            private readonly byte[] data;
            private          int    position;

            public Reader(byte[] data) {
                this.data = data;
            }

            public byte[] Read(byte tag) {
                if (position >= data.Length || data[position] != tag) throw new FormatException($"Expected DER tag 0x{tag:X2} at {position}");
                position++;

                var length = ReadLength();
                if (position + length > data.Length) throw new FormatException("DER value runs past the end of the key");

                var value = new byte[length];
                Buffer.BlockCopy(data, position, value, 0, length);
                position += length;
                return value;
            }

            // Unsigned big-endian, without the sign byte DER adds before a high bit.
            public byte[] Integer() {
                var value = Read(IntegerTag);
                var skip  = 0;
                while (skip < value.Length - 1 && value[skip] == 0) skip++;
                if (skip == 0) return value;

                var trimmed = new byte[value.Length - skip];
                Buffer.BlockCopy(value, skip, trimmed, 0, trimmed.Length);
                return trimmed;
            }

            private int ReadLength() {
                int first = data[position++];
                if (first < 0x80) return first;

                var count  = first & 0x7F;
                var length = 0;
                for (var i = 0; i < count; i++) length = (length << 8) | data[position++];
                return length;
            }
        }
    }
}