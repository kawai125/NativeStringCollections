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
        public int HP, MP;
        public float Attack, Defence;
        //public int Attack, Defence;

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

            float vv = id;
            var name_tmp = "Chara" + (vv * vv).GetHashCode().ToString();
            NSL.Add(name_tmp);

            vv *= 0.1f;
            vv = vv * vv;

            tmp.ID = id;
            tmp.Name = NSL.Last;
            tmp.HP = (int)id * 100;
            tmp.MP = (int)id * 50;
            tmp.Attack = vv * 4;
            tmp.Defence = vv * 3;
            //tmp.Attack = (int)id * 4;
            //tmp.Defence = (int)id * 3;

            return tmp;
        }
    }

    public class CharaDataGenerator
    {
        private string _path = "";
        private string _lf = "\n";
        private char _delim = '\t';
        private bool _standby = true;

        private int _n_chara;
        private int _i_chara;

        private float _n_chara_inv = 1.0f;

        private System.Diagnostics.Stopwatch _timer = new System.Diagnostics.Stopwatch();

        public int N { get { return _n_chara; } }
        public int Inc { get { return _i_chara; } }
        public bool IsStandby { get { return _standby; } }
        public float Progress
        {
            get
            {
                return _i_chara * _n_chara_inv;
            }
        }

        public void SetPath(string path)
        {
            _path = path;
        }
        public long ElapsedMilliseconds
        {
            get { return _timer.ElapsedMilliseconds; }
        }

        /// <summary>
        /// sample file generator
        /// </summary>
        /// <param name="encoding">text encoding</param>
        /// <param name="n">the total number of CharaData</param>
        /// <param name="d">the interval of ID, must be >= 1.</param>
        /// <param name="r">the ratio of dummy data line, must be in range of [0, 1).</param>
        public async void GenerateAsync(Encoding encoding, int n, int d, float r)
        {
            await Task.Run(() =>
            {
                this.Generate(encoding, n, d, r);
            });
        }
        public void Generate(Encoding encoding, int n, int d, float r)
        {
            if (!_standby) throw new InvalidOperationException("the task running now.");
            _standby = false;
            this.CheckArgs(n, d, r);

            _timer.Restart();
            
            _n_chara = n;
            _n_chara_inv = 1.0f / n;
            this.GenerateImpl(encoding, n, d, r);

            _timer.Stop();
        }
        private void CheckArgs(int n, int d, float r)
        {
            if (n <= 10) throw new ArgumentOutOfRangeException("n must be > 10.");
            if (d <= 0) throw new ArgumentOutOfRangeException("d must be > 0.");
            if (r < 0.0 || r >= 1.0) throw new ArgumentOutOfRangeException("r must be in range of [0,1).");
        }
        private unsafe void GenerateImpl(Encoding encoding, int n, int d, float r)
        {
            _i_chara = 0;
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
                id_Sequence.Add(id * d);
            }
            //int n_swap = (int)Math.Sqrt(n);
            int n_swap = n;
            var random = new System.Random(n_swap * d);
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

            var b64_Sequence = new NativeList<Char16>(Allocator.Persistent);
            var b64_Encoder = new NativeBase64Encoder(Allocator.Persistent);

            b64_Encoder.GetChars(b64_Sequence, byte_Sequence);
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
            b64_Encoder.Dispose();

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
                        int id = id_Sequence[i];
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

                    if(sb.Length > 4096)  // 4k: page size
                    {
                        writer.Write(sb);
                        sb.Clear();
                    }
                }

                if(sb.Length > 0) writer.Write(sb);
            }

            id_Sequence.Dispose();
            NSL_tmp.Dispose();

            _standby = true;
        }
    }
}
