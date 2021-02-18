using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Jobs;

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
    public unsafe bool ParseLine(ReadOnlyStringEntity se)
    {
        Lines++;
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

    public Button _generateButton;
    public Slider _generateProgressSlider;
    private float _generateProgress;
    private bool _generateInCurrentProc;

    public Toggle _toggleLoadFileInMainThread;
    public Button _loadButton;
    public Slider _loadProgressSlider;
    private float _loadProgress;
    public TextMeshProUGUI _loadProgressText;

    public Button _unLoadButton;

    public TextMeshProUGUI _loadTimeText;

    private bool _generatorPrevState;
    private ReadState _loaderPrevState;


    private CharaDataGenerator _generator;
    private AsyncTextFileLoader<CharaDataParser> _loader;
    //private AsyncTextFileLoader<DummyParser> _loader;

    // Start is called before the first frame update
    void Start()
    {
#if UNITY_EDITOR
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
        _path = Application.dataPath + "/../Assets/NativeStringCollections/Demo/sample_demo.tsv";
#else
        _path = Application.dataPath + "/../sample_demo.tsv";
#endif

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

        _dataSizeList = new List<int>();
        _dataSizeList.Clear();

        _dataSizeList.Add(1024);
        _dataSizeList.Add(4096);
        _dataSizeList.Add(125000);
        _dataSizeList.Add(250000);
        _dataSizeList.Add(500000);

        if (_dropdownDataSize)
        {
            _dropdownDataSize.ClearOptions();

            var drop_menu = new List<string>();
            foreach(var s in _dataSizeList)
            {
                drop_menu.Add(s.ToString());
            }
            _dropdownDataSize.AddOptions(drop_menu);
            _dropdownDataSize.value = 0;
        }

        _generateInCurrentProc = false;
        _generateProgress = 0.0f;
        _loadProgress = 0.0f;


        _generator = new CharaDataGenerator();
        _generator.SetPath(_path);
        _generatorPrevState = _generator.IsCompleted;

        _loader = new AsyncTextFileLoader<CharaDataParser>(Allocator.Persistent);
        //_loader = new AsyncTextFileLoader<DummyParser>(Allocator.Persistent);
        _loader.AddFile(_path);
        _loaderPrevState = _loader.GetState(0);
    }
    private void OnDestroy()
    {
        _loader.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        // calling "AsyncTextFileLoader<>.Update()" on update is necessary.
        _loader.Update();


        // progress bar
        if(_generator.N > 0 && !_generator.IsCompleted)
        {
            _generateProgress = (float)_generator.Inc / (float)_generator.N;
        }
        else
        {
            if (_generator.IsCompleted && _generateInCurrentProc)
            {
                _generateProgress = 100.0f;
            }
            else
            {
                _generateProgress = 0.0f;
            }
        }
        _generateProgressSlider.value = _generateProgress;

        var loadInfo = _loader.GetState(0);
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
        if (_generator.IsCompleted)
        {
            _generateButton.interactable = true;
            if(!_generatorPrevState) _generateButton.name = "Write File";
        }
        if (loadInfo.IsCompleted)
        {
            _loadButton.interactable = true;
            if(!_loaderPrevState.IsCompleted) _loadButton.name = "Load file";
        }

        if(loadInfo.RefCount > 0)
        {
            _unLoadButton.interactable = true;
        }
        else
        {
            _unLoadButton.interactable = false;
        }


        // progress text
        if(loadInfo.State == ReadJobState.ParseText)
        {
            _loadProgressText.text = loadInfo.State.ToString() + ": [" + loadInfo.Read.ToString() + '/' + loadInfo.Length.ToString() + ']';
        }
        else
        {
            var sb = new StringBuilder();
            sb.Append(loadInfo.State.ToString());
            if(loadInfo.RefCount > 0)
            {
                sb.Append(", RefCount = " + loadInfo.RefCount.ToString());
            }
            _loadProgressText.text = sb.ToString();
        }


        // load time
        if(loadInfo.State != _loaderPrevState.State)
        {
            if (loadInfo.IsCompleted)
            {

                var sb = new StringBuilder();
                sb.Append("# of Lines : " + _loader[0].Lines.ToString() + '\n');
                sb.Append("# of Data : " + _loader[0].Data.Length.ToString() + '\n');
                sb.Append('\n');
                sb.Append("ReadAsync: " + loadInfo.DelayReadAsync.ToString("e") + '\n');
                sb.Append("ParseText: " + loadInfo.DelayParseText.ToString("e") + '\n');
                sb.Append("PostProc : " + loadInfo.DelayPostProc.ToString("e") + '\n');
                sb.Append("Total    : " + loadInfo.Delay.ToString("e") + " ms\n");
                _loadTimeText.text = sb.ToString();
                sb.Clear();

                /*
                var parser = _loader[0];
                if (parser.ParserState != CharaDataParser.ReadMode.Complete)
                {
                    sb.Append("Parser ERROR:\n");
                    sb.Append("  # of Data: " + parser.Data.Length.ToString() + '\n');
                    sb.Append("  line     : " + parser.Lines.ToString() + '\n');
                    sb.Append("  state    : " + parser.ParserState + '\n');
                    sb.Append('\n');

                    if(parser.ParserState == CharaDataParser.ReadMode.HeaderError)
                    {
                        sb.Append("  N = " + parser.N + '\n');
                        sb.Append("  D = " + parser.D + '\n');
                        sb.Append("  R = " + parser.R + '\n');
                    }
                    Debug.LogError(sb.ToString());
                }
                */
            }
            else
            {
                _loadTimeText.text = "---";
            }
        }


        // save current state
        _generatorPrevState = _generator.IsCompleted;
        _loaderPrevState = _loader.GetState(0);
    }

    public void OnClickGenerateFile()
    {
        if (_generator.IsCompleted)
        {
            _generateButton.interactable = false;
            _generateButton.name = "Now Writing...";

            var e = _encodingList[_dropdownEncoding.value];
            int n = _dataSizeList[_dropdownDataSize.value];
            _generateInCurrentProc = true;
            _generator.Generate(e, n, 1, 0.005f);
        }
    }

    public void OnClickLoadFileAsync()
    {
        if (_toggleLoadFileInMainThread.isOn)
        {
            _loader.LoadFileInMainThread(0);
            return;
        }
        else
        {
            var info = _loader.GetState(0);
            if (info.IsStandby)
            {
                _loadButton.interactable = false;
                _loadButton.name = "Now Loading...";

                _loader.Encoding = _encodingList[_dropdownEncoding.value];
                _loader.LoadFile(0);
            }
        }
    }
    public void OnClickUnLoadFile()
    {
        _loader.UnLoadFile(0);
    }
}

