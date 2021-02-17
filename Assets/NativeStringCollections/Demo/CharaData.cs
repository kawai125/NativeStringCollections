using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using NativeStringCollections;

namespace NativeStringCollections.Demo
{
    public struct CharaData : IComparable<CharaData>
    {
        public long ID;
        public StringEntity Name;
        public int HP, MP, Attack, Defence;

        public bool Equals(CharaData rhs)
        {
            return (
                this.ID == rhs.ID &&
                this.Name == rhs.Name &&
                this.HP == rhs.HP &&
                this.MP == rhs.MP &&
                this.Attack == rhs.Attack &&
                this.Defence == rhs.Defence
                );
        }
        public static bool operator ==(CharaData lhs, CharaData rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(CharaData lhs, CharaData rhs) { return !lhs.Equals(rhs); }
        public override bool Equals(object obj)
        {
            return obj is CharaData && this.Equals((CharaData)obj);
        }
        public override int GetHashCode()
        {
            int hash = this.ID.GetHashCode();
            hash = hash ^ this.Name.GetHashCode();
            hash = hash ^ this.HP.GetHashCode();
            hash = hash ^ this.MP.GetHashCode();
            hash = hash ^ this.Attack.GetHashCode();
            hash = hash ^ this.Defence.GetHashCode();

            return hash;
        }

        //--- used for sord by ID
        public int CompareTo(CharaData other)
        {
            if (other == null) return 1;
            return ID.CompareTo(other.ID);
        }

        public override string ToString()
        {
            var delim = '\t';
            return this.ToString(delim);
        }
        public string ToString(char delim)
        {
            var sb = new StringBuilder();
            sb.Append(ID);
            sb.Append(delim);
            sb.Append(Name);
            sb.Append(delim);
            sb.Append(HP);
            sb.Append(delim);
            sb.Append(MP);
            sb.Append(delim);
            sb.Append(Attack);
            sb.Append(delim);
            sb.Append(Defence);

            return sb.ToString();
        }
    }

    public static class CharaDataExt
    {
        public static CharaData Generate(long id, NativeStringList NSL)
        {
            var tmp = new CharaData();

            var name_tmp = "Chara" + id.GetHashCode().ToString();
            NSL.Add(name_tmp);

            tmp.ID = id;
            tmp.Name = NSL.Last;
            tmp.HP = (int)id * 100;
            tmp.MP = (int)id * 50;
            tmp.Attack = (int)id * 4;
            tmp.Defence = (int)id * 3;

            return tmp;
        }
    }

    public class CharaDataGenerator
    {
        private string _path = "";
        private string _lf = "\n";
        private char _delim = '\t';
        private Task _task;
        private bool _run = false;

        private int _n_chara;
        private int _i_chara;

        public int N { get { return _n_chara; } }
        public int Inc { get { return _i_chara; } }
        public bool IsCompleted { get { return !_run || (_run && _task.IsCompleted); } }

