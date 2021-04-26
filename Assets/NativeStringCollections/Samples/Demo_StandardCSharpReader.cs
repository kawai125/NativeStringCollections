using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;

using TMPro;


using Demo = NativeStringCollections.Demo;
using ReadMode = NativeStringCollections.Demo.CharaDataParser.ReadMode;


internal struct CharaData_Standard
{
    public long ID;
    public string Name;
    public int HP, MP;
    public float Attack, Defence;
}
internal class CharaDataParser_Standard
{
    public int Lines;
    public int N, D;
    public float R;

    public ReadMode read_mode;

    public List<CharaData_Standard> Data;

    public bool IsStandby { get { return _is_standby; } }
    public bool IsCompleted { get { return _is_complete; } }
    private bool _is_standby;
    private bool _is_complete;

    public int Length { get { return line_list.Length; } }

    private string[] line_list;
    private int _b64_start, _b64_end;

    private System.Diagnostics.Stopwatch _timer;
    private float _timer_ms_coef;

    private float _delay_ReadAllLines;
    private float _delay_ParseLines;
    private float _delay_PostReadProc;

    public float DeLayReadAllLines {  get { return _delay_ReadAllLines; } }
    public float DelayParseLines { get { return _delay_ParseLines; } }
    public float DelayPostProc { get { return _delay_PostReadProc; } }
    public float Delay { get { return _delay_ReadAllLines + _delay_ParseLines + _delay_PostReadProc; } }

    public ReadMode ParserState { get { return read_mode; } }

    public CharaDataParser_Standard()
    {
        Data = new List<CharaData_Standard>();

        _is_standby = true;
        _is_complete = false;

        _timer = new System.Diagnostics.Stopwatch();
        _timer_ms_coef = 1000.0f / System.Diagnostics.Stopwatch.Frequency;
    }
    ~CharaDataParser_Standard()
    {

    }

