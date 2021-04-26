using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Unity.Collections;

using TMPro;

using NativeStringCollections;

namespace NativeStringCollections.Demo
{
    public class ConsumerMultiCast : MonoBehaviour
    {
        // this is reference
        public AsyncTextFileLoader<CharaDataParser> loader;

        public Button Button_LoadALL;
        public Button Button_LoadOdd;
        public Button Button_LoadEven;
        public Button Button_LoadBy3;
        public Button Button_LoadBy4;

        private bool _load_all, _load_odd, _load_even, _load_by3, _load_by4;

        public void Start()
        {
            _load_all = false;
            _load_odd = false;
            _load_even = false;
            _load_by3 = false;
            _load_by4 = false;
        }
        public void Update()
        {
            
        }

        public void OnClickLoadAll()
        {
            if (_load_all)
            {
                for (int i = 0; i < loader.Length; i++) loader.UnLoadFile(i);
                var txt = Button_LoadALL.GetComponentInChildren<TMP_Text>();
                txt.text = "* UnLoad ALL";
                _load_all = false;
            }
            else
            {
                for (int i = 0; i < loader.Length; i++) loader.LoadFile(i);
                var txt = Button_LoadALL.GetComponentInChildren<TMP_Text>();
                txt.text = "Load ALL";
                _load_all = true;
            }
            SwitchButtonColor(Button_LoadALL, _load_all);
        }
        public void OnClickLoadOdd()
        {
            if (_load_odd)
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if(i % 2 != 0) loader.UnLoadFile(i);
                }
                var txt = Button_LoadOdd.GetComponentInChildren<TMP_Text>();
                txt.text = "* UnLoad Odd";
                _load_odd = false;
            }
            else
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if (i % 2 != 0) loader.LoadFile(i);
                }
                var txt = Button_LoadOdd.GetComponentInChildren<TMP_Text>();
                txt.text = "Load Odd";
                _load_odd = true;
            }
            SwitchButtonColor(Button_LoadOdd, _load_odd);
        }
        public void OnClickLoadEven()
        {
            if (_load_even)
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if (i % 2 == 0) loader.UnLoadFile(i);
                }
                var txt = Button_LoadEven.GetComponentInChildren<TMP_Text>();
                txt.text = "* UnLoad Even";
                _load_even = false;
            }
            else
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if (i % 2 == 0) loader.LoadFile(i);
                }
                var txt = Button_LoadEven.GetComponentInChildren<TMP_Text>();
                txt.text = "Load Even";
                _load_even = true;
            }
            SwitchButtonColor(Button_LoadEven, _load_even);
        }
        public void OnClickLoadBy3()
        {
            if (_load_by3)
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if (i % 3 == 0) loader.UnLoadFile(i);
                }
                var txt = Button_LoadBy3.GetComponentInChildren<TMP_Text>();
                txt.text = "* UnLoad by 3";
                _load_by3 = false;
            }
            else
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if (i % 3 == 0) loader.LoadFile(i);
                }
                var txt = Button_LoadBy3.GetComponentInChildren<TMP_Text>();
                txt.text = "Load by 3";
                _load_by3 = true;
            }
            SwitchButtonColor(Button_LoadBy3, _load_by3);
        }
        public void OnClickLoadBy4()
        {
            if (_load_by4)
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if (i % 4 == 0) loader.UnLoadFile(i);
                }
                var txt = Button_LoadBy4.GetComponentInChildren<TMP_Text>();
                txt.text = "* UnLoad by 4";
                _load_by4 = false;
            }
            else
            {
                for (int i = 0; i < loader.Length; i++)
                {
                    if (i % 4 == 0) loader.LoadFile(i);
                }
                var txt = Button_LoadBy4.GetComponentInChildren<TMP_Text>();
                txt.text = "Load by 4";
                _load_by4 = true;
            }
            SwitchButtonColor(Button_LoadBy4, _load_by4);
        }

        private static void SwitchButtonColor(Button btn, bool mode)
        {
            var txt = btn.GetComponentInChildren<TMP_Text>();
            var cb = btn.colors;
            if (!mode)
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
    }
}
