using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.Mathematics;

using NativeStringCollections;

namespace Tests
{
    public class Test_Base64Encoding
    {
        private int seed;
        private System.Random random;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            this.seed = 123456;
            this.random = new System.Random(this.seed);
        }

        [Test]
        public void CheckParseBase64_Fixed()
        {
            string[] str_list = new string[]
            {
                "ABCDEFG",
                "abcdefg",
                "ABCDEF",
                "abcdef",
            };

            var bytes_native = new NativeList<byte>(Allocator.Persistent);
            var encoded_native = new NativeList<Char16>(Allocator.Persistent);
            var bytes_decoded = new NativeList<byte>(Allocator.Persistent);

            var b64_encoder = new NativeBase64Encoder(Allocator.Persistent);
            var b64_decoder = new NativeBase64Decoder(Allocator.Persistent);

            foreach (var str_ref in str_list)
            {
                byte[] bytes_ref = Encoding.UTF8.GetBytes(str_ref);
                string encoded_ref = Convert.ToBase64String(bytes_ref);

                bytes_native.Clear();
                encoded_native.Clear();
                bytes_decoded.Clear();

                foreach(var b in bytes_ref)
                {
                    bytes_native.Add(b);
                }

                b64_encoder.Clear();
                b64_encoder.GetChars(encoded_native, bytes_native);

                CheckBase64EncodedStr(encoded_native, encoded_ref, true);

                b64_decoder.Clear();
                b64_decoder.GetBytes(bytes_decoded, encoded_native);

                CheckBase64DecodedBytes(bytes_decoded, bytes_ref, true);
            }

            bytes_native.Dispose();
            encoded_native.Dispose();
            bytes_decoded.Dispose();

            b64_encoder.Dispose();
            b64_decoder.Dispose();
        }
        [TestCase(1024)]
        public void CheckParseBase64_Random(int n)
        {
            Assert.IsTrue(n > 0);
            const int Base64InLine = 76;

            var bytes_native = new NativeList<byte>(Allocator.Persistent);
            var encoded_native = new NativeList<Char16>(Allocator.Persistent);
            var bytes_decoded = new NativeList<byte>(Allocator.Persistent);

            var b64_encoder = new NativeBase64Encoder(Allocator.Persistent);
            var b64_decoder = new NativeBase64Decoder(Allocator.Persistent);

            long total_bytes = 0;
            for(int i=0; i<n; i++)
            {
                int byte_len = random.Next(16, 256);
                total_bytes += byte_len;

                byte[] bytes_ref = new byte[byte_len];
                bytes_native.Clear();
                for(int j=0; j<byte_len; j++)
                {
                    byte b = (byte)random.Next(0, 255);
                    bytes_ref[j] = b;
                    bytes_native.Add(b);
                }

                string encoded_ref = Convert.ToBase64String(bytes_ref);

                var sb = new StringBuilder();
                int charcount = 0;
                foreach (char c in encoded_ref)
                {
                    if (charcount == Base64InLine)
                    {
                        sb.Append("\r\n");
                        charcount = 0;
                    }

                    sb.Append(c);
                    charcount++;
                }
                string encoded_ref_withLF = sb.ToString();

                // test for encoded str with CRLF
                encoded_native.Clear();
                bytes_decoded.Clear();

                b64_encoder.Clear();
                b64_encoder.GetChars(encoded_native, bytes_native);

                CheckBase64EncodedStr(encoded_native, encoded_ref_withLF);

                b64_decoder.Clear();
                b64_decoder.GetBytes(bytes_decoded, encoded_native);

                CheckBase64DecodedBytes(bytes_decoded, bytes_ref);

                // test for encoded str without CRLF
                encoded_native.Clear();
                bytes_decoded.Clear();

                b64_encoder.Clear();
                b64_encoder.InsertLineBrakes = false;  // default: encode with "CRLF".
                b64_encoder.GetChars(encoded_native, bytes_native);
                b64_encoder.InsertLineBrakes = true;

                CheckBase64EncodedStr(encoded_native, encoded_ref);

                b64_decoder.Clear();
                b64_decoder.GetBytes(bytes_decoded, encoded_native);

                CheckBase64DecodedBytes(bytes_decoded, bytes_ref);
            }

            Debug.Log("total test bytes: " + total_bytes.ToString() + " B\n");

            bytes_native.Dispose();
            encoded_native.Dispose();
            bytes_decoded.Dispose();

            b64_encoder.Dispose();
            b64_decoder.Dispose();
        }
        private void CheckBase64EncodedStr(NativeList<Char16> str_native, string str_ref, bool forceLog = false)
        {
            bool check_flag;
            var diff_list = new List<int>();
            int diff_len = Math.Abs(str_native.Length - str_ref.Length);
            check_flag = (str_native.Length == str_ref.Length);

            for (int i = 0; i < Math.Min(str_native.Length, str_ref.Length); i++)
            {
                bool c_equals = (str_native[i] == str_ref[i]);
                check_flag = check_flag && c_equals;

                if (!c_equals) diff_list.Add(i);
            }

            if (!check_flag || forceLog)
            {
                var sb = new StringBuilder();
                sb.Append("str_native: len=" + str_native.Length.ToString() + "\n");
                foreach(char c in str_native)
                {
                    sb.Append(c);
                }
                sb.Append("\n\n");
                sb.Append("str_ref: len=" + str_ref.Length.ToString() + "\n");
                foreach(char c in str_ref)
                {
                    sb.Append(c);
                }
                sb.Append("\n\n");
                sb.Append((diff_list.Count + diff_len).ToString() + " differs were found (in code):\n");
                foreach(int index in diff_list)
                {
                    sb.Append("index=" + index.ToString() + ": [native/ref] = ["
                        + ((int)(str_native[index])).ToString() + '/' + ((int)str_ref[index]).ToString()
                        + "]\n");
                }
                for(int i=0; i<diff_len; i++)
                {
                    int index = i + Math.Min(str_native.Length, str_ref.Length);
                    if(str_native.Length > str_ref.Length)
                    {
                        sb.Append("index=" + index.ToString() + ": [native/ref] = ["
                            + ((int)str_native[index]).ToString() + "/]\n");
                    }
                    else if(str_native.Length < str_ref.Length)
                    {
                        sb.Append("index=" + index.ToString() + ": [native/ref] = [/"
                            + ((int)str_ref[index]).ToString() + "]\n");
                    }
                }
                if (!check_flag)
                {
                    Debug.LogError(sb);
                }
                else
                {
                    Debug.Log(sb);
                }
            }
            Assert.IsTrue(check_flag);
        }
        private void CheckBase64DecodedBytes(NativeList<byte> bytes_native, byte[] bytes_ref, bool forceLog = false)
        {
            bool check_flag = (bytes_native.Length == bytes_ref.Length);

            if (check_flag)
            {
                for(int i=0; i<bytes_native.Length; i++)
                {
                    check_flag = check_flag && (bytes_native[i] == bytes_ref[i]);
                }
            }

            if (!check_flag || forceLog)
            {
                var sb = new StringBuilder();
                sb.Append("bytes_native:\n");
                byte[] bytes = new byte[1];
                foreach(byte b in bytes_native)
                {
                    bytes[0] = b;
                    string str = BitConverter.ToString(bytes).Replace("-", "");
                    sb.Append(str);
                    sb.Append(' ');
                }
                sb.Append("\n\n");
                sb.Append("bytes_ref:\n");
                foreach (byte b in bytes_ref)
                {
                    bytes[0] = b;
                    string str = BitConverter.ToString(bytes).Replace("-", "");
                    sb.Append(str);
                    sb.Append(' ');
                }
                sb.Append('\n');

                if (!check_flag)
                {
                    Debug.LogError(sb);
                }
                else
                {
                    Debug.Log(sb);
                }
            }
            Assert.IsTrue(check_flag);
        }
    }
}
