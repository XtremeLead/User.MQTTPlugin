using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class ActionRulesTests
    {
        private static ActionsValidation Validate(params string[] fields)
        {
            var rows = new List<ValueTuple<string, string, string>>();
            for (int i = 0; i < fields.Length; i += 3)
            {
                rows.Add((fields[i], fields[i + 1], fields[i + 2]));
            }
            return ActionRules.Validate(rows);
        }

        [TestMethod]
        public void Validate_CompleteRows_BecomeActionsInRowOrder()
        {
            ActionsValidation result = Validate(
                "Zeta", "a/z", "1",
                "Alpha", "a/a", "2");

            CollectionAssert.AreEqual(new[] { "Zeta", "Alpha" }, result.Actions.Select(a => a.Name).ToArray());
            Assert.AreEqual("a/a", result.Actions[1].Topic);
            Assert.AreEqual("2", result.Actions[1].Message);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.SavedRows.ToArray());
            Assert.AreEqual(0, result.SkippedCount);
            Assert.IsFalse(result.HasDuplicates);
        }

        [TestMethod]
        public void Validate_IncompleteRows_AreSkippedAndCounted()
        {
            ActionsValidation result = Validate(
                "", "a/x", "1",
                "Light", "a/l", "on",
                "NoMessage", "a/m", "");

            CollectionAssert.AreEqual(new[] { "Light" }, result.Actions.Select(a => a.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 1 }, result.SavedRows.ToArray());
            Assert.AreEqual(2, result.SkippedCount);
        }

        // D6: names must be unique.
        [TestMethod]
        public void Validate_SameNameTwice_ReportsDuplicate()
        {
            ActionsValidation result = Validate(
                "Light", "a/1", "on",
                "Light", "a/2", "off");

            Assert.IsTrue(result.HasDuplicates);
            CollectionAssert.AreEqual(new[] { "Light" }, result.DuplicateNames.ToArray());
        }

        // D6: names that differ only in case count as duplicates.
        [TestMethod]
        public void Validate_SameNameDifferentCase_ReportsDuplicate()
        {
            ActionsValidation result = Validate(
                "Light", "a/1", "on",
                "LIGHT", "a/2", "off");

            CollectionAssert.AreEqual(new[] { "LIGHT" }, result.DuplicateNames.ToArray());
        }

        [TestMethod]
        public void Validate_NameThreeTimes_ReportsItOnce()
        {
            ActionsValidation result = Validate(
                "Light", "a/1", "on",
                "Light", "a/2", "off",
                "light", "a/3", "dim");

            CollectionAssert.AreEqual(new[] { "Light" }, result.DuplicateNames.ToArray());
        }

        [TestMethod]
        public void Validate_SeveralDuplicateNames_ReportsEach()
        {
            ActionsValidation result = Validate(
                "A", "t", "1",
                "B", "t", "2",
                "A", "t", "3",
                "B", "t", "4");

            CollectionAssert.AreEqual(new[] { "A", "B" }, result.DuplicateNames.ToArray());
        }

        // An incomplete row is skipped before names are compared.
        [TestMethod]
        public void Validate_SameNameOnIncompleteRow_IsNotADuplicate()
        {
            ActionsValidation result = Validate(
                "Light", "a/1", "",
                "Light", "a/2", "off");

            Assert.IsFalse(result.HasDuplicates);
            Assert.AreEqual("a/2", result.Actions.Single().Topic);
            Assert.AreEqual("off", result.Actions.Single().Message);
            Assert.AreEqual(1, result.SkippedCount);
        }

        [TestMethod]
        public void Validate_NoRows_ReturnsNothing()
        {
            ActionsValidation result = Validate();

            Assert.AreEqual(0, result.Actions.Count);
            Assert.AreEqual(0, result.SkippedCount);
            Assert.IsFalse(result.HasDuplicates);
        }
    }
}
