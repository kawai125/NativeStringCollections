using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;
using UnityEngine;


namespace Tests
{
    using CodeSet = NativeStringCollections.Impl.UTF16CodeSet;

    public class Test_UTF16CodeSet
    {
        [Test]
        public void CheckUnicodeValue()
        {
            Assert.AreEqual(CodeSet.code_tab, '\t');
            Assert.AreEqual(CodeSet.code_LF, '\n');
            Assert.AreEqual(CodeSet.code_CR, '\r');
            Assert.AreEqual(CodeSet.code_space, ' ');

            Assert.AreEqual(CodeSet.code_0, '0');
            Assert.AreEqual(CodeSet.code_9, '9');

            Assert.AreEqual(CodeSet.code_A, 'A');
            Assert.AreEqual(CodeSet.code_E, 'E');
            Assert.AreEqual(CodeSet.code_F, 'F');
            Assert.AreEqual(CodeSet.code_L, 'L');
            Assert.AreEqual(CodeSet.code_R, 'R');
            Assert.AreEqual(CodeSet.code_S, 'S');
            Assert.AreEqual(CodeSet.code_T, 'T');
            Assert.AreEqual(CodeSet.code_U, 'U');

            Assert.AreEqual(CodeSet.code_a, 'a');
            Assert.AreEqual(CodeSet.code_e, 'e');
            Assert.AreEqual(CodeSet.code_f, 'f');
            Assert.AreEqual(CodeSet.code_l, 'l');
            Assert.AreEqual(CodeSet.code_r, 'r');
            Assert.AreEqual(CodeSet.code_s, 's');
            Assert.AreEqual(CodeSet.code_t, 't');
            Assert.AreEqual(CodeSet.code_u, 'u');
        }
    }
}
