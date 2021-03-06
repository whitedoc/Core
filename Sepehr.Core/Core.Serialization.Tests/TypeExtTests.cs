﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using static Core.Serialization.Test.ObjectMetadataTests;

namespace Core.Serialization.Tests
{
    [TestClass]
    public class TypeExtTests
    {
        private enum JustForTest
        {
            Test1 = 100,
            Test2,
            Test3
        }

        [TestMethod]
        public void IsNullable_ForAllTypes()
        {
            Assert.IsFalse(typeof(int).IsNullable());
            Assert.IsTrue(typeof(int?).IsNullable());
            Assert.IsFalse(typeof(TestEnum2).IsNullable());
            Assert.IsTrue(typeof(Enum).IsNullable());
            Assert.IsTrue(typeof(int[]).IsNullable());
            Assert.IsTrue(typeof(List<int>).IsNullable());
            Assert.IsTrue(typeof(Dictionary<int, string>).IsNullable());
            Assert.IsTrue(typeof(string).IsNullable());
            Assert.IsTrue(typeof(User<int>).IsNullable());
            Assert.IsTrue(typeof(Role).IsNullable());
            Assert.IsFalse(typeof(Point).IsNullable());
            Assert.IsTrue(typeof(Point?).IsNullable());
        }
        [TestMethod]
        public void IsInheritable_ForAllTypes()
        {
            Assert.IsFalse(typeof(int).IsInheritable());
            Assert.IsFalse(typeof(int?).IsInheritable());
            Assert.IsFalse(typeof(TestEnum2).IsInheritable());
            Assert.IsTrue(typeof(Enum).IsInheritable());
            Assert.IsFalse(typeof(int[]).IsInheritable());
            Assert.IsTrue(typeof(List<int>).IsInheritable());
            Assert.IsTrue(typeof(Dictionary<int, string>).IsInheritable());
            Assert.IsFalse(typeof(string).IsInheritable());
            Assert.IsTrue(typeof(User<int>).IsInheritable());
            Assert.IsTrue(typeof(Role).IsInheritable());
            Assert.IsFalse(typeof(Point).IsInheritable());
            Assert.IsFalse(typeof(Point?).IsInheritable());
        }

        [TestMethod]
        public void IsSimple_Test_AllSimple_Type_And_Some_Complex()
        {
            Assert.IsTrue(typeof(string).IsSimple());
            Assert.IsTrue(typeof(int).IsSimple());
            Assert.IsTrue(typeof(decimal).IsSimple());
            Assert.IsTrue(typeof(float).IsSimple());
            Assert.IsTrue(typeof(StringComparison).IsSimple());  // enum
            Assert.IsTrue(typeof(int?).IsSimple());
            Assert.IsTrue(typeof(decimal?).IsSimple());
            Assert.IsTrue(typeof(JustForTest).IsSimple());
            Assert.IsTrue(typeof(StringComparison?).IsSimple());
            Assert.IsTrue(typeof(StringComparison?).IsSimple());
            Assert.IsFalse(typeof(object).IsSimple());
            Assert.IsFalse(typeof(Point).IsSimple());  // struct
            Assert.IsFalse(typeof(Point?).IsSimple());
            Assert.IsFalse(typeof(StringBuilder).IsSimple()); // refer
        }
    }
}