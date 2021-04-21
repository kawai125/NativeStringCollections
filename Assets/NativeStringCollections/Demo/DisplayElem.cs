using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

using NativeStringCollections;

namespace NativeStringCollections.Demo
{
    public class DisplayElem : MonoBehaviour
    {
        // this is reference for display
        public AsyncTextFileLoader<CharaDataParser> loader;
        private int id;

        public int ID
        {
            set
            {
                id = value;
                stateText.text = $"ID = {id}\nStand by";
            }
        }

        // display interface
        public TMP_Text stateText;
        public TMP_Text timeText;
        public Slider slider;

        private ReadState _prev_info;

        // Update is called once per frame
        void Update()
        {
            if(0<= id && id < loader.Length)
            {
                var info = loader.GetState(id);

                if (!info.IsStandby)
                {
                    // in progress
                    float progress = 0;
                    if(info.Length > 0) progress = (float)info.Read / (float)info.Length;
                    slider.value = progress;

                    if (_prev_info.JobState != info.JobState)
                    {
                        stateText.text = $"ID = {id}\n{info.JobState}";
                    }
                    if (_prev_info.JobState != info.JobState)
                    {
                        timeText.text = $"lines: ------\ntime: ------ ms";
                    }
                }
                else
                {
                    // stand by
                    if (info.JobState == ReadJobState.Completed) slider.value = 1.0f;
                    else slider.value = 0.0f;

                    if((_prev_info.JobState != info.JobState) || (_prev_info.RefCount != info.RefCount))
                    {
                        stateText.text = $"ID = {id}, Ref: {info.RefCount}\n{info.JobState}";
                    }
                    if(info.JobState == ReadJobState.Completed)
                    {
                        var parser = loader[id];
                        if(parser.ParserState == CharaDataParser.ReadMode.Complete)
                        {
                            timeText.text = $"lines: {loader[id].Lines}\ntime: {info.Delay.ToString("F2")} ms";
                        }
                        else
                        {
                            timeText.text = $"lines: {loader[id].Lines}\n{parser.ParserState}";
                        }
                    }
                }

                _prev_info = info;
            }
        }
    }
}

