using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;

using TMPro;

using NativeStringCollections;
using NativeStringCollections.Utility;

using NativeStringCollections.Demo;


class DummyParser : ITextFileParser, IDisposable
{
    public long Lines;

    public struct DummyData
    {
        public long Length;
    }
    public DummyData Data;

    public unsafe void Init()
    {
        Lines = 0;
    }
    public unsafe void Clear()
    {
        Lines = 0;
    }
    public unsafe bool ParseLines(NativeStringList lines)
    {
        Lines += lines.Length;
        return true;
    }
    public void PostReadProc()
    {
        Data.Length = Lines;
    }
    public void UnLoad()
    {

    }
    public void Dispose()
    {
        
    }
}

public class Demo_ReadingLargeFile : MonoBehaviour
{
    private string _path;

    public TMP_Dropdown _dropdownEncoding;
    private List<Encoding> _encodingList;

    public TMP_Dropdown _dropdownDataSize;
    private List<int> _dataSizeList;

    public TMP_Dropdown _dropdownIDInterval;
    private List<int> _idIntervalList;

    public TMP_Dropdown _dropdownDecodeSize;
    private List<int> _decodeSizeList;

    public Button _generateButton;
    private TMP_Text _generateButtonText;
    public Slider _generateProgressSlider;
    private float _generateProgress;
    private bool _generateInCurrentProc;

    public Toggle _toggleLoadFileInMainThread;
    public Button _loadButton;
    private TMP_Text _loadButtonText;
    private bool _loadAction;

    public Slider _loadProgressSlider;
    private float _loadProgress;
    public TextMeshProUGUI _loadProgressText;

    public TextMeshProUGUI _generateTimeText;
    public TextMeshProUGUI _loadTimeText;

    private bool _generatorPrevState;
    private ReadState _loaderPrevState;


    private CharaDataGenerator _generator;

    private AsyncTextFileReader<CharaDataParser> _loader;
    //private AsyncTextFileReader<DummyParser> _loader;

    public GameObject burstSwitch;