        public void SetPath(string path)
        {
            _path = path;
        }
        /// <summary>
        /// sample file generator
        /// </summary>
        /// <param name="encoding">text encoding</param>
        /// <param name="n">the total number of CharaData</param>
        /// <param name="d">the interval of ID, must be >= 1.</param>
        /// <param name="r">the ratio of dummy data line, must be in range of [0, 1).</param>
        public void Generate(Encoding encoding, int n, int d, float r)
        {
            if (!IsCompleted) throw new InvalidOperationException("the task running now.");

            if (n <= 10) throw new ArgumentOutOfRangeException("n must be > 10.");
            if (d <= 0) throw new ArgumentOutOfRangeException("d must be > 0.");
            if (r < 0.0 || r >= 1.0) throw new ArgumentOutOfRangeException("r must be in range of [0,1).");

            _task = Task.Run(() =>
            {
                _run = true;
                _n_chara = n;
                this.GenerateImpl(encoding, n, d, r);
            });
        }
        private unsafe void GenerateImpl(Encoding encoding, int n, int d, float r)
        {
            var sb = new StringBuilder();

            Debug.Log(" >> write header >> ");

            //--- header
            sb.Append("#=======================================");
            sb.Append(_lf);
            sb.Append("# the belong area is a header data.");
            sb.Append(_lf);
            sb.Append("#=======================================");
            sb.Append(_lf);
            sb.Append("<@MARK>" + _delim + "Header");
            sb.Append(_lf);
            sb.Append("n_total" + _delim + n.ToString());
            sb.Append(_lf);
            sb.Append("d" + _delim + d.ToString());
            sb.Append(_lf);
            sb.Append("r" + _delim + r.ToString());
            sb.Append(_lf);

            sb.Append(_lf);  // insert empty lines
            sb.Append(_lf);
            sb.Append(_lf);


            //--- generate random id sequence
            var id_Sequence = new NativeList<int>(n, Allocator.Persistent);
            id_Sequence.Clear();
            for (int id = 0; id < n; id++)
            {
                id_Sequence.Add(id);
            }
            //int n_swap = (int)Math.Sqrt(n);
            int n_swap = n;
            var random = new System.Random(n_swap);
            for (int i_swap = 0; i_swap < n_swap; i_swap++)
            {
                int index = (int)(n *  random.NextDouble() * 0.95);
                var tmp = id_Sequence[index];
                id_Sequence.RemoveAtSwapBack(index);
                id_Sequence.Add(tmp);
            }

            Debug.Log(" >> write Ex Data >> ");

            //--- Ex data
            sb.Append("#=======================================");
            sb.Append(_lf);
            sb.Append("# the belong area is a Base64 encorded ext data.");
            sb.Append(_lf);
            sb.Append("#=======================================");
            sb.Append(_lf);
            sb.Append("<@MARK>" + _delim + "ExtData");
            sb.Append(_lf);

            int byte_len = id_Sequence.Length * UnsafeUtility.SizeOf<int>();
            var byte_Sequence = new NativeArray<byte>(byte_len, Allocator.Persistent);
            UnsafeUtility.MemCpy(byte_Sequence.GetUnsafePtr(), id_Sequence.GetUnsafePtr(), byte_len);

            var b64_Sequence = new NativeList<char>(Allocator.Persistent);
            var b64_Encorder = new NativeBase64Encoder(Allocator.Persistent);

            b64_Encorder.GetChars(b64_Sequence, byte_Sequence);
            for(int i=0; i<b64_Sequence.Length; i++)
            {
                sb.Append(b64_Sequence[i]);
            }
            sb.Append(_lf);
            sb.Append("<@MARK>" + _delim + "ExtDataEnd");

            sb.Append(_lf);
            sb.Append(_lf);

            byte_Sequence.Dispose();
            b64_Sequence.Dispose();
            b64_Encorder.Dispose();

            Debug.Log(" >> write body >> ");

            //--- body
            sb.Append("#=======================================");
            sb.Append(_lf);
            sb.Append("# the belong area is a data body.");
            sb.Append(_lf);
            sb.Append("#=======================================");
            sb.Append(_lf);
            sb.Append("<@MARK>" + _delim + "Body");
            sb.Append(_lf);

            var dummy_line = "# ---" + _delim + "dummy" + _delim + "line" + _delim + "--- #";

            //--- write CharaData
            var NSL_tmp = new NativeStringList(Allocator.Persistent);

            using (StreamWriter writer = new StreamWriter(_path, false, encoding))
            {
                int i = 0;
                _i_chara = i;
                while (i < n)
                {
                    if (random.NextDouble() > r)
                    {
                        NSL_tmp.Clear();
                        int id = id_Sequence[i] * d;
                        var chara_tmp = CharaDataExt.Generate(id, NSL_tmp);
                        sb.Append(chara_tmp.ToString(_delim));
                        sb.Append(_lf);
                        i++;
                        _i_chara = i;

                        //Debug.Log("   -- body [" + i.ToString() + '/' + n.ToString() + ']');
                    }
                    else
                    {
                        sb.Append(dummy_line);
                        sb.Append(_lf);
                    }

                    if(sb.Length > 1024)
                    {
                        writer.Write(sb);
                        sb.Clear();
                    }
                }

                if(sb.Length > 0) writer.Write(sb);
            }

            id_Sequence.Dispose();
            NSL_tmp.Dispose();
        }
    }
}
