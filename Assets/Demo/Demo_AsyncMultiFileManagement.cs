using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Unity.Collections;

using TMPro;

using NativeStringCollections;

namespace NativeStringCollections.Demo
{
    public class Demo_AsyncMultiFileManagement : MonoBehaviour
    {
        private const int nx_files = 6;
        private const int ny_files = 4;
        private const int n_files = nx_files * ny_files;

        private int _i_file;
        private long _write_file_time;

        private AsyncTextFileLoader<CharaDataParser> _loader;
        private CharaDataGenerator _generator;

        [SerializeField]
        public GameObject displayElem;
        [SerializeField]
        public GameObject canvas;

        public ConsumerMultiCast consumerMultiCast;
        public ConsumerIncremental consumerIncremental;

        public Button generateFilesButton;

        public TMP_Dropdown dropdownEncoding;
        private List<Encoding> _encodingList;

        public TMP_Dropdown dropdownDataSize;
        private List<int> _dataSizeList;

        public Slider generateProgressSlider;

        public TMP_Text generateTime;

        public TMP_Dropdown dropdownMaxJob;
        private List<int> _maxJobList;
        public Toggle flushing;

        // Start is called before the first frame update
        void Start()
        {
#if UNITY_EDITOR
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
#endif

            this.InitializeDropdown();

            _i_file = 0;
            _loader = new AsyncTextFileLoader<CharaDataParser>(Allocator.Persistent);
            _generator = new CharaDataGenerator();

            this.InitializeFileList();

            generateProgressSlider.value = 0.0f;

            this.InstanciateDisplayElem();

            // input reference
            consumerMultiCast.loader = _loader;
            consumerIncremental.loader = _loader;
        }
        private void OnDestroy()
        {
            foreach (var data in _loader.DataList) data.Dispose();
            _loader.Dispose();
        }

        // Update is called once per frame
        void Update()
        {
            // progress for generator
            if((_i_file == 0 || _i_file == n_files) && _generator.IsStandby)
            {
                if(_i_file == n_files)
                {
                    generateProgressSlider.value = 1.0f;
                    generateTime.text = $"{_write_file_time} ms\n{((float)_write_file_time / n_files).ToString("F2")} ms";
                }
                else
                {
                    generateProgressSlider.value = 0.0f;
                    generateTime.text = "---";
                }
            }
            else
            {
                float progress = ((float)_i_file + _generator.Progress) / (float)n_files;
                generateProgressSlider.value = progress;
            }

            // swich loader mode
            _loader.FlushLoadJobs = flushing.isOn;
            _loader.MaxJobCount = _maxJobList[dropdownMaxJob.value];

            // update loader
            _loader.Update();
        }

        public async void OnClickGenerateFiles()
        {
            generateFilesButton.interactable = false;
            _i_file = 0;

            await Task.Run(() =>
            {
                var encoding = _encodingList[dropdownEncoding.value];
                var data_size = _dataSizeList[dropdownDataSize.value];

                _write_file_time = 0;
                for(int i=0; i<n_files; i++)
                {
                    _generator.SetPath(_loader.GetFilePath(i));
                    _generator.Generate(encoding, data_size, i+1, 0.1f);

                    _write_file_time += _generator.ElapsedMilliseconds;
                    _i_file++;
                }
            });

            generateFilesButton.interactable = true;
        }

        private void InitializeDropdown()
        {
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

            if (dropdownEncoding)
            {
                dropdownEncoding.ClearOptions();

                var drop_menu = new List<string>();
                foreach (var e in _encodingList)
                {
                    drop_menu.Add(e.EncodingName);
                }

                dropdownEncoding.AddOptions(drop_menu);
                dropdownEncoding.value = 0;
            }

            _dataSizeList = new List<int>
            {
                1024, 4096, 16384, 32768,
                125000, 250000, 500000, 1000000,
            };

            if (dropdownDataSize)
            {
                dropdownDataSize.ClearOptions();

                var drop_menu = new List<string>();
                int index_default;
                foreach (var s in _dataSizeList)
                {
                    if (s == NativeStringCollections.Define.DefaultDecodeBlock)
                    {
                        index_default = drop_menu.Count;
                        drop_menu.Add(s.ToString() + " (default)");
                    }
                    else
                    {
                        drop_menu.Add(s.ToString());
                    }
                }
                dropdownDataSize.AddOptions(drop_menu);
                dropdownDataSize.value = 0;
            }


            _maxJobList = new List<int>
            {
                1, 2, 3, 4, 6, 8, 12, 16, 24
            };

            if (dropdownMaxJob)
            {
                dropdownMaxJob.ClearOptions();

                var drop_menu = new List<string>();
                int index_default = 0;
                foreach (var m in _maxJobList)
                {
                    if (m == Define.DefaultNumParser) index_default = drop_menu.Count;
                    drop_menu.Add(m.ToString());
                }
                dropdownMaxJob.AddOptions(drop_menu);
                dropdownMaxJob.value = index_default;
            }
        }
        private void InitializeFileList()
        {
#if UNITY_EDITOR
            string path = Application.dataPath + "/../Assets/Demo/Temp/";
#else
            string path = Application.dataPath + "/Demo/";
#endif
            DirectoryUtil.SafeCreateDirectory(path);

            var list = new List<string>();
            for(int i=0; i<n_files; i++)
            {
                list.Add($"{path}demo_data_{i}.tsv");
            }

            _loader.AddFile(list);
        }
        private void InstanciateDisplayElem()
        {
            int id = 0;
            float x_unit = 1.0f / 14.0f;
            float y_unit = 2.5f / 32.0f;
            for(int iy=ny_files - 1; iy>=0; iy--)
            {
                for(int ix=0; ix<nx_files; ix++)
                {
                    var obj = Instantiate(displayElem);
                    obj.transform.SetParent(canvas.transform, false);
                    obj.name = $"displayElem_{id}";

                    var rt = obj.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(1280 * (2 * ix - nx_files) * x_unit + 80,
                                                      1080 * (2 * iy - ny_files) * y_unit);

                    var comp = obj.GetComponent<DisplayElem>();

                    comp.loader = _loader;
                    comp.ID = id;

                    id++;
                }
            }
        }
    }

    internal static class DirectoryUtil
    {
        public static DirectoryInfo SafeCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return null;
            }
            return Directory.CreateDirectory(path);
        }
    }
}

