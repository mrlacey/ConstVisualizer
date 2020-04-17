// <copyright file="StringExtensionTests.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConstVisualizer.Tests
{
    [TestClass]
    public class StringExtensionTests
    {
        [TestMethod]
        public async Task IsValidVariableNameAsync_Valid_SpacesEitherSide()
        {
            var actual = await "SOMETHING".IsValidVariableNameAsync(' ', ' ');

            Assert.IsTrue(actual);
        }

        [TestMethod]
        public async Task IsValidVariableNameAsync_Valid_NewLineAtEnd()
        {
            var actual = await "SOMETHING".IsValidVariableNameAsync(' ', "\r"[0]);

            Assert.IsTrue(actual);
        }

        [TestMethod]
        public async Task IsValidVariableNameAsync_Valid_CarriageReturnAtEnd()
        {
            var actual = await "SOMETHING".IsValidVariableNameAsync(' ', "\n"[0]);

            Assert.IsTrue(actual);
        }

        [TestMethod]
        public async Task IsValidVariableNameAsync_Invalid_LetterEitherSide()
        {
            var actual = await "SOMETHING".IsValidVariableNameAsync('a', 'c');

            Assert.IsFalse(actual);
        }

        [TestMethod]
        public async Task IsValidVariableNameAsync_Invalid_UnderscoreAfter()
        {
            var actual = await "SOMETHING".IsValidVariableNameAsync(' ', '_');

            Assert.IsFalse(actual);
        }

        [TestMethod]
        public async Task IsValidVariableNameAsync_Invalid_AtsignBefore()
        {
            var actual = await "SOMETHING".IsValidVariableNameAsync('@', ' ');

            Assert.IsFalse(actual);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_NoSearchTermsFindsNothing()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync();

            Assert.AreEqual(-1, index);
            Assert.AreEqual(string.Empty, value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_SearchIsCaseSensitive()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("ABC");

            Assert.AreEqual(-1, index);
            Assert.AreEqual(string.Empty, value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_SingleTerm_FindAtStart()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("abc");

            Assert.AreEqual(0, index);
            Assert.AreEqual("abc", value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_SingleTerm_FindInMiddle()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("cde");

            Assert.AreEqual(2, index);
            Assert.AreEqual("cde", value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_SingleTerm_FindAtEnd()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("jkl");

            Assert.AreEqual(9, index);
            Assert.AreEqual("jkl", value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_MultipleTerms_SecondFound()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("gfd", "jkl");

            Assert.AreEqual(9, index);
            Assert.AreEqual("jkl", value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_DuplicateTerm_Found()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("gfd", "efg", "efg");

            Assert.AreEqual(4, index);
            Assert.AreEqual("efg", value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_DuplicateTerm_NotFound()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("gfd", "efsg", "efsg");

            Assert.AreEqual(-1, index);
            Assert.AreEqual(string.Empty, value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_MultipleMatches()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("hij", "efg");

            Assert.AreEqual(4, index);
            Assert.AreEqual("efg", value);
            Assert.IsFalse(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_OverlappingMatches_ShortestMatchLast()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("efgh", "efg");

            Assert.AreEqual(4, index);
            Assert.AreEqual("efg", value);
            Assert.IsTrue(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_OverlappingMatches_ShortestMatchFirst()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("efg", "efgh");

            Assert.AreEqual(4, index);
            Assert.AreEqual("efg", value);
            Assert.IsTrue(retry);
        }

        [TestMethod]
        public async Task IndexOfAnyAsync_IndicatesRetriesNeeded()
        {
            var (index, value, retry) = await "abcdefghijkl".IndexOfAnyAsync("abc", "ab");

            Assert.AreEqual(0, index);
            Assert.AreEqual("ab", value);
            Assert.IsTrue(retry);
        }

        [TestMethod]
        public async Task GetAllWholeWordIndexesAsync_BigComplexScenario()
        {
            var searchTerms = new string[] { "bcd", "bc", "bcd", "sded", "ghi" };
            var actual = await "[bcd] ghi)jkl".GetAllWholeWordIndexesAsync(searchTerms);

            Assert.AreEqual(2, actual.Count);

            Assert.AreEqual(1, actual[0].index);
            Assert.AreEqual("bcd", actual[0].value);
            Assert.AreEqual(6, actual[1].index);
            Assert.AreEqual("ghi", actual[1].value);
        }
    }
}
