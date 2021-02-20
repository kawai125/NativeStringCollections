using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Unity.Collections;

using TMPro;

using NativeStringCollections;

namespace NativeStringCollections.Demo
{
    public class ConsumerIncremental : MonoBehaviour
    {
        // this is reference
        public AsyncTextFileLoader<CharaDataParser> loader;

        public TMP_Dropdown dropdownInterval;
        private List<int> _intervalList;

        public TMP_Dropdown dropdownWidth;
        private List<int> _widthList;

        public Button buttonContinue;
        private bool _continueRead;

        private NativeQueue<int> _loadingQueue;
        private int _last_loaded;
        private int _intervalCount;

        // Start is called before the first frame update
        void Start()
        {
            _continueRead = false;

            _loadingQueue = new NativeQueue<int>(Allocator.Persistent);
            _last_loaded = -1;

            this.InitializeDropdown();
        }
        private void OnDestroy()
        {
            _loadingQueue.Dispose();
        }

        // Update is called once per frame
        void Update()
        {

        }
        private void FixedUpdate()
        {
            if (!_continueRead) return;

            _intervalCount++;
            if (_intervalCount < _intervalList[dropdownInterval.value]) return;
            _intervalCount = 0;

            // next step
            if (_loadingQueue.Count > 0) this.UnLoadLast();
            this.LoadNext();

            // change width
            int new_width = _widthList[dropdownWidth.value];
            int old_width = _loadingQueue.Count;
            if(new_width > old_width)
            {
                int n_load = new_width - old_width;
                for(int i=0; i<n_load; i++) this.LoadNext();
            }
            else if (new_width < old_width)
            {
                int n_unload = old_width - new_width;
                for(int i=0; i<n_unload; i++) this.UnLoadLast();
            }
        }
        private void LoadNext()
        {
            int new_id = this.NextIndex();
            loader.LoadFile(new_id);
            _loadingQueue.Enqueue(new_id);
        }
        private void UnLoadLast()
        {
            int old_id = _loadingQueue.Dequeue();
            loader.UnLoadFile(old_id);
        }
        private int NextIndex()
        {
            _last_loaded++;
            if (_last_loaded >= loader.Length) _last_loaded = 0;
            return _last_loaded;
        }

        public void OnClickContinue()
        {
            var txt = buttonContinue.GetComponentInChildren<TMP_Text>();

            if (!_continueRead)
            {
                _continueRead = true;
                txt.text = "Loading...";
            }
            else
            {
                _continueRead = false;
                txt.text = "Pause\n(Press to Restart)";
            }

            SwitchButtonColor(buttonContinue, txt, !_continueRead);
        }
        private static void SwitchButtonColor(Button btn, TMP_Text txt, bool mode)
        {
            var cb = btn.colors;
            if (mode)
            {
                // normal color
                cb.normalColor = new Color32(255, 255, 255, 255);
                cb.highlightedColor = new Color32(245, 245, 245, 255);

                txt.color = new Color32(50, 50, 50, 255);
            }
            else
            {
                // dark color
                cb.normalColor = new Color32(50, 50, 50, 255);
                cb.highlightedColor = new Color32(30, 30, 30, 255);

                txt.color = new Color32(235, 235, 235, 255);
            }
            btn.colors = cb;
        }
        private void InitializeDropdown()
        {
            _intervalList = new List<int>();

            if (dropdownInterval)
            {
                dropdownInterval.ClearOptions();

                var dt = Time.fixedDeltaTime;
                float dt_inv = 1.0f / dt;

                var elem_list = new List<float>();
                elem_list.Add(0.1f);   //  1/10
                elem_list.Add(0.2f);   //  1/5
                elem_list.Add(0.5f);   //  1/2
                elem_list.Add(1.0f);   //  1/1
                elem_list.Add(1.5f);   //  2/1
                elem_list.Add(2.0f);   //  2/1

                int default_index = 0;
                var drop_menu = new List<string>();

                foreach (var t in elem_list)
                {
                    _intervalList.Add((int)Math.Round(t * dt_inv));

                    if (t == 0.5f)
                    {
                        default_index = drop_menu.Count;
                    }
                    drop_menu.Add(t.ToString() + " [s]");
                }

                dropdownInterval.AddOptions(drop_menu);
                dropdownInterval.value = default_index;
            }

            _widthList = new List<int>();
            _widthList.Add(0);
            _widthList.Add(1);
            _widthList.Add(2);
            _widthList.Add(3);
            _widthList.Add(4);
            _widthList.Add(5);

            if (dropdownWidth)
            {
                dropdownWidth.ClearOptions();

                var drop_menu = new List<string>();
                foreach (var w in _widthList) drop_menu.Add(w.ToString());
                dropdownWidth.AddOptions(drop_menu);
                dropdownWidth.value = 1;
            }
        }
    }
}