    public async void ReadFileAsync(string path, Encoding encoding)
    {
        await Task.Run(() =>
        {
            ReadFile(path, encoding);
        });
    }
    public void ReadFile(string path, Encoding encoding)
    {
        _is_standby = false;
        _is_complete = false;

        _delay_ReadAllLines = 0f;
        _delay_ParseLines = 0f;
        _delay_PostReadProc = 0f;
        _timer.Reset();
        _timer.Start();


        Data.Clear();

        Lines = 0;
        _b64_start = -1;
        _b64_end = -1;

        line_list = File.ReadAllLines(path, encoding);

        _timer.Stop();
        _delay_ReadAllLines = TimerElapsedMilliSeconds();
        _timer.Restart();

        foreach(var line in line_list)
        {
            bool success = ParseLine(line);
            if (!success) break;
        }

        _timer.Stop();
        _delay_ParseLines = TimerElapsedMilliSeconds();
        _timer.Restart();

        PostReadProc();

        _timer.Stop();
        _delay_PostReadProc = TimerElapsedMilliSeconds();

        _is_complete = true;
        _is_standby = true;
    }
    private bool ParseLine(string line)
    {
        const string mark_comment = "#";

        const string mark_tag = "<@MARK>";
        const string mark_header = "Header";
        const string mark_ext = "ExtData";
        const string mark_ext_end = "ExtDataEnd";
        const string mark_body = "Body";

        const string mark_n_total = "n_total";
        const string mark_d = "d";
        const string mark_r = "r";

        Lines++;
        if (line.Length < 1) return true;

        var str_list = line.Split('\t');

        //--- check data block
        if (str_list.Length >= 2 && str_list[0] == mark_tag)
        {
            if (str_list[1] == mark_header) read_mode = ReadMode.Header;
            else if (str_list[1] == mark_ext) read_mode = ReadMode.ExtData;
            else if (str_list[1] == mark_ext_end) read_mode = ReadMode.None;
            else if (str_list[1] == mark_body)
            {
                //--- check header info was read correctly or not
                if (N <= 0 || D <= 0 || R < 0.0 || R >= 1.0)
                {
                    read_mode = ReadMode.HeaderError;
                    return false;
                }
                read_mode = ReadMode.Body;
            }
            return true;
        }
        if (read_mode == ReadMode.None) return true;

        //--- ignore comment line
        if (str_list[0].Length >= 1 && str_list[0].Substring(0, 1) == mark_comment)
        {
            return true;
        }

        //--- store data
        if (read_mode == ReadMode.Header)
        {
            bool success = true;
            // using normal TryParse()
            if (str_list[0] == mark_n_total) { success = int.TryParse(str_list[1], out N); }
            else if (str_list[0] == mark_d) { success = int.TryParse(str_list[1], out D); }
            else if (str_list[0] == mark_r) { success = float.TryParse(str_list[1], out R); }

            if (!success)
            {
                read_mode = ReadMode.HeaderError;
                return false;
            }
        }
        else if (read_mode == ReadMode.ExtData)
        {
            // recode region of ExtData end decode in PostReadProc
            if (_b64_start < 0) _b64_start = Lines - 1;  // Lines incremented at first in ParseLine().
            _b64_end = Lines - 1;

            return true;
        }
        else if (read_mode == ReadMode.Body)
        {
            if (str_list.Length < 6) return true;

            var tmp = new CharaData_Standard();
            bool success = true;
            success = success && long.TryParse(str_list[0], out tmp.ID);
            success = success && int.TryParse(str_list[2], out tmp.HP);
            success = success && int.TryParse(str_list[3], out tmp.MP);
            success = success && float.TryParse(str_list[4], out tmp.Attack);
            success = success && float.TryParse(str_list[5], out tmp.Defence);

            if (!success)
            {
                read_mode = ReadMode.FormatError;
                return false;
            }

            tmp.Name = str_list[1];

            Data.Add(tmp);

            if (Data.Count > N)
            {
                read_mode = ReadMode.FormatError;
                return false;
            }
        }

        return true;
    }
    private void PostReadProc()
    {
        //--- check reading was success or not
        if (read_mode != ReadMode.Body) return;

        //--- store Base64 ext data
        var sb = new StringBuilder();
        for(int i=_b64_start; i<=_b64_end; i++)
        {
            sb.Append(line_list[i]);
        }
        byte[] b64_decoded_bytes = Convert.FromBase64String(sb.ToString());

        int index_len = b64_decoded_bytes.Length / 4;
        if(b64_decoded_bytes.Length % 4 != 0)
        {
            read_mode = ReadMode.Base64DataError;
            return;
        }
        int[] IndexSeq = new int[b64_decoded_bytes.Length / 4];
        for(int i=0; i<b64_decoded_bytes.Length / 4; i++)
        {
            IndexSeq[i] = BitConverter.ToInt32(b64_decoded_bytes, i * 4);
        }

        //--- check Base64 ext data
        if (IndexSeq.Length != Data.Count)
        {
            read_mode = ReadMode.PostProcError;

            //    var sb = new System.Text.StringBuilder();
            //    sb.Append("ERROR in PostReadProc().\n");
            //    sb.Append($"IndexSeq.Length = {IndexSeq.Length}\n");
            //    sb.Append($"Data.Length     = {Data.Length}\n");
            //    UnityEngine.Debug.LogError(sb.ToString());

            return;
        }
        for (int i = 0; i < IndexSeq.Length; i++)
        {
            if (IndexSeq[i] != Data[i].ID)
            {
                read_mode = ReadMode.PostProcError;

                //    var sb = new System.Text.StringBuilder();
                //    sb.Append("ERROR in PostReadProc().\n");
                //    for(int j=0; j<IndexSeq.Length; j++)
                //    {
                //        sb.Append($"  Elems[{j}]: [IndexSeq[i] / Data[i].ID] = [{IndexSeq[j]} / {Data[j].ID}]");
                //        if (IndexSeq[j] != Data[j].ID) sb.Append(" -- differ.");
                //        sb.Append('\n');
                //    }
                //    UnityEngine.Debug.LogError(sb.ToString());

                return;
            }
        }

        read_mode = ReadMode.Complete;
        return;
    }
    private float TimerElapsedMilliSeconds()
    {
        return (float)(_timer.ElapsedTicks * _timer_ms_coef);
    }
}