    // Start is called before the first frame update
    void Start()
    {
#if UNITY_EDITOR
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
        _path = Application.dataPath + "/../Assets/Demo/Temp/";
#else
        _path = Application.dataPath + "/Demo/";
#endif
        DirectoryUtil.SafeCreateDirectory(_path);
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

        _decodeSizeList = new List<int>
        {
            64, 256, 1024, 2048, 4096, 8192,
            16384, 32768, 65536, 131072, 262144,
        };

        if (_dropdownDecodeSize)
        {
            _dropdownDecodeSize.ClearOptions();

            var drop_menu = new List<string>();
            int index_default = 0;
            foreach(var s in _decodeSizeList)
            {
                if(s == NativeStringCollections.Define.DefaultDecodeBlock)
                {
                    index_default = drop_menu.Count;
                    drop_menu.Add(s.ToString() + " (default)");
                }
                else
                {
                    drop_menu.Add(s.ToString());
                }
            }
            _dropdownDecodeSize.AddOptions(drop_menu);
            _dropdownDecodeSize.value = index_default;
        }

        _generateInCurrentProc = false;
        _generateProgress = 0.0f;
        _loadProgress = 0.0f;

        _generator = new CharaDataGenerator();
        _generator.SetPath(_path);
        _generatorPrevState = _generator.IsStandby;

        _loader = new AsyncTextFileReader<CharaDataParser>(_path, Allocator.Persistent);
        //_loader = new AsyncTextFileReader<DummyParser>(Allocator.Persistent);
        _loaderPrevState = _loader.GetState;
        _loadAction = false;

        var switch_obj = burstSwitch.GetComponent<BurstSwitch>();
        switch_obj.loader = _loader;

        _generateButtonText = _generateButton.GetComponentInChildren<TMP_Text>();
        _loadButtonText = _loadButton.GetComponentInChildren<TMP_Text>();
    }
    private void OnDestroy()
    {
        var data = _loader.Data;
        data.Dispose();
        _loader.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        // check AsyncTextFileReader<>.JobState for calling Complete().
        if (_loader.JobState == ReadJobState.WaitForCallingComplete) _loader.Complete();

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

        var loadInfo = _loader.GetState;

        if(loadInfo.Length > 0 && !loadInfo.IsStandby)
        {
            _loadProgress = (float)loadInfo.Read / (float)loadInfo.Length;
        }
        else
        {
            if (loadInfo.IsCompleted)
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
        if (loadInfo.IsStandby && _loadAction)
        {
            _loadButton.interactable = true;

            if(loadInfo.RefCount > 0)
            {
                _loadButtonText.text = "UnLoad file";
            }
            else
            {
                _loadButtonText.text = "Load file";
            }

            Debug.Log($"ref count = {loadInfo.RefCount}");

            _loadAction = false;
        }


        // progress text
        if(loadInfo.JobState == ReadJobState.ParseText)
        {
            _loadProgressText.text = loadInfo.JobState.ToString() + ": [" + loadInfo.Read.ToString() + '/' + loadInfo.Length.ToString() + ']';
        }
        else
        {
            var sb = new StringBuilder();
            sb.Append(loadInfo.JobState.ToString());
            if(loadInfo.RefCount > 0)
            {
                sb.Append(", RefCount = " + loadInfo.RefCount.ToString());
            }
            _loadProgressText.text = sb.ToString();
        }

        // generate time
        if (_generator.IsStandby)
        {
            _generateTimeText.text = $"generate file in {_generator.ElapsedMilliseconds} ms";
        }

        // load time
        if(loadInfo.JobState != _loaderPrevState.JobState)
        {
            if (loadInfo.IsCompleted)
            {
                var data = _loader.Data;

                var sb = new StringBuilder();
                sb.Append($"# of Lines : {data.Lines}\n");
                sb.Append($"# of Data : {data.Data.Length}\n");
                sb.Append('\n');
                sb.Append($"ReadAsync: {loadInfo.DelayReadAsync.ToString("F5")}\n");
                sb.Append($"ParseText: {loadInfo.DelayParseText.ToString("F5")}\n");
                sb.Append($"PostProc : {loadInfo.DelayPostProc.ToString("F5")}\n");
                sb.Append($"Total    : {loadInfo.Delay.ToString("F5")} ms\n");
                _loadTimeText.text = sb.ToString();
                sb.Clear();

                if (data.ParserState != CharaDataParser.ReadMode.Complete)
                {
                    sb.Append("Parser ERROR:\n");
                    sb.Append($"  # of Data: {data.Data.Length}\n");
                    sb.Append($"  line     : {data.Lines}\n");
                    sb.Append($"  state    : {data.ParserState}\n");
                    sb.Append('\n');

                    if(data.ParserState == CharaDataParser.ReadMode.HeaderError)
                    {
                        sb.Append($"  N = {data.N}\n");
                        sb.Append($"  D = {data.D}\n");
                        sb.Append($"  R = {data.R}\n");
                    }
                    Debug.LogError(sb.ToString());
                }
            }
            else
            {
                _loadTimeText.text = "---";
            }
        }


        // save current state
        _generatorPrevState = _generator.IsStandby;
        _loaderPrevState = loadInfo;
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
        _loader.BlockSize = _decodeSizeList[_dropdownDecodeSize.value];

        if (_toggleLoadFileInMainThread.isOn)
        {
            _loader.LoadFileInMainThread();
            return;
        }
        else
        {
            var info = _loader.GetState;
            if (info.IsStandby)
            {
                _loadButton.interactable = false;
                _loadButtonText.text = "Now Loading...";

                _loader.Encoding = _encodingList[_dropdownEncoding.value];

                if (info.RefCount == 0)
                {
                    _loader.LoadFile();
                }
                else
                {
                    _loader.UnLoadFile();
                }

                _loadAction = true;
            }
        }
    }
}

