using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NativeStringCollections
{
    /// <summary>
    /// status pf async parser
    /// </summary>
    public readonly struct ReadState
    {
        public ReadJobState JobState { get; }
        /// <summary>
        /// total length by BrockSize.
        /// </summary>
        public int Length { get; }
        /// <summary>
        /// progress by BrockSize.
        /// </summary>
        public int Read { get; }
        /// <summary>
        /// total reference count.
        /// </summary>
        public int RefCount { get; }
        /// <summary>
        /// elapsed milliseconds for AsyncReadManager.Read()
        /// </summary>
        public float DelayReadAsync { get; }
        /// <summary>
        /// elapsed milliseconds for ITextFileParser.ParseLine()
        /// </summary>
        public float DelayParseText { get; }
        /// <summary>
        /// elapsed milliseconds for ITextFileParser.PostReadProc()
        /// </summary>
        public float DelayPostProc { get; }

        /// <summary>
        /// elapsed milliseconds to parse the file.
        /// </summary>
        public double Delay { get { return DelayReadAsync + DelayParseText + DelayPostProc; } }
        /// <summary>
        /// loading data is completed or not.
        /// </summary>
        public bool IsCompleted { get { return (JobState == ReadJobState.Completed); } }
        /// <summary>
        /// the JobState is Completed or UnLoaded. it must be true to access parser class.
        /// </summary>
        public bool IsStandby
        {
            get { return (JobState == ReadJobState.Completed || JobState == ReadJobState.UnLoaded); }
        }

        public ReadState(ReadJobState job_state, int len, int read, int ref_count,
            float delay_read_async,
            float delay_parse_text,
            float delay_post_proc)
        {
            JobState = job_state;
            Length = len;
            Read = read;
            RefCount = ref_count;
            DelayReadAsync = delay_read_async;
            DelayParseText = delay_parse_text;
            DelayPostProc = delay_post_proc;
        }
    }
}