public class Demo_StandardCSharpReader : MonoBehaviour
{
    private string _path;

    public TMP_Dropdown _dropdownEncoding;
    private List<Encoding> _encodingList;

    public TMP_Dropdown _dropdownDataSize;
    private List<int> _dataSizeList;

    public TMP_Dropdown _dropdownIDInterval;
    private List<int> _idIntervalList;

    public Button _generateButton;
    private TMP_Text _generateButtonText;
    public Slider _generateProgressSlider;
    private float _generateProgress;
    private bool _generateInCurrentProc;

    public Button _loadButton;
    private TMP_Text _loadButtonText;
    private bool _loadAction;

    public Slider _loadProgressSlider;
    private float _loadProgress;
    public TextMeshProUGUI _loadProgressText;

    public TextMeshProUGUI _generateTimeText;
    public TextMeshProUGUI _loadTimeText;

    private bool _generatorPrevState;

    private Demo.CharaDataGenerator _generator;
    private CharaDataParser_Standard _reader;

    // Start is called before the first frame update
    void Start()
    {
#if UNITY_EDITOR
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
        _path = Application.dataPath + "/../Assets/NativeStringCollections/Samples/Temp/";
#else
        _path = Application.dataPath + "/Demo/";
#endif
        NativeStringCollections.Demo.DirectoryUtil.SafeCreateDirectory(_path);
        _path += "sample_demo.tsv";

        _encodingList = new List<Encoding>();
        _encodingList.Clear();

        // default Unity doesn't have some japanese local text encodings for standalone build.
        // ref: https://helpdesk.unity3d.co.jp/hc/ja/articles/204694010-System-Text-Encoding-%E3%81%A7-Shift-JIS-%E3%82%92%E4%BD%BF%E3%81%84%E3%81%9F%E3%81%84
        _encodingList.Add(Encoding.UTF8);
        _encodingList.Add(Encoding.UTF32);
        _encodingList.Add(Encoding.Unicode);
        //_encodingList.Add(Encoding.GetEncoding("shift_jis"));
        //_encodingList.Add(Encoding.GetEncoding("euc-jp"));
        //_encodingList.Add(Encoding.GetEncoding("iso-2022-jp"));

        if (_dropdownEncoding)
        {
            _dropdownEncoding.ClearOptions();

            var drop_menu = new List<string>();
            foreach (var e in _encodingList)
            {
                drop_menu.Add(e.EncodingName);
            }

            _dropdownEncoding.AddOptions(drop_menu);
            _dropdownEncoding.value = 0;
        }

        _dataSizeList = new List<int>
        {
            1024, 4096, 16384, 32768,
            125000, 250000, 500000, 1000000,
        };

        if (_dropdownDataSize)
        {
            _dropdownDataSize.ClearOptions();

            var drop_menu = new List<string>();
            foreach(var s in _dataSizeList)
            {
                drop_menu.Add(s.ToString());
            }
            _dropdownDataSize.AddOptions(drop_menu);
            _dropdownDataSize.value = 1;
        }

        _idIntervalList = new List<int>
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
            11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
            21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
        };
        if (_dropdownIDInterval)
        {
            _dropdownIDInterval.ClearOptions();

            var drop_menu = new List<string>();
            foreach(var s in _idIntervalList)
            {
                drop_menu.Add(s.ToString());
            }
            _dropdownIDInterval.AddOptions(drop_menu);
            _dropdownIDInterval.value = 0;
        }

        _generateInCurrentProc = false;
        _generateProgress = 0.0f;
        _loadProgress = 0.0f;

        _generator = new Demo.CharaDataGenerator();
        _generator.SetPath(_path);
        _generatorPrevState = _generator.IsStandby;

        _loadAction = false;

        _generateButtonText = _generateButton.GetComponentInChildren<TMP_Text>();
        _loadButtonText = _loadButton.GetComponentInChildren<TMP_Text>();

