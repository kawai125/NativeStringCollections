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

public class Demo_ReadingLargeFile : MonoBehaviour
{
    private string _path;
    private Encoding _encoding;
    private NativeTextStreamReader _reader;

    private NativeList<char> _charBuff;
    private NativeList<ReadOnlyStringEntity> _seBuff;
    private bool _reading;

    private List<int> _buffSizeList;

    private JobHandle _readJobHandle;
    public ReadData readData;


    [SerializeField]
    public TMP_Dropdown _dropdownBufferSize;

    public Button _readButton;

    public Slider _progressSlider;
    private float _progress;

    public TextMeshProUGUI _progressText;

    // Start is called before the first frame update
    void Start()
    {
        _path = Application.dataPath + "/../sample_heavy.csv";
        _encoding = Encoding.UTF8;
        _reader = new NativeTextStreamReader(Allocator.Persistent);
        _charBuff = new NativeList<char>(Allocator.Persistent);
        _seBuff = new NativeList<ReadOnlyStringEntity>(Allocator.Persistent);
        _reading = false;

        readData = new ReadData(Allocator.Persistent);

        _buffSizeList = new List<int>();
        _buffSizeList.Clear();
        _buffSizeList.Add(4096);
        _buffSizeList.Add(16384);
        _buffSizeList.Add(65536);
        _buffSizeList.Add(262144);
        _buffSizeList.Add(1048576);
        _buffSizeList.Add(1024);

        if (_dropdownBufferSize)
        {
            _dropdownBufferSize.ClearOptions();

            var drop_menu = new List<string>();
            drop_menu.Add("  4kB (= strage page size)");
            drop_menu.Add(" 16kB");
            drop_menu.Add(" 64kB");
            drop_menu.Add("256kB");
            drop_menu.Add("  1MB");
            drop_menu.Add("  1kB (< strage page size)");

            _dropdownBufferSize.AddOptions(drop_menu);
            _dropdownBufferSize.value = 0;
        }

        _progress = 0.0f;
    }
    private void OnDestroy()
    {
        _reader.Dispose();
        _charBuff.Dispose();
        _seBuff.Dispose();

        readData.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        // progress bar
        if(_reader.Length > 0)
        {
            _progress = (float)_reader.Pos / (float)_reader.Length;
        }
        else
        {
            _progress = 0.0f;
        }
        _progressSlider.value = _progress;

        // progress text
        if(_reader.Length > 0)
        {
            _progressText.text = "Progress: " + _progress.ToString() + "  [" + _reader.Pos.ToString() + "/" + _reader.Length.ToString() + "]";
        }
        else
        {
            _progressText.text = "no data";
        }

        // read job
        if(_reading && _readJobHandle.IsCompleted)
        {
            _readJobHandle.Complete();

            _readButton.interactable = true;
            _readButton.name = "Read File";

            _reading = false;
        }
    }
    private struct ReadJob : IJob
    {
        public NativeTextStreamReader reader;
        public ReadData data;

        public NativeList<char> charBuff;
        public NativeList<ReadOnlyStringEntity> seBuff;

        public void Execute()
        {
            while (!reader.EndOfStream)
            {
                reader.ReadLine(charBuff);
                var se = charBuff.ToStringEntity().GetReadOnlyEntity();
                se.Split(',', seBuff);

                // format check
                if (seBuff.Length != 5) throw new InvalidOperationException("invalid element length");
                if (!seBuff[1].IsIntegral()) throw new InvalidOperationException("the element[1] is not integral.");
                if (!seBuff[2].IsIntegral()) throw new InvalidOperationException("the element[2] is not integral.");
                if (!seBuff[3].IsIntegral()) throw new InvalidOperationException("the element[3] is not integral.");
                if (!seBuff[4].IsIntegral()) throw new InvalidOperationException("the element[4] is not integral.");

                // store value
                seBuff[1].TryParse(out int HP);
                seBuff[2].TryParse(out int MP);
                seBuff[3].TryParse(out int Attack);
                seBuff[4].TryParse(out int Defence);
                data.Add(seBuff[0], HP, MP, Attack, Defence);
            }

            data.PostReadProc();
        }
    }

    public void OnClickLoadFileAsync()
    {
        if (!_reading)
        {
            _readButton.interactable = false;
            _readButton.name = "reading...";

            int bs_index = _dropdownBufferSize.value;
            if (bs_index < 0 || bs_index >= _buffSizeList.Count) throw new InvalidOperationException("invalid buffer size was selected.");

            int buffer_size = _buffSizeList[bs_index];

            _reader.Init(_path, buffer_size);

            var job = new ReadJob();
            job.reader = _reader;
            job.data = readData;
            job.charBuff = _charBuff;
            job.seBuff = _seBuff;

            _readJobHandle = job.Schedule();

            _reading = true;
        }
    }
}



public struct CharactorData
{
    public ReadOnlyStringEntity name;
    public int HP;
    public int MP;
    public int Attack;
    public int Defence;
}
public struct ReadData
{
    private NativeStringList _nameData;
    private NativeList<CharactorData> _data;

    public ReadData(Allocator alloc)
    {
        _nameData = new NativeStringList(alloc);
        _data = new NativeList<CharactorData>(alloc);
    }
    public CharactorData this[int index]
    {
        get { return _data[index]; }
    }
    public void Add(ReadOnlyStringEntity name, int HP, int MP, int Attack, int Defence)
    {
        _nameData.Add(name);
        var se = _nameData.Last.GetReadOnlyEntity();

        var tmp = new CharactorData();
        tmp.name = se;  // this data will be invalid (because of reallocate in NativeStringList)
        tmp.HP = HP;
        tmp.MP = MP;
        tmp.Attack = Attack;
        tmp.Defence = Defence;

        _data.Add(tmp);
    }
    public void PostReadProc()
    {
        // set valid StringEntity into _data
        if (_data.Length != _nameData.Length) throw new InvalidOperationException("number of data was invalid.");
        for(int i=0; i<_data.Length; i++)
        {
            var tmp_data = _data[i];
            tmp_data.name = _nameData[i];
            _data[i] = tmp_data;
        }
    }

    public void Dispose()
    {
        _data.Dispose();
        _nameData.Dispose();
    }
}
