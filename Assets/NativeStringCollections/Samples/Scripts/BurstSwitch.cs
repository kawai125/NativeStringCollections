using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using NativeStringCollections;

namespace NativeStringCollections.Demo
{
    public class BurstSwitch : MonoBehaviour
    {
        public Slider toggleBurstParseLines;
        public Slider toggleBurstParseLinesFromBuffer_internal;
        public Slider toggleBurstFunc;

        // reference
        public AsyncTextFileReader<CharaDataParser> loader;

        public void OnToggleBurstSwitch()
        {
            if (loader is null) return;

            if(toggleBurstParseLines.value != 0)
            {
                loader.Data.EnableBurst = true;
            }
            else
            {
                loader.Data.EnableBurst = false;
            }

            if(toggleBurstFunc.value != 0)
            {
                loader.Data.EnableBurstFunc = true;
            }
            else
            {
                loader.Data.EnableBurstFunc = false;
            }

            if(toggleBurstParseLinesFromBuffer_internal.value != 0)
            {
                loader.EnableBurst = true;
            }
            else
            {
                loader.EnableBurst = false;
            }
        }
    }
}