        _reader = new CharaDataParser_Standard();
    }
    private void OnDestroy()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // progress bar
        if(_generator.N > 0 && !_generator.IsStandby)
        {
            _generateProgress = (float)_generator.Inc / (float)_generator.N;
        }
        else
        {
            if (_generator.IsStandby && _generateInCurrentProc)
            {
                _generateProgress = 100.0f;
            }
            else
            {
                _generateProgress = 0.0f;
            }
        }
        _generateProgressSlider.value = _generateProgress;

        if(_reader.Lines > 0 && !_reader.IsStandby)
        {
            _loadProgress = (float)_reader.Lines / (float)_reader.Length;
        }
        else
        {
            if (_reader.IsCompleted)
            {
                _loadProgress = 100.0f;
            }
            else
            {
                _loadProgress = 0.0f;
            }
        }
        _loadProgressSlider.value = _loadProgress;


        // update button
        if (_generator.IsStandby)
        {
            _generateButton.interactable = true;
            if(!_generatorPrevState) _generateButtonText.text = "Write File";
        }
        if (_reader.IsStandby && _loadAction)
        {
            _loadButton.interactable = true;

            if(_reader.IsCompleted)
            {
                _loadButtonText.text = "UnLoad file";
            }
            else
            {
                _loadButtonText.text = "Load file";
            }

            _loadAction = false;
        }


        // progress text
        if(!_reader.IsStandby)
        {
            _loadProgressText.text = $"Reading... : [{_reader.Lines}/{_reader.Length}]";
        }

        // generate time
        if (_generator.IsStandby)
        {
            _generateTimeText.text = $"generate file in {_generator.ElapsedMilliseconds} ms";
        }

        // load time
        if (_reader.IsCompleted)
        {
            var data = _reader.Data;

            var sb = new StringBuilder();
            sb.Append($"# of Lines : {_reader.Lines}\n");
            sb.Append($"# of Data : {_reader.Data.Count}\n");
            sb.Append('\n');
            sb.Append($"File.ReadAllLines: {_reader.DeLayReadAllLines.ToString("F5")}\n");
            sb.Append($"ParseText: {_reader.DelayParseLines.ToString("F5")}\n");
            sb.Append($"PostProc : {_reader.DelayPostProc.ToString("F5")}\n");
            sb.Append($"Total    : {_reader.Delay.ToString("F5")} ms\n");
            _loadTimeText.text = sb.ToString();
            sb.Clear();

            if (_reader.ParserState != ReadMode.Complete)
            {
                sb.Append("Parser ERROR:\n");
                sb.Append($"  # of Data: {_reader.Data.Count}\n");
                sb.Append($"  line     : {_reader.Lines}\n");
                sb.Append($"  state    : {_reader.ParserState}\n");
                sb.Append('\n');

                if (_reader.ParserState == ReadMode.HeaderError)
                {
                    sb.Append($"  N = {_reader.N}\n");
                    sb.Append($"  D = {_reader.D}\n");
                    sb.Append($"  R = {_reader.R}\n");
                }
                Debug.LogError(sb.ToString());
            }
        }
        else
        {
            _loadTimeText.text = "---";
        }


        // save current state
        _generatorPrevState = _generator.IsStandby;
    }

    public void OnClickGenerateFile()
    {
        if (_generator.IsStandby)
        {
            _generateButton.interactable = false;
            _generateButtonText.text = "Now Writing...";

            var encoding = _encodingList[_dropdownEncoding.value];
            int data_size = _dataSizeList[_dropdownDataSize.value];
            int id_interval = _idIntervalList[_dropdownIDInterval.value];
            _generateInCurrentProc = true;
            _generator.GenerateAsync(encoding, data_size, id_interval, 0.1f);
        }
    }

    public void OnClickLoadFileAsync()
    {
        if (_reader.IsStandby)
        {
            _reader.ReadFileAsync(_path, _encodingList[_dropdownEncoding.value]);
        }
    }
}